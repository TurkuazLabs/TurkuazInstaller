// Dosya Yolu: REPO_ROOT/rust/crates/deployment-activation/src/lib.rs
// Amac: Kurulum payload'ini immutable surum dizinine yayinlayip etkin surumu atomik secmek
// Modul - Rust Library
// Version: 0.2.0
// Aciklama: Yeni deployment hazirlanirken etkin surum isaretcisini korur
// Bagimli Oldugu Katman: Service | Infrastructure

use std::{
    error::Error,
    fmt, fs, io,
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use turkuaz_installer_domain::{package_id::PackageId, semantic_version::SemanticVersion};
use turkuaz_installer_package_engine::PackageStage;
use uuid::Uuid;

mod integrity;
pub use integrity::DeploymentIntegrityReport;

const ACTIVE_FILE: &str = "active.json";
const MAXIMUM_ACTIVE_FILE_BYTES: u64 = 4 * 1024;

#[derive(Debug)]
pub enum DeploymentActivationError {
    Io(io::Error),
    Json(serde_json::Error),
    InvalidLayout(&'static str),
    ActiveDeploymentMismatch,
    VersionAlreadyPublished,
    InvalidInventory(&'static str),
    IntegrityMismatch,
}

impl fmt::Display for DeploymentActivationError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Deployment activation I/O failed: {error}"),
            Self::Json(error) => write!(formatter, "Deployment JSON is invalid: {error}"),
            Self::InvalidLayout(message) => {
                write!(formatter, "Deployment layout is invalid: {message}")
            }
            Self::ActiveDeploymentMismatch => formatter
                .write_str("Active deployment record does not match the requested package."),
            Self::VersionAlreadyPublished => {
                formatter.write_str("This package version is already published.")
            }
            Self::InvalidInventory(message) => {
                write!(formatter, "Deployment inventory is invalid: {message}")
            }
            Self::IntegrityMismatch => formatter
                .write_str("Deployment files do not match the recorded integrity inventory."),
        }
    }
}

impl Error for DeploymentActivationError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Json(error) => Some(error),
            Self::InvalidLayout(_)
            | Self::ActiveDeploymentMismatch
            | Self::VersionAlreadyPublished
            | Self::InvalidInventory(_)
            | Self::IntegrityMismatch => None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct DeploymentActivationOptions {
    root_directory: PathBuf,
}

