// Dosya Yolu: REPO_ROOT/rust/crates/install-state-store/src/lib.rs
// Amac: Kurulu package state kayitlarini kalici JSON dosyalarinda yonetmek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Bounded okuma, deterministik listeleme ve atomik state guncellemesi saglar
// Bagimli Oldugu Katman: Repo

use std::{
    error::Error,
    fmt, io,
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use tokio::{
    fs::{self, OpenOptions},
    io::{AsyncReadExt, AsyncWriteExt},
};
use turkuaz_installer_domain::{
    package_id::PackageId,
    release_channel::ReleaseChannel,
    semantic_version::SemanticVersion,
    state::InstalledPackageState,
};
use uuid::Uuid;

const MAX_STATE_BYTES: u64 = 1024 * 1024;
const STATE_EXTENSION: &str = "json";

#[derive(Debug)]
pub enum InstallStateStoreError {
    Io(io::Error),
    Json(serde_json::Error),
    InvalidState(&'static str),
}

impl fmt::Display for InstallStateStoreError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Install state I/O failed: {error}"),
            Self::Json(error) => write!(formatter, "Install state JSON is invalid: {error}"),
            Self::InvalidState(message) => {
                write!(formatter, "Install state is invalid: {message}")
            }
        }
    }
}

impl Error for InstallStateStoreError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Json(error) => Some(error),
            Self::InvalidState(_) => None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct JsonInstallStateRepositoryOptions {
    root_directory: PathBuf,
}

