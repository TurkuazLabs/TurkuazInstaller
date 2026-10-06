# 📄 Dosya Yolu: /tools/validate_contracts.py
# 📌 Amac: TurkuazInstaller Community manifest, manifest trust, provider ve guvenlik contract invariantlarini statik dogrulamak
# 📌 Modul - Python
# Version: 1.6.1
# Aciklama: Detached trust, reboot resume, bootstrap self-update digest/signer, HTTPS/hash ve Community-Pro boundary kurallarini kontrol eder
# Bagimli Oldugu Katman: Tool | Config

from pathlib import Path
import re
import sys

import yaml


ROOT = Path(__file__).resolve().parents[1]

MANIFEST = ROOT / "contracts" / "installer-manifest.yml"
MANIFEST_TRUST = ROOT / "contracts" / "manifest-trust.yml"
BOOTSTRAP_SELF_UPDATE = ROOT / "contracts" / "bootstrap-self-update.yml"
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

prerequisite_policy = (
    manifest
    .get("install", {})
    .get("prerequisites", {})
)

detection_policy = prerequisite_policy.get(
    "detection",
    {},
)

if detection_policy.get("engine") != "detector_registry":
    fail(
        "prerequisite detection engine must use detector_registry"
    )

if detection_policy.get("unknown_id") != "deny":
    fail(
        "unknown prerequisite IDs must fail closed"
    )

auto_install_policy = prerequisite_policy.get(
    "auto_install",
    {},
)

if auto_install_policy.get("optional") is not True:
    fail(
        "prerequisite auto-install must remain optional"
    )

prerequisite_artifact_policy = auto_install_policy.get(
    "artifact",
    {},
)

if set(
    prerequisite_artifact_policy.get(
        "allowed_schemes",
        [],
    )
) != EXPECTED_URI_SCHEMES:
    fail(
        "prerequisite installer URI schemes must match artifact policy"
    )

if (
    prerequisite_artifact_policy
    .get("file_extension")
    != ".exe"
):
    fail(
        "prerequisite auto-install must remain restricted to direct EXE artifacts"
    )

for key in (
    "sha256_required_before_execute",
    "authenticode_required",
    "publisher_subject_required",
):
    if prerequisite_artifact_policy.get(key) is not True:
        fail(
            f"prerequisite installer invariant missing: {key}"
        )

if (
    auto_install_policy
    .get("arguments", {})
    .get("shell_interpretation")
    is not False
):
    fail(
        "prerequisite installer arguments must remain shell-free"
    )

if (
    auto_install_policy
    .get("post_install_reprobe_required")
    is not True
):
    fail(
        "prerequisite auto-install must re-probe after execution"
    )

if (
    auto_install_policy
    .get("reboot_required_exit")
    != "persist_request_schedule_runonce_reprobe_resume"
):
    fail(
        "reboot-required prerequisite result must use persisted RunOnce resume orchestration"
    )

resume_policy = auto_install_policy.get(
    "resume",
    {},
)

if resume_policy.get("journal_phases") != [
    "reboot_resume_armed",
    "awaiting_reboot",
]:
    fail(
        "reboot resume journal phases must preserve pre-execution arm and reboot wait"
    )

expected_resume_policy = {
    "persisted_request_required": True,
    "relaunch": "hkcu_runonce_bootstrap",
    "deferred_delete_value_prefix": True,
    "signed_manifest_revalidation_required": True,
    "expected_version_pin_required": True,
    "expected_artifact_sha256_pin_required": True,
    "pending_prerequisite_reprobe_required": True,
    "repeat_same_installer_when_still_unsatisfied": False,
    "manual_mutation_while_awaiting_reboot": "deny",
}

