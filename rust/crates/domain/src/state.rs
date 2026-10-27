// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/state.rs
// Amac: Kurulu package bilgisini storage bagimsiz typed domain modeli olarak tasimak
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Package, SemVer, release channel ve hedef yolu kurulu state kaydinda birlestirir
// Bagimli Oldugu Katman: Model | Service

use crate::{
    package_id::PackageId,
    release_channel::ReleaseChannel,
    semantic_version::SemanticVersion,
};

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct InstalledPackageState {
    package_id: PackageId,
    version: SemanticVersion,
    channel: ReleaseChannel,
    target_path: String,
}

impl InstalledPackageState {
    pub fn new(
        package_id: PackageId,
        version: SemanticVersion,
        channel: ReleaseChannel,
        target_path: &str,
    ) -> Self {
        Self {
            package_id,
            version,
            channel,
            target_path: target_path.to_owned(),
        }
    }

    pub fn package_id(&self) -> &PackageId {
        &self.package_id
    }

    pub fn version(&self) -> &SemanticVersion {
        &self.version
    }

    pub const fn channel(&self) -> ReleaseChannel {
        self.channel
    }

    pub fn target_path(&self) -> &str {
        &self.target_path
    }
}
