// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/operation.rs
// Amac: Installer operation journal ve event contract enumlarini tanimlamak
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: C# enum sayisal degerlerini journal uyumlulugu icin korur
// Bagimli Oldugu Katman: Model

/// Mutating operation kinds persisted in operation journals.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum InstallerOperationType {
    Install = 0,
    Update = 1,
    Repair = 2,
    Rollback = 3,
    Uninstall = 4,
}

/// Checkpoints persisted by the crash and reboot recovery journal.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum InstallerOperationPhase {
    Started = 0,
    ValidatingPrerequisites = 1,
    Downloading = 2,
    Verifying = 3,
    Staging = 4,
    Applying = 5,
    SavingState = 6,
    RemovingState = 7,
    Completed = 8,
    Cancelled = 9,
    Failed = 10,
    AwaitingReboot = 11,
    RebootResumeArmed = 12,
    ApplyingIntegrations = 13,
}

/// Severity levels for structured installer events.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum InstallerEventLevel {
    Information = 0,
    Warning = 1,
    Error = 2,
}
