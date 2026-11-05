// Dosya Yolu: REPO_ROOT/rust/crates/metadata/src/bounded_reader.rs
// Amac: Manifest ve detached signature streamlerini sabit maksimum byte siniri ile okumak
// Modul - Rust Tool
// Version: 0.1.0
// Aciklama: Content-Length olmasa da stream limiti parser ve verifier oncesi fail-closed uygular
// Bagimli Oldugu Katman: Tool | Config

use std::{fmt, io};

use tokio::io::{AsyncRead, AsyncReadExt};

const BUFFER_SIZE: usize = 81_920;

#[derive(Debug, Default, Clone, Copy)]
pub struct BoundedContentReader;

impl BoundedContentReader {
    pub async fn read<R>(
        source: &mut R,
        maximum_bytes: usize,
        content_name: &str,
    ) -> Result<Vec<u8>, BoundedContentError>
    where
        R: AsyncRead + Unpin,
    {
        if maximum_bytes == 0 {
            return Err(BoundedContentError::InvalidLimit);
        }
        let content_name = content_name.trim();
        if content_name.is_empty() {
            return Err(BoundedContentError::InvalidContentName);
        }

        let initial_capacity = maximum_bytes.min(BUFFER_SIZE);
        let mut destination = Vec::with_capacity(initial_capacity);
        let mut buffer = vec![0_u8; initial_capacity];

        loop {
            let read = source
                .read(&mut buffer)
                .await
                .map_err(BoundedContentError::ReadFailed)?;
            if read == 0 {
                break;
            }
            if read > maximum_bytes.saturating_sub(destination.len()) {
                return Err(BoundedContentError::TooLarge {
                    content_name: content_name.to_owned(),
                });
            }
            destination.extend_from_slice(&buffer[..read]);
        }
        Ok(destination)
    }
}

pub fn validate_declared_length(
    content_length: Option<u64>,
    maximum_bytes: usize,
    content_name: &str,
) -> Result<(), BoundedContentError> {
    if maximum_bytes == 0 {
        return Err(BoundedContentError::InvalidLimit);
    }
    let content_name = content_name.trim();
    if content_name.is_empty() {
        return Err(BoundedContentError::InvalidContentName);
    }
    if content_length.is_some_and(|length| length > maximum_bytes as u64) {
        return Err(BoundedContentError::TooLarge {
            content_name: content_name.to_owned(),
        });
    }
    Ok(())
}

#[derive(Debug)]
pub enum BoundedContentError {
    InvalidLimit,
    InvalidContentName,
    TooLarge { content_name: String },
    ReadFailed(io::Error),
}

impl fmt::Display for BoundedContentError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidLimit => formatter.write_str("Maximum content size must be positive."),
            Self::InvalidContentName => formatter.write_str("Content name must not be empty."),
            Self::TooLarge { content_name } => write!(
                formatter,
                "{content_name} exceeds the configured maximum size."
            ),
            Self::ReadFailed(_) => formatter.write_str("Content stream could not be read."),
        }
    }
}

impl std::error::Error for BoundedContentError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            Self::ReadFailed(error) => Some(error),
            _ => None,
        }
    }
}
