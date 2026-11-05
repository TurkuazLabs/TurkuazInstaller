//! Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/tests/cli_version.rs
//! Amac: CLI surum bayraklarinin Cargo package surumuyle uyumunu dogrulamak
//! Modul - Rust Integration Tests
//! Version: 0.1.0
//! Aciklama: --version ve -V ciktilarini kontrol eder
//! Bagimli Oldugu Katman: QA | Tool

use std::process::Command;

fn version_output(flag: &str) -> String {
    let output = Command::new(env!("CARGO_BIN_EXE_turkuaz-installer"))
        .arg(flag)
        .output()
        .expect("the installer CLI binary should run");

    assert!(
        output.status.success(),
        "version flag failed: {}",
        String::from_utf8_lossy(&output.stderr)
    );
    String::from_utf8(output.stdout).expect("version output should be UTF-8")
}

#[test]
fn long_version_flag_reports_cargo_package_version() {
    assert_eq!(
        version_output("--version").trim_end(),
        format!("Turkuaz Installer v{}", env!("CARGO_PKG_VERSION"))
    );
}

#[test]
fn short_version_flag_reports_cargo_package_version() {
    assert_eq!(
        version_output("-V").trim_end(),
        format!("Turkuaz Installer v{}", env!("CARGO_PKG_VERSION"))
    );
}
