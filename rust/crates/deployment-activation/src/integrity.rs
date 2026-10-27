// Dosya Yolu: REPO_ROOT/rust/crates/deployment-activation/src/integrity.rs
// Amac: Surumlu deployment dosyalarinin SHA-256 envanterini olusturmak ve denetlemek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Etkin surumde ekleme, silme, boyut ve icerik degisikliklerini kayitli envanterle karsilastirir
// Bagimli Oldugu Katman: Service | Infrastructure

use std::{
    error::Error,
    fmt::Write as _,
    fs, io,
    io::{Read, Write},
    path::{Component, Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use turkuaz_installer_domain::{
    package_id::PackageId, semantic_version::SemanticVersion,
};
use uuid::Uuid;

use super::{
    ActiveDeployment, DeploymentActivationError, VersionedDeploymentStore,
};

const INVENTORY_DIRECTORY: &str = "integrity";
const INVENTORY_FORMAT_VERSION: u32 = 1;
const MAXIMUM_INVENTORY_BYTES: u64 = 64 * 1024 * 1024;
const MAXIMUM_INVENTORY_ENTRIES: usize = 250_000;
const MAXIMUM_INVENTORY_DIRECTORY_DEPTH: usize = 128;
const MAXIMUM_INVENTORY_TOTAL_BYTES: u64 = 32 * 1024 * 1024 * 1024;
const HASH_BUFFER_BYTES: usize = 64 * 1024;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct DeploymentIntegrityReport {
    active: ActiveDeployment,
    verified_file_count: usize,
}

impl DeploymentIntegrityReport {
    pub fn active(&self) -> &ActiveDeployment {
        &self.active
    }

    pub const fn verified_file_count(&self) -> usize {
        self.verified_file_count
    }
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct DeploymentInventory {
    format_version: u32,
    package_id: String,
    version: String,
    files: Vec<InventoryEntry>,
}

#[derive(Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct InventoryEntry {
    path: String,
    size_bytes: u64,
    sha256: String,
}

impl DeploymentInventory {
    fn new(
        package_id: &PackageId,
        version: &SemanticVersion,
        files: Vec<InventoryEntry>,
    ) -> Self {
        Self {
            format_version: INVENTORY_FORMAT_VERSION,
            package_id: package_id.to_string(),
            version: version.to_string(),
            files,
        }
    }
}

impl VersionedDeploymentStore {
    /// @dev-id TIZ-DEPLOY-015
    /// @purpose Verify every regular file in the active deployment against its recorded SHA-256 inventory.
    /// @reason Detect local file additions, removals, size changes, or content corruption after publication.
    /// @contract Requires an existing active deployment and a valid bounded inventory; rejects symlinks, special files, unsafe inventory metadata, and mismatched file sets or digests.
    /// @depends Active version pointer, deployment inventory, SHA-256, and bounded filesystem traversal.
    pub fn verify_current(
        &self,
        package_id: &PackageId,
    ) -> Result<Option<DeploymentIntegrityReport>, DeploymentActivationError> {
        let Some(active) = self.current(package_id)? else {
            return Ok(None);
        };
        let inventory = read_inventory(self, &active)?;
        let mut expected_files = inventory.files;
        expected_files.sort();

        if expected_files
            .windows(2)
            .any(|pair| pair[0].path == pair[1].path)
        {
            return Err(DeploymentActivationError::InvalidInventory(
                "inventory contains duplicate paths",
            ));
        }

        let actual_files = collect_inventory(&active.directory)?;
        if actual_files != expected_files {
            return Err(DeploymentActivationError::IntegrityMismatch);
        }

        Ok(Some(DeploymentIntegrityReport {
            active,
            verified_file_count: actual_files.len(),
        }))
    }
}

pub(super) fn write_inventory(
    package_root: &Path,
    package_id: &PackageId,
    version: &SemanticVersion,
    deployment_directory: &Path,
) -> Result<(), DeploymentActivationError> {
    let files = collect_inventory(deployment_directory)?;
    let inventory = DeploymentInventory::new(package_id, version, files);
    let bytes = serde_json::to_vec_pretty(&inventory).map_err(DeploymentActivationError::Json)?;
    if bytes.len() as u64 > MAXIMUM_INVENTORY_BYTES {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory exceeds the configured size limit",
        ));
    }

    let inventory_root = package_root.join(INVENTORY_DIRECTORY);
    ensure_real_directory(&inventory_root)?;
    let destination = inventory_path(package_root, version);
    let temporary = inventory_root.join(format!(".inventory-{}.tmp", Uuid::new_v4()));
    let write_result = (|| {
        let mut file = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&temporary)
            .map_err(DeploymentActivationError::Io)?;
        file.write_all(&bytes)
            .map_err(DeploymentActivationError::Io)?;
        file.sync_all().map_err(DeploymentActivationError::Io)?;
        drop(file);
        fs::rename(&temporary, &destination).map_err(DeploymentActivationError::Io)
    })();
    if write_result.is_err() {
        let _ = fs::remove_file(&temporary);
    }
    write_result
}

