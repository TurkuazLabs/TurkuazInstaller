// Dosya Yolu: REPO_ROOT/rust/crates/operation-lock/src/lib.rs
// Amac: Ayni package icin concurrent installer islemlerini processler arasinda engellemek
// Modul - Rust Library
// Version: 0.3.0
// Aciklama: OS advisory file lock uzerinden fail-fast package scoped lease saglar
// Bagimli Oldugu Katman: Tool | Service

use std::{
    error::Error,
    fmt,
    fs::{self, File, OpenOptions},
    io::{self, Write},
    path::{Path, PathBuf},
};

use fs2::FileExt;
use turkuaz_installer_domain::package_id::PackageId;

#[derive(Debug)]
pub enum OperationLockError {
    Locked(PackageId),
    Io(io::Error),
}

impl fmt::Display for OperationLockError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Locked(package_id) => {
                write!(
                    formatter,
                    "An operation is already active for package {package_id}"
                )
            }
            Self::Io(error) => {
                write!(
                    formatter,
                    "Could not acquire installer operation lock: {error}"
                )
            }
        }
    }
}

impl Error for OperationLockError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Locked(_) => None,
            Self::Io(error) => Some(error),
        }
    }
}

#[derive(Debug, Clone)]
pub struct FileOperationLockOptions {
    root_directory: PathBuf,
}

impl FileOperationLockOptions {
    pub fn new(root_directory: impl AsRef<Path>) -> io::Result<Self> {
        let root_directory = root_directory.as_ref();
        if root_directory.as_os_str().is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Operation lock root directory cannot be empty",
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

/// Holds the OS lock until dropped. The .lock file itself remains in place so another
/// process cannot race a new lock against deletion and recreation of the same path.
pub struct OperationLockLease {
    file: File,
}

impl Drop for OperationLockLease {
    fn drop(&mut self) {
        let _ = self.file.set_len(0);
        let _ = self.file.sync_all();
        let _ = FileExt::unlock(&self.file);
    }
}

/// @dev-id TIZ-OP-LOCK-010
/// @purpose Acquire one cross-process lock for a package operation.
/// @reason Concurrent mutations can corrupt package state or race staging and rollback.
/// @contract Fails immediately when another process holds the same package lock; different packages use separate files.
/// @depends Filesystem directory and OS advisory lock support.
pub fn acquire_package_lock(
    options: &FileOperationLockOptions,
    package_id: &PackageId,
) -> Result<OperationLockLease, OperationLockError> {
    fs::create_dir_all(options.root_directory()).map_err(OperationLockError::Io)?;

    let path = options
        .root_directory()
        .join(format!("{}.lock", package_id.as_str()));
    let file = OpenOptions::new()
        .create(true)
        .truncate(false)
        .read(true)
        .write(true)
        .open(path)
        .map_err(OperationLockError::Io)?;

    if let Err(error) = file.try_lock_exclusive() {
        if error.kind() == io::ErrorKind::WouldBlock {
            return Err(OperationLockError::Locked(package_id.clone()));
        }
        return Err(OperationLockError::Io(error));
    }

    write_owner_metadata(&file).map_err(OperationLockError::Io)?;
    Ok(OperationLockLease { file })
}

fn write_owner_metadata(file: &File) -> io::Result<()> {
    file.set_len(0)?;
    let metadata = format!(
        "pid={}\nutc_unix_seconds={}\n",
        std::process::id(),
        unix_timestamp_seconds(),
    );
    let mut writer = file;
    writer.write_all(metadata.as_bytes())?;
    writer.sync_all()
}

fn unix_timestamp_seconds() -> u64 {
    // Unix seconds are locale independent and sufficient for lock diagnostics.
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}
