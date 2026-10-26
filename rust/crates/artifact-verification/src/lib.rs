// Dosya Yolu: REPO_ROOT/rust/crates/artifact-verification/src/lib.rs
// Amac: Artifact stream'ini kurulumdan once imzali descriptor boyut ve SHA-256 degerleriyle dogrulamak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Beklenen boyut asilirsa erken durur; kisa stream ve digest uyusmazliginda fail-closed sonuc verir
// Bagimli Oldugu Katman: Tool | Model

#![forbid(unsafe_code)]

use std::{fmt, io};

use sha2::{Digest, Sha256};
use tokio::io::{AsyncRead, AsyncReadExt};
use turkuaz_installer_domain::artifacts::ArtifactDescriptor;

const READ_BUFFER_BYTES: usize = 64 * 1024;

/// Artifact stream dogrulamasi sirasinda olusabilecek hassas olmayan hata.
#[derive(Debug)]
pub enum ArtifactVerificationError {
    Read(io::Error),
    LengthExceeded { expected: u64, observed: u64 },
    LengthMismatch { expected: u64, observed: u64 },
    DigestMismatch,
}

impl fmt::Display for ArtifactVerificationError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Read(_) => formatter.write_str("Artifact stream could not be read."),
            Self::LengthExceeded { expected, observed } => write!(
                formatter,
                "Artifact exceeds its declared length (expected {expected} bytes, observed at least {observed})."
            ),
            Self::LengthMismatch { expected, observed } => write!(
                formatter,
                "Artifact length does not match its declaration (expected {expected} bytes, observed {observed})."
            ),
            Self::DigestMismatch => {
                formatter.write_str("Artifact SHA-256 digest does not match its declaration.")
            }
        }
    }
}

impl std::error::Error for ArtifactVerificationError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            Self::Read(error) => Some(error),
            _ => None,
        }
    }
}

/// Reads the complete stream and returns success only when its exact length and SHA-256
/// match the signed artifact descriptor. Callers must keep the bytes in staging storage
/// and must not apply them until this function succeeds.
pub async fn verify_reader<R>(
    reader: &mut R,
    descriptor: &ArtifactDescriptor,
) -> Result<(), ArtifactVerificationError>
where
    R: AsyncRead + Unpin,
{
    let expected_length = descriptor.size_bytes();
    let mut observed_length = 0_u64;
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; READ_BUFFER_BYTES];

    loop {
        let bytes_read = reader
            .read(&mut buffer)
            .await
            .map_err(ArtifactVerificationError::Read)?;
        if bytes_read == 0 {
            break;
        }

        let chunk_length = u64::try_from(bytes_read).unwrap_or(u64::MAX);
        let next_length = observed_length
            .checked_add(chunk_length)
            .unwrap_or(u64::MAX);
        if next_length > expected_length {
            return Err(ArtifactVerificationError::LengthExceeded {
                expected: expected_length,
                observed: next_length,
            });
        }

        hasher.update(&buffer[..bytes_read]);
        observed_length = next_length;
    }

    if observed_length != expected_length {
        return Err(ArtifactVerificationError::LengthMismatch {
            expected: expected_length,
            observed: observed_length,
        });
    }

    let actual_digest = encode_lowercase_hex(&hasher.finalize());
    if actual_digest != descriptor.digest().as_str() {
        return Err(ArtifactVerificationError::DigestMismatch);
    }

    Ok(())
}

fn encode_lowercase_hex(bytes: &[u8]) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut output = String::with_capacity(bytes.len() * 2);
    for byte in bytes {
        output.push(HEX[(byte >> 4) as usize] as char);
        output.push(HEX[(byte & 0x0f) as usize] as char);
    }
    output
}
