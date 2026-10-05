# 📄 Dosya Yolu: /tools/validate_contracts.py
# 📌 Amac: TurkuazInstaller Community manifest, provider ve guvenlik contract invariantlarini statik dogrulamak
# 📌 Modul - Python
# Version: 1.0.0
# Aciklama: Stable v1 full install, Authenticode, prerequisite, HTTPS/hash ve Community-Pro boundary kurallarini kontrol eder
# Bagimli Oldugu Katman: Tool | Config

from pathlib import Path
import re
import sys

import yaml


ROOT = Path(__file__).resolve().parents[1]

MANIFEST = ROOT / "contracts" / "installer-manifest.yml"
PROVIDER = ROOT / "contracts" / "release-provider.yml"
EXAMPLE = ROOT / "examples" / "community-manifest.yml"
SECURITY = ROOT / "docs" / "SECURITY_MODEL.md"
ARCHITECTURE = ROOT / "docs" / "ARCHITECTURE.md"

EXPECTED_PROVIDERS = {"github", "gitea", "http"}
EXPECTED_CHANNELS = {"stable", "beta"}
EXPECTED_INSTALL_MODES = {"full"}
EXPECTED_URI_SCHEMES = {"https", "file"}
EXPECTED_SIGNATURE_ALGORITHMS = {"authenticode"}
EXPECTED_PREREQUISITES = {
    "windows-build",
    "architecture",
    "dotnet-desktop-runtime",
}
SHA256_PATTERN = re.compile(r"^[0-9a-f]{64}$")


def fail(message: str) -> None:
    print(
        f"CONTRACT_VALIDATION_FAIL: {message}",
        file=sys.stderr,
    )
    raise SystemExit(1)


def load_yaml(path: Path) -> dict:
    if not path.is_file():
        fail(
            f"missing file: {path.relative_to(ROOT)}"
        )

    loaded = yaml.safe_load(
        path.read_text(encoding="utf-8")
    )

    if not isinstance(loaded, dict):
        fail(
            f"mapping root required: {path.relative_to(ROOT)}"
        )

    return loaded


manifest = load_yaml(MANIFEST)

if manifest.get("schema_version") != 1:
    fail(
        "installer manifest schema_version must be 1"
    )

channels = set(
    manifest
    .get("package", {})
    .get("channel", {})
    .get("allowed", [])
)

if channels != EXPECTED_CHANNELS:
    fail(
        f"release channels drifted: {sorted(channels)}"
    )

schemes = set(
    manifest
    .get("artifact", {})
    .get("uri", {})
    .get("allowed_schemes", [])
)

if schemes != EXPECTED_URI_SCHEMES:
    fail(
        f"artifact URI schemes drifted: {sorted(schemes)}"
    )

install_modes = set(
    manifest
    .get("install", {})
    .get("mode", {})
    .get("allowed", [])
)

if install_modes != EXPECTED_INSTALL_MODES:
    fail(
        f"install modes drifted: {sorted(install_modes)}"
    )

signature_algorithms = set(
    manifest
    .get("artifact", {})
    .get("signature", {})
    .get("algorithm", [])
)

if signature_algorithms != EXPECTED_SIGNATURE_ALGORITHMS:
    fail(
        "signature algorithms drifted: "
        f"{sorted(signature_algorithms)}"
    )

prerequisites = set(
    manifest
    .get("install", {})
    .get("prerequisites", {})
    .get("supported_ids", [])
)

if prerequisites != EXPECTED_PREREQUISITES:
    fail(
        f"prerequisite IDs drifted: {sorted(prerequisites)}"
    )

security = manifest.get("security", {})

for key in (
    "checksum_failure",
    "invalid_signature",
    "transport_http_remote",
    "path_escape",
    "unsupported_prerequisite",
):
    if security.get(key) != "deny":
        fail(
            f"manifest security invariant must deny: {key}"
        )

artifact = manifest.get("artifact", {})

if (
    artifact
    .get("sha256", {})
    .get("required_before_install")
    is not True
):
    fail(
        "SHA-256 must be required before install"
    )

provider = load_yaml(PROVIDER)

if provider.get("schema_version") != 1:
    fail(
        "release provider schema_version must be 1"
    )

provider_ids = set(
    provider.get("providers", {}).keys()
)

if provider_ids != EXPECTED_PROVIDERS:
    fail(
        f"provider IDs drifted: {sorted(provider_ids)}"
    )

for provider_id, definition in (
    provider.get("providers", {}).items()
):
    if definition.get("community") is not True:
        fail(
            f"{provider_id}: Community provider must remain available"
        )

    authentication = definition.get(
        "authentication",
        {},
    )

    if (
        authentication.get("secret_in_manifest")
        is not False
    ):
        fail(
            f"{provider_id}: secret_in_manifest must be false"
        )

if (
    provider
    .get("providers", {})
    .get("http", {})
    .get("remote_plain_http")
    is not False
):
    fail(
        "generic HTTP provider must reject remote plain HTTP"
    )

example = load_yaml(EXAMPLE)

if example.get("schema_version") != 1:
    fail(
        "example manifest schema_version must be 1"
    )

example_hash = str(
    example
    .get("artifact", {})
    .get("sha256", "")
).strip().lower()

if not SHA256_PATTERN.fullmatch(example_hash):
    fail(
        "example SHA-256 must contain exactly 64 lowercase hex characters"
    )

if (
    example
    .get("package", {})
    .get("channel")
    not in EXPECTED_CHANNELS
):
    fail(
        "example release channel unsupported"
    )

if (
    example
    .get("install", {})
    .get("mode")
    not in EXPECTED_INSTALL_MODES
):
    fail(
        "example install mode unsupported"
    )

example_signature = (
    example
    .get("artifact", {})
    .get("signature", {})
    .get("algorithm")
)

if (
    example_signature
    not in EXPECTED_SIGNATURE_ALGORITHMS
):
    fail(
        "example signature algorithm unsupported"
    )

security_text = SECURITY.read_text(
    encoding="utf-8"
)

for required in (
    "SHA-256",
    "path traversal",
    "HTTPS",
    "private signing key",
    "rollback",
):
    if (
        required.lower()
        not in security_text.lower()
    ):
        fail(
            f"security model invariant missing: {required}"
        )

architecture_text = ARCHITECTURE.read_text(
    encoding="utf-8"
)

if (
    "TurkuazSoft/TurkuazInstaller-Pro"
    not in architecture_text
):
    fail(
        "Community/Pro dependency boundary missing"
    )

if (
    "Community hicbir zaman"
    not in architecture_text
):
    fail(
        "Community reverse-dependency rule missing"
    )

print(
    "TURKUAZ_INSTALLER_CONTRACTS_OK"
)
