// Dosya Yolu: REPO_ROOT/rust/crates/artifact-validation/src/lib.rs
// Amac: Artifact download, digest ve publisher signature kapilarini sirali uygulamak
// Modul - Rust Library
// Version: 0.2.0
// Aciklama: Dogrulanmamis artifactleri VerifiedArtifact olarak disari cikarmadan staging kalintilarini temizler
// Bagimli Oldugu Katman: Service | Tool

use std::{
    error::Error,
    fmt, io,
    path::{Path, PathBuf},
};

use tokio::{
    fs::{self, File},
    task,
};
use turkuaz_installer_artifact_transfer::{ArtifactTransferError, DefaultArtifactDownloader};
use turkuaz_installer_artifact_verification::{ArtifactVerificationError, verify_reader};
use turkuaz_installer_authenticode_verification::{
    AuthenticodeVerificationError, verify_authenticode,
};
use turkuaz_installer_domain::artifacts::ArtifactDescriptor;

#[derive(Debug)]
pub enum ArtifactValidationError {
    Transfer(ArtifactTransferError),
    Io(io::Error),
    Integrity(ArtifactVerificationError),
    Signature(AuthenticodeVerificationError),
    BlockingTask(task::JoinError),
}

impl fmt::Display for ArtifactValidationError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Transfer(error) => write!(formatter, "{error}"),
            Self::Io(error) => write!(formatter, "Artifact staging failed: {error}"),
            Self::Integrity(error) => write!(formatter, "Artifact integrity failed: {error}"),
            Self::Signature(error) => write!(formatter, "Artifact signature failed: {error}"),
            Self::BlockingTask(error) => {
                write!(formatter, "Artifact signature worker failed: {error}")
            }
        }
    }
}

impl Error for ArtifactValidationError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Transfer(error) => Some(error),
            Self::Io(error) => Some(error),
            Self::Integrity(error) => Some(error),
            Self::Signature(error) => Some(error),
            Self::BlockingTask(error) => Some(error),
        }
    }
}

/// An opaque handle proving the staged artifact passed each applicable validation gate.
/// Callers should require this value before asking the package engine to stage or apply bytes.
#[derive(Debug)]
pub struct VerifiedArtifact {
    path: PathBuf,
    descriptor: ArtifactDescriptor,
}

impl VerifiedArtifact {
    pub fn path(&self) -> &Path {
        &self.path
    }

    pub fn descriptor(&self) -> &ArtifactDescriptor {
        &self.descriptor
    }
}

/// @dev-id TIZ-ART-010
/// @purpose Download and validate the signed artifact before returning it to application code.
/// @reason A transfer path alone does not prove that its bytes match the signed release.
/// @contract Requires exact byte length and SHA-256; Authenticode is required when the descriptor declares it.
/// @depends Artifact transfer, SHA-256 verifier, and Windows Authenticode adapter.
pub async fn download_and_verify(
    downloader: &DefaultArtifactDownloader,
    artifact: &ArtifactDescriptor,
    staging_directory: impl AsRef<Path>,
) -> Result<VerifiedArtifact, ArtifactValidationError> {
    let downloaded_path = downloader
        .download(artifact, staging_directory)
        .await
        .map_err(ArtifactValidationError::Transfer)?;

    match verify_staged_artifact(&downloaded_path, artifact).await {
        Ok(verified) => Ok(verified),
        Err(error) => {
            cleanup_failed_validation(&downloaded_path).await;
            Err(error)
        }
    }
}

pub async fn verify_staged_artifact(
    artifact_path: impl AsRef<Path>,
    artifact: &ArtifactDescriptor,
) -> Result<VerifiedArtifact, ArtifactValidationError> {
    let artifact_path = artifact_path.as_ref().to_path_buf();
    let mut staged_file = File::open(&artifact_path)
        .await
        .map_err(ArtifactValidationError::Io)?;

    verify_reader(&mut staged_file, artifact)
        .await
        .map_err(ArtifactValidationError::Integrity)?;
    drop(staged_file);

    if let Some(signature) = artifact.signature().cloned() {
        let path_for_worker = artifact_path.clone();
        task::spawn_blocking(move || verify_authenticode(&path_for_worker, &signature))
            .await
            .map_err(ArtifactValidationError::BlockingTask)?
            .map_err(ArtifactValidationError::Signature)?;
    }

    Ok(VerifiedArtifact {
        path: artifact_path,
        descriptor: artifact.clone(),
    })
}

async fn cleanup_failed_validation(artifact_path: &Path) {
    let Some(download_directory) = artifact_path.parent() else {
        return;
    };
    let Some(downloads_directory) = download_directory.parent() else {
        return;
    };

    let _ = fs::remove_file(artifact_path).await;
    let _ = fs::remove_dir(download_directory).await;
    let _ = fs::remove_dir(downloads_directory).await;
}
