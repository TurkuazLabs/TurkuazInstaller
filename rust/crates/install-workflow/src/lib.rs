// Dosya Yolu: REPO_ROOT/rust/crates/install-workflow/src/lib.rs
// Amac: Verified full package'i stage edip versioned deployment ve installed state akisina baglamak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Package-scoped lock altinda install/update state gecislerini orkestre eder
// Bagimli Oldugu Katman: Service

use std::{
    error::Error,
    fmt, fs, io,
    path::{Path, PathBuf},
};

use turkuaz_installer_artifact_validation::VerifiedArtifact;
use turkuaz_installer_deployment_activation::{
    ActiveDeployment, DeploymentActivationError, VersionedDeploymentStore,
};
use turkuaz_installer_domain::{
    operation::{InstallerOperationPhase, InstallerOperationType},
    release::PackageRelease,
    state::InstalledPackageState,
};
use turkuaz_installer_install_state_store::{
    InstallStateStoreError, JsonInstallStateRepository,
};
use turkuaz_installer_operation_context::InstallerOperationContext;
use turkuaz_installer_operation_journal::JsonOperationJournalRepository;
use turkuaz_installer_operation_lock::{
    FileOperationLockOptions, OperationLockError, acquire_package_lock,
};
use turkuaz_installer_package_engine::{
    PackageEngineError, PackageExtractionLimits, stage_full_package,
};
use uuid::Uuid;

#[derive(Debug)]
pub enum InstallWorkflowError {
    Io(io::Error),
    Lock(OperationLockError),
    State(InstallStateStoreError),
    Package(PackageEngineError),
    Activation(DeploymentActivationError),
    UnsupportedOperation,
    UnsupportedPrerequisites,
    UnsupportedWindowsIntegrations,
    UnsupportedPreservePaths,
    AlreadyInstalled,
    NotInstalled,
    TargetPathChanged,
    UpdateIsNotForward,
    ActiveAndInstalledStateMismatch,
    CurrentReleaseMismatch,
    RollbackNotAllowed,
    PreviousVersionUnavailable,
    ActivationCompensationFailed(String, String),
}

impl fmt::Display for InstallWorkflowError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Install workflow I/O failed: {error}"),
            Self::Lock(error) => write!(formatter, "{error}"),
            Self::State(error) => write!(formatter, "{error}"),
            Self::Package(error) => write!(formatter, "{error}"),
            Self::Activation(error) => write!(formatter, "{error}"),
            Self::UnsupportedOperation => formatter.write_str(
                "This workflow does not support the requested operation.",
            ),
            Self::UnsupportedPrerequisites => formatter.write_str(
                "This release requires prerequisites that the current workflow cannot install.",
            ),
            Self::UnsupportedWindowsIntegrations => formatter.write_str(
                "This release declares Windows integrations that the current workflow cannot apply.",
            ),
            Self::UnsupportedPreservePaths => formatter.write_str(
                "This release declares preserved paths that the current workflow cannot migrate.",
            ),
            Self::AlreadyInstalled => {
                formatter.write_str("Package is already installed; use the update workflow.")
            }
            Self::NotInstalled => {
                formatter.write_str("Package is not installed; use the install workflow.")
            }
            Self::TargetPathChanged => formatter.write_str(
                "An update cannot change the target path of an existing installation.",
            ),
            Self::UpdateIsNotForward => formatter.write_str(
                "Update version must have higher SemVer precedence than the installed version.",
            ),
            Self::ActiveAndInstalledStateMismatch => formatter.write_str(
                "Active deployment and installed package state disagree; recovery is required.",
            ),
            Self::CurrentReleaseMismatch => formatter.write_str(
                "Signed current release does not match the installed package state.",
            ),
            Self::RollbackNotAllowed => formatter.write_str(
                "The current signed release policy does not allow rollback.",
            ),
            Self::PreviousVersionUnavailable => {
                formatter.write_str("No previous deployment version is available for rollback.")
            }
            Self::ActivationCompensationFailed(state_error, activation_error) => write!(
                formatter,
                "State persistence failed ({state_error}) and activation rollback also failed ({activation_error})."
            ),
        }
    }
}

impl Error for InstallWorkflowError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Lock(error) => Some(error),
            Self::State(error) => Some(error),
            Self::Package(error) => Some(error),
            Self::Activation(error) => Some(error),
            Self::UnsupportedOperation
            | Self::UnsupportedPrerequisites
            | Self::UnsupportedWindowsIntegrations
            | Self::UnsupportedPreservePaths
            | Self::AlreadyInstalled
            | Self::NotInstalled
            | Self::TargetPathChanged
            | Self::UpdateIsNotForward
            | Self::ActiveAndInstalledStateMismatch
            | Self::CurrentReleaseMismatch
            | Self::RollbackNotAllowed
            | Self::PreviousVersionUnavailable
            | Self::ActivationCompensationFailed(_, _) => None,
        }
    }
}

