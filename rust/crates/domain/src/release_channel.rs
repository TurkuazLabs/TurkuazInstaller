// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/release_channel.rs
// Amac: Community release kanallarini typed enum olarak tanimlamak
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Contract ile sabitlenen stable ve beta kanallarini temsil eder
// Bagimli Oldugu Katman: Model

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub enum ReleaseChannel {
    Stable,
    Beta,
}
