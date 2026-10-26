// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/prerequisites.rs
// Amac: Prerequisite gereksinimi ve dogrulanmis auto-install eylemini typed domain modelinde tasimak
// Modul - Rust Domain
// Version: 0.2.0
// Aciklama: Prerequisite installer artifacti icin signature policy zorunlulugunu korur
// Bagimli Oldugu Katman: Model | Service | Tool

use crate::{artifacts::ArtifactDescriptor, DomainError};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PrerequisiteInstallAction {
    artifact: ArtifactDescriptor,
    arguments: Vec<String>,
    requires_elevation: bool,
}

impl PrerequisiteInstallAction {
    pub fn new(
        artifact: ArtifactDescriptor,
        arguments: Vec<String>,
        requires_elevation: bool,
    ) -> Result<Self, DomainError> {
        if artifact.signature().is_none() {
            return Err(DomainError::InvalidPrerequisite);
        }
        Ok(Self {
            artifact,
            arguments,
            requires_elevation,
        })
    }

    pub fn artifact(&self) -> &ArtifactDescriptor {
        &self.artifact
    }

    pub fn arguments(&self) -> &[String] {
        &self.arguments
    }

    pub const fn requires_elevation(&self) -> bool {
        self.requires_elevation
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Prerequisite {
    id: String,
    version_expression: String,
    install_action: Option<PrerequisiteInstallAction>,
}

impl Prerequisite {
    pub fn new(
        id: &str,
        version_expression: &str,
        install_action: Option<PrerequisiteInstallAction>,
    ) -> Result<Self, DomainError> {
        let id = id.trim();
        let version_expression = version_expression.trim();
        if id.is_empty() || version_expression.is_empty() {
            return Err(DomainError::InvalidPrerequisite);
        }
        Ok(Self {
            id: id.to_owned(),
            version_expression: version_expression.to_owned(),
            install_action,
        })
    }

    pub fn id(&self) -> &str {
        &self.id
    }

    pub fn version_expression(&self) -> &str {
        &self.version_expression
    }

    pub fn install_action(&self) -> Option<&PrerequisiteInstallAction> {
        self.install_action.as_ref()
    }
}
