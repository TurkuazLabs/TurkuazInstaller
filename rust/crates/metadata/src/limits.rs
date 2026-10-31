// Dosya Yolu: REPO_ROOT/rust/crates/metadata/src/limits.rs
// Amac: Manifest ve detached CMS signature byte sinirlarini merkezi sabitlerde toplamak
// Modul - Rust Config
// Version: 0.1.0
// Aciklama: Contracts/manifest-trust.yml degerlerini runtime konfigrasyonuna tasir
// Bagimli Oldugu Katman: Config | Tool

pub const MAXIMUM_MANIFEST_BYTES: usize = 1024 * 1024;
pub const MAXIMUM_DETACHED_SIGNATURE_BYTES: usize = 256 * 1024;

#[derive(Debug, Default, Clone, Copy)]
pub struct ManifestContentLimits;

impl ManifestContentLimits {
    pub const MANIFEST_BYTES: usize = MAXIMUM_MANIFEST_BYTES;
    pub const DETACHED_SIGNATURE_BYTES: usize = MAXIMUM_DETACHED_SIGNATURE_BYTES;
}
