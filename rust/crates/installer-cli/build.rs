// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/build.rs
// Amac: Windows PE binary'lerine UAC uygulama manifestini link asamasinda gommek
// Modul - Rust Build Script
// Version: 0.1.1
// Aciklama: Windows MSVC hedeflerinde acik asInvoker politikasini butun installer binary'lerine uygular
// Bagimli Oldugu Katman: Tool | Build

use std::{env, path::PathBuf, process::Command};

fn main() {
    println!("cargo:rerun-if-changed=windows-app.manifest");
    println!("cargo:rerun-if-changed=src/cleanup_worker.rs");

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

    if env::var_os("CARGO_FEATURE_SETUP_AUTHORING_GUI").is_some() {
        let output = PathBuf::from(env::var_os("OUT_DIR").expect("OUT_DIR"))
            .join("turkuaz-cleanup-worker.exe");
        let status = Command::new(env::var_os("RUSTC").expect("RUSTC"))
            .args([
                "--edition=2024",
                "--crate-name",
                "turkuaz_cleanup_worker",
                "src/cleanup_worker.rs",
                "--target",
                &env::var("TARGET").expect("TARGET"),
                "-C",
                "opt-level=s",
                "-C",
                "panic=abort",
                "-C",
                "link-arg=/MANIFEST:EMBED",
                "-C",
                &format!("link-arg=/MANIFESTINPUT:{manifest}"),
                "-o",
            ])
            .arg(output)
            .status()
            .expect("launch Rust cleanup worker compiler");
        assert!(status.success(), "Rust cleanup worker compilation failed");
    }
    println!("cargo:rustc-link-arg-bin=turkuaz-installer=/MANIFEST:EMBED");
    println!("cargo:rustc-link-arg-bin=turkuaz-installer=/MANIFESTINPUT:{manifest}");
}
