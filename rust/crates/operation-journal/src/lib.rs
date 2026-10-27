// Dosya Yolu: REPO_ROOT/rust/crates/operation-journal/src/lib.rs
// Amac: Install workflow recovery icin operation checkpointlerini kalici JSON journalda saklamak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Journal entry'lerini bounded okur ve ayni dizinde atomik rename ile gunceller
// Bagimli Oldugu Katman: Repo

use std::{
    error::Error,
    fmt,
    io,
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use time::OffsetDateTime;
use tokio::{
    fs::{self, OpenOptions},
    io::{AsyncReadExt, AsyncWriteExt},
};
use turkuaz_installer_domain::{
    operation::{InstallerOperationPhase, InstallerOperationType},
    package_id::PackageId,
};
use uuid::Uuid;

const MAX_JOURNAL_BYTES: u64 = 1024 * 1024;
const JOURNAL_EXTENSION: &str = "json";

#[derive(Debug)]
pub enum JournalError {
    Io(io::Error),
    Json(serde_json::Error),
    InvalidEntry(&'static str),
}

impl fmt::Display for JournalError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Operation journal I/O failed: {error}"),
            Self::Json(error) => write!(formatter, "Operation journal JSON is invalid: {error}"),
            Self::InvalidEntry(message) => write!(formatter, "Operation journal entry is invalid: {message}"),
        }
    }
}

impl Error for JournalError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Json(error) => Some(error),
            Self::InvalidEntry(_) => None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct JsonOperationJournalRepositoryOptions {
    root_directory: PathBuf,
}