for key, expected_value in expected_resume_policy.items():
    if resume_policy.get(key) != expected_value:
        fail(
            f"reboot resume invariant mismatch: {key}"
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
    "unsigned_prerequisite_installer",
    "prerequisite_hash_failure",
    "prerequisite_signature_failure",
    "prerequisite_post_install_unsatisfied",
    "resume_journal_mismatch",
    "resume_release_identity_mismatch",
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

bootstrap_self_update = load_yaml(
    BOOTSTRAP_SELF_UPDATE
)

if bootstrap_self_update.get("schema_version") != 1:
    fail(
        "bootstrap self-update schema_version must be 1"
    )

bootstrap_discovery = bootstrap_self_update.get(
    "discovery",
    {},
)

expected_bootstrap_discovery = {
    "provider": "github_latest_release",
    "stable_only": True,
    "newer_version_only": True,
    "asset_name": "TurkuazInstaller.Bootstrapper.exe",
}

for key, expected_value in expected_bootstrap_discovery.items():
    if bootstrap_discovery.get(key) != expected_value:
        fail(
            f"bootstrap self-update discovery invariant mismatch: {key}"
        )

bootstrap_asset = bootstrap_discovery.get(
    "asset",
    {},
)

if bootstrap_asset.get("scheme") != "https":
    fail(
        "bootstrap self-update asset must require HTTPS"
    )

if bootstrap_asset.get("host") != "github.com":
    fail(
        "bootstrap self-update asset host must remain github.com"
    )

if bootstrap_asset.get("state") != "uploaded":
    fail(
        "bootstrap self-update asset must require uploaded state"
    )

if bootstrap_asset.get("size_required") is not True:
    fail(
        "bootstrap self-update asset size must be required"
    )

bootstrap_digest = bootstrap_asset.get(
    "digest",
    {},
)

if (
    bootstrap_digest.get("algorithm") != "sha256"
    or bootstrap_digest.get("required") is not True
):
    fail(
        "bootstrap self-update must require GitHub SHA-256 asset digest"
    )

bootstrap_download = bootstrap_self_update.get(
    "download",
    {},
)

for key in (
    "exact_size_required",
    "exact_sha256_required",
):
    if bootstrap_download.get(key) is not True:
        fail(
            f"bootstrap self-update download invariant missing: {key}"
        )

for key in (
    "oversize_stream",
    "hash_mismatch",
):
    if bootstrap_download.get(key) != "deny":
        fail(
            f"bootstrap self-update download must deny: {key}"
        )

bootstrap_trust = bootstrap_self_update.get(
    "trust",
    {},
)

if (
    bootstrap_trust
    .get("current_bootstrap", {})
    .get("authenticode_required_for_auto_update")
    is not True
):
    fail(
        "bootstrap self-update current executable must require Authenticode"
    )

replacement_trust = bootstrap_trust.get(
    "replacement",
    {},
)

for key in (
    "authenticode_required",
    "win_verify_trust_required",
    "publisher_subject_must_match_current",
    "certificate_sha256_must_match_current",
):
    if replacement_trust.get(key) is not True:
        fail(
            f"bootstrap replacement trust invariant missing: {key}"
        )

if (
    bootstrap_trust
    .get("explicit_replacement_path", {})
    .get("same_trust_verification_required")
    is not True
):
    fail(
        "explicit bootstrap replacement path must not bypass signer verification"
    )

if (
    bootstrap_trust
    .get("completion_source", {})
    .get("same_trust_verification_required")
    is not True
):
    fail(
        "bootstrap self-update completion source must not bypass signer verification"
    )

bootstrap_handoff = bootstrap_self_update.get(
    "handoff",
    {},
)

if bootstrap_handoff.get("mode") != "two_process":
    fail(
        "bootstrap self-update handoff must remain two_process"
    )

for key in (
    "replacement_name_must_match_current",
    "resume_arguments_preserved",
    "cleanup_staged_source_after_resume",
):
    if bootstrap_handoff.get(key) is not True:
        fail(
            f"bootstrap self-update handoff invariant missing: {key}"
        )

bootstrap_release = bootstrap_self_update.get(
    "release",
    {},
)

for key in (
    "standalone_bootstrap_asset_required",
    "signed_before_publish",
    "github_asset_digest_required",
    "provenance_attestation_required",
):
    if bootstrap_release.get(key) is not True:
        fail(
            f"bootstrap self-update release invariant missing: {key}"
        )

bootstrap_security = bootstrap_self_update.get(
    "security",
    {},
)

for key in (
    "plain_http",
    "missing_digest",
    "invalid_digest",
    "invalid_size",
    "invalid_authenticode",
    "signer_subject_mismatch",
    "signer_certificate_mismatch",
    "unverified_explicit_handoff",
    "unverified_completion_source",
):
    if bootstrap_security.get(key) != "deny":
        fail(
            f"bootstrap self-update security invariant must deny: {key}"
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

example_prerequisites = (
    example
    .get("install", {})
    .get("prerequisites", [])
)

auto_install_examples = [
    prerequisite
    for prerequisite in example_prerequisites
    if isinstance(prerequisite, dict)
    and isinstance(
        prerequisite.get("install"),
        dict,
    )
]

if not auto_install_examples:
    fail(
        "example manifest must include one prerequisite auto-install policy"
    )

example_auto_install = auto_install_examples[0]["install"]
example_prerequisite_artifact = example_auto_install.get(
    "artifact",
    {},
)

example_prerequisite_hash = str(
    example_prerequisite_artifact.get(
        "sha256",
        "",
    )
).strip().lower()

if not SHA256_PATTERN.fullmatch(
    example_prerequisite_hash
):
    fail(
        "example prerequisite installer SHA-256 must contain exactly 64 lowercase hex characters"
    )

if (
    example_prerequisite_artifact
    .get("signature", {})
    .get("algorithm")
    not in EXPECTED_SIGNATURE_ALGORITHMS
):
    fail(
        "example prerequisite installer must declare Authenticode"
    )

if (
    not example_prerequisite_artifact
    .get("signature", {})
    .get("publisher_subject")
):
    fail(
        "example prerequisite installer publisher_subject is required"
    )

if (
    example_auto_install
    .get("requires_elevation")
    is not True
):
    fail(
        "example prerequisite installer must demonstrate explicit elevation"
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
