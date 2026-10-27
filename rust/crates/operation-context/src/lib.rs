// Dosya Yolu: REPO_ROOT/rust/crates/operation-context/src/lib.rs
// Amac: Installer operation journal checkpoint yasam dongusunu koordine etmek
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: .NET workflow context'inin typed phase ve reboot resume sozlesmesini tasir
// Bagimli Oldugu Katman: Service

use std::{error::Error, fmt};

use time::OffsetDateTime;
use turkuaz_installer_domain::{
    operation::{InstallerOperationPhase, InstallerOperationType},
    package_id::PackageId,
};
use turkuaz_installer_operation_journal::{
    JournalEntry, JsonOperationJournalRepository,
};
use uuid::Uuid;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum OperationContextError {
    InvalidPrerequisiteId,
    ResumePhaseNotSafe,
    ResumeRequestMismatch,
}

impl fmt::Display for OperationContextError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        let message = match self {
            Self::InvalidPrerequisiteId => "Prerequisite ID cannot be empty.",
            Self::ResumePhaseNotSafe => {
                "Operation can only resume from a reboot-safe checkpoint phase."
            }
            Self::ResumeRequestMismatch => {
                "Reboot resume journal does not match the requested operation."
            }
        };
        formatter.write_str(message)
    }
}

impl Error for OperationContextError {}

#[derive(Debug)]
pub struct InstallerOperationContext {
    entry: JournalEntry,
    journal: Option<JsonOperationJournalRepository>,
}

impl InstallerOperationContext {
    /// @dev-id TIZ-OP-CONTEXT-010
    /// @purpose Start an operation with an initial durable checkpoint.
    /// @reason A workflow should have a stable operation ID and recoverable initial state.
    /// @contract Journal persistence is best-effort, matching the current .NET diagnostic boundary.
    /// @depends Domain operation types and optional journal repository.
    pub async fn start(
        package_id: PackageId,
        operation: InstallerOperationType,
        version: Option<String>,
        target_path: String,
        journal: Option<JsonOperationJournalRepository>,
    ) -> Self {
        let now = OffsetDateTime::now_utc();
        let entry = JournalEntry {
            operation_id: Uuid::new_v4(),
            package_id,
            operation,
            version,
            target_path,
            phase: InstallerOperationPhase::Started,
            started_at_utc: now,
            updated_at_utc: now,
            failure: None,
            pending_prerequisite_id: None,
        };
        let context = Self { entry, journal };
        context.persist_best_effort().await;
        context
    }

    /// @dev-id TIZ-OP-CONTEXT-011
    /// @purpose Resume only a validated reboot-safe journal for the exact requested operation.
    /// @reason A stale or mismatched journal must not authorize a different package mutation.
    /// @contract Requires AwaitingReboot/RebootResumeArmed, matching package/op/version/target and pending prerequisite.
    /// @depends Previously loaded typed journal entry.
    pub async fn resume(
        entry: JournalEntry,
        package_id: &PackageId,
        operation: InstallerOperationType,
        version: Option<&str>,
        target_path: &str,
        journal: Option<JsonOperationJournalRepository>,
    ) -> Result<Self, OperationContextError> {
        if !matches!(
            entry.phase,
            InstallerOperationPhase::AwaitingReboot
                | InstallerOperationPhase::RebootResumeArmed
        ) {
            return Err(OperationContextError::ResumePhaseNotSafe);
        }

        let request_matches = &entry.package_id == package_id
            && entry.operation == operation
            && entry.version.as_deref() == version
            && entry.target_path.eq_ignore_ascii_case(target_path)
            && entry
                .pending_prerequisite_id
                .as_deref()
                .is_some_and(|value| !value.trim().is_empty());
        if !request_matches {
            return Err(OperationContextError::ResumeRequestMismatch);
        }

        let context = Self { entry, journal };
        context.persist_best_effort().await;
        Ok(context)
    }

    pub fn entry(&self) -> &JournalEntry {
        &self.entry
    }

    pub fn pending_prerequisite_id(&self) -> Option<&str> {
        self.entry.pending_prerequisite_id.as_deref()
    }

    pub async fn set_phase(&mut self, phase: InstallerOperationPhase) {
        self.entry.phase = phase;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = None;
        self.persist_best_effort().await;
    }

    pub async fn arm_reboot_resume(
        &mut self,
        prerequisite_id: &str,
    ) -> Result<(), OperationContextError> {
        let prerequisite_id = prerequisite_id.trim();
        if prerequisite_id.is_empty() {
            return Err(OperationContextError::InvalidPrerequisiteId);
        }
        self.entry.phase = InstallerOperationPhase::RebootResumeArmed;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = None;
        self.entry.pending_prerequisite_id = Some(prerequisite_id.to_owned());
        self.persist_best_effort().await;
        Ok(())
    }

    pub async fn await_reboot(
        &mut self,
        prerequisite_id: &str,
    ) -> Result<(), OperationContextError> {
        let prerequisite_id = prerequisite_id.trim();
        if prerequisite_id.is_empty() {
            return Err(OperationContextError::InvalidPrerequisiteId);
        }
        self.entry.phase = InstallerOperationPhase::AwaitingReboot;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = None;
        self.entry.pending_prerequisite_id = Some(prerequisite_id.to_owned());
        self.persist_best_effort().await;
        Ok(())
    }

    pub async fn clear_reboot_checkpoint(&mut self) {
        if self.entry.pending_prerequisite_id.is_none() {
            return;
        }
        self.entry.phase = InstallerOperationPhase::ValidatingPrerequisites;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = None;
        self.entry.pending_prerequisite_id = None;
        self.persist_best_effort().await;
    }

    pub async fn complete(&mut self) {
        self.entry.phase = InstallerOperationPhase::Completed;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = None;
        self.persist_best_effort().await;
        if let Some(journal) = &self.journal {
            let _ = journal.delete(&self.entry.package_id).await;
        }
    }

    pub async fn cancel(&mut self) {
        self.finish_with_failure(
            InstallerOperationPhase::Cancelled,
            "Installer operation was cancelled.",
        )
        .await;
    }

    pub async fn fail(&mut self, message: &str) {
        self.finish_with_failure(InstallerOperationPhase::Failed, message)
            .await;
    }

    async fn finish_with_failure(&mut self, phase: InstallerOperationPhase, message: &str) {
        self.entry.phase = phase;
        self.entry.updated_at_utc = OffsetDateTime::now_utc();
        self.entry.failure = Some(message.to_owned());
        self.persist_best_effort().await;
    }

    async fn persist_best_effort(&self) {
        if let Some(journal) = &self.journal {
            let _ = journal.save(&self.entry).await;
        }
    }
}
