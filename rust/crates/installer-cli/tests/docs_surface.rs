//! Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/tests/docs_surface.rs
//! Amac: Rust migration docs ve eski contracts'in production gercegiyle karistirilmasini engellemek
//! Modul - Rust Integration Tests
//! Version: 0.1.0
//! Aciklama: Markdown metadata, signed .tzpkg fixture ve legacy YAML statulerini denetler
//! Bagimli Oldugu Katman: QA | Documentation

use std::{fs, path::{Path, PathBuf}};

fn repository_root() -> PathBuf {
    Path::new(env!("CARGO_MANIFEST_DIR")).join("../../..")
}

fn read_repository_file(path: &str) -> String {
    let file = repository_root().join(path);
    fs::read_to_string(&file)
        .unwrap_or_else(|error| panic!("Cannot read {}: {error}", file.display()))
}

#[test]
fn public_rust_markdown_metadata_is_not_visible_as_a_heading() {
    const MARKDOWN: &[&str] = &[
        "README.md",
        "SECURITY.md",
        "AGENTS.md",
        "rust/README.md",
        "rust/RUST_READINESS.md",
        "rust/CLI.md",
        "rust/DEPLOYMENT_LAYOUT.md",
        "rust/INSTALL_WORKFLOW.md",
        "rust/PACKAGE_FORMAT.md",
        "rust/RELEASE_SIGNING.md",
        "rust/SETUP_BUILDER.md",
        "rust/SMOKE_TEST.md",
    ];
    for path in MARKDOWN {
        let content = read_repository_file(path);
        assert!(content.starts_with("<!--\n"), "{path}: metadata must use HTML comment");
        let heading = content
            .lines()
            .find(|line| line.starts_with("# "))
            .unwrap_or_else(|| panic!("{path}: missing visible document heading"));
        assert!(
            !heading.contains("Dosya Yolu") && !heading.contains("Amac:"),
            "{path}: metadata must not render as title"
        );
    }
}

#[test]
fn example_signed_manifest_matches_current_rust_full_package_contract() {
    let example = read_repository_file("examples/community-manifest.yml");
    assert!(example.contains("application.tzpkg"), "Rust package example must use .tzpkg");
    for unsupported in [
        "  prerequisites:",
        "  preserve_paths:",
        "    shortcuts:",
        "    protocols:",
        "    delta:",
    ] {
        assert!(
            !example.contains(unsupported),
            "example claims unsupported Rust runtime integration: {unsupported}"
        );
    }
    turkuaz_installer_manifest_parser::parse_manifest(&example)
        .expect("signed full package example must parse through the actual Rust manifest parser");
}

#[test]
fn historical_yaml_contracts_are_not_mistaken_for_executed_rust_runtime() {
    const HISTORICAL: &[&str] = &[
        "background-update-policy.yml",
        "bootstrap-self-update.yml",
        "installed-app-catalog.yml",
        "installer-manifest.yml",
        "manifest-trust.yml",
        "network.yml",
        "release-provider.yml",
        "ui-profile.yml",
        "update-discovery.yml",
        "version-policy.yml",
        "windows-integration.yml",
    ];
    for file in HISTORICAL {
        let path = format!("contracts/{file}");
        let content = read_repository_file(&path);
        assert!(
            content.contains("# Rust-Runtime-Status: legacy-design-only"),
            "{path}: old design contract must be marked as non-executable"
        );
    }
}
