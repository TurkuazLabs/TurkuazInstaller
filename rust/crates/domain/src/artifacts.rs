// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/artifacts.rs
// Amac: Signed release artifact URI, digest, size ve publisher pin policy'sini typed domain modelinde tasimak
// Modul - Rust Domain
// Version: 0.2.0
// Aciklama: HTTPS/file scope, pozitif boyut ve Authenticode publisher/SHA-256 kontratini uygular
// Bagimli Oldugu Katman: Model | Service | Tool

use url::Url;

use crate::{DomainError, artifact_digest::ArtifactDigest};

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum ArtifactSignatureAlgorithm {
    Authenticode = 0,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArtifactSignatureDescriptor {
    algorithm: ArtifactSignatureAlgorithm,
    publisher_subject: String,
    certificate_sha256: Option<String>,
}

impl ArtifactSignatureDescriptor {
    pub fn new(
        algorithm: ArtifactSignatureAlgorithm,
        publisher_subject: &str,
        certificate_sha256: Option<&str>,
    ) -> Result<Self, DomainError> {
        let publisher_subject = publisher_subject.trim();
        if publisher_subject.is_empty() {
            return Err(DomainError::InvalidSignatureDescriptor);
        }
        let certificate_sha256 = normalize_optional_sha256(certificate_sha256)?;
        Ok(Self {
            algorithm,
            publisher_subject: publisher_subject.to_owned(),
            certificate_sha256,
        })
    }

    pub const fn algorithm(&self) -> ArtifactSignatureAlgorithm {
        self.algorithm
    }

    pub fn publisher_subject(&self) -> &str {
        &self.publisher_subject
    }

    pub fn certificate_sha256(&self) -> Option<&str> {
        self.certificate_sha256.as_deref()
    }
}

fn normalize_optional_sha256(value: Option<&str>) -> Result<Option<String>, DomainError> {
    let Some(value) = value.filter(|value| !value.trim().is_empty()) else {
        return Ok(None);
    };
    let normalized: String = value
        .trim()
        .chars()
        .filter(|character| *character != ':' && *character != ' ')
        .flat_map(char::to_lowercase)
        .collect();
    if normalized.len() != 64 || !normalized.bytes().all(|byte| byte.is_ascii_hexdigit()) {
        return Err(DomainError::InvalidSignatureDescriptor);
    }
    Ok(Some(normalized))
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ArtifactDescriptor {
    uri: Url,
    digest: ArtifactDigest,
    size_bytes: u64,
    signature: Option<ArtifactSignatureDescriptor>,
}

impl ArtifactDescriptor {
    pub fn new(
        uri: &str,
        digest: ArtifactDigest,
        size_bytes: u64,
        signature: Option<ArtifactSignatureDescriptor>,
    ) -> Result<Self, DomainError> {
        let uri = Url::parse(uri).map_err(|_| DomainError::InvalidArtifactDescriptor)?;
        if !matches!(uri.scheme(), "https" | "file") || size_bytes == 0 {
            return Err(DomainError::InvalidArtifactDescriptor);
        }
        Ok(Self {
            uri,
            digest,
            size_bytes,
            signature,
        })
    }

    pub fn uri(&self) -> &Url {
        &self.uri
    }

    pub fn digest(&self) -> &ArtifactDigest {
        &self.digest
    }

    pub const fn size_bytes(&self) -> u64 {
        self.size_bytes
    }

    pub fn signature(&self) -> Option<&ArtifactSignatureDescriptor> {
        self.signature.as_ref()
    }
}
