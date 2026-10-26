// Dosya Yolu: REPO_ROOT/rust/crates/manifest-pipeline/src/lib.rs
// Amac: Bounded input, package trust policy ve detached CMS verification'i sirasiyla calistirmak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Imza dogrulanmadan UTF-8 decode veya YAML parse icin icerik dondurmez
// Bagimli Oldugu Katman: Application

use std::{error::Error, fmt};

use tokio::io::AsyncRead;
use turkuaz_installer_domain::package_id::PackageId;
use turkuaz_installer_manifest_trust_store::{
    TrustStoreError, YamlManifestTrustPolicyRepository,
};
use turkuaz_installer_manifest_verification::{
    ManifestVerificationError, verify_and_decode_manifest,
};
use turkuaz_installer_metadata::{
    BoundedContentError, BoundedContentReader, MAXIMUM_DETACHED_SIGNATURE_BYTES,
    MAXIMUM_MANIFEST_BYTES, validate_declared_length,
};

#[derive(Debug)]
pub enum VerifiedManifestError {
    ContentLimit(BoundedContentError),
    TrustStore(TrustStoreError),
    MissingTrustPolicy,
    Verification(ManifestVerificationError),
}

impl fmt::Display for VerifiedManifestError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::ContentLimit(error) => write!(formatter, "{error}"),
            Self::TrustStore(error) => write!(formatter, "{error}"),
            Self::MissingTrustPolicy => {
                formatter.write_str("No manifest signer trust policy is configured for this package.")
            }
            Self::Verification(error) => write!(formatter, "{error}"),
        }
    }
}

impl Error for VerifiedManifestError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::ContentLimit(error) => Some(error),
            Self::TrustStore(error) => Some(error),
            Self::Verification(error) => Some(error),
            Self::MissingTrustPolicy => None,
        }
    }
}

/// Reads bounded input streams, resolves the external package trust policy, verifies
/// the detached signature, then returns strict UTF-8 text for the YAML parser.
pub async fn read_verified_manifest<M, S>(
    manifest_source: &mut M,
    detached_signature_source: &mut S,
    manifest_content_length: Option<u64>,
    signature_content_length: Option<u64>,
    requested_package_id: &PackageId,
    trust_store: &YamlManifestTrustPolicyRepository,
) -> Result<String, VerifiedManifestError>
where
    M: AsyncRead + Unpin,
    S: AsyncRead + Unpin,
{
    validate_declared_length(
        manifest_content_length,
        MAXIMUM_MANIFEST_BYTES,
        "Manifest",
    )
    .map_err(VerifiedManifestError::ContentLimit)?;
    validate_declared_length(
        signature_content_length,
        MAXIMUM_DETACHED_SIGNATURE_BYTES,
        "Detached signature",
    )
    .map_err(VerifiedManifestError::ContentLimit)?;

    let manifest = BoundedContentReader::read(
        manifest_source,
        MAXIMUM_MANIFEST_BYTES,
        "Manifest",
    )
    .await
    .map_err(VerifiedManifestError::ContentLimit)?;
    let signature = BoundedContentReader::read(
        detached_signature_source,
        MAXIMUM_DETACHED_SIGNATURE_BYTES,
        "Detached signature",
    )
    .await
    .map_err(VerifiedManifestError::ContentLimit)?;

    let trust_policy = trust_store
        .get(requested_package_id)
        .map_err(VerifiedManifestError::TrustStore)?
        .ok_or(VerifiedManifestError::MissingTrustPolicy)?;

    verify_and_decode_manifest(&manifest, &signature, &trust_policy)
        .map_err(VerifiedManifestError::Verification)
}
