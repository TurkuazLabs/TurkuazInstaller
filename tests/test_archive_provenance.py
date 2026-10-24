# Dosya Yolu: REPO_ROOT/tests/test_archive_provenance.py
# Amac: ZIP ve EXE identity kanit zincirinin guvenli red senaryolarini test eder
# Modul - Python Unit Test
# Version: 1.0.0
# Aciklama: Hash tutarsizligi, ZIP traversal, symlink, duplicate, overwrite red testleri
# Bagimli Oldugu Katman: Tool | Testing

import hashlib
import json
from pathlib import Path
import stat
import subprocess
import sys
import tempfile
import unittest
import zipfile

TOOL = Path(__file__).resolve().parents[1] / "tools/diagnostics/verify_archive_provenance.py"


class ArchiveProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.archive = self.root / "qa.zip"
        self.output = self.root / "evidence.json"
        self.payload = b"offline-QA-exe\\x00" * 1024
        self.member = "RagnarClassic-Turkuaz-Setup.exe"
        self.expected = hashlib.sha256(self.payload).hexdigest()
        self._write_zip([(self.member, self.payload)])

    def _write_zip(self, files):
        with zipfile.ZipFile(self.archive, "w", compression=zipfile.ZIP_DEFLATED) as z:
            for name, payload in files:
                z.writestr(name, payload)

    def _run(self, zip_hash=None, member=None, output=None):
        return subprocess.run([
            sys.executable, str(TOOL),
            "--archive", str(self.archive),
            "--expected-archive-sha256", zip_hash or hashlib.sha256(self.archive.read_bytes()).hexdigest(),
            "--expected-member", member or f"{self.member}={self.expected}",
            "--output", str(output or self.output),
        ], capture_output=True, text=True)

    def test_matches_both_digests_without_execution_or_clean_verdict(self):
        run = self._run()
        self.assertEqual(run.returncode, 0, run.stderr)
        report = json.loads(self.output.read_text())
        self.assertEqual(report["members"][0]["sha256"], self.expected)
        self.assertEqual(report["browser_verdict"], "NOT_RECORDED")
        self.assertEqual(report["production_acceptance"], "NOT_ACCEPTED")
        self.assertFalse(report["antivirus_scan_performed"])
        self.assertFalse(report["files_executed"])
        self.assertFalse(report["files_extracted"])

    def test_fails_closed_for_archive_hash_mismatch(self):
        self.assertNotEqual(self._run(zip_hash="0" * 64).returncode, 0)
        self.assertFalse(self.output.exists())

    def test_fails_closed_for_executable_hash_mismatch(self):
        self.assertNotEqual(self._run(member=f"{self.member}={'0'*64}").returncode, 0)
        self.assertFalse(self.output.exists())

    def test_rejects_missing_or_malformed_member(self):
        self.assertNotEqual(self._run(member=f"missing.exe={self.expected}").returncode, 0)
        self.assertNotEqual(self._run(member=f"{self.member}=oops").returncode, 0)
        self.assertFalse(self.output.exists())

    def test_rejects_traversal_and_duplicate_entries(self):
        for entries in [
            [("../escape.exe", b"X"), (self.member, self.payload)],
            [(self.member, self.payload), (self.member.upper(), self.payload)],
        ]:
            with self.subTest(entries=entries):
                self._write_zip(entries)
                self.assertNotEqual(self._run().returncode, 0)
                self.assertFalse(self.output.exists())

    def test_rejects_symlink_member(self):
        link = zipfile.ZipInfo("shortcut")
        link.create_system = 3
        link.external_attr = (stat.S_IFLNK | 0o777) << 16
        with zipfile.ZipFile(self.archive, "w") as z:
            z.writestr(link, "not-a-file")
            z.writestr(self.member, self.payload)
        self.assertNotEqual(self._run().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_rejects_selected_oversized_member_without_decompression(self):
        with zipfile.ZipFile(self.archive, "w") as z:
            z.writestr(self.member, b"tiny")
        raw = self.archive.read_bytes()
        central = raw.index(b"PK\\x01\\x02")
        modified = bytearray(raw)
        modified[central + 24:central + 28] = (0xFFFFFFFF).to_bytes(4, "little")
        self.archive.write_bytes(modified)
        self.assertNotEqual(self._run().returncode, 0)
        self.assertFalse(self.output.exists())

    def test_never_overwrites_report(self):
        self.assertEqual(self._run().returncode, 0)
        original = self.output.read_bytes()
        self.assertNotEqual(self._run().returncode, 0)
        self.assertEqual(self.output.read_bytes(), original)


if __name__ == "__main__":
    unittest.main()