impl DeploymentActivationOptions {
    /// @dev-id TIZ-DEPLOY-010
    /// @purpose Configure the trusted root for versioned application deployments.
    /// @reason Package IDs and versions are resolved beneath one application-owned data root.
    /// @contract The caller supplies a trusted, writable root; package/version components come from validated domain values.
    /// @depends std filesystem path handling.
    pub fn new(root_directory: impl AsRef<Path>) -> io::Result<Self> {
        let root_directory = root_directory.as_ref();
        if root_directory.as_os_str().is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Deployment root directory cannot be empty",
            ));
        }
        let absolute_directory = if root_directory.is_absolute() {
            root_directory.to_path_buf()
        } else {
            std::env::current_dir()?.join(root_directory)
        };
        let root_directory = match fs::canonicalize(&absolute_directory) {
            Ok(path) => path,
            Err(error) if error.kind() == io::ErrorKind::NotFound => absolute_directory,
            Err(error) => return Err(error),
        };
        Ok(Self { root_directory })
    }

    pub fn root_directory(&self) -> &Path {
        &self.root_directory
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ActiveDeployment {
    package_id: PackageId,
    version: SemanticVersion,
    directory: PathBuf,
    previous_version: Option<SemanticVersion>,
}

impl ActiveDeployment {
    pub fn package_id(&self) -> &PackageId {
        &self.package_id
    }

    pub fn version(&self) -> &SemanticVersion {
        &self.version
    }

    pub fn directory(&self) -> &Path {
        &self.directory
    }

    pub fn previous_version(&self) -> Option<&SemanticVersion> {
        self.previous_version.as_ref()
    }
}

#[derive(Debug, Clone)]
pub struct VersionedDeploymentStore {
    options: DeploymentActivationOptions,
}

impl VersionedDeploymentStore {
    pub fn new(options: DeploymentActivationOptions) -> Self {
        Self { options }
    }

    /// @dev-id TIZ-DEPLOY-011
    /// @purpose Publish a staged payload as an immutable version and make it active.
    /// @reason Preparing a new version must not modify files used by the running application.
    /// @contract Copies only regular files/directories into a fresh version path, then atomically replaces active.json; a failed pointer update leaves the previous active record intact.
    /// @depends Verified PackageStage, trusted deployment root, and atomic same-volume file replacement.
    pub fn publish_and_activate(
        &self,
        stage: PackageStage,
    ) -> Result<ActiveDeployment, DeploymentActivationError> {
        let package_id = stage.package_id().clone();
        let version = stage.version().clone();
        fs::create_dir_all(self.options.root_directory()).map_err(DeploymentActivationError::Io)?;
        let package_root = self.package_root(&package_id);
        let versions_root = package_root.join("versions");
        ensure_directory(&package_root)?;
        ensure_directory(&versions_root)?;

        let destination = versions_root.join(version.to_string());
        match fs::symlink_metadata(&destination) {
            Ok(_) => return Err(DeploymentActivationError::VersionAlreadyPublished),
            Err(error) if error.kind() == io::ErrorKind::NotFound => {}
            Err(error) => return Err(DeploymentActivationError::Io(error)),
        }

        let temporary_directory = versions_root.join(format!(".stage-{}", Uuid::new_v4()));
        fs::create_dir(&temporary_directory).map_err(DeploymentActivationError::Io)?;
        let copy_result = copy_payload(stage.payload_directory(), &temporary_directory);
        if let Err(error) = copy_result {
            let _ = fs::remove_dir_all(&temporary_directory);
            return Err(error);
        }
        if let Err(error) = fs::rename(&temporary_directory, &destination) {
            let _ = fs::remove_dir_all(&temporary_directory);
            return Err(DeploymentActivationError::Io(error));
        }

        if let Err(error) =
            integrity::write_inventory(&package_root, &package_id, &version, &destination)
        {
            let _ = fs::remove_dir_all(&destination);
            let _ = integrity::remove_inventory(&package_root, &version);
            return Err(error);
        }

        match self.activate_published(&package_id, &version, &destination) {
            Ok(active) => Ok(active),
            Err(error) => {
                let _ = fs::remove_dir_all(&destination);
                let _ = integrity::remove_inventory(&package_root, &version);
                Err(error)
            }
        }
    }

    /// @dev-id TIZ-DEPLOY-012
    /// @purpose Switch the active pointer to a previously published package version.
    /// @reason Rollback and repair need to select an immutable version without rewriting its files.
    /// @contract Requires the version directory to exist and atomically replaces the package's active record.
    /// @depends Existing version directory and atomic same-volume file replacement.
    pub fn activate_existing(
        &self,
        package_id: &PackageId,
        version: &SemanticVersion,
    ) -> Result<ActiveDeployment, DeploymentActivationError> {
        let package_root = self.package_root(package_id);
        let versions_root = package_root.join("versions");
        ensure_existing_directory(&package_root)?;
        ensure_existing_directory(&versions_root)?;
        let directory = self.version_directory(package_id, version);
        ensure_existing_directory(&directory)?;
        self.activate_published(package_id, version, &directory)
    }

    /// @dev-id TIZ-DEPLOY-014
    /// @purpose Remove the active record only when it still selects the expected version.
    /// @reason A failed first-install state write must be able to undo activation without touching another operation's version.
    /// @contract Removes active.json only after a package-scoped lock is held and the record matches expected_version.
    /// @depends Current active deployment and package-scoped operation lock.
    pub fn deactivate_if_current(
        &self,
        package_id: &PackageId,
        expected_version: &SemanticVersion,
    ) -> Result<(), DeploymentActivationError> {
        match self.current(package_id)? {
            Some(active) if active.version() == expected_version => {
                fs::remove_file(self.package_root(package_id).join(ACTIVE_FILE))
                    .map_err(DeploymentActivationError::Io)
            }
            Some(_) => Err(DeploymentActivationError::ActiveDeploymentMismatch),
            None => Ok(()),
        }
    }

    /// @dev-id TIZ-DEPLOY-013
    /// @purpose Resolve the package's current immutable deployment.
    /// @reason Launch and recovery code need one validated active-version lookup.
    /// @contract Rejects oversized, malformed, mismatched, or missing deployment records.
    /// @depends Active JSON record and package deployment directory.
    pub fn current(
        &self,
        package_id: &PackageId,
    ) -> Result<Option<ActiveDeployment>, DeploymentActivationError> {
        let package_root = self.package_root(package_id);
        match fs::symlink_metadata(&package_root) {
            Ok(metadata) if metadata.file_type().is_dir() => {}
            Ok(_) => {
                return Err(DeploymentActivationError::InvalidLayout(
                    "package deployment root is not a real directory",
                ));
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(DeploymentActivationError::Io(error)),
        }
        let versions_root = package_root.join("versions");
        ensure_existing_directory(&versions_root)?;
        let active_path = package_root.join(ACTIVE_FILE);
        match fs::symlink_metadata(&active_path) {
            Ok(metadata) if metadata.file_type().is_file() => {}
            Ok(_) => {
                return Err(DeploymentActivationError::InvalidLayout(
                    "active deployment record is not a regular file",
                ));
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(DeploymentActivationError::Io(error)),
        }
        let file = match fs::File::open(&active_path) {
            Ok(file) => file,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(DeploymentActivationError::Io(error)),
        };
        let metadata = file.metadata().map_err(DeploymentActivationError::Io)?;
        if !metadata.is_file() || metadata.len() > MAXIMUM_ACTIVE_FILE_BYTES {
            return Err(DeploymentActivationError::InvalidLayout(
                "active deployment record is not a bounded regular file",
            ));
        }
        use std::io::Read;
        let mut bytes = Vec::with_capacity(metadata.len() as usize);
        file.take(MAXIMUM_ACTIVE_FILE_BYTES + 1)
            .read_to_end(&mut bytes)
            .map_err(DeploymentActivationError::Io)?;
        if bytes.len() as u64 > MAXIMUM_ACTIVE_FILE_BYTES {
            return Err(DeploymentActivationError::InvalidLayout(
                "active deployment record exceeds the 4 KiB limit",
            ));
        }
        let document: ActiveDeploymentDocument =
            serde_json::from_slice(&bytes).map_err(DeploymentActivationError::Json)?;
        let recorded_package = PackageId::parse(&document.package_id)
            .map_err(|_| DeploymentActivationError::InvalidLayout("package ID is invalid"))?;
        let version = SemanticVersion::parse(&document.version)
            .map_err(|_| DeploymentActivationError::InvalidLayout("version is invalid"))?;
        let previous_version = document
            .previous_version
            .map(|value| {
                SemanticVersion::parse(&value).map_err(|_| {
                    DeploymentActivationError::InvalidLayout("previous version is invalid")
                })
            })
            .transpose()?;
        if &recorded_package != package_id {
            return Err(DeploymentActivationError::ActiveDeploymentMismatch);
        }
        let directory = self.version_directory(package_id, &version);
        ensure_existing_directory(&directory)?;
        Ok(Some(ActiveDeployment {
            package_id: recorded_package,
            version,
            directory,
            previous_version,
        }))
    }

    fn activate_published(
        &self,
        package_id: &PackageId,
        version: &SemanticVersion,
        directory: &Path,
    ) -> Result<ActiveDeployment, DeploymentActivationError> {
        let package_root = self.package_root(package_id);
        let previous = self.current(package_id)?;
        let previous_version = previous.map(|active| active.version);
        let document = ActiveDeploymentDocument {
            package_id: package_id.to_string(),
            version: version.to_string(),
            previous_version: previous_version.as_ref().map(ToString::to_string),
        };
        let bytes =
            serde_json::to_vec_pretty(&document).map_err(DeploymentActivationError::Json)?;
        let active_path = package_root.join(ACTIVE_FILE);
        let temporary_path = package_root.join(format!(".active-{}.tmp", Uuid::new_v4()));
        let write_result = (|| {
            let mut file = fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&temporary_path)
                .map_err(DeploymentActivationError::Io)?;
            use std::io::Write;
            file.write_all(&bytes)
                .map_err(DeploymentActivationError::Io)?;
            file.sync_all().map_err(DeploymentActivationError::Io)?;
            drop(file);
            replace_file(&temporary_path, &active_path).map_err(DeploymentActivationError::Io)
        })();
        if write_result.is_err() {
            let _ = fs::remove_file(&temporary_path);
        }
        write_result?;

        Ok(ActiveDeployment {
            package_id: package_id.clone(),
            version: version.clone(),
            directory: directory.to_path_buf(),
            previous_version,
        })
    }

    fn package_root(&self, package_id: &PackageId) -> PathBuf {
        self.options.root_directory().join(package_id.as_str())
    }

    fn version_directory(&self, package_id: &PackageId, version: &SemanticVersion) -> PathBuf {
        self.package_root(package_id)
            .join("versions")
            .join(version.to_string())
    }
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct ActiveDeploymentDocument {
    package_id: String,
    version: String,
    previous_version: Option<String>,
}

fn ensure_directory(path: &Path) -> Result<(), DeploymentActivationError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_dir() => Ok(()),
        Ok(_) => Err(DeploymentActivationError::InvalidLayout(
            "deployment directory is not a real directory",
        )),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {
            fs::create_dir(path).map_err(DeploymentActivationError::Io)
        }
        Err(error) => Err(DeploymentActivationError::Io(error)),
    }
}

fn ensure_existing_directory(path: &Path) -> Result<(), DeploymentActivationError> {
    match fs::symlink_metadata(path) {
        Ok(metadata) if metadata.file_type().is_dir() => Ok(()),
        Ok(_) => Err(DeploymentActivationError::InvalidLayout(
            "published deployment is not a real directory",
        )),
        Err(error) => Err(DeploymentActivationError::Io(error)),
    }
}

fn copy_payload(source: &Path, destination: &Path) -> Result<(), DeploymentActivationError> {
    ensure_existing_directory(source)?;
    for entry in fs::read_dir(source).map_err(DeploymentActivationError::Io)? {
        let entry = entry.map_err(DeploymentActivationError::Io)?;
        let source_path = entry.path();
        let destination_path = destination.join(entry.file_name());
        let metadata = fs::symlink_metadata(&source_path).map_err(DeploymentActivationError::Io)?;
        if metadata.file_type().is_dir() {
            fs::create_dir(&destination_path).map_err(DeploymentActivationError::Io)?;
            copy_payload(&source_path, &destination_path)?;
        } else if metadata.file_type().is_file() {
            let mut input = fs::File::open(&source_path).map_err(DeploymentActivationError::Io)?;
            let opened_metadata = input.metadata().map_err(DeploymentActivationError::Io)?;
            if !opened_metadata.is_file() || opened_metadata.len() != metadata.len() {
                return Err(DeploymentActivationError::InvalidLayout(
                    "staged payload entry changed during publication",
                ));
            }
            let mut output = fs::OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&destination_path)
                .map_err(DeploymentActivationError::Io)?;
            let copied_bytes =
                io::copy(&mut input, &mut output).map_err(DeploymentActivationError::Io)?;
            if copied_bytes != metadata.len() {
                return Err(DeploymentActivationError::InvalidLayout(
                    "staged payload changed size during publication",
                ));
            }
            output.sync_all().map_err(DeploymentActivationError::Io)?;
        } else {
            return Err(DeploymentActivationError::InvalidLayout(
                "staged payload contains a symlink or special file",
            ));
        }
    }
    Ok(())
}

fn replace_file(source: &Path, destination: &Path) -> io::Result<()> {
    #[cfg(windows)]
    {
        use std::os::windows::ffi::OsStrExt;
        use windows_sys::Win32::Storage::FileSystem::{
            MOVEFILE_REPLACE_EXISTING, MOVEFILE_WRITE_THROUGH, MoveFileExW,
        };
        let source: Vec<u16> = source
            .as_os_str()
            .encode_wide()
            .chain(std::iter::once(0))
            .collect();
        let destination: Vec<u16> = destination
            .as_os_str()
            .encode_wide()
            .chain(std::iter::once(0))
            .collect();
        let succeeded = unsafe {
            MoveFileExW(
                source.as_ptr(),
                destination.as_ptr(),
                MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH,
            )
        };
        if succeeded == 0 {
            return Err(io::Error::last_os_error());
        }
        Ok(())
    }
    #[cfg(not(windows))]
    {
        fs::rename(source, destination)
    }
}
