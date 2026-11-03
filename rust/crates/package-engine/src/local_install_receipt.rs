// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/local_install_receipt.rs
// Amac: Imzasiz yerel kurulumun sahip oldugu payload dosyalarini guvenli kaydetmek ve kaldirmak
// Modul - Rust Package Lifecycle Service
// Version: 0.1.0
// Aciklama: SHA-256 envanter, path siniri, kullanici verisini koruyan no-overwrite uninstall
// Bagimli Oldugu Katman: Service | Security

use std::{
    collections::HashSet,
    fs,
    io::{self, Read, Write},
    path::{Component, Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::{PackageEngineError, validate_archive_path};

const RECORD_NAME: &str = ".turkuaz-local-install.json";
const RECORD_VERSION: u32 = 1;
const MAX_RECORD_BYTES: u64 = 32 * 1024 * 1024;
const MAX_OWNED_FILES: usize = 100_000;

fn invalid(reason: &'static str) -> PackageEngineError {
    PackageEngineError::InvalidPackage(reason)
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct OwnedFile {
    path: String,
    size_bytes: u64,
    sha256: String,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct InstallRecord {
    format_version: u32,
    package_id: String,
    version: String,
    files: Vec<OwnedFile>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct LocalUninstallSummary {
    pub removed_files: usize,
    pub retained_unknown_files: bool,
}

fn is_reparse(metadata: &fs::Metadata) -> bool {
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        metadata.file_attributes() & 0x0400 != 0
    }
    #[cfg(not(windows))]
    {
        let _ = metadata;
        false
    }
}

fn safe_regular(path: &Path) -> Result<fs::Metadata, PackageEngineError> {
    let metadata = fs::symlink_metadata(path).map_err(PackageEngineError::Io)?;
    if !metadata.is_file() || metadata.file_type().is_symlink() || is_reparse(&metadata) {
        return Err(invalid("local install entry is not a regular file"));
    }
    Ok(metadata)
}

fn safe_root(path: &Path) -> Result<(), PackageEngineError> {
    if !path.is_absolute()
        || path.components().count() < 3
        || path
            .components()
            .any(|c| matches!(c, Component::ParentDir | Component::CurDir))
    {
        return Err(invalid(
            "local install target must be a non-root absolute path",
        ));
    }
    let mut current = PathBuf::new();
    for part in path.components() {
        current.push(part.as_os_str());
        let meta = match fs::symlink_metadata(&current) {
            Ok(meta) => meta,
            Err(e) if e.kind() == io::ErrorKind::NotFound => return Err(PackageEngineError::Io(e)),
            Err(e) => return Err(PackageEngineError::Io(e)),
        };
        if !meta.is_dir() || meta.file_type().is_symlink() || is_reparse(&meta) {
            return Err(invalid("local install target contains a junction or link"));
        }
    }
    Ok(())
}

fn digest_file(path: &Path) -> Result<String, PackageEngineError> {
    safe_regular(path)?;
    let mut file = fs::File::open(path).map_err(PackageEngineError::Io)?;
    let mut hash = Sha256::new();
    let mut block = [0u8; 64 * 1024];
    loop {
        let count = file.read(&mut block).map_err(PackageEngineError::Io)?;
        if count == 0 {
            break;
        }
        hash.update(&block[..count]);
    }
    Ok(hash
        .finalize()
        .iter()
        .map(|b| format!("{b:02x}"))
        .collect::<String>())
}

fn relative_name(root: &Path, file: &Path) -> Result<String, PackageEngineError> {
    let relative = file
        .strip_prefix(root)
        .map_err(|_| invalid("owned file escaped local install root"))?;
    let mut parts = Vec::new();
    for part in relative.components() {
        let Component::Normal(name) = part else {
            return Err(invalid("owned file path is not relative"));
        };
        parts.push(
            name.to_str()
                .ok_or(invalid("owned file name must be Unicode"))?,
        );
    }
    let text = parts.join("/");
    validate_archive_path(&text)?;
    if text.eq_ignore_ascii_case(RECORD_NAME) {
        return Err(invalid("payload must not contain reserved install receipt"));
    }
    Ok(text)
}

/// @dev-id BRK-LOCAL-RECEIPT-001
/// @purpose Record a verified package's exact staged files before publishing.
/// @contract Receipt exists only for new installs; no overwrites or foreign files.
pub(crate) fn write_local_install_record(
    stage: &Path,
    package_id: &str,
    version: &str,
) -> Result<(), PackageEngineError> {
    let metadata = fs::symlink_metadata(stage).map_err(PackageEngineError::Io)?;
    if !metadata.is_dir() || metadata.file_type().is_symlink() || is_reparse(&metadata) {
        return Err(invalid("local install stage is unsafe"));
    }
    let mut directories = vec![stage.to_path_buf()];
    let mut files = Vec::new();
    while let Some(dir) = directories.pop() {
        for entry in fs::read_dir(dir).map_err(PackageEngineError::Io)? {
            let entry = entry.map_err(PackageEngineError::Io)?;
            let path = entry.path();
            let meta = fs::symlink_metadata(&path).map_err(PackageEngineError::Io)?;
            if meta.file_type().is_symlink() || is_reparse(&meta) {
                return Err(invalid("local install contains a link or junction"));
            }
            if meta.is_dir() {
                directories.push(path);
            } else if meta.is_file() {
                if files.len() >= MAX_OWNED_FILES {
                    return Err(invalid("local install receipt file limit exceeded"));
                }
                let name = relative_name(stage, &path)?;
                files.push(OwnedFile {
                    path: name,
                    size_bytes: meta.len(),
                    sha256: digest_file(&path)?,
                });
            } else {
                return Err(invalid("local install contains a special file"));
            }
        }
    }
    if files.is_empty() {
        return Err(invalid("local install has no regular payload files"));
    }
    files.sort_by(|a, b| a.path.cmp(&b.path));
    let record = InstallRecord {
        format_version: RECORD_VERSION,
        package_id: package_id.to_owned(),
        version: version.to_owned(),
        files,
    };
    let data = serde_json::to_vec(&record)
        .map_err(|_| invalid("local install receipt could not be serialized"))?;
    if data.len() as u64 > MAX_RECORD_BYTES {
        return Err(invalid("local install receipt exceeds size limit"));
    }
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(stage.join(RECORD_NAME))
        .map_err(PackageEngineError::Io)?;
    output.write_all(&data).map_err(PackageEngineError::Io)?;
    output.sync_all().map_err(PackageEngineError::Io)
}

fn checked_record(target: &Path, expected_id: &str) -> Result<InstallRecord, PackageEngineError> {
    safe_root(target)?;
    let path = target.join(RECORD_NAME);
    let metadata = safe_regular(&path)?;
    if metadata.len() == 0 || metadata.len() > MAX_RECORD_BYTES {
        return Err(invalid("local install receipt size invalid"));
    }
    let raw = fs::read(path).map_err(PackageEngineError::Io)?;
    let record: InstallRecord =
        serde_json::from_slice(&raw).map_err(|_| invalid("local install receipt JSON invalid"))?;
    if record.format_version != RECORD_VERSION
        || record.package_id != expected_id
        || record.files.is_empty()
        || record.files.len() > MAX_OWNED_FILES
        || record.version.is_empty()
    {
        return Err(invalid(
            "local install receipt identity or version mismatch",
        ));
    }
    let mut seen = HashSet::new();
    for entry in &record.files {
        let relative = validate_archive_path(&entry.path)?;
        if entry.path.eq_ignore_ascii_case(RECORD_NAME)
            || relative.components().count() == 0
            || !seen.insert(entry.path.to_lowercase())
            || entry.sha256.len() != 64
            || !entry.sha256.bytes().all(|b| b.is_ascii_hexdigit())
        {
            return Err(invalid("local install receipt contains invalid file entry"));
        }
    }
    Ok(record)
}

fn preflight_file(target: &Path, item: &OwnedFile) -> Result<(), PackageEngineError> {
    let relative = validate_archive_path(&item.path)?;
    let full = target.join(relative);
    let mut parent = full.parent();
    while let Some(dir) = parent {
        if dir == target {
            break;
        }
        let meta = fs::symlink_metadata(dir).map_err(PackageEngineError::Io)?;
        if !meta.is_dir() || meta.file_type().is_symlink() || is_reparse(&meta) {
            return Err(invalid("local install receipt traverses a directory link"));
        }
        parent = dir.parent();
    }
    match fs::symlink_metadata(&full) {
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(PackageEngineError::Io(error)),
        Ok(meta) => {
            if !meta.is_file() || meta.file_type().is_symlink() || is_reparse(&meta) {
                return Err(invalid(
                    "local install file was replaced with a special file",
                ));
            }
            if meta.len() != item.size_bytes || digest_file(&full)? != item.sha256 {
                return Err(invalid("local install file was changed; uninstall refused"));
            }
            Ok(())
        }
    }
}

/// @dev-id BRK-LOCAL-RECEIPT-002
/// @purpose Remove only unchanged package-owned files, preserve everything else.
/// @contract No receipt => no deletion; changed files => abort before deletion;
/// file-by-file check and no symlink traversal. Only empty folders are removed.
/// Note: local unsigned receipt is not cryptographically authenticated.
pub fn uninstall_local_install(
    target: impl AsRef<Path>,
    expected_package_id: &str,
) -> Result<LocalUninstallSummary, PackageEngineError> {
    let target = target.as_ref();
    let record = checked_record(target, expected_package_id)?;
    // Preflight all entries so a changed file fails without partial deletion.
    for item in &record.files {
        preflight_file(target, item)?;
    }
    let mut removed = 0;
    let mut dirs = HashSet::new();
    for item in &record.files {
        preflight_file(target, item)?;
        let file = target.join(validate_archive_path(&item.path)?);
        if fs::symlink_metadata(&file).is_ok() {
            fs::remove_file(&file).map_err(PackageEngineError::Io)?;
            removed += 1;
        }
        let mut parent = file.parent();
        while let Some(dir) = parent {
            if dir == target {
                break;
            }
            dirs.insert(dir.to_path_buf());
            parent = dir.parent();
        }
    }
    let receipt = target.join(RECORD_NAME);
    safe_regular(&receipt)?;
    fs::remove_file(receipt).map_err(PackageEngineError::Io)?;
    let mut dirs: Vec<_> = dirs.into_iter().collect();
    dirs.sort_by_key(|dir| std::cmp::Reverse(dir.components().count()));
    for dir in dirs {
        let _ = fs::remove_dir(dir); // only empty directories disappear
    }
    let retained_unknown_files = match fs::remove_dir(target) {
        Ok(()) => false,
        Err(error) if error.kind() == io::ErrorKind::DirectoryNotEmpty => true,
        Err(error) => return Err(PackageEngineError::Io(error)),
    };
    Ok(LocalUninstallSummary {
        removed_files: removed,
        retained_unknown_files,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    fn fixture() -> (PathBuf, PathBuf) {
        let base = std::env::temp_dir().join(format!("turkuaz-owned-{}", Uuid::new_v4()));
        let target = base.join("kurulu uygulama");
        fs::create_dir_all(target.join("bin")).expect("create nested test folder");
        fs::write(target.join("bin/hello.txt"), b"trusted contents").expect("fixture");
        write_local_install_record(&target, "qa.native", "1.0.0").expect("inventory");
        (base, target)
    }

    #[test]
    fn records_and_removes_only_owned_files() {
        let (base, target) = fixture();
        fs::write(target.join("user-notes.txt"), b"never delete").expect("user file");
        let result = uninstall_local_install(&target, "qa.native").expect("uninstall");
        assert_eq!(result.removed_files, 1);
        assert!(result.retained_unknown_files);
        assert!(!target.join("bin/hello.txt").exists());
        assert_eq!(
            fs::read(target.join("user-notes.txt")).unwrap(),
            b"never delete"
        );
        fs::remove_dir_all(base).expect("cleanup");
    }

    #[test]
    fn modified_files_and_unknown_installs_cannot_be_removed() {
        let (base, target) = fixture();
        fs::write(target.join("bin/hello.txt"), b"user modified").expect("modify");
        assert!(uninstall_local_install(&target, "qa.native").is_err());
        assert!(target.join("bin/hello.txt").exists());
        assert!(uninstall_local_install(&target, "other.package").is_err());
        assert!(target.join(RECORD_NAME).exists());
        fs::remove_dir_all(base).expect("cleanup");
    }

    #[test]
    fn forged_escape_in_receipt_is_rejected() {
        let (base, target) = fixture();
        let receipt = target.join(RECORD_NAME);
        let mut record: InstallRecord =
            serde_json::from_slice(&fs::read(&receipt).unwrap()).unwrap();
        record.files[0].path = "../outside.txt".into();
        fs::write(receipt, serde_json::to_vec(&record).unwrap()).expect("test mutation");
        assert!(uninstall_local_install(&target, "qa.native").is_err());
        assert!(target.join("bin/hello.txt").exists());
        fs::remove_dir_all(base).expect("cleanup");
    }
}
