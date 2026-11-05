// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/package_policy.rs
// Amac: Manifest paket kurulum ve rollback politikalarini typed degerlerle temsil etmek
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Community manifest contract'indaki tam paket ve rollback varsayilanlarini korur
// Bagimli Oldugu Katman: Model

/// Install payload mode supported by the current stable contract.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum PackageInstallMode {
    Full = 0,
}

/// Whether rollback is supported and whether a previous version is mandatory.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct PackageRollbackPolicy {
    supported: bool,
    previous_version_required: bool,
}

impl PackageRollbackPolicy {
    pub const DISABLED: Self = Self {
        supported: false,
        previous_version_required: false,
    };

    pub const fn new(supported: bool, previous_version_required: bool) -> Self {
        Self {
            supported,
            previous_version_required,
        }
    }

    pub const fn supported(self) -> bool {
        self.supported
    }

    pub const fn previous_version_required(self) -> bool {
        self.previous_version_required
    }
}
