# 📄 Dosya Yolu: /tools/release/package_release.py
# 📌 Amac: Release publish klasorunu deterministic ZIP archive dosyasina donusturur
# 📌 Modul - Tool Python
# Version: 1.0.0
# Aciklama: Dosya sirasi, ZIP timestamp, permission ve compression metadata degerlerini sabitleyerek ayni input byte'larindan ayni ZIP byte'larini uretir
# Bagimli Oldugu Katman: Tool

from __future__ import annotations

import argparse
from pathlib import Path
import zipfile

FIXED_ZIP_TIMESTAMP = (1980, 1, 1, 0, 0, 0)
FILE_PERMISSION_BITS = 0o100644 << 16
COMPRESSION_LEVEL = 9


def iter_files(root: Path) -> list[Path]:
    return sorted(
        (path for path in root.rglob("*") if path.is_file()),
        key=lambda path: path.relative_to(root).as_posix(),
    )


def package_directory(source: Path, destination: Path) -> None:
    source = source.resolve()
    destination = destination.resolve()

    if not source.is_dir():
        raise ValueError(f"Source directory does not exist: {source}")

    destination.parent.mkdir(parents=True, exist_ok=True)

    with zipfile.ZipFile(
        destination,
        mode="w",
        compression=zipfile.ZIP_DEFLATED,
        compresslevel=COMPRESSION_LEVEL,
    ) as archive:
        for file_path in iter_files(source):
            relative_name = file_path.relative_to(source).as_posix()
            info = zipfile.ZipInfo(relative_name, date_time=FIXED_ZIP_TIMESTAMP)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = FILE_PERMISSION_BITS
            info.create_system = 3

            archive.writestr(
                info,
                file_path.read_bytes(),
                compress_type=zipfile.ZIP_DEFLATED,
                compresslevel=COMPRESSION_LEVEL,
            )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    package_directory(args.input, args.output)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
