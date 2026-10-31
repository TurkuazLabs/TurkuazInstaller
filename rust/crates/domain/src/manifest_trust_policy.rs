// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/manifest_trust_policy.rs
// Amac: Manifest imzacisi icin package kapsamli publisher subject ve SHA-256 trust pinini tasimak
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Imza dogrulama anahtarini manifestten ayri tutar ve certificate hash pinini zorunlu kilir
// Bagimli Oldugu Katman: Model

use crate::{DomainError, package_id::PackageId};

/// Manifest CMS imzacisi icin paket disinda tanimlanan publisher trust policy.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ManifestTrustPolicy {
    package_id: PackageId,
    publisher_subject: String,
    certificate_sha256: String,
}

impl ManifestTrustPolicy {
    pub fn new(
        package_id: PackageId,
        publisher_subject: &str,
        certificate_sha256: &str,
    ) -> Result<Self, DomainError> {
        let publisher_subject = publisher_subject.trim();
        let certificate_sha256 = certificate_sha256.trim().to_ascii_lowercase();
        if publisher_subject.is_empty()
            || certificate_sha256.len() != 64
            || !certificate_sha256
                .bytes()
                .all(|byte| byte.is_ascii_hexdigit())
        {
            return Err(DomainError::InvalidManifestTrustPolicy);
        }

        Ok(Self {
            package_id,
            publisher_subject: publisher_subject.to_owned(),
            certificate_sha256,
        })
    }

    pub fn package_id(&self) -> &PackageId {
        &self.package_id
    }

    pub fn publisher_subject(&self) -> &str {
        &self.publisher_subject
    }

    pub fn certificate_sha256(&self) -> &str {
        &self.certificate_sha256
    }
}
