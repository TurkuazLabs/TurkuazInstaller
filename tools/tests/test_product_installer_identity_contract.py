#!/usr/bin/env python3
# Dosya Yolu: REPO_ROOT/tools/tests/test_product_installer_identity_contract.py
# Amac: Gorsel ad, client/packet versiyonu ve release ayrimini dokumanda kilitler
# Modul - Python unittest
# Version: 1.0.0
# Aciklama: Runtime ya da baska repo dosyasi degistirmez
# Bagimli Oldugu Katman: Documentation | Tests

from pathlib import Path
import unittest

DOC = Path(__file__).resolve().parents[2] / "docs/PRODUCT_INSTALLER_IDENTITY.md"


class IdentityContractTests(unittest.TestCase):
    def test_product_is_not_engine(self):
        text = DOC.read_text("utf-8")
        for required in ("product neutral", "ui-profile.yml", "package_id",
                         "installer_version", "client_build", "packetver",
                         "source_commit"):
            self.assertIn(required, text)

    def test_2015_and_2022_reserved_until_validated(self):
        text = DOC.read_text("utf-8")
        for required in ("classic-2015", "renewal-2022", "preview-qa",
                         "ready:false", "production_publish_allowed",
                         "client_year: null"):
            self.assertIn(required, text)

    def test_filename_visible_but_profile_hidden(self):
        text = DOC.read_text("utf-8")
        self.assertIn("Turkuaz-Ragnarok-Online-Setup.exe", text)
        self.assertIn("artifact manifest", text)
        self.assertIn("source-sha12", text)

    def test_no_release_on_branding_only(self):
        text = DOC.read_text("utf-8")
        self.assertIn("No arbitrary remote XAML", text)
        self.assertIn("automatic\n  release on a branding-only commit", text.lower())


if __name__ == "__main__":
    unittest.main()
