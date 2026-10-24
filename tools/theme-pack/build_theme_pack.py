#!/usr/bin/env python3
# Dosya Yolu: tools/theme-pack/build_theme_pack.py
# Amac: CMS imzali .tpk tema paketini guvenilir yerel dosyalardan uretir
# Modul - Python Build Tool
# Version: 0.1.0-qa
# Aciklama: Imza anahtarini koda gommez; OpenSSL CMS detached binary signature olusturur
# Bagimli Oldugu Katman: Build | Security | SignedThemePackReader

"""Build signed theme.tpk with a separately managed publisher signing key.

The signing certificate must already be trusted/pinned by the installer.
This tool NEVER embeds the signing private key or a self-authorizing trust key.
The .tpk is ZIP; its PNG files are NOT secret or encrypted.
"""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import tempfile
import zipfile

MAX_IMAGE = 16 * 1024 * 1024
MAX_ASSETS = 8
PNG = b"\x89PNG\r\n\x1a\n"
IDENTITY = re.compile(r"[a-z][a-z0-9-]{1,47}\Z", re.ASCII)
PRODUCT = re.compile(r"[a-z0-9][a-z0-9._-]{1,63}\Z", re.ASCII)
SEMVER = re.compile(
    r"(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)"
    r"(?:-((?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)"
    r"(?:\.(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*))*))?"
    r"(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\Z",
    re.ASCII,
)


def pairs(values, label):
    result = {}
    for value in values:
        if "=" not in value:
            raise ValueError(f"{label} requires identifier=path or screen=asset")
        name, path = value.split("=", 1)
        if not IDENTITY.fullmatch(name) or not path or name in result:
            raise ValueError(f"{label} identifier invalid or duplicated")
        result[name] = path
    return result


def describe_png(path):
    if path.is_symlink() or not path.is_file():
        raise ValueError("Only normal local PNG files are accepted")
    if not 33 <= path.stat().st_size <= MAX_IMAGE:
        raise ValueError("PNG file size is outside policy")
    digest = hashlib.sha256()
    with path.open("rb") as source:
        header = source.read(33)
        if header[:8] != PNG or header[12:16] != b"IHDR":
            raise ValueError("Not a PNG with a valid IHDR header")
        width, height = struct.unpack(">II", header[16:24])
        if not 1 <= width <= 8192 or not 1 <= height <= 8192 or width * height > 16777216:
            raise ValueError("PNG dimensions are outside policy")
        digest.update(header)
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return {
        "sha256": digest.hexdigest(),
        "size_bytes": path.stat().st_size,
        "width": width,
        "height": height,
    }


def build(args):
    if not PRODUCT.fullmatch(args.package_id) or not IDENTITY.fullmatch(args.theme_id):
        raise ValueError("Unsafe product/theme identity")
    if not SEMVER.fullmatch(args.version):
        raise ValueError("Version must be canonical SemVer")
    items = pairs(args.asset, "asset")
    screens = pairs(args.screen, "screen")
    if not (1 <= len(items) <= MAX_ASSETS and 1 <= len(screens) <= 12):
        raise ValueError("Unexpected asset/screen count")
    if any(asset not in items for asset in screens.values()):
        raise ValueError("Screen references undeclared asset")
    paths = {key: Path(value).resolve(strict=True) for key, value in items.items()}
    specs = [{"id": key, **describe_png(path)} for key, path in sorted(paths.items())]
    manifest = {
        "schema_version": 1, "format": "turkuaz.theme",
        "package_id": args.package_id, "theme_id": args.theme_id,
        "theme_version": args.version,
        "assets": specs, "screens": dict(sorted(screens.items())),
    }
    output = Path(args.output).absolute()
    if output.exists():
        raise ValueError("Will not overwrite an existing signed theme package")
    if not output.parent.is_dir():
        raise ValueError("Output directory must already exist")
    certificate = Path(args.certificate).resolve(strict=True)
    private_key = Path(args.private_key).resolve(strict=True)

    with tempfile.TemporaryDirectory(prefix="turkuaz-sign-") as temporary:
        temp = Path(temporary)
        manifest_bytes = (json.dumps(manifest, ensure_ascii=True, separators=(",", ":"), sort_keys=True)
                          + "\n").encode("utf-8")
        manifest_file, signature_file = temp / "manifest.json", temp / "manifest.p7s"
        manifest_file.write_bytes(manifest_bytes)
        try:
            subprocess.run([
                "openssl", "cms", "-sign", "-binary",
                "-in", str(manifest_file), "-signer", str(certificate),
                "-inkey", str(private_key), "-md", "sha256",
                "-outform", "DER", "-out", str(signature_file),
                "-nosmimecap",
            ], check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        except (subprocess.CalledProcessError, FileNotFoundError) as exc:
            raise ValueError("OpenSSL CMS signing failed; verify signer/key outside CI logs") from exc
        signature = signature_file.read_bytes()
        if not 1 <= len(signature) <= 16384:
            raise ValueError("CMS signature length outside policy")

        stage = temp / "unsigned-upload.tpk"
        with zipfile.ZipFile(stage, mode="w", compression=zipfile.ZIP_DEFLATED,
                             compresslevel=6, allowZip64=False) as archive:
            def add(name, content):
                info = zipfile.ZipInfo(filename=name, date_time=(1980, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                info.external_attr = 0o100644 << 16
                archive.writestr(info, content, compress_type=zipfile.ZIP_DEFLATED)
            add("manifest.json", manifest_bytes)
            add("manifest.p7s", signature)
            for asset_id, file_path in sorted(paths.items()):
                add(f"assets/{asset_id}.png", file_path.read_bytes())
        if stage.stat().st_size > 80 * 1024 * 1024:
            raise ValueError("Archive exceeds bounded reader limit")
        # Verify local archive inventory before publishing it.
        with zipfile.ZipFile(stage) as archive:
            assert set(archive.namelist()) == (
                {"manifest.json", "manifest.p7s"}
                | {f"assets/{asset_id}.png" for asset_id in paths})
            assert archive.read("manifest.json") == manifest_bytes
        os.replace(stage, output)
    print(f"THEME_PACK_BUILT file={output.name} sha256={hashlib.sha256(output.read_bytes()).hexdigest()}")
    print("QA_ONLY: certificate must be pinned externally; theme assets are NOT encrypted.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package-id", required=True)
    parser.add_argument("--theme-id", required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--asset", action="append", required=True, metavar="ID=PNG_PATH")
    parser.add_argument("--screen", action="append", required=True, metavar="SCREEN=ASSET_ID")
    parser.add_argument("--certificate", required=True, help="Trusted signing certificate PEM path")
    parser.add_argument("--private-key", required=True, help="Signing key PEM path; never commit this key")
    parser.add_argument("--output", required=True, help="Output .tpk path; existing files are not replaced")
    args = parser.parse_args()
    if not args.output.lower().endswith(".tpk"):
        parser.error("Output must have .tpk suffix")
    try:
        build(args)
    except (OSError, ValueError) as exc:
        parser.exit(2, f"THEME_PACK_FAILED: {exc}\n")


if __name__ == "__main__":
    main()