fn read_inventory(
    store: &VersionedDeploymentStore,
    active: &ActiveDeployment,
) -> Result<DeploymentInventory, DeploymentActivationError> {
    let package_root = store.package_root(&active.package_id);
    let inventory_root = package_root.join(INVENTORY_DIRECTORY);
    ensure_existing_real_directory(&inventory_root)?;
    let path = inventory_path(&package_root, &active.version);
    let metadata = fs::symlink_metadata(&path).map_err(DeploymentActivationError::Io)?;
    if !metadata.file_type().is_file() || metadata.len() > MAXIMUM_INVENTORY_BYTES {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory is not a bounded regular file",
        ));
    }

    let mut file = fs::File::open(&path).map_err(DeploymentActivationError::Io)?;
    let opened_metadata = file.metadata().map_err(DeploymentActivationError::Io)?;
    if !opened_metadata.is_file() || opened_metadata.len() != metadata.len() {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory changed while it was being opened",
        ));
    }
    let mut bytes = Vec::with_capacity(metadata.len() as usize);
    file.take(MAXIMUM_INVENTORY_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(DeploymentActivationError::Io)?;
    if bytes.len() as u64 > MAXIMUM_INVENTORY_BYTES {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory exceeds the configured size limit",
        ));
    }

    let mut inventory: DeploymentInventory =
        serde_json::from_slice(&bytes).map_err(DeploymentActivationError::Json)?;
    let recorded_package = PackageId::parse(&inventory.package_id)
        .map_err(|_| DeploymentActivationError::InvalidInventory("package ID is invalid"))?;
    let recorded_version = SemanticVersion::parse(&inventory.version)
        .map_err(|_| DeploymentActivationError::InvalidInventory("version is invalid"))?;
    if inventory.format_version != INVENTORY_FORMAT_VERSION
        || recorded_package != active.package_id
        || recorded_version != active.version
    {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory identity does not match the active deployment",
        ));
    }
    for entry in &inventory.files {
        validate_inventory_path(&entry.path)?;
        if entry.sha256.len() != 64
            || !entry.sha256.bytes().all(|byte| byte.is_ascii_hexdigit())
        {
            return Err(DeploymentActivationError::InvalidInventory(
                "inventory contains an invalid SHA-256 digest",
            ));
        }
    }
    inventory.files.sort();
    Ok(inventory)
}

fn inventory_path(package_root: &Path, version: &SemanticVersion) -> PathBuf {
    package_root
        .join(INVENTORY_DIRECTORY)
        .join(format!("{version}.json"))
}

fn collect_inventory(
    deployment_directory: &Path,
) -> Result<Vec<InventoryEntry>, DeploymentActivationError> {
    ensure_existing_real_directory(deployment_directory)?;
    let mut files = Vec::new();
    let mut total_bytes = 0_u64;
    let mut total_entries = 0_usize;
    collect_directory(
        deployment_directory,
        deployment_directory,
        &mut files,
        &mut total_entries,
        0,
        &mut total_bytes,
    )?;
    files.sort();
    Ok(files)
}

fn collect_directory(
    root: &Path,
    directory: &Path,
    files: &mut Vec<InventoryEntry>,
    total_entries: &mut usize,
    depth: usize,
    total_bytes: &mut u64,
) -> Result<(), DeploymentActivationError> {
    if depth > MAXIMUM_INVENTORY_DIRECTORY_DEPTH {
        return Err(DeploymentActivationError::InvalidInventory(
            "deployment exceeds the inventory directory depth limit",
        ));
    }
    let mut children = Vec::new();
    for entry in fs::read_dir(directory).map_err(DeploymentActivationError::Io)? {
        if children.len() >= MAXIMUM_INVENTORY_ENTRIES {
            return Err(DeploymentActivationError::InvalidInventory(
                "directory exceeds the inventory entry limit",
            ));
        }
        children.push(entry.map_err(DeploymentActivationError::Io)?.path());
    }
    children.sort();

    for path in children {
        let metadata = fs::symlink_metadata(&path).map_err(DeploymentActivationError::Io)?;
        *total_entries = total_entries.checked_add(1).ok_or(
            DeploymentActivationError::InvalidInventory("deployment entry count overflowed"),
        )?;
        if *total_entries > MAXIMUM_INVENTORY_ENTRIES {
            return Err(DeploymentActivationError::InvalidInventory(
                "deployment exceeds the inventory entry limit",
            ));
        }
        if metadata.file_type().is_dir() {
            collect_directory(root, &path, files, total_entries, depth + 1, total_bytes)?;
            continue;
        }
        if !metadata.file_type().is_file() {
            return Err(DeploymentActivationError::InvalidInventory(
                "deployment contains a symlink or special file",
            ));
        }
        let relative_path = path.strip_prefix(root).map_err(|_| {
            DeploymentActivationError::InvalidInventory("deployment entry escaped its root")
        })?;
        let archive_path = relative_path_string(relative_path)?;
        validate_inventory_path(&archive_path)?;

        let mut file = fs::File::open(&path).map_err(DeploymentActivationError::Io)?;
        let opened_metadata = file.metadata().map_err(DeploymentActivationError::Io)?;
        if !opened_metadata.is_file() || opened_metadata.len() != metadata.len() {
            return Err(DeploymentActivationError::InvalidInventory(
                "deployment file changed while it was being opened",
            ));
        }

        let mut digest = Sha256::new();
        let mut buffer = [0_u8; HASH_BUFFER_BYTES];
        let mut file_bytes = 0_u64;
        loop {
            let read = file
                .read(&mut buffer)
                .map_err(DeploymentActivationError::Io)?;
            if read == 0 {
                break;
            }
            file_bytes = file_bytes.checked_add(read as u64).ok_or(
                DeploymentActivationError::InvalidInventory("deployment file size overflowed"),
            )?;
            *total_bytes = total_bytes.checked_add(read as u64).ok_or(
                DeploymentActivationError::InvalidInventory("deployment size overflowed"),
            )?;
            if *total_bytes > MAXIMUM_INVENTORY_TOTAL_BYTES {
                return Err(DeploymentActivationError::InvalidInventory(
                    "deployment exceeds the inventory size limit",
                ));
            }
            digest.update(&buffer[..read]);
        }
        if file_bytes != metadata.len() {
            return Err(DeploymentActivationError::InvalidInventory(
                "deployment file changed while it was being hashed",
            ));
        }

        let mut sha256 = String::with_capacity(64);
        for byte in digest.finalize() {
            write!(&mut sha256, "{byte:02x}").map_err(|_| {
                DeploymentActivationError::InvalidInventory("SHA-256 encoding failed")
            })?;
        }
        files.push(InventoryEntry {
            path: archive_path,
            size_bytes: file_bytes,
            sha256,
        });
    }
    Ok(())
}