#[derive(Debug, Clone)]
pub struct InstallWorkflowOptions {
    lock: FileOperationLockOptions,
    journal: Option<JsonOperationJournalRepository>,
    state: JsonInstallStateRepository,
    deployment: VersionedDeploymentStore,
    staging_root: PathBuf,
}

impl InstallWorkflowOptions {
    pub fn new(
        lock: FileOperationLockOptions,
        journal: Option<JsonOperationJournalRepository>,
        state: JsonInstallStateRepository,
        deployment: VersionedDeploymentStore,
        staging_root: impl AsRef<Path>,
    ) -> io::Result<Self> {
        if staging_root.as_ref().as_os_str().is_empty() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Install staging root cannot be empty",
            ));
        }
        let staging_root = if staging_root.as_ref().is_absolute() {
            staging_root.as_ref().to_path_buf()
        } else {
            std::env::current_dir()?.join(staging_root)
        };
        Ok(Self {
            lock,
            journal,
            state,
            deployment,
            staging_root,
        })
    }
}

#[derive(Debug, Clone)]
pub struct InstallWorkflow {
    options: InstallWorkflowOptions,
}

impl InstallWorkflow {
    pub fn new(options: InstallWorkflowOptions) -> Self {
        Self { options }
    }

    /// @dev-id TIZ-WORKFLOW-INSTALL-010
    /// @purpose Apply a verified full package for a first install or forward update.
    /// @reason Package verification, extraction, activation, and installed-state writes must share one serialized operation boundary.
    /// @contract Accepts only a VerifiedArtifact matching the signed release; holds the package lock, stages the package, atomically switches the active version, and compensates activation if installed-state persistence fails.
    /// @depends Manifest-verified PackageRelease, VerifiedArtifact, package engine, operation journal, lock, deployment store, and installed-state repository.
    pub async fn install_or_update(
        &self,
        verified_artifact: VerifiedArtifact,
        release: PackageRelease,
        operation: InstallerOperationType,
        target_path: &str,
        limits: PackageExtractionLimits,
    ) -> Result<ActiveDeployment, InstallWorkflowError> {
        if !matches!(
            operation,
            InstallerOperationType::Install | InstallerOperationType::Update
        ) {
            return Err(InstallWorkflowError::UnsupportedOperation);
        }
        let install_policy = release.install();
        if !install_policy.prerequisites().is_empty() {
            return Err(InstallWorkflowError::UnsupportedPrerequisites);
        }
        if install_policy.windows_integration().has_actions() {
            return Err(InstallWorkflowError::UnsupportedWindowsIntegrations);
        }
        if !install_policy.preserve_paths().is_empty() {
            return Err(InstallWorkflowError::UnsupportedPreservePaths);
        }

        let target_path = target_path.trim();
        if target_path.is_empty() {
            return Err(InstallWorkflowError::Io(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Install target path cannot be empty",
            )));
        }
        if verified_artifact.descriptor() != release.artifact() {
            return Err(InstallWorkflowError::Package(
                PackageEngineError::InvalidPackage(
                    "verified artifact does not match the requested signed release",
                ),
            ));
        }

        let package_id = release.package_id().clone();
        let _lock_lease = acquire_package_lock(&self.options.lock, &package_id)
            .map_err(InstallWorkflowError::Lock)?;
        let previous_state = self
            .options
            .state
            .get(&package_id)
            .await
            .map_err(InstallWorkflowError::State)?;
        let previous_active = self
            .options
            .deployment
            .current(&package_id)
            .map_err(InstallWorkflowError::Activation)?;

        match (operation, previous_state.as_ref(), previous_active.as_ref()) {
            (InstallerOperationType::Install, None, None) => {}
            (InstallerOperationType::Install, Some(_), _) => {
                return Err(InstallWorkflowError::AlreadyInstalled);
            }
            (InstallerOperationType::Install, None, Some(_)) => {
                return Err(InstallWorkflowError::ActiveAndInstalledStateMismatch);
            }
            (InstallerOperationType::Update, None, _) => {
                return Err(InstallWorkflowError::NotInstalled);
            }
            (InstallerOperationType::Update, Some(_), None) => {
                return Err(InstallWorkflowError::ActiveAndInstalledStateMismatch);
            }
            (InstallerOperationType::Update, Some(state), Some(active)) => {
                if state.version() != active.version() {
                    return Err(InstallWorkflowError::ActiveAndInstalledStateMismatch);
                }
                if !state.target_path().eq_ignore_ascii_case(target_path) {
                    return Err(InstallWorkflowError::TargetPathChanged);
                }
                if release.version().compare_precedence(state.version()).is_le() {
                    return Err(InstallWorkflowError::UpdateIsNotForward);
                }
            }
            _ => return Err(InstallWorkflowError::UnsupportedOperation),
        }

        let mut context = InstallerOperationContext::start(
            package_id.clone(),
            operation,
            Some(release.version().to_string()),
            target_path.to_owned(),
            self.options.journal.clone(),
        )
        .await;
        context.set_phase(InstallerOperationPhase::Verifying).await;
        context.set_phase(InstallerOperationPhase::Staging).await;

