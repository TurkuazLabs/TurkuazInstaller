# 📄 Dosya Yolu: /tools/validate_contracts.py
# 📌 Amac: TurkuazInstaller Community manifest, manifest trust, provider ve guvenlik contract invariantlarini statik dogrulamak
# 📌 Modul - Python
# Version: 1.10.0
# Aciklama: Detached trust, private credentials, proxy policy, installed app catalog, update discovery, reboot resume ve Community-Pro boundary kurallarini kontrol eder
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
NETWORK = ROOT / "contracts" / "network.yml"
INSTALLED_APP_CATALOG = ROOT / "contracts" / "installed-app-catalog.yml"
UPDATE_DISCOVERY = ROOT / "contracts" / "update-discovery.yml"
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

credential_store = provider.get(
    "credential_store",
    {},
)

expected_credential_store = {
    "platform": "windows",
    "adapter": "windows_credential_manager",
    "credential_type": "generic",
    "target_format": "TurkuazInstaller/provider/{provider}/{authority}",
    "secret_in_manifest": False,
    "secret_in_log": False,
    "missing_credential": "anonymous",
}

for key, expected_value in expected_credential_store.items():
    if credential_store.get(key) != expected_value:
        fail(
            f"provider credential store invariant mismatch: {key}"
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

    if provider_id in {"github", "gitea"}:
        if authentication.get("optional") is not True:
            fail(
                f"{provider_id}: authentication must remain optional"
            )

        if (
            authentication.get("authority_scope")
            != "explicit_allow_list"
        ):
            fail(
                f"{provider_id}: credential authority scope must use explicit_allow_list"
            )

        if (
            authentication.get("api_authority_required")
            is not True
        ):
            fail(
                f"{provider_id}: API authority must remain in credential scope"
            )

expected_provider_header_schemes = {
    "github": "Bearer",
    "gitea": "token",
}

for provider_id, expected_scheme in (
    expected_provider_header_schemes.items()
):
    scheme = (
        provider
        .get("providers", {})
        .get(provider_id, {})
        .get("authentication", {})
        .get("header_scheme")
    )

    if scheme != expected_scheme:
        fail(
            f"{provider_id}: credential header scheme mismatch"
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

if (
    provider
    .get("providers", {})
    .get("http", {})
    .get("authentication", {})
    .get("private_credential_adapter")
    is not False
):
    fail(
        "generic HTTP private credential adapter must remain disabled in this tranche"
    )

provider_security = provider.get(
    "security",
    {},
)

for key in (
    "credential_in_query_string",
    "credential_in_manifest",
    "credential_in_log",
    "credential_cross_authority",
    "credential_userinfo_origin",
):
    if provider_security.get(key) != "deny":
        fail(
            f"provider credential security invariant must deny: {key}"
        )

network = load_yaml(NETWORK)

if network.get("schema_version") != 1:
    fail(
        "network schema_version must be 1"
    )

network_runtime_config = network.get(
    "runtime_config",
    {},
)

expected_network_runtime = {
    "format": "json",
    "path": "%LOCALAPPDATA%/TurkuazInstaller/config/network.json",
    "missing_config": "system",
    "unknown_property": "deny",
}

for key, expected_value in expected_network_runtime.items():
    if network_runtime_config.get(key) != expected_value:
        fail(
            f"network runtime config invariant mismatch: {key}"
        )

network_modes = network.get(
    "modes",
    {},
)

if set(network_modes.get("allowed", [])) != {
    "system",
    "direct",
    "custom",
}:
    fail(
        "network proxy modes must remain system/direct/custom"
    )

system_proxy = network_modes.get(
    "system",
    {},
)

expected_system_proxy = {
    "use_os_proxy": True,
    "custom_proxy_forbidden": True,
    "default_credentials_optional": True,
}

for key, expected_value in expected_system_proxy.items():
    if system_proxy.get(key) != expected_value:
        fail(
            f"system proxy invariant mismatch: {key}"
        )

direct_proxy = network_modes.get(
    "direct",
    {},
)

expected_direct_proxy = {
    "use_proxy": False,
    "custom_proxy_forbidden": True,
    "default_credentials_forbidden": True,
}

for key, expected_value in expected_direct_proxy.items():
    if direct_proxy.get(key) != expected_value:
        fail(
            f"direct proxy invariant mismatch: {key}"
        )

custom_proxy = network_modes.get(
    "custom",
    {},
)

if set(custom_proxy.get("allowed_schemes", [])) != {
    "http",
}:
    fail(
        "custom proxy scheme must remain HTTP in this tranche"
    )

for key in (
    "proxy_uri_required",
    "userinfo_forbidden",
    "path_forbidden",
    "query_forbidden",
    "fragment_forbidden",
    "bypass_local_optional",
    "default_credentials_optional",
):
    if custom_proxy.get(key) is not True:
        fail(
            f"custom proxy invariant missing: {key}"
        )

if set(network.get("consumers", [])) != {
    "bootstrap_self_update",
    "winui_manifest_and_artifact",
    "cli_manifest_and_artifact",
}:
    fail(
        "network proxy consumers drifted"
    )

network_security = network.get(
    "security",
    {},
)

for key in (
    "proxy_username_in_config",
    "proxy_password_in_config",
    "proxy_token_in_config",
    "proxy_uri_userinfo",
    "malformed_config",
    "unsupported_mode",
    "unsupported_proxy_scheme",
):
    if network_security.get(key) != "deny":
        fail(
            f"network proxy security invariant must deny: {key}"
        )

installed_app_catalog = load_yaml(
    INSTALLED_APP_CATALOG
)

if installed_app_catalog.get("schema_version") != 1:
    fail(
        "installed app catalog schema_version must be 1"
    )

catalog_source = installed_app_catalog.get(
    "source",
    {},
)

if catalog_source.get("repository") != "install_state":
    fail(
        "installed app catalog must use install_state repository"
    )

if catalog_source.get("committed_state_only") is not True:
    fail(
        "installed app catalog must use committed state only"
    )

if catalog_source.get("direct_view_filesystem_access") is not False:
    fail(
        "installed app catalog View must not read filesystem directly"
    )

catalog_list = installed_app_catalog.get(
    "list",
    {},
)

if catalog_list.get("sort_by") != "package_id":
    fail(
        "installed app catalog must sort by package_id"
    )

if catalog_list.get("sort_order") != "ascending":
    fail(
        "installed app catalog sort order must remain ascending"
    )

if set(catalog_list.get("fields", [])) != {
    "package_id",
    "version",
    "channel",
    "target_path",
}:
    fail(
        "installed app catalog fields drifted"
    )

if catalog_list.get("read_only") is not True:
    fail(
        "installed app catalog must remain read-only"
    )

catalog_refresh = installed_app_catalog.get(
    "refresh",
    {},
)

for key in (
    "startup",
    "manual",
    "after_successful_mutation",
    "after_successful_resume",
):
    if catalog_refresh.get(key) is not True:
        fail(
            f"installed app catalog refresh invariant missing: {key}"
        )

if catalog_refresh.get("while_operation_busy") != "deny":
    fail(
        "installed app catalog refresh while operation busy must be denied"
    )

catalog_error_boundary = installed_app_catalog.get(
    "error_boundary",
    {},
)

if (
    catalog_error_boundary
    .get("catalog_error_separate_from_operation_error")
    is not True
):
    fail(
        "catalog error must remain separate from installer operation error"
    )

if (
    catalog_error_boundary
    .get("catalog_failure_marks_installer_operation_failed")
    is not False
):
    fail(
        "catalog failure must not mark installer operation failed"
    )

if catalog_error_boundary.get("malformed_state") != "reject":
    fail(
        "malformed install state must be rejected"
    )

catalog_security = installed_app_catalog.get(
    "security",
    {},
)

for key in (
    "infer_manifest_source_from_state",
    "mutate_state_from_catalog_view",
    "bypass_repository",
):
    if catalog_security.get(key) != "deny":
        fail(
            f"installed app catalog security invariant must deny: {key}"
        )

update_discovery = load_yaml(
    UPDATE_DISCOVERY
)

if update_discovery.get("schema_version") != 1:
    fail(
        "update discovery schema_version must be 1"
    )

if set(
    update_discovery
    .get("inputs", {})
    .get("required", [])
) != {
    "package_id",
    "channel",
    "manifest_source",
}:
    fail(
        "update discovery inputs drifted"
    )

update_sources = update_discovery.get(
    "sources",
    {},
)

release_source = update_sources.get(
    "release",
    {},
)

expected_release_source = {
    "provider": "signed_manifest",
    "detached_signature_required": True,
    "external_trust_policy_required": True,
}

for key, expected_value in expected_release_source.items():
    if release_source.get(key) != expected_value:
        fail(
            f"update discovery release source invariant mismatch: {key}"
        )

installed_source = update_sources.get(
    "installed",
    {},
)

if installed_source.get("repository") != "install_state":
    fail(
        "update discovery must read installed state from install_state repository"
    )

if installed_source.get("committed_state_only") is not True:
    fail(
        "update discovery must use committed installed state only"
    )

if set(
    update_discovery
    .get("availability", {})
    .get("allowed", [])
) != {
    "release_not_found",
    "current",
    "available",
}:
    fail(
        "update discovery availability values drifted"
    )

if set(
    update_discovery
    .get("result", {})
    .get("fields", [])
) != {
    "availability",
    "installed_version",
    "latest_version",
}:
    fail(
        "update discovery result fields drifted"
    )

update_ux = update_discovery.get(
    "ux",
    {},
)

for key in (
    "manual_check",
    "reset_on_package_change",
    "reset_on_channel_change",
    "reset_on_manifest_source_change",
    "separate_error_state",
    "not_installed_is_not_update_available",
):
    if update_ux.get(key) is not True:
        fail(
            f"update discovery UX invariant missing: {key}"
        )

if update_ux.get("concurrent_installer_mutation") != "deny":
    fail(
        "update discovery must deny concurrent installer mutation"
    )

update_read_only = update_discovery.get(
    "read_only",
    {},
)

for key in (
    "artifact_download",
    "package_stage",
    "package_apply",
    "install_state_write",
    "operation_journal_write",
    "resume_request_write",
    "background_update_start",
):
    if update_read_only.get(key) is not False:
        fail(
            f"update discovery must remain read-only: {key}"
        )

update_security = update_discovery.get(
    "security",
    {},
)

for key in (
    "unsigned_manifest",
    "untrusted_manifest_signer",
    "direct_state_mutation",
    "mutation_from_discovery_result",
):
    if update_security.get(key) != "deny":
        fail(
            f"update discovery security invariant must deny: {key}"
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
