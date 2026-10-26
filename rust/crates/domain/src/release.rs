// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/release.rs
// Amac: Package release, install policy, delta metadata ve immutable operation plansini typed tasimak
// Modul - Rust Domain
// Version: 0.2.0
// Aciklama: Update ve rollback hedeflerinin surum yonunu domain seviyesinde dogrular
// Bagimli Oldugu Katman: Model | Service

use crate::{
    artifacts::ArtifactDescriptor,
    integrations::WindowsIntegrationPolicy,
    package_id::PackageId,
    package_policy::{PackageInstallMode, PackageRollbackPolicy},
    prerequisites::Prerequisite,
    release_channel::ReleaseChannel,
    semantic_version::SemanticVersion,
    DomainError,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PackageInstallPolicy {
    mode: PackageInstallMode,
    default_target_path: Option<String>,
    prerequisites: Vec<Prerequisite>,
    preserve_paths: Vec<String>,
    windows_integration: WindowsIntegrationPolicy,
}

impl PackageInstallPolicy {
    pub fn new(
        mode: PackageInstallMode,
        default_target_path: Option<&str>,
        prerequisites: Vec<Prerequisite>,
        preserve_paths: Vec<String>,
        windows_integration: Option<WindowsIntegrationPolicy>,
    ) -> Self {
        Self {
            mode,
            default_target_path: default_target_path
                .filter(|value| !value.trim().is_empty())
                .map(|value| value.trim().to_owned()),
            prerequisites,
            preserve_paths,
            windows_integration: windows_integration
                .unwrap_or_else(WindowsIntegrationPolicy::empty),
        }
    }

    pub fn legacy_default() -> Self {
        Self::new(PackageInstallMode::Full, None, Vec::new(), Vec::new(), None)
    }

    pub const fn mode(&self) -> PackageInstallMode { self.mode }
    pub fn default_target_path(&self) -> Option<&str> { self.default_target_path.as_deref() }
    pub fn prerequisites(&self) -> &[Prerequisite] { &self.prerequisites }
    pub fn preserve_paths(&self) -> &[String] { &self.preserve_paths }
    pub fn windows_integration(&self) -> &WindowsIntegrationPolicy { &self.windows_integration }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PackageDeltaArtifact {
    from_version: SemanticVersion,
    artifact: ArtifactDescriptor,
}

impl PackageDeltaArtifact {
    pub fn new(from_version: SemanticVersion, artifact: ArtifactDescriptor) -> Self {
        Self { from_version, artifact }
    }
    pub fn from_version(&self) -> &SemanticVersion { &self.from_version }
    pub fn artifact(&self) -> &ArtifactDescriptor { &self.artifact }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PackageRelease {
    package_id: PackageId,
    version: SemanticVersion,
    channel: ReleaseChannel,
    artifact: ArtifactDescriptor,
    install: PackageInstallPolicy,
    rollback: PackageRollbackPolicy,
    delta_artifact: Option<PackageDeltaArtifact>,
}

impl PackageRelease {
    pub fn new(
        package_id: PackageId,
        version: SemanticVersion,
        channel: ReleaseChannel,
        artifact: ArtifactDescriptor,
        install: PackageInstallPolicy,
        rollback: PackageRollbackPolicy,
        delta_artifact: Option<PackageDeltaArtifact>,
    ) -> Result<Self, DomainError> {
        if delta_artifact.as_ref().is_some_and(|delta| {
            delta.from_version.compare_precedence(&version).is_ge()
        }) {
            return Err(DomainError::InvalidRelease);
        }
        Ok(Self { package_id, version, channel, artifact, install, rollback, delta_artifact })
    }

    pub fn package_id(&self) -> &PackageId { &self.package_id }
    pub fn version(&self) -> &SemanticVersion { &self.version }
    pub const fn channel(&self) -> ReleaseChannel { self.channel }
    pub fn artifact(&self) -> &ArtifactDescriptor { &self.artifact }
    pub fn install(&self) -> &PackageInstallPolicy { &self.install }
    pub const fn rollback(&self) -> PackageRollbackPolicy { self.rollback }
    pub fn delta_artifact(&self) -> Option<&PackageDeltaArtifact> { self.delta_artifact.as_ref() }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct InstallPlan {
    release: PackageRelease,
    target_path: String,
    prerequisites: Vec<Prerequisite>,
    preserve_paths: Vec<String>,
}

impl InstallPlan {
    pub fn new(
        release: PackageRelease,
        target_path: &str,
        prerequisites: Vec<Prerequisite>,
        preserve_paths: Vec<String>,
    ) -> Result<Self, DomainError> {
        let target_path = target_path.trim();
        if target_path.is_empty() {
            return Err(DomainError::InvalidPlan);
        }
        Ok(Self {
            release,
            target_path: target_path.to_owned(),
            prerequisites,
            preserve_paths,
        })
    }
    pub fn release(&self) -> &PackageRelease { &self.release }
    pub fn target_path(&self) -> &str { &self.target_path }
    pub fn prerequisites(&self) -> &[Prerequisite] { &self.prerequisites }
    pub fn preserve_paths(&self) -> &[String] { &self.preserve_paths }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct UpdatePlan {
    current_version: SemanticVersion,
    target: InstallPlan,
}

impl UpdatePlan {
    pub fn new(current_version: SemanticVersion, target: InstallPlan) -> Result<Self, DomainError> {
        if target.release.version.compare_precedence(&current_version).is_le() {
            return Err(DomainError::InvalidPlan);
        }
        Ok(Self { current_version, target })
    }
    pub fn current_version(&self) -> &SemanticVersion { &self.current_version }
    pub fn target(&self) -> &InstallPlan { &self.target }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RepairPlan {
    release: PackageRelease,
    target_path: String,
}

impl RepairPlan {
    pub fn new(release: PackageRelease, target_path: &str) -> Result<Self, DomainError> {
        let target_path = target_path.trim();
        if target_path.is_empty() { return Err(DomainError::InvalidPlan); }
        Ok(Self { release, target_path: target_path.to_owned() })
    }
    pub fn release(&self) -> &PackageRelease { &self.release }
    pub fn target_path(&self) -> &str { &self.target_path }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RollbackPlan {
    current_release: PackageRelease,
    previous_release: PackageRelease,
    target_path: String,
}

impl RollbackPlan {
    pub fn new(
        current_release: PackageRelease,
        previous_release: PackageRelease,
        target_path: &str,
    ) -> Result<Self, DomainError> {
        if previous_release.version.compare_precedence(&current_release.version).is_ge() {
            return Err(DomainError::InvalidPlan);
        }
        let target_path = target_path.trim();
        if target_path.is_empty() { return Err(DomainError::InvalidPlan); }
        Ok(Self { current_release, previous_release, target_path: target_path.to_owned() })
    }
    pub fn current_release(&self) -> &PackageRelease { &self.current_release }
    pub fn previous_release(&self) -> &PackageRelease { &self.previous_release }
    pub fn target_path(&self) -> &str { &self.target_path }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct UninstallPlan {
    package_id: PackageId,
    installed_version: SemanticVersion,
    target_path: String,
}

impl UninstallPlan {
    pub fn new(
        package_id: PackageId,
        installed_version: SemanticVersion,
        target_path: &str,
    ) -> Result<Self, DomainError> {
        let target_path = target_path.trim();
        if target_path.is_empty() { return Err(DomainError::InvalidPlan); }
        Ok(Self { package_id, installed_version, target_path: target_path.to_owned() })
    }
    pub fn package_id(&self) -> &PackageId { &self.package_id }
    pub fn installed_version(&self) -> &SemanticVersion { &self.installed_version }
    pub fn target_path(&self) -> &str { &self.target_path }
}
