#!/usr/bin/env python3
# Dosya Yolu: REPO_ROOT/rust/scripts/check_rust_primary_only.py
# Amac: Tek Rust TurkuazSetup dagitimini NSIS ve .NET kalintilarina karsi kilitlemek
# Modul - Python / Offline CI policy
# Version: 1.0.0-candidate
# Aciklama: Aktif CI, Runtime ve Studio kaynaklarini salt-okunur denetler
# Bagimli Oldugu Katman: rust/crates/installer-cli | .github/workflows/rust-migration.yml

"""Prevent retired installer engines from returning to active Rust distribution."""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = (
    "rust/crates/installer-cli/src/main.rs",
    "rust/crates/installer-cli/src/setup_authoring_gui.rs",
    "rust/scripts/windows-signed-bundle-smoke.ps1",
    ".github/workflows/rust-migration.yml",
)
RETIRED = (
    "packaging/TurkuazInstallerBackup.nsi",
    "packaging/TurkuazLocalTest.nsi",
)
LEGACY_SOURCE = re.compile(r"(?i)(?:\.(?:nsi|iss|csproj|sln)$)")
ACTIVE_COMMANDS = re.compile(
    r"(?i)(?:build-distribution-pair|TurkuazInstallerBackup|"
    r"\bmakensis(?:\.exe)?\b|\bchoco\s+install\s+nsis\b|"
    r"\bdotnet\s+(?:build|publish|tool\s+install)\b)"
)


def check(root: Path) -> list[str]:
    problems: list[str] = []
    for relative in REQUIRED:
        if not (root / relative).is_file():
            problems.append("required Rust installer source missing: " + relative)
    for relative in RETIRED:
        if (root / relative).exists():
            problems.append("obsolete fallback source still tracked: " + relative)
    # Do not reject .NET tooling in unrelated repos or Git history.
    for location in ("packaging", "rust", ".github/workflows"):
        folder = root / location
        if folder.is_dir():
            for path in folder.rglob("*"):
                if path.is_file() and LEGACY_SOURCE.search(path.name):
                    problems.append("retired installer source still active: " +
                                    path.relative_to(root).as_posix())
    for relative in REQUIRED:
        path = root / relative
        if not path.is_file():
            continue
        source = path.read_text(encoding="utf-8-sig")
        if ACTIVE_COMMANDS.search(source):
            problems.append("legacy installer operation in: " + relative)
    main = root / REQUIRED[0]
    if main.is_file() and "build-self-extracting-setup" not in main.read_text(
        encoding="utf-8-sig"
    ):
        problems.append("Rust single primary setup command removed")
    workflow = root / REQUIRED[3]
    if workflow.is_file() and "windows-signed-bundle-smoke.ps1" not in workflow.read_text(
        encoding="utf-8-sig"
    ):
        problems.append("Windows signed-bundle smoke coverage removed")
    return problems


def main() -> int:
    issues = check(ROOT)
    for issue in issues:
        print("TURKUAZ_RUST_SINGLE_PRIMARY_FAIL: " + issue, file=sys.stderr)
    if issues:
        return 1
    print("TURKUAZ_RUST_SINGLE_PRIMARY_POLICY_PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
