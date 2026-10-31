// Dosya Yolu: REPO_ROOT/rust/crates/artifact-transfer/src/lib.rs
// Amac: Signed artifact descriptor'larini guvenli temporary staging alanina stream etmek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: HTTPS/file transferde exact-size kontrolu ve partial file cleanup uygular
// Bagimli Oldugu Katman: Infrastructure

use std::{
    error::Error,
    fmt, io,
    path::{Path, PathBuf},
};

use percent_encoding::percent_decode_str;
use reqwest::{Client, Response, redirect::Policy};
use tokio::{
    fs::{self, File, OpenOptions},
    io::{AsyncRead, AsyncReadExt, AsyncWriteExt},
};
use turkuaz_installer_domain::artifacts::ArtifactDescriptor;
use uuid::Uuid;

const DOWNLOADS_DIRECTORY: &str = "downloads";
const PARTIAL_SUFFIX: &str = ".partial";
const FALLBACK_FILE_NAME: &str = "artifact.bin";
const BUFFER_SIZE: usize = 81_920;

#[derive(Debug)]
pub enum ArtifactTransferError {
    InvalidStagingPath,
    UnsupportedScheme,
    InvalidFileUri,
    Http(reqwest::Error),
    Io(io::Error),
    ContentLengthMismatch,
    SourceSizeMismatch,
    ExceededDeclaredSize,
    FinalSizeMismatch,
    SizeOverflow,
}

impl fmt::Display for ArtifactTransferError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidStagingPath => formatter.write_str("Staging directory is invalid."),
            Self::UnsupportedScheme => {
                formatter.write_str("Artifact downloader only supports HTTPS and file URIs.")
            }
            Self::InvalidFileUri => formatter.write_str("Local artifact file URI is invalid."),
            Self::Http(error) => write!(formatter, "Artifact download failed: {error}"),
            Self::Io(error) => write!(formatter, "Artifact staging failed: {error}"),
            Self::ContentLengthMismatch => formatter
                .write_str("Artifact Content-Length does not match signed release metadata."),
            Self::SourceSizeMismatch => {
                formatter.write_str("Local artifact size does not match signed release metadata.")
            }
            Self::ExceededDeclaredSize => formatter
                .write_str("Artifact exceeded the size declared by signed release metadata."),
            Self::FinalSizeMismatch => {
                formatter.write_str("Artifact size does not match signed release metadata.")
            }
            Self::SizeOverflow => formatter.write_str("Artifact byte count overflowed."),
        }
    }
}

impl Error for ArtifactTransferError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Http(error) => Some(error),
            Self::Io(error) => Some(error),
            _ => None,
        }
    }
}

/// Streams signed artifacts to unique staging directories. Successful downloads
/// are only staged; callers must verify digest and Authenticode before applying.
#[derive(Debug, Clone)]
pub struct DefaultArtifactDownloader {
    http_client: Client,
}

impl DefaultArtifactDownloader {
    pub fn new() -> Result<Self, ArtifactTransferError> {
        let http_client = Client::builder()
            .redirect(Policy::custom(|attempt| {
                if attempt.url().scheme() == "https" && attempt.previous().len() < 10 {
                    attempt.follow()
                } else {
                    attempt.stop()
                }
            }))
            .build()
            .map_err(ArtifactTransferError::Http)?;

        Ok(Self { http_client })
    }

    pub async fn download(
        &self,
        artifact: &ArtifactDescriptor,
        staging_directory: impl AsRef<Path>,
    ) -> Result<PathBuf, ArtifactTransferError> {
        let staging_directory = staging_directory.as_ref();
        if staging_directory.as_os_str().is_empty() {
            return Err(ArtifactTransferError::InvalidStagingPath);
        }

        fs::create_dir_all(staging_directory)
            .await
            .map_err(ArtifactTransferError::Io)?;
        let staging_root = staging_directory
            .canonicalize()
            .map_err(ArtifactTransferError::Io)?;
        if !staging_root.is_dir() {
            return Err(ArtifactTransferError::InvalidStagingPath);
        }
        let downloads_root = staging_root.join(DOWNLOADS_DIRECTORY);
        fs::create_dir_all(&downloads_root)
            .await
            .map_err(ArtifactTransferError::Io)?;

        let download_root = create_unique_directory(&downloads_root).await?;
        let destination_path = download_root.join(resolve_file_name(artifact));
        let partial_path = append_suffix(&destination_path, PARTIAL_SUFFIX);

        match self.download_into(artifact, &partial_path).await {
            Ok(()) => {
                if let Err(error) = fs::rename(&partial_path, &destination_path).await {
                    cleanup_failed_staging(&partial_path, &download_root, &downloads_root).await;
                    return Err(ArtifactTransferError::Io(error));
                }
                Ok(destination_path)
            }
            Err(error) => {
                cleanup_failed_staging(&partial_path, &download_root, &downloads_root).await;
                Err(error)
            }
        }
    }

