//! Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/tests/cli_help.rs
//! Amac: Kok CLI yardim ciktisinda guncel public komutlarin gorunmesini dogrulamak
//! Modul - Rust Integration Tests
//! Version: 0.1.0
//! Aciklama: Rust CLI eklenen kurulum ve kaynak denetimi komutlarini --help'e dahil eder
//! Bagimli Oldugu Katman: QA | Tool

use std::process::Command;

#[test]
fn root_help_lists_existing_native_setup_and_release_commands() {
    let output = Command::new(env!("CARGO_BIN_EXE_turkuaz-installer"))
        .arg("--help")
        .output()
        .expect("CLI process should start");

    assert!(
        output.status.success(),
        "CLI help failed: {}",
        String::from_utf8_lossy(&output.stderr)
    );

    let stdout = String::from_utf8(output.stdout).expect("CLI help must be UTF-8");
    for command in [
        "check-release-source",
        "build-native-setup",
        "install-local-self",
        "uninstall-local",
        "build-distribution-pair",
        "--version",
    ] {
        assert!(
            stdout.contains(command),
            "CLI root help does not list {command}"
        );
    }
}
