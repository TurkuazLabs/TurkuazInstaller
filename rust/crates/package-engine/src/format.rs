// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/format.rs
// Amac: Turkuaz .tzpkg arşivinin versioned metadata sozlesmesini tanimlamak
// Modul - Rust Package Format
// Version: 0.2.0
// Aciklama: Paket kimligi ve surumunu payload arşivinden bagimsiz olarak acikca tasir
// Bagimli Oldugu Katman: Model | Service | Tool

use serde::{Deserialize, Serialize};
use turkuaz_installer_domain::{
    package_id::PackageId, release::PackageRelease, semantic_version::SemanticVersion,
};

pub(crate) const PACKAGE_METADATA_ENTRY: &str = "turkuaz-package.json";
pub(crate) const PACKAGE_PAYLOAD_ROOT: &str = "payload/";
pub(crate) const MAXIMUM_PACKAGE_METADATA_BYTES: u64 = 64 * 1024;
pub(crate) const PACKAGE_FORMAT_VERSION: u32 = 1;

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub(crate) struct PackageArchiveMetadata {
    pub(crate) format_version: u32,
    pub(crate) package_id: String,
    pub(crate) version: String,
}

impl PackageArchiveMetadata {
    pub(crate) fn new(package_id: &PackageId, version: &SemanticVersion) -> Self {
        Self {
            format_version: PACKAGE_FORMAT_VERSION,
            package_id: package_id.to_string(),
            version: version.to_string(),
        }
    }

    pub(crate) fn for_release(release: &PackageRelease) -> Self {
        Self::new(release.package_id(), release.version())
    }
}