        let stage_directory = self
            .options
            .staging_root
            .join(format!("{}-{}", package_id.as_str(), Uuid::new_v4()));
        let stage = match stage_full_package(
            verified_artifact,
            &release,
            &stage_directory,
            limits,
        )
        .await
        {
            Ok(stage) => stage,
            Err(error) => {
                context.fail(&error.to_string()).await;
                return Err(InstallWorkflowError::Package(error));
            }
        };

        context.set_phase(InstallerOperationPhase::Applying).await;
        let active = match self.options.deployment.publish_and_activate(stage) {
            Ok(active) => active,
            Err(error) => {
                let _ = fs::remove_dir_all(&stage_directory);
                context.fail(&error.to_string()).await;
                return Err(InstallWorkflowError::Activation(error));
            }
        };

        context.set_phase(InstallerOperationPhase::SavingState).await;
        let new_state = InstalledPackageState::new(
            package_id.clone(),
            release.version().clone(),
            release.channel(),
            target_path,
        );
        if let Err(state_error) = self.options.state.save(&new_state).await {
            let compensation = match previous_active.as_ref() {
                Some(previous) => self
                    .options
                    .deployment
                    .activate_existing(&package_id, previous.version()),
                None => self
                    .options
                    .deployment
                    .deactivate_if_current(&package_id, active.version()),
            };
            let _ = fs::remove_dir_all(&stage_directory);
            context.fail(&state_error.to_string()).await;
            if let Err(activation_error) = compensation {
                return Err(InstallWorkflowError::ActivationCompensationFailed(
                    state_error.to_string(),
                    activation_error.to_string(),
                ));
            }
            return Err(InstallWorkflowError::State(state_error));
        }

        let _ = fs::remove_dir_all(&stage_directory);
        context.complete().await;
        Ok(active)
    }

    /// @dev-id TIZ-WORKFLOW-ROLLBACK-010
    /// @purpose Roll back to the previous immutable deployment selected by the active record.
    /// @reason Rollback must follow the signed current release policy and update local installed state with the active pointer.
    /// @contract Requires an installed state matching current_release and a supported rollback policy; compensates activation if state persistence fails.
    /// @depends Package lock, current signed release, active deployment history, and installed-state repository.
    pub async fn rollback(
        &self,
        current_release: PackageRelease,
    ) -> Result<ActiveDeployment, InstallWorkflowError> {
        let package_id = current_release.package_id().clone();
        let _lock_lease = acquire_package_lock(&self.options.lock, &package_id)
            .map_err(InstallWorkflowError::Lock)?;
        let current_state = self
            .options
            .state
            .get(&package_id)
            .await
            .map_err(InstallWorkflowError::State)?
            .ok_or(InstallWorkflowError::NotInstalled)?;
        let current_active = self
            .options
            .deployment
            .current(&package_id)
            .map_err(InstallWorkflowError::Activation)?
            .ok_or(InstallWorkflowError::ActiveAndInstalledStateMismatch)?;

        if current_state.version() != current_active.version() {
            return Err(InstallWorkflowError::ActiveAndInstalledStateMismatch);
        }
        if current_release.version() != current_state.version() {
            return Err(InstallWorkflowError::CurrentReleaseMismatch);
        }
        if !current_release.rollback().supported() {
            return Err(InstallWorkflowError::RollbackNotAllowed);
        }
        let previous_version = current_active
            .previous_version()
            .cloned()
            .ok_or(InstallWorkflowError::PreviousVersionUnavailable)?;

        let mut context = InstallerOperationContext::start(
            package_id.clone(),
            InstallerOperationType::Rollback,
            Some(previous_version.to_string()),
            current_state.target_path().to_owned(),
            self.options.journal.clone(),
        )
        .await;
        context.set_phase(InstallerOperationPhase::Applying).await;
        let active = match self
            .options
            .deployment
            .activate_existing(&package_id, &previous_version)
        {
            Ok(active) => active,
            Err(error) => {
                context.fail(&error.to_string()).await;
                return Err(InstallWorkflowError::Activation(error));
            }
        };

        context.set_phase(InstallerOperationPhase::SavingState).await;
        let previous_state = InstalledPackageState::new(
            package_id.clone(),
            previous_version,
            current_state.channel(),
            current_state.target_path(),
        );
        if let Err(state_error) = self.options.state.save(&previous_state).await {
            let compensation = self
                .options
                .deployment
                .activate_existing(&package_id, current_active.version());
            context.fail(&state_error.to_string()).await;
            if let Err(activation_error) = compensation {
                return Err(InstallWorkflowError::ActivationCompensationFailed(
                    state_error.to_string(),
                    activation_error.to_string(),
                ));
            }
            return Err(InstallWorkflowError::State(state_error));
        }

        context.complete().await;
        Ok(active)
    }

}
