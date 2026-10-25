// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/lib.rs
// Amac: Rust domain kutuphanesinin public modullerini ve ortak hata tipini sunmak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: .NET Domain tiplerinin Rust portu icin platformdan bagimsiz giris noktasi
// Bagimli Oldugu Katman: Model

#![forbid(unsafe_code)]

pub mod artifact_digest;
pub mod package_id;
pub mod release_channel;
pub mod semantic_version;

/// Domain degerleri parse edilirken olusan guvenli ve hassas veri icermeyen hata.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DomainError {
    InvalidPackageId,
    InvalidSemanticVersion,
    InvalidSha256Digest,
}

impl std::fmt::Display for DomainError {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        let message = match self {
            Self::InvalidPackageId => "Package id does not match the installer contract.",
            Self::InvalidSemanticVersion => "Version is not valid SemVer 2.0.",
            Self::InvalidSha256Digest => {
                "SHA-256 digest must contain exactly 64 hexadecimal characters."
            }
        };

        formatter.write_str(message)
    }
}

impl std::error::Error for DomainError {}
