// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/artifact_digest.rs
// Amac: SHA-256 artifact digest degerini typed ve dogrulanmis bicimde temsil etmek
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Install ve rollback oncesi 64 hex karakter integrity kurali uygular
// Bagimli Oldugu Katman: Model

use crate::DomainError;

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct ArtifactDigest(String);

impl ArtifactDigest {
    pub fn parse_sha256(value: &str) -> Result<Self, DomainError> {
        let normalized = value.trim().to_ascii_lowercase();
        if normalized.len() != 64 || !normalized.bytes().all(|byte| byte.is_ascii_hexdigit()) {
            return Err(DomainError::InvalidSha256Digest);
        }

        Ok(Self(normalized))
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl std::fmt::Display for ArtifactDigest {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        formatter.write_str(&self.0)
    }
}