impl JsonInstallStateRepositoryOptions {
    pub fn new(root_directory: impl AsRef<Path>) -> io::Result<Self> {
        let root_directory = root_directory.as_ref();
        if root_directory.as_os_str().is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Install state root directory cannot be empty",
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
pub struct JsonInstallStateRepository {
    options: JsonInstallStateRepositoryOptions,
}

impl JsonInstallStateRepository {
    pub fn new(options: JsonInstallStateRepositoryOptions) -> Self {
        Self { options }
    }

    pub async fn get(
        &self,
        package_id: &PackageId,
    ) -> Result<Option<InstalledPackageState>, InstallStateStoreError> {
        let path = self.path_for(package_id);
        let document = match read_document(&path).await {
            Ok(document) => document,
            Err(InstallStateStoreError::Io(error))
                if error.kind() == io::ErrorKind::NotFound =>
            {
                return Ok(None);
            }
            Err(error) => return Err(error),
        };
        let state = document.into_state()?;
        if state.package_id() != package_id {
            return Err(InstallStateStoreError::InvalidState(
                "package ID does not match its file name",
            ));
        }
        Ok(Some(state))
    }

    pub async fn list(
        &self,
    ) -> Result<Vec<InstalledPackageState>, InstallStateStoreError> {
        let mut directory = match fs::read_dir(self.options.root_directory()).await {
            Ok(directory) => directory,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(Vec::new()),
            Err(error) => return Err(InstallStateStoreError::Io(error)),
        };

        let mut paths = Vec::new();
        while let Some(entry) = directory
            .next_entry()
            .await
            .map_err(InstallStateStoreError::Io)?
        {
            let path = entry.path();
            if path.extension().is_some_and(|extension| extension == STATE_EXTENSION) {
                paths.push(path);
            }
        }
        paths.sort_by(|left, right| left.file_name().cmp(&right.file_name()));

        let mut states = Vec::with_capacity(paths.len());
        for path in paths {
            let document = read_document(&path).await?;
            let state = document.into_state()?;
            let expected_name = format!("{}.{}", state.package_id(), STATE_EXTENSION);
            if path.file_name().is_none_or(|name| name != expected_name.as_str()) {
                return Err(InstallStateStoreError::InvalidState(
                    "package ID does not match its file name",
                ));
            }
            states.push(state);
        }
        Ok(states)
    }

    /// @dev-id TIZ-STATE-010
    /// @purpose Persist installed package version, channel, and target state.
    /// @reason Subsequent update, repair, rollback, and uninstall workflows need an authoritative local record.
    /// @contract Serializes the existing PascalCase/numeric-enum JSON shape and atomically replaces the package file.
    /// @depends Tokio filesystem, serde JSON, and Community domain types.
    pub async fn save(
        &self,
        state: &InstalledPackageState,
    ) -> Result<(), InstallStateStoreError> {
        fs::create_dir_all(self.options.root_directory())
            .await
            .map_err(InstallStateStoreError::Io)?;

        let path = self.path_for(state.package_id());
        let temporary_path = path.with_extension(format!("tmp-{}", Uuid::new_v4()));
        let document = StateDocument::from_state(state);
        let bytes = serde_json::to_vec_pretty(&document).map_err(InstallStateStoreError::Json)?;

        let write_result = async {
            let mut file = OpenOptions::new()
                .create_new(true)
                .write(true)
                .open(&temporary_path)
                .await
                .map_err(InstallStateStoreError::Io)?;
            file.write_all(&bytes)
                .await
                .map_err(InstallStateStoreError::Io)?;
            file.sync_all().await.map_err(InstallStateStoreError::Io)?;
            drop(file);
            fs::rename(&temporary_path, &path)
                .await
                .map_err(InstallStateStoreError::Io)
        }
        .await;

        if write_result.is_err() {
            let _ = fs::remove_file(&temporary_path).await;
        }
        write_result
    }

    pub async fn delete(
        &self,
        package_id: &PackageId,
    ) -> Result<(), InstallStateStoreError> {
        match fs::remove_file(self.path_for(package_id)).await {
            Ok(()) => Ok(()),
            Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
            Err(error) => Err(InstallStateStoreError::Io(error)),
        }
    }

    fn path_for(&self, package_id: &PackageId) -> PathBuf {
        self.options
            .root_directory()
            .join(format!("{}.{}", package_id.as_str(), STATE_EXTENSION))
    }
}

async fn read_document(path: &Path) -> Result<StateDocument, InstallStateStoreError> {
    let file = fs::File::open(path)
        .await
        .map_err(InstallStateStoreError::Io)?;
    let file_length = file
        .metadata()
        .await
        .map_err(InstallStateStoreError::Io)?
        .len();
    if file_length > MAX_STATE_BYTES {
        return Err(InstallStateStoreError::InvalidState(
            "state document exceeds the 1 MiB limit",
        ));
    }

    let mut bytes = Vec::with_capacity(file_length as usize);
    file.take(MAX_STATE_BYTES + 1)
        .read_to_end(&mut bytes)
        .await
        .map_err(InstallStateStoreError::Io)?;
    if bytes.len() as u64 > MAX_STATE_BYTES {
        return Err(InstallStateStoreError::InvalidState(
            "state document exceeds the 1 MiB limit",
        ));
    }
    serde_json::from_slice(&bytes).map_err(InstallStateStoreError::Json)
}

#[derive(Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct StateDocument {
    package_id: String,
    version: String,
    channel: i32,
    target_path: String,
}

impl StateDocument {
    fn from_state(state: &InstalledPackageState) -> Self {
        Self {
            package_id: state.package_id().to_string(),
            version: state.version().to_string(),
            channel: match state.channel() {
                ReleaseChannel::Stable => 0,
                ReleaseChannel::Beta => 1,
            },
            target_path: state.target_path().to_owned(),
        }
    }

    fn into_state(self) -> Result<InstalledPackageState, InstallStateStoreError> {
        let package_id = PackageId::parse(&self.package_id)
            .map_err(|_| InstallStateStoreError::InvalidState("package ID is invalid"))?;
        let version = SemanticVersion::parse(&self.version)
            .map_err(|_| InstallStateStoreError::InvalidState("version is invalid"))?;
        let channel = match self.channel {
            0 => ReleaseChannel::Stable,
            1 => ReleaseChannel::Beta,
            _ => {
                return Err(InstallStateStoreError::InvalidState(
                    "release channel value is unknown",
                ));
            }
        };

        Ok(InstalledPackageState::new(
            package_id,
            version,
            channel,
            &self.target_path,
        ))
    }
}