    async fn download_into(
        &self,
        artifact: &ArtifactDescriptor,
        partial_path: &Path,
    ) -> Result<(), ArtifactTransferError> {
        let uri = artifact.uri();
        match uri.scheme() {
            "file" => {
                let source_path = uri
                    .to_file_path()
                    .map_err(|_| ArtifactTransferError::InvalidFileUri)?;
                let mut source = File::open(source_path)
                    .await
                    .map_err(ArtifactTransferError::Io)?;
                let metadata = source.metadata().await.map_err(ArtifactTransferError::Io)?;
                if metadata.len() != artifact.size_bytes() {
                    return Err(ArtifactTransferError::SourceSizeMismatch);
                }

                let mut destination = create_destination(partial_path).await?;
                copy_exact(&mut source, &mut destination, artifact.size_bytes()).await?;
                destination.flush().await.map_err(ArtifactTransferError::Io)
            }
            "https" => {
                let response = self
                    .http_client
                    .get(uri.clone())
                    .send()
                    .await
                    .map_err(ArtifactTransferError::Http)?
                    .error_for_status()
                    .map_err(ArtifactTransferError::Http)?;
                if response
                    .content_length()
                    .is_some_and(|length| length != artifact.size_bytes())
                {
                    return Err(ArtifactTransferError::ContentLengthMismatch);
                }

                let mut destination = create_destination(partial_path).await?;
                copy_response_exact(response, &mut destination, artifact.size_bytes()).await?;
                destination.flush().await.map_err(ArtifactTransferError::Io)
            }
            _ => Err(ArtifactTransferError::UnsupportedScheme),
        }
    }
}

async fn create_unique_directory(downloads_root: &Path) -> Result<PathBuf, ArtifactTransferError> {
    for _ in 0..8 {
        let directory = downloads_root.join(Uuid::new_v4().simple().to_string());
        match fs::create_dir(&directory).await {
            Ok(()) => return Ok(directory),
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => continue,
            Err(error) => return Err(ArtifactTransferError::Io(error)),
        }
    }
    Err(ArtifactTransferError::Io(io::Error::new(
        io::ErrorKind::AlreadyExists,
        "Could not allocate a unique artifact staging directory.",
    )))
}

async fn create_destination(path: &Path) -> Result<File, ArtifactTransferError> {
    OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .await
        .map_err(ArtifactTransferError::Io)
}

async fn copy_response_exact(
    mut response: Response,
    destination: &mut File,
    expected_size: u64,
) -> Result<(), ArtifactTransferError> {
    let mut total_bytes = 0_u64;
    while let Some(chunk) = response
        .chunk()
        .await
        .map_err(ArtifactTransferError::Http)?
    {
        let chunk_size =
            u64::try_from(chunk.len()).map_err(|_| ArtifactTransferError::SizeOverflow)?;
        total_bytes = total_bytes
            .checked_add(chunk_size)
            .ok_or(ArtifactTransferError::SizeOverflow)?;
        if total_bytes > expected_size {
            return Err(ArtifactTransferError::ExceededDeclaredSize);
        }
        destination
            .write_all(&chunk)
            .await
            .map_err(ArtifactTransferError::Io)?;
    }
    if total_bytes != expected_size {
        return Err(ArtifactTransferError::FinalSizeMismatch);
    }
    Ok(())
}

async fn copy_exact<R>(
    source: &mut R,
    destination: &mut File,
    expected_size: u64,
) -> Result<(), ArtifactTransferError>
where
    R: AsyncRead + Unpin,
{
    let mut buffer = vec![0_u8; BUFFER_SIZE];
    let mut total_bytes = 0_u64;
    loop {
        let read = source
            .read(&mut buffer)
            .await
            .map_err(ArtifactTransferError::Io)?;
        if read == 0 {
            break;
        }
        let read_size = u64::try_from(read).map_err(|_| ArtifactTransferError::SizeOverflow)?;
        total_bytes = total_bytes
            .checked_add(read_size)
            .ok_or(ArtifactTransferError::SizeOverflow)?;
        if total_bytes > expected_size {
            return Err(ArtifactTransferError::ExceededDeclaredSize);
        }
        destination
            .write_all(&buffer[..read])
            .await
            .map_err(ArtifactTransferError::Io)?;
    }
    if total_bytes != expected_size {
        return Err(ArtifactTransferError::FinalSizeMismatch);
    }
    Ok(())
}

fn resolve_file_name(artifact: &ArtifactDescriptor) -> String {
    let last_segment = artifact
        .uri()
        .path_segments()
        .and_then(Iterator::last)
        .unwrap_or_default();
    let decoded = percent_decode_str(last_segment).decode_utf8_lossy();
    let normalized = decoded.replace('\\', "/");
    let sanitized = normalized.rsplit('/').next().unwrap_or_default().trim();

    if sanitized.is_empty() || matches!(sanitized, "." | "..") || sanitized.contains('\0') {
        FALLBACK_FILE_NAME.to_owned()
    } else {
        sanitized.to_owned()
    }
}

fn append_suffix(path: &Path, suffix: &str) -> PathBuf {
    let mut name = path.as_os_str().to_os_string();
    name.push(suffix);
    PathBuf::from(name)
}

async fn cleanup_failed_staging(partial_path: &Path, download_root: &Path, downloads_root: &Path) {
    let _ = fs::remove_file(partial_path).await;
    let _ = fs::remove_dir(download_root).await;
    let _ = fs::remove_dir(downloads_root).await;
}
