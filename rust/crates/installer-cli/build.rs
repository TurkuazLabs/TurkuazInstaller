// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/build.rs
// Amac: Windows PE binary'lerine UAC uygulama manifestini link asamasinda gommek
// Modul - Rust Build Script
// Version: 0.1.0
// Aciklama: Windows MSVC hedeflerinde acik asInvoker politikasini butun installer binary'lerine uygular
// Bagimli Oldugu Katman: Tool | Build

use std::{env, path::PathBuf};

fn main() {
    println!("cargo:rerun-if-changed=windows-app.manifest");

    if env::var("CARGO_CFG_TARGET_OS").as_deref() != Ok("windows") {
        return;
    }

    if env::var("CARGO_CFG_TARGET_ENV").as_deref() != Ok("msvc") {
        panic!(
            "Windows installer binaries require the MSVC linker to embed the UAC application manifest"
        );
    }

    let manifest = PathBuf::from(
        env::var_os("CARGO_MANIFEST_DIR").expect("Cargo must provide CARGO_MANIFEST_DIR"),
    )
    .join("windows-app.manifest");

    if !manifest.is_file() {
        panic!("Windows application manifest must exist");
    }

    let manifest = manifest.display().to_string().replace('\\', "/");

    println!("cargo:rustc-link-arg-bin=turkuaz-installer=/MANIFEST:EMBED");
    println!("cargo:rustc-link-arg-bin=turkuaz-installer=/MANIFESTINPUT:\"{manifest}\"");
}
