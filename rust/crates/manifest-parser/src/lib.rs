// Dosya Yolu: REPO_ROOT/rust/crates/manifest-parser/src/lib.rs
// Amac: Signed YAML manifest alanlarini dogrulayip domain release modeline cevirmek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Stable v1 alanlari, domain invariants ve unsupported policy degerlerini fail-closed uygular
// Bagimli Oldugu Katman: Application

use std::{error::Error, fmt};

use serde::Deserialize;
use turkuaz_installer_domain::{
    DomainError,
    artifacts::{ArtifactDescriptor, ArtifactSignatureAlgorithm, ArtifactSignatureDescriptor},
    artifact_digest::ArtifactDigest,
    integrations::{
        WindowsIntegrationPolicy, WindowsProtocolIntegration, WindowsShortcutIntegration,
        WindowsShortcutLocation,
    },
    package_id::PackageId,
    package_policy::{PackageInstallMode, PackageRollbackPolicy},
    prerequisites::{Prerequisite, PrerequisiteInstallAction},
    release::{PackageDeltaArtifact, PackageInstallPolicy, PackageRelease},
    release_channel::ReleaseChannel,
    semantic_version::SemanticVersion,
};

const SUPPORTED_SCHEMA_VERSION: i32 = 1;
const AUTHENTICODE_ALGORITHM: &str = "authenticode";

#[derive(Debug)]
pub enum ManifestParseError {
    InvalidYaml(serde_yaml::Error),
    EmptyManifest,
    UnsupportedSchemaVersion,
    MissingInstallMode,
    MissingInstallTarget,
    UnsupportedChannel,
    UnsupportedInstallMode,
    UnsupportedShortcutLocation,
    UnsupportedSignatureAlgorithm,
    MissingPublisherSubject,
    MissingPrerequisiteSignature,
    InvalidPreservePath,
    Domain(DomainError),
}

impl fmt::Display for ManifestParseError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::InvalidYaml(error) => {
                write!(formatter, "Installer manifest YAML is invalid: {error}")
            }
            Self::EmptyManifest => formatter.write_str("Installer manifest is empty."),
            Self::UnsupportedSchemaVersion => {
                formatter.write_str("Installer manifest schema version is not supported.")
            }
            Self::MissingInstallMode => {
                formatter.write_str("Installer manifest install mode is required.")
            }
            Self::MissingInstallTarget => {
                formatter.write_str("Installer manifest install target is required.")
            }
            Self::UnsupportedChannel => {
                formatter.write_str("Installer manifest release channel is not supported.")
            }
            Self::UnsupportedInstallMode => formatter
                .write_str("Installer manifest install mode is not supported by Stable v1."),
            Self::UnsupportedShortcutLocation => {
                formatter.write_str("Windows shortcut location is not supported.")
            }
            Self::UnsupportedSignatureAlgorithm => formatter
                .write_str("Installer manifest artifact signature algorithm is not supported."),
            Self::MissingPublisherSubject => {
                formatter.write_str("Authenticode signature policy requires publisher_subject.")
            }
            Self::MissingPrerequisiteSignature => formatter.write_str(
                "Prerequisite auto-install artifact requires Authenticode signature policy.",
            ),
            Self::InvalidPreservePath => formatter.write_str("Preserve path must not be empty."),
            Self::Domain(error) => write!(formatter, "{error}"),
        }
    }
}

impl Error for ManifestParseError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::InvalidYaml(error) => Some(error),
            Self::Domain(error) => Some(error),
            _ => None,
        }
    }
}

impl From<DomainError> for ManifestParseError {
    fn from(error: DomainError) -> Self {
        Self::Domain(error)
    }
}

