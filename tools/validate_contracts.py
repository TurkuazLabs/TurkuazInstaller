# 📄 Dosya Yolu: /tools/validate_contracts.py
# 📌 Amac: TurkuazInstaller Community manifest, manifest trust, provider ve guvenlik contract invariantlarini statik dogrulamak
# 📌 Modul - Python
# Version: 1.2.0
# Aciklama: Detached CMS trust, Stable v1 full install, Authenticode, prerequisite, HTTPS/hash ve Community-Pro boundary kurallarini kontrol eder
# Bagimli Oldugu Katman: Tool | Config

from pathlib import Path
import re
import sys

import yaml


ROOT = Path(__file__).resolve().parents[1]

MANIFEST = ROOT / "contracts" / "installer-manifest.yml"
MANIFEST_TRUST = ROOT / "contracts" / "manifest-trust.yml"
PROVIDER = ROOT / "contracts" / "release-provider.yml"
EXAMPLE = ROOT / "examples" / "community-manifest.yml"
SECURITY = ROOT / "docs" / "SECURITY_MODEL.md"
ARCHITECTURE = ROOT / "docs" / "ARCHITECTURE.md"

EXPECTED_PROVIDERS = {"github", "gitea", "http"}
EXPECTED_CHANNELS = {"stable", "beta"}
EXPECTED_INSTALL_MODES = {"full"}
EXPECTED_URI_SCHEMES = {"https", "file"}
EXPECTED_SIGNATURE_ALGORITHMS = {"authenticode"}
EXPECTED_MANIFEST_SIGNATURE_FORMAT = "cms-pkcs7-detached"
EXPECTED_MANIFEST_SIGNATURE_SUFFIX = ".p7s"
REQUIRED_SIGNATURE_POLICY_FIELDS = {
    "publisher_subject",
}
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

manifest_trust_reference = manifest.get(
    "manifest_trust",
    {},
)

if (
    manifest_trust_reference.get("contract")
    != MANIFEST_TRUST.name
):
    fail(
        "installer manifest must reference manifest-trust.yml"
    )

if (
    manifest_trust_reference.get("required_before_parse")
    is not True
):
    fail(
        "manifest trust must be required before YAML parsing"
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

signature_policy = (
    manifest
    .get("artifact", {})
    .get("signature", {})
)

missing_signature_fields = {
    field
    for field in REQUIRED_SIGNATURE_POLICY_FIELDS
    if field not in signature_policy
}

if missing_signature_fields:
    fail(
        "signature policy fields missing: "
        f"{sorted(missing_signature_fields)}"
    )

if (
    signature_policy
    .get("publisher_subject", {})
    .get("required_when_present")
    is not True
):
    fail(
        "Authenticode publisher_subject must be required when signature is present"
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
    "unsigned_manifest",
    "missing_manifest_trust",
    "checksum_failure",
    "invalid_signature",
    "publisher_mismatch",
    "certificate_pin_mismatch",
    "concurrent_package_operation",
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

manifest_trust = load_yaml(MANIFEST_TRUST)

if manifest_trust.get("schema_version") != 1:
    fail(
        "manifest trust schema_version must be 1"
    )

manifest_signature = manifest_trust.get(
    "signature",
    {},
)

if (
    manifest_signature.get("format")
    != EXPECTED_MANIFEST_SIGNATURE_FORMAT
):
    fail(
        "manifest signature format must be cms-pkcs7-detached"
    )

if (
    manifest_signature.get("detached_suffix")
    != EXPECTED_MANIFEST_SIGNATURE_SUFFIX
):
    fail(
        "manifest detached signature suffix must be .p7s"
    )

if manifest_signature.get("signer_count") != 1:
    fail(
        "manifest detached signature must have exactly one signer"
    )

if (
    manifest_signature.get("signs_raw_manifest_bytes")
    is not True
):
    fail(
        "manifest signature must cover raw manifest bytes"
    )

if (
    manifest_signature.get("signer_certificate_embedded")
    is not True
):
    fail(
        "manifest signature must embed signer certificate"
    )

trust_store = manifest_trust.get(
    "trust_store",
    {},
)

if trust_store.get("external_to_manifest") is not True:
    fail(
        "manifest trust store must remain external to manifest"
    )

if trust_store.get("package_scoped") is not True:
    fail(
        "manifest trust store must remain package scoped"
    )

publisher_subject = trust_store.get(
    "publisher_subject",
    {},
)

if publisher_subject.get("required") is not True:
    fail(
        "manifest publisher subject pin must be required"
    )

certificate_sha256 = trust_store.get(
    "certificate_sha256",
    {},
)

if certificate_sha256.get("required") is not True:
    fail(
        "manifest certificate SHA-256 pin must be required"
    )

if certificate_sha256.get("length") != 64:
    fail(
        "manifest certificate SHA-256 pin must be 64 hex characters"
    )

manifest_trust_security = manifest_trust.get(
    "security",
    {},
)

for key in (
    "unsigned_manifest",
    "missing_package_trust",
    "invalid_cms_signature",
    "multiple_signers",
    "publisher_mismatch",
    "certificate_pin_mismatch",
    "expired_signer_certificate",
    "parse_before_verification",
):
    if manifest_trust_security.get(key) != "deny":
        fail(
            f"manifest trust invariant must deny: {key}"
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
    "CMS/PKCS#7",
    "manifest-trust.yml",
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
