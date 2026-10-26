// Dosya Yolu: REPO_ROOT/rust/crates/manifest-trust-store/src/lib.rs
// Amac: Package bazli manifest signer policy'lerini dis YAML dosyasindan resolve etmek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Schema version, package kimligi, duplicate kayit ve policy pinini fail-closed dogrular
// Bagimli Oldugu Katman: Infrastructure

use std::{
    collections::HashSet,
    error::Error,
    fmt,
    fs,
    io,
    path::{Path, PathBuf},
};

use serde::Deserialize;
use turkuaz_installer_domain::{
    manifest_trust_policy::ManifestTrustPolicy, package_id::PackageId,
};

const SUPPORTED_SCHEMA_VERSION: u32 = 1;

#[derive(Debug)]
pub enum TrustStoreError {
    Io(io::Error),
    InvalidYaml(serde_yaml::Error),
    EmptyDocument,
    UnsupportedSchemaVersion,
    InvalidPackageId,
    DuplicatePackageId(String),
    InvalidTrustPolicy,
}

impl fmt::Display for TrustStoreError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Could not read manifest trust store: {error}"),
            Self::InvalidYaml(error) => write!(formatter, "Manifest trust store YAML is invalid: {error}"),
            Self::EmptyDocument => formatter.write_str("Manifest trust store is empty."),
            Self::UnsupportedSchemaVersion => {
                formatter.write_str("Manifest trust store schema version is not supported.")
            }
            Self::InvalidPackageId => {
                formatter.write_str("Manifest trust store contains an invalid package id.")
            }
            Self::DuplicatePackageId(package_id) => {
                write!(formatter, "Manifest trust store contains duplicate package id: {package_id}")
            }
            Self::InvalidTrustPolicy => {
                formatter.write_str("Manifest trust policy is invalid.")
            }
        }
    }
}

impl Error for TrustStoreError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::InvalidYaml(error) => Some(error),
            _ => None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct YamlManifestTrustPolicyRepository {
    file_path: PathBuf,
}

impl YamlManifestTrustPolicyRepository {
    pub fn new(file_path: impl AsRef<Path>) -> Result<Self, TrustStoreError> {
        let path = file_path.as_ref();
        if path.as_os_str().is_empty() {
            return Err(TrustStoreError::Io(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Trust store path is empty.",
            )));
        }

        let file_path = if path.is_absolute() {
            path.to_path_buf()
        } else {
            std::env::current_dir()
                .map_err(TrustStoreError::Io)?
                .join(path)
        };

        Ok(Self { file_path })
    }

    /// Returns no policy when the out-of-band trust store does not exist.
    pub fn get(&self, package_id: &PackageId) -> Result<Option<ManifestTrustPolicy>, TrustStoreError> {
        let yaml = match fs::read_to_string(&self.file_path) {
            Ok(yaml) => yaml,
            Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(TrustStoreError::Io(error)),
        };

        let document: Option<TrustStoreDocument> =
            serde_yaml::from_str(&yaml).map_err(TrustStoreError::InvalidYaml)?;
        let document = document.ok_or(TrustStoreError::EmptyDocument)?;

        if document.schema_version != SUPPORTED_SCHEMA_VERSION {
            return Err(TrustStoreError::UnsupportedSchemaVersion);
        }

        let mut seen_package_ids = HashSet::new();
        let mut matching_policy = None;

        for entry in document.packages {
            let configured_package_id = PackageId::parse(&entry.id)
                .map_err(|_| TrustStoreError::InvalidPackageId)?;

            if !seen_package_ids.insert(configured_package_id.clone()) {
                return Err(TrustStoreError::DuplicatePackageId(
                    configured_package_id.to_string(),
                ));
            }

            if configured_package_id == *package_id {
                matching_policy = Some(
                    ManifestTrustPolicy::new(
                        configured_package_id,
                        &entry.publisher_subject,
                        &entry.certificate_sha256,
                    )
                    .map_err(|_| TrustStoreError::InvalidTrustPolicy)?,
                );
            }
        }

        Ok(matching_policy)
    }
}

#[derive(Debug, Default, Deserialize)]
#[serde(default, rename_all = "snake_case")]
struct TrustStoreDocument {
    schema_version: u32,
    packages: Vec<PackageTrustDocument>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default, rename_all = "snake_case")]
struct PackageTrustDocument {
    id: String,
    publisher_subject: String,
    certificate_sha256: String,
}