/// Parses YAML only after callers have verified the detached signature.
pub fn parse_manifest(yaml: &str) -> Result<PackageRelease, ManifestParseError> {
    if yaml.trim().is_empty() {
        return Err(ManifestParseError::EmptyManifest);
    }

    let document: Option<ManifestDocument> =
        serde_yaml::from_str(yaml).map_err(ManifestParseError::InvalidYaml)?;
    let document = document.ok_or(ManifestParseError::EmptyManifest)?;

    if document.schema_version != SUPPORTED_SCHEMA_VERSION {
        return Err(ManifestParseError::UnsupportedSchemaVersion);
    }
    if document.install.mode.trim().is_empty() {
        return Err(ManifestParseError::MissingInstallMode);
    }
    if document.install.target.trim().is_empty() {
        return Err(ManifestParseError::MissingInstallTarget);
    }

    let package_id = PackageId::parse(&document.package.id)?;
    let version = SemanticVersion::parse(&document.package.version)?;
    let channel = parse_channel(&document.package.channel)?;
    let install_mode = parse_install_mode(&document.install.mode)?;
    let artifact = parse_artifact(&document.artifact, false)?;
    let delta_artifact = parse_delta(document.artifact.delta.as_ref())?;
    let prerequisites = document
        .install
        .prerequisites
        .iter()
        .map(parse_prerequisite)
        .collect::<Result<Vec<_>, _>>()?;
    let preserve_paths = document
        .install
        .preserve_paths
        .iter()
        .map(|value| {
            let value = value.trim();
            if value.is_empty() {
                return Err(ManifestParseError::InvalidPreservePath);
            }
            Ok(value.to_owned())
        })
        .collect::<Result<Vec<_>, _>>()?;
    let windows_integration = parse_windows(document.install.windows.as_ref())?;
    let install = PackageInstallPolicy::new(
        install_mode,
        Some(&document.install.target),
        prerequisites,
        preserve_paths,
        Some(windows_integration),
    );
    let rollback = PackageRollbackPolicy::new(
        document.rollback.supported,
        document.rollback.previous_version_required,
    );

    Ok(PackageRelease::new(
        package_id,
        version,
        channel,
        artifact,
        install,
        rollback,
        delta_artifact,
    )?)
}

fn parse_channel(value: &str) -> Result<ReleaseChannel, ManifestParseError> {
    match value.trim().to_ascii_lowercase().as_str() {
        "stable" => Ok(ReleaseChannel::Stable),
        "beta" => Ok(ReleaseChannel::Beta),
        _ => Err(ManifestParseError::UnsupportedChannel),
    }
}

fn parse_install_mode(value: &str) -> Result<PackageInstallMode, ManifestParseError> {
    match value.trim().to_ascii_lowercase().as_str() {
        "full" => Ok(PackageInstallMode::Full),
        _ => Err(ManifestParseError::UnsupportedInstallMode),
    }
}

fn parse_signature(
    signature: Option<&SignatureSection>,
) -> Result<Option<ArtifactSignatureDescriptor>, ManifestParseError> {
    let Some(signature) = signature else {
        return Ok(None);
    };
    if signature.publisher_subject.trim().is_empty() {
        return Err(ManifestParseError::MissingPublisherSubject);
    }
    if signature.algorithm.trim().to_ascii_lowercase() != AUTHENTICODE_ALGORITHM {
        return Err(ManifestParseError::UnsupportedSignatureAlgorithm);
    }

    Ok(Some(ArtifactSignatureDescriptor::new(
        ArtifactSignatureAlgorithm::Authenticode,
        &signature.publisher_subject,
        signature.certificate_sha256.as_deref(),
    )?))
}

fn parse_artifact(
    artifact: &ArtifactSection,
    require_signature: bool,
) -> Result<ArtifactDescriptor, ManifestParseError> {
    let signature = parse_signature(artifact.signature.as_ref())?;
    if require_signature && signature.is_none() {
        return Err(ManifestParseError::MissingPrerequisiteSignature);
    }

    Ok(ArtifactDescriptor::new(
        &artifact.uri,
        ArtifactDigest::parse_sha256(&artifact.sha256)?,
        artifact.size_bytes,
        signature,
    )?)
}

