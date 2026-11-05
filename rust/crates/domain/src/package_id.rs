// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/package_id.rs
// Amac: Installer paket kimligini typed deger olarak temsil etmek
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: PackageId contract desenini ve invariantini tek noktada uygular
// Bagimli Oldugu Katman: Model

use crate::DomainError;

#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct PackageId(String);

impl PackageId {
    pub fn parse(value: &str) -> Result<Self, DomainError> {
        let normalized = value.trim().to_lowercase();
        let bytes = normalized.as_bytes();

        let valid = (2..=64).contains(&bytes.len())
            && (bytes[0].is_ascii_lowercase() || bytes[0].is_ascii_digit());

        if !valid
            || !bytes[1..].iter().all(|byte| {
                byte.is_ascii_lowercase()
                    || byte.is_ascii_digit()
                    || matches!(*byte, b'.' | b'_' | b'-')
            })
        {
            return Err(DomainError::InvalidPackageId);
        }

        Ok(Self(normalized))
    }

    pub fn as_str(&self) -> &str {
        &self.0
    }
}

impl std::fmt::Display for PackageId {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        formatter.write_str(&self.0)
    }
}
