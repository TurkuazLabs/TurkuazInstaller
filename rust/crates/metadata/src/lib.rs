// Dosya Yolu: REPO_ROOT/rust/crates/metadata/src/lib.rs
// Amac: Rust manifest intake icin bounded stream okuyucu ve merkezi byte limitlerini sunmak
// Modul - Rust Library
// Version: 0.1.0
// Aciklama: Parse/verify oncesi bellek kullanimi icin fail-closed API
// Bagimli Oldugu Katman: Tool | Config

#![forbid(unsafe_code)]

mod bounded_reader;
mod limits;

pub use bounded_reader::{BoundedContentError, BoundedContentReader, validate_declared_length};
pub use limits::{MAXIMUM_DETACHED_SIGNATURE_BYTES, MAXIMUM_MANIFEST_BYTES, ManifestContentLimits};