fn parse_delta(
    delta: Option<&DeltaArtifactSection>,
) -> Result<Option<PackageDeltaArtifact>, ManifestParseError> {
    let Some(delta) = delta else {
        return Ok(None);
    };
    let artifact = parse_artifact(
        &ArtifactSection {
            uri: delta.uri.clone(),
            sha256: delta.sha256.clone(),
            size_bytes: delta.size_bytes,
            signature: delta.signature.clone(),
            delta: None,
        },
        false,
    )?;
    Ok(Some(PackageDeltaArtifact::new(
        SemanticVersion::parse(&delta.from_version)?,
        artifact,
    )))
}

fn parse_prerequisite(
    prerequisite: &PrerequisiteSection,
) -> Result<Prerequisite, ManifestParseError> {
    let install_action = prerequisite
        .install
        .as_ref()
        .map(|install| -> Result<PrerequisiteInstallAction, ManifestParseError> {
            let artifact = parse_artifact(&install.artifact, true)?;
            Ok(PrerequisiteInstallAction::new(
                artifact,
                install.arguments.clone(),
                install.requires_elevation,
            )?)
        })
        .transpose()?;
    Ok(Prerequisite::new(
        &prerequisite.id,
        &prerequisite.version,
        install_action,
    )?)
}

fn parse_windows(
    windows: Option<&WindowsIntegrationSection>,
) -> Result<WindowsIntegrationPolicy, ManifestParseError> {
    let Some(windows) = windows else {
        return Ok(WindowsIntegrationPolicy::empty());
    };

    let shortcuts = windows
        .shortcuts
        .iter()
        .map(|shortcut| {
            let location = match shortcut.location.trim().to_ascii_lowercase().as_str() {
                "desktop" => WindowsShortcutLocation::Desktop,
                "start_menu" => WindowsShortcutLocation::StartMenu,
                _ => return Err(ManifestParseError::UnsupportedShortcutLocation),
            };
            Ok(WindowsShortcutIntegration::new(
                &shortcut.id,
                &shortcut.name,
                location,
                &shortcut.executable,
            )?)
        })
        .collect::<Result<Vec<_>, ManifestParseError>>()?;
    let protocols = windows
        .protocols
        .iter()
        .map(|protocol| {
            Ok(WindowsProtocolIntegration::new(
                &protocol.scheme,
                &protocol.executable,
            )?)
        })
        .collect::<Result<Vec<_>, ManifestParseError>>()?;

    Ok(WindowsIntegrationPolicy::new(shortcuts, protocols)?)
}

#[derive(Debug, Default, Deserialize)]
#[serde(default, rename_all = "snake_case")]
struct ManifestDocument {
    schema_version: i32,
    package: PackageSection,
    artifact: ArtifactSection,
    install: InstallSection,
    rollback: RollbackSection,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct PackageSection {
    id: String,
    version: String,
    channel: String,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct ArtifactSection {
    uri: String,
    sha256: String,
    size_bytes: u64,
    signature: Option<SignatureSection>,
    delta: Option<DeltaArtifactSection>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct DeltaArtifactSection {
    from_version: String,
    uri: String,
    sha256: String,
    size_bytes: u64,
    signature: Option<SignatureSection>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct SignatureSection {
    algorithm: String,
    publisher_subject: String,
    certificate_sha256: Option<String>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct InstallSection {
    mode: String,
    target: String,
    prerequisites: Vec<PrerequisiteSection>,
    preserve_paths: Vec<String>,
    windows: Option<WindowsIntegrationSection>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct WindowsIntegrationSection {
    shortcuts: Vec<WindowsShortcutSection>,
    protocols: Vec<WindowsProtocolSection>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct WindowsShortcutSection {
    id: String,
    name: String,
    location: String,
    executable: String,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct WindowsProtocolSection {
    scheme: String,
    executable: String,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct PrerequisiteSection {
    id: String,
    version: String,
    install: Option<PrerequisiteInstallSection>,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct PrerequisiteInstallSection {
    artifact: ArtifactSection,
    arguments: Vec<String>,
    requires_elevation: bool,
}

#[derive(Debug, Default, Deserialize)]
#[serde(default)]
struct RollbackSection {
    supported: bool,
    previous_version_required: bool,
}
