#!/usr/bin/env python3
# Dosya Yolu: REPO_ROOT/rust/scripts/test_rust_primary_only.py
# Amac: Rust tek kurulum CI politikasinin fail-closed negatif regresyonlari
# Modul - Python / unittest
# Version: 1.0.0-candidate
# Aciklama: Sadece sentetik repo fixture; gercek signer veya kurulum testi degil
# Bagimli Oldugu Katman: rust/scripts/check_rust_primary_only.py
from pathlib import Path
import tempfile
import unittest

from check_rust_primary_only import check, REQUIRED, RETIRED, ROOT


class RustSinglePrimarySourcePolicyTests(unittest.TestCase):
    def _fixture(self, root: Path) -> None:
        for name in REQUIRED:
            p = root / name
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_text(
                "build-self-extracting-setup\nwindows-signed-bundle-smoke.ps1\n",
                encoding="utf-8",
            )

    def test_actual_repository_has_no_retired_runtime_engine(self):
        self.assertEqual(check(ROOT), [])

    def test_obsolete_nsi_and_dotnet_projects_are_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self._fixture(root)
            self.assertFalse(check(root))
            for name in ("packaging/Old.nsi", "rust/Old.csproj", "rust/Old.sln",
                         "packaging/Old.iss", RETIRED[0]):
                with self.subTest(path=name):
                    f = root / name
                    f.parent.mkdir(parents=True, exist_ok=True)
                    f.write_text("retired", encoding="utf-8")
                    self.assertTrue(check(root))
                    f.unlink()

    def test_retired_runtime_command_and_ci_compiler_are_rejected(self):
        for token in ("build-distribution-pair", "TurkuazInstallerBackup.exe",
                      "makensis.exe", "choco install nsis", "dotnet publish"):
            with self.subTest(token=token):
                with tempfile.TemporaryDirectory() as folder:
                    root = Path(folder)
                    self._fixture(root)
                    p = root / ".github/workflows/rust-migration.yml"
                    p.write_text(p.read_text(encoding="utf-8") + token,
                                 encoding="utf-8")
                    self.assertTrue(check(root))

    def test_missing_signed_smoke_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            self._fixture(root)
            p = root / ".github/workflows/rust-migration.yml"
            p.write_text("single Rust installer only", encoding="utf-8")
            self.assertTrue(any("signed-bundle smoke" in item for item in check(root)))


if __name__ == "__main__":
    unittest.main()