fn relative_path_string(path: &Path) -> Result<String, DeploymentActivationError> {
    let segments = path
        .components()
        .map(|component| match component {
            Component::Normal(segment) => segment.to_str().ok_or(
                DeploymentActivationError::InvalidInventory(
                    "deployment path is not valid Unicode",
                ),
            ),
            _ => Err(DeploymentActivationError::InvalidInventory(
                "deployment path is not relative",
            )),
        })
        .collect::<Result<Vec<_>, _>>()?;
    if segments.is_empty() {
        return Err(DeploymentActivationError::InvalidInventory(
            "deployment path is empty",
        ));
    }
    let path = segments.join("/");
    if path.len() > 32 * 1024 {
        return Err(DeploymentActivationError::InvalidInventory(
            "deployment path exceeds the inventory path limit",
        ));
    }
    Ok(path)
}

fn validate_inventory_path(path: &str) -> Result<(), DeploymentActivationError> {
    if path.is_empty()
        || path.starts_with('/')
        || path.contains('\\')
        || path.contains(':')
        || path.contains('\0')
    {
        return Err(DeploymentActivationError::InvalidInventory(
            "inventory contains an unsafe path",
        ));
    }
    for segment in path.split('/') {
        if segment.is_empty()
            || segment == "."
            || segment == ".."
            || segment.ends_with('.')
            || segment.ends_with(' ')
            || segment.chars().any(|character| {
                character < ' ' || matches!(character, '<' | '>' | '"' | '|' | '?' | '*')
            })
        {
            return Err(DeploymentActivationError::InvalidInventory(
                "inventory contains an unsafe path segment",
            ));
        }
        let stem = segment
            .split('.')
            .next()
            .unwrap_or_default()
            .to_ascii_uppercase();
        let reserved = matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
            || ["COM", "LPT"].iter().any(|prefix| {
                stem.strip_prefix(prefix)
                    .is_some_and(|suffix| matches!(suffix, "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"))
            });
        if reserved {
            return Err(DeploymentActivationError::InvalidInventory(
                "inventory uses a reserved Windows device path",
            ));
        }
    }
    Ok(())
}

fn ensure_existing_real_directory(path: &Path) -> Result<(), DeploymentActivationError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_dir() => Ok(()),
        Ok(_) => Err(DeploymentActivationError::InvalidInventory(
            "deployment path is not a real directory",
        )),
        Err(error) => Err(DeploymentActivationError::Io(error)),
    }
}

fn ensure_real_directory(path: &Path) -> Result<(), DeploymentActivationError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_dir() => Ok(()),
        Ok(_) => Err(DeploymentActivationError::InvalidInventory(
            "inventory path is not a real directory",
        )),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {
            fs::create_dir(path).map_err(DeploymentActivationError::Io)
        }
        Err(error) => Err(DeploymentActivationError::Io(error)),
    }
}

pub(super) fn remove_inventory(
    package_root: &Path,
    version: &SemanticVersion,
) -> Result<(), DeploymentActivationError> {
    match fs::remove_file(inventory_path(package_root, version)) {
        Ok(()) => Ok(()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(DeploymentActivationError::Io(error)),
    }
}
