//! Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/tests/cli_release_urls.rs
//! Amac: Online CLI imzali kaynak adreslerinin ag erisimi oncesi guvenli dogrulanmasi
//! Modul - Rust Integration Tests
//! Version: 0.1.0
//! Aciklama: Gomulu kimlik bilgileri ve fragment adreslerini kurulumdan once reddeder
//! Bagimli Oldugu Katman: QA | Tool

use std::process::Command;

/// @dev-id BRK-CLI-URL-PROCESS-001
/// @purpose Exercise the real CLI parser without making any network requests.
/// @contract Invalid source URLs fail before trust-store or target paths are accessed.
fn run_with_source_urls(operation: &str, manifest: &str, signature: &str) -> std::process::Output {
    Command::new(env!("CARGO_BIN_EXE_turkuaz-installer"))
        .args([
            operation,
            "--package-id",
            "example.product",
            "--manifest-url",
            manifest,
            "--signature-url",
            signature,
            "--trust-store",
            "not-read-policy.yaml",
            "--target",
            "not-created-target",
            "--data-root",
            "not-created-data",
        ])
        .output()
        .expect("CLI process should start")
}

#[test]
fn online_install_rejects_manifest_credentials_before_network_io() {
    let output = run_with_source_urls(
        "install",
        "https://user:password@updates.example.test/stable.yaml",
        "https://updates.example.test/stable.yaml.p7s",
    );
    assert!(!output.status.success());
    let stderr = String::from_utf8_lossy(&output.stderr);
    assert!(stderr.contains("must not contain credentials or fragments"));
    assert!(!stderr.contains("password"), "credentials leaked into error output");
}

#[test]
fn online_update_and_rollback_reject_signature_fragments_before_network_io() {
    for operation in ["update", "rollback"] {
        let output = run_with_source_urls(
            operation,
            "https://updates.example.test/stable.yaml",
            "https://updates.example.test/stable.yaml.p7s#ignored",
        );
        assert!(!output.status.success(), "{operation} accepted a fragment");
        assert!(
            String::from_utf8_lossy(&output.stderr)
                .contains("must not contain credentials or fragments"),
            "{operation} did not reject the invalid signature URL"
        );
    }
}