impl JsonOperationJournalRepositoryOptions {
    pub fn new(root_directory: impl AsRef<Path>) -> io::Result<Self> {
        let root_directory = root_directory.as_ref();
        if root_directory.as_os_str().is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Operation journal root directory cannot be empty",
            ));
        }

        let absolute_directory = if root_directory.is_absolute() {
            root_directory.to_path_buf()
        } else {
            std::env::current_dir()?.join(root_directory)
        };
        let root_directory = match std::fs::canonicalize(&absolute_directory) {
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

#[derive(Debug, Clone)]
pub struct JournalEntry {
    pub operation_id: Uuid,
    pub package_id: PackageId,
    pub operation: InstallerOperationType,
    pub version: Option<String>,
    pub target_path: String,
    pub phase: InstallerOperationPhase,
    pub started_at_utc: OffsetDateTime,
    pub updated_at_utc: OffsetDateTime,
    pub failure: Option<String>,
    pub pending_prerequisite_id: Option<String>,
}

#[derive(Debug, Clone)]
pub struct JsonOperationJournalRepository {
    options: JsonOperationJournalRepositoryOptions,
}

impl JsonOperationJournalRepository {
    pub fn new(options: JsonOperationJournalRepositoryOptions) -> Self {
        Self { options }
    }

    /// @dev-id TIZ-OP-JOURNAL-010
    /// @purpose Load a bounded package journal and reconstruct its typed recovery checkpoint.
    /// @reason Recovery must reject malformed or unbounded state before using it.
    /// @contract Returns None when no journal exists; rejects corrupt or mismatched package data.
    /// @depends Tokio filesystem and the Community domain operation types.
    pub async fn get(&self, package_id: &PackageId) -> Result<Option<JournalEntry>, JournalError> {
        let path = self.path_for(package_id);
        let file = match fs::File::open(&path).await {
            Ok(file) => file,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(JournalError::Io(error)),
        };

        let file_length = file.metadata().await.map_err(JournalError::Io)?.len();
        if file_length > MAX_JOURNAL_BYTES {
            return Err(JournalError::InvalidEntry("journal exceeds the 1 MiB limit"));
        }

        let mut bytes = Vec::with_capacity(file_length as usize);
        file.take(MAX_JOURNAL_BYTES + 1)
            .read_to_end(&mut bytes)
            .await
            .map_err(JournalError::Io)?;
        if bytes.len() as u64 > MAX_JOURNAL_BYTES {
            return Err(JournalError::InvalidEntry("journal exceeds the 1 MiB limit"));
        }

        let document: JournalDocument =
            serde_json::from_slice(&bytes).map_err(JournalError::Json)?;
        let entry = document.into_entry()?;
        if &entry.package_id != package_id {
            return Err(JournalError::InvalidEntry(
                "journal package ID does not match its file name",
            ));
        }
        Ok(Some(entry))
    }

    /// @dev-id TIZ-OP-JOURNAL-011
    /// @purpose Persist a typed operation checkpoint for crash and reboot recovery.
    /// @reason Workflow progress must survive process termination and reboot.
    /// @contract Writes a same-directory temporary file, flushes it, then atomically replaces the package journal.
    /// @depends Tokio filesystem, JSON serialization, and unique temporary names.
    pub async fn save(&self, entry: &JournalEntry) -> Result<(), JournalError> {
        fs::create_dir_all(self.options.root_directory())
            .await
            .map_err(JournalError::Io)?;

        let path = self.path_for(&entry.package_id);
        let temporary_path = path.with_extension(format!(
            "tmp-{}",
            Uuid::new_v4()
        ));
        let document = JournalDocument::from_entry(entry);
        let bytes = serde_json::to_vec_pretty(&document).map_err(JournalError::Json)?;

        let write_result = async {
            let mut file = OpenOptions::new()
                .create_new(true)
                .write(true)
                .open(&temporary_path)
                .await
                .map_err(JournalError::Io)?;
            file.write_all(&bytes).await.map_err(JournalError::Io)?;
            file.sync_all().await.map_err(JournalError::Io)?;
            drop(file);
            fs::rename(&temporary_path, &path)
                .await
                .map_err(JournalError::Io)
        }
        .await;

        if write_result.is_err() {
            let _ = fs::remove_file(&temporary_path).await;
        }
        write_result
    }

    pub async fn delete(&self, package_id: &PackageId) -> Result<(), JournalError> {
        match fs::remove_file(self.path_for(package_id)).await {
            Ok(()) => Ok(()),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
            Err(error) => Err(JournalError::Io(error)),
        }
    }

    fn path_for(&self, package_id: &PackageId) -> PathBuf {
        self.options
            .root_directory()
            .join(format!("{}.{}", package_id.as_str(), JOURNAL_EXTENSION))
    }
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct JournalDocument {
    operation_id: Uuid,
    package_id: String,
    operation: i32,
    version: Option<String>,
    target_path: String,
    phase: i32,
    #[serde(with = "time::serde::rfc3339")]
    started_at_utc: OffsetDateTime,
    #[serde(with = "time::serde::rfc3339")]
    updated_at_utc: OffsetDateTime,
    failure: Option<String>,
    pending_prerequisite_id: Option<String>,
}

impl JournalDocument {
    fn from_entry(entry: &JournalEntry) -> Self {
        Self {
            operation_id: entry.operation_id,
            package_id: entry.package_id.to_string(),
            operation: entry.operation as i32,
            version: entry.version.clone(),
            target_path: entry.target_path.clone(),
            phase: entry.phase as i32,
            started_at_utc: entry.started_at_utc,
            updated_at_utc: entry.updated_at_utc,
            failure: entry.failure.clone(),
            pending_prerequisite_id: entry.pending_prerequisite_id.clone(),
        }
    }

    fn into_entry(self) -> Result<JournalEntry, JournalError> {
        Ok(JournalEntry {
            operation_id: self.operation_id,
            package_id: PackageId::parse(&self.package_id)
                .map_err(|_| JournalError::InvalidEntry("package ID is invalid"))?,
            operation: operation_from_number(self.operation)?,
            version: self.version,
            target_path: self.target_path,
            phase: phase_from_number(self.phase)?,
            started_at_utc: self.started_at_utc,
            updated_at_utc: self.updated_at_utc,
            failure: self.failure,
            pending_prerequisite_id: self.pending_prerequisite_id,
        })
    }
}

fn operation_from_number(value: i32) -> Result<InstallerOperationType, JournalError> {
    match value {
        0 => Ok(InstallerOperationType::Install),
        1 => Ok(InstallerOperationType::Update),
        2 => Ok(InstallerOperationType::Repair),
        3 => Ok(InstallerOperationType::Rollback),
        4 => Ok(InstallerOperationType::Uninstall),
        _ => Err(JournalError::InvalidEntry("operation value is unknown")),
    }
}

fn phase_from_number(value: i32) -> Result<InstallerOperationPhase, JournalError> {
    match value {
        0 => Ok(InstallerOperationPhase::Started),
        1 => Ok(InstallerOperationPhase::ValidatingPrerequisites),
        2 => Ok(InstallerOperationPhase::Downloading),
        3 => Ok(InstallerOperationPhase::Verifying),
        4 => Ok(InstallerOperationPhase::Staging),
        5 => Ok(InstallerOperationPhase::Applying),
        6 => Ok(InstallerOperationPhase::SavingState),
        7 => Ok(InstallerOperationPhase::RemovingState),
        8 => Ok(InstallerOperationPhase::Completed),
        9 => Ok(InstallerOperationPhase::Cancelled),
        10 => Ok(InstallerOperationPhase::Failed),
        11 => Ok(InstallerOperationPhase::AwaitingReboot),
        12 => Ok(InstallerOperationPhase::RebootResumeArmed),
        13 => Ok(InstallerOperationPhase::ApplyingIntegrations),
        _ => Err(JournalError::InvalidEntry("phase value is unknown")),
    }
}
