#!/usr/bin/env python3
# Dosya Yolu: tools/theme-pack/test_build_theme_pack.py
# Amac: CLI tarafinda deterministik imza, fail-closed metadata ve anahtar gizliligi testleri
# Modul - Python unittest
# Version: 0.1.0-qa
# Aciklama: Gecici RSA self-signed test cert; hardcoded veya uretim anahtari kullanmaz
# Bagimli Oldugu Katman: Tool | CI | Security

import base64
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import types
import unittest
import zipfile

from build_theme_pack import build

PNG = base64.b64decode(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+XGmYAAAAASUVORK5CYII="
)


@unittest.skipUnless(shutil.which("openssl"), "Requires OpenSSL CLI")
class ThemePackBuilderTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="tpk-tests-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.cert = self.root / "cert.pem"
        self.key = self.root / "key.pem"
        result = subprocess.run(
            ["openssl", "req", "-x509", "-newkey", "rsa:2048", "-sha256",
             "-nodes", "-subj", "/CN=QA Ephemeral Theme Test",
             "-days", "1", "-keyout", str(self.key), "-out", str(self.cert)],
            capture_output=True, text=True)
        self.assertEqual(result.returncode, 0, result.stderr[:300])
        (self.root / "approved.png").write_bytes(PNG)
        self.args = types.SimpleNamespace(
            package_id="levelupgt.ragnar-classic-preview",
            theme_id="turkuaz-fantasy",
            version="0.1.0",
            asset=["concept-c=" + str(self.root / "approved.png")],
            screen=["welcome=concept-c", "complete=concept-c"],
            certificate=str(self.cert),
            private_key=str(self.key),
            output=str(self.root / "theme.tpk")
        )

    def test_build_has_only_manifest_signature_and_assets(self):
        build(self.args)
        with zipfile.ZipFile(self.args.output) as package:
            self.assertEqual(set(package.namelist()), {
                "manifest.json", "manifest.p7s", "assets/concept-c.png"
            })
            manifest = package.read("manifest.json")
            meta = json.loads(manifest)
            self.assertEqual(meta["package_id"], self.args.package_id)
            self.assertEqual(meta["assets"][0]["size_bytes"], len(PNG))
            self.assertEqual(package.read("assets/concept-c.png"), PNG)
            signature = self.root / "signature.p7s"
            signature.write_bytes(package.read("manifest.p7s"))
            data = self.root / "manifest.json"
            data.write_bytes(manifest)
            verify = subprocess.run([
                "openssl", "cms", "-verify", "-binary", "-inform", "DER",
                "-in", str(signature), "-content", str(data),
                "-noverify", "-out", str(self.root / "verified-content.txt")
            ], capture_output=True, text=True)
            # -noverify is QA-only. The C# reader independently enforces
            # exact publisher subject and certificate SHA-256 pins.
            self.assertEqual(verify.returncode, 0, verify.stderr[:300])
            self.assertNotIn(self.key.read_bytes(), package.read("manifest.json"))
            self.assertNotIn(self.key.read_bytes(), package.read("manifest.p7s"))

    def test_rejects_unknown_screen_asset(self):
        self.args.screen = ["welcome=missing-asset"]
        with self.assertRaisesRegex(ValueError, "undeclared"):
            build(self.args)

    def test_rejects_duplicate_id(self):
        self.args.asset = self.args.asset * 2
        with self.assertRaisesRegex(ValueError, "duplicated"):
            build(self.args)

    def test_rejects_non_png(self):
        (self.root / "approved.png").write_bytes(b"Not a PNG")
        with self.assertRaises(ValueError):
            build(self.args)

    def test_rejects_existing_output(self):
        Path(self.args.output).write_bytes(b"existing")
        with self.assertRaisesRegex(ValueError, "overwrite"):
            build(self.args)
        self.assertEqual(Path(self.args.output).read_bytes(), b"existing")

    def test_rejects_bad_semver(self):
        self.args.version = "1.0"
        with self.assertRaisesRegex(ValueError, "SemVer"):
            build(self.args)


if __name__ == "__main__":
    unittest.main()
