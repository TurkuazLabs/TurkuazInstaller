# Dosya Yolu: REPO_ROOT/tools/diagnostics/verify_archive_provenance.py
# Amac: Supheli GitHub Actions ZIP ile icindeki secili dosyalari bagimsiz hashlerle eslestirir
# Modul - Python Tool
# Version: 1.0.0
# Aciklama: Salt okunur, streaming ZIP/uye SHA-256 kaniti; antivirus taramasi degildir
# Bagimli Oldugu Katman: Tool | Security | Testing

"""Verify independently supplied ZIP and member digests; never extract or run files."""

import argparse
import hashlib
import json
from pathlib import Path
import re
import stat
import sys
import zipfile

_SHA256 = re.compile(r"[0-9a-fA-F]{64}\Z")
_MAX_MEMBERS = 10_000
_MAX_SELECTED_UNCOMPRESSED_BYTES = 512 * 1024 * 1024
_CHUNK = 1024 * 1024


class EvidenceError(Exception):
    """Reject ambiguous/untrusted input without recording success."""


def _sha256_of_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(_CHUNK), b""):
            digest.update(chunk)
    return digest.hexdigest()


def _digest(value: str) -> str:
    if not _SHA256.fullmatch(value):
        raise EvidenceError("Expected SHA-256 must contain exactly 64 hex characters")
    return value.lower()


def _member_name(name: str) -> str:
    if not name or "\\" in name or "\x00" in name or name.startswith("/") or re.match(r"^[A-Za-z]:", name):
        raise EvidenceError("ZIP contains an unsafe member path")
    parts = name.split("/")
    if any(part in ("", ".", "..") for part in parts):
        raise EvidenceError("ZIP contains an ambiguous or traversing member path")
    return name


def _expected_member(raw: str) -> tuple[str, str]:
    if "=" not in raw:
        raise EvidenceError("Expected member must be given as path=SHA256")
    name, digest = raw.rsplit("=", 1)
    return _member_name(name), _digest(digest)


def verify_archive(archive: Path, expected_archive: str, expected_members: list[str]) -> dict:
    expected_zip_hash = _digest(expected_archive)
    members = dict(_expected_member(raw) for raw in expected_members)
    if not members or len(members) != len(expected_members):
        raise EvidenceError("At least one uniquely named expected member is required")
    if not archive.is_file() or archive.is_symlink():
        raise EvidenceError("Archive must be an existing regular non-symlink file")
    before = archive.stat()
    zip_hash = _sha256_of_file(archive)
    if zip_hash != expected_zip_hash:
        raise EvidenceError("ZIP SHA-256 mismatch; no evidence created")

    found = {}
    try:
        with zipfile.ZipFile(archive, "r") as container:
            entries = container.infolist()
            if len(entries) > _MAX_MEMBERS:
                raise EvidenceError("ZIP member count exceeds diagnostic safety cap")
            seen = set()
            for entry in entries:
                name = _member_name(entry.filename.rstrip("/") if entry.is_dir() else entry.filename)
                folded = name.casefold()
                if folded in seen:
                    raise EvidenceError("ZIP contains duplicate/case-colliding member names")
                seen.add(folded)
                file_type = (entry.external_attr >> 16) & 0o170000
                if file_type not in (0, stat.S_IFREG, stat.S_IFDIR):
                    raise EvidenceError("ZIP contains a non-regular or symlink member")
                if name not in members:
                    continue
                if entry.is_dir() or file_type == stat.S_IFDIR or (entry.flag_bits & 1):
                    raise EvidenceError("Expected ZIP member must be an unencrypted regular file")
                if entry.file_size > _MAX_SELECTED_UNCOMPRESSED_BYTES:
                    raise EvidenceError("Selected ZIP member exceeds decompression safety limit")
                digest = hashlib.sha256()
                length = 0
                # Only read the selected member. ZipFile.open checks CRC at EOF.
                with container.open(entry, "r") as handle:
                    while True:
                        chunk = handle.read(_CHUNK)
                        if not chunk:
                            break
                        length += len(chunk)
                        if length > _MAX_SELECTED_UNCOMPRESSED_BYTES:
                            raise EvidenceError("Selected ZIP member exceeded decompression safety limit")
                        digest.update(chunk)
                actual = digest.hexdigest()
                if actual != members[name] or length != entry.file_size:
                    raise EvidenceError("Selected ZIP member digest/size mismatch")
                found[name] = {"relative_path": name, "sha256": actual, "size_bytes": length}
    except (zipfile.BadZipFile, EOFError, RuntimeError, NotImplementedError) as error:
        raise EvidenceError("Invalid, encrypted or unsupported ZIP archive") from error

    if set(found) != set(members):
        raise EvidenceError("One or more expected ZIP members are missing")
    after = archive.stat()
    if ((before.st_size, before.st_mtime_ns, before.st_ino) !=
            (after.st_size, after.st_mtime_ns, after.st_ino) or
            _sha256_of_file(archive) != zip_hash):
        raise EvidenceError("ZIP changed during evidence collection")

    return {
        "schema_version": 1,
        "diagnostic_only": True,
        "archive_sha256": zip_hash,
        "archive_size_bytes": after.st_size,
        "matches_independently_supplied_hashes": True,
        "members": [found[name] for name in sorted(found)],
        "files_executed": False,
        "files_extracted": False,
        "antivirus_scan_performed": False,
        "browser_verdict": "NOT_RECORDED",
        "production_acceptance": "NOT_ACCEPTED",
        "warning": "Hash identity is not malware clearance, reputation approval, or code signing.",
    }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--expected-archive-sha256", required=True)
    parser.add_argument("--expected-member", action="append", required=True, metavar="PATH=SHA256")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if not args.output.is_absolute() or not args.output.parent.is_dir():
            raise EvidenceError("Output must be an absolute path in an existing directory")
        if args.output.exists() or args.output.is_symlink():
            raise EvidenceError("Refusing to overwrite existing evidence")
        report = verify_archive(args.archive, args.expected_archive_sha256, args.expected_member)
        # Exclusive create protects existing evidence from replacement.
        with args.output.open("x", encoding="utf-8") as out:
            json.dump(report, out, indent=2, ensure_ascii=True)
            out.write("\n")
    except (EvidenceError, OSError, ValueError) as error:
        print(f"ARCHIVE_PROVENANCE_NOT_VERIFIED: {error}", file=sys.stderr)
        return 1
    print("ARCHIVE_PROVENANCE_VERIFIED (diagnostic only; AV verdict NOT_RECORDED)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
