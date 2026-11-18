// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_owned_browser_cleanup.rs
// Amac: Bilinen urun icin native messaging HKCU registry sahiplik temizligi
// Modul - Rust Windows Adapter
// Version: 0.1.1
// Aciklama: Yalniz sabit TPM package ID, manifest ve registry sahipligi; script calistirilmaz
// Bagimli Oldugu Katman: Tool | Security | Windows Uninstall

use std::{fs, path::{Path, PathBuf}, process::{Command, Stdio}};
use serde_json::Value;

const PACKAGE_ID: &str = "com.turkuazlabs.passwordmanager";
const HOST_ID: &str = "com.turkuazlabs.passwordmanager";
const CHROME_ID: &str = "mkfdmajphdpgbcpknniljbimgobndpih";
const FIREFOX_ID: &str = "turkuaz-password-manager@turkuazlabs.com";
const MAX_MANIFEST_BYTES: u64 = 32 * 1024;

const KEYS: [&str; 3] = [
    r"HKCU\Software\Google\Chrome\NativeMessagingHosts\com.turkuazlabs.passwordmanager",
    r"HKCU\Software\Microsoft\Edge\NativeMessagingHosts\com.turkuazlabs.passwordmanager",
    r"HKCU\Software\Mozilla\NativeMessagingHosts\com.turkuazlabs.passwordmanager",
];

#[derive(Clone)]
pub(super) struct VerifiedHost {
    registry_entries: Vec<(String, PathBuf)>,
    manifest_paths: Vec<PathBuf>,
    installed_host: PathBuf,
}

fn same_path(left: &Path, right: &Path) -> bool {
    left.to_string_lossy().eq_ignore_ascii_case(&right.to_string_lossy())
}

fn reg(args: &[&str]) -> Result<std::process::Output, String> {
    use std::os::windows::process::CommandExt;
    const CREATE_NO_WINDOW: u32 = 0x0800_0000;
    // Resolve from the operating system root, not PATH or the package CWD.
    let system_root = std::env::var_os("SystemRoot")
        .filter(|value| !value.is_empty())
        .ok_or("Windows SystemRoot is unavailable")?;
    let reg_exe = PathBuf::from(system_root).join("System32").join("reg.exe");
    Command::new(reg_exe)
        .args(args)
        .creation_flags(CREATE_NO_WINDOW)
        .stdin(Stdio::null())
        .output()
        .map_err(|e| e.to_string())
}

fn registry_default(key: &str) -> Result<Option<String>, String> {
    let queried = reg(&["query", key, "/ve"])?;
    if !queried.status.success() {
        if reg(&["query", key])?.status.success() {
            return Err(format!("Native Messaging key exists without a default value: {key}"));
        }
        return Ok(None);
    }
    let stdout = String::from_utf8_lossy(&queried.stdout);
    for line in stdout.lines() {
        if let Some((_, raw)) = line.split_once("REG_SZ") {
            let value = raw.trim();
            if value.is_empty() {
                return Err(format!("Native Messaging key has an empty value: {key}"));
            }
            return Ok(Some(value.to_owned()));
        }
    }
    Err(format!("Native Messaging registry value is not a readable REG_SZ: {key}"))
}

fn verify_regular_file(path: &Path) -> Result<(), String> {
    let meta = fs::symlink_metadata(path).map_err(|e| e.to_string())?;
    #[cfg(windows)]
    use std::os::windows::fs::MetadataExt;
    if !meta.is_file() || meta.file_type().is_symlink() || meta.file_attributes() & 0x400 != 0
        || meta.len() > MAX_MANIFEST_BYTES {
        return Err("Native messaging manifest is not a bounded regular file".into());
    }
    Ok(())
}

fn verify_manifest(path: &Path, host: &Path, chromium: bool) -> Result<(), String> {
    verify_regular_file(path)?;
    let body = fs::read(path).map_err(|e| e.to_string())?;
    let manifest: Value = serde_json::from_slice(&body).map_err(|e| e.to_string())?;
    if manifest.get("name").and_then(Value::as_str) != Some(HOST_ID)
        || manifest.get("type").and_then(Value::as_str) != Some("stdio")
        || !manifest.get("path").and_then(Value::as_str)
            .is_some_and(|value| same_path(Path::new(value), host))
    {
        return Err("Foreign or modified native messaging manifest".into());
    }
    let (field, expected) = if chromium {
        ("allowed_origins", format!("chrome-extension://{CHROME_ID}/"))
    } else {
        ("allowed_extensions", FIREFOX_ID.to_owned())
    };
    if manifest.get(field).and_then(Value::as_array).is_none_or(|values| {
        values.len() != 1 || values[0].as_str() != Some(expected.as_str())
    }) {
        return Err("Native messaging extension permissions mismatch".into());
    }
    Ok(())
}

/// @dev-id TI-WIN-BROWSER-CLEANUP-001
/// @purpose Prepare a strictly typed integration cleanup after validated
/// installation ownership; do not execute code supplied by the package.
/// @contract Unknown package IDs are no-ops. Existing foreign keys fail closed.
pub(super) fn preflight(package_id: &str, target: &Path) -> Result<Option<VerifiedHost>, String> {
    if package_id != PACKAGE_ID {
        return Ok(None);
    }
    let local_app_data = std::env::var_os("LOCALAPPDATA")
        .filter(|value| !value.is_empty())
        .ok_or("LOCALAPPDATA is unavailable")?;
    let root = PathBuf::from(local_app_data).join("TurkuazPasswordManager").join("native-messaging");
    let chrome = root.join("chrome.json");
    let firefox = root.join("firefox.json");
    let host = target.join("tpm-native-host.exe");
    let expected = [&chrome, &chrome, &firefox];
    let mut entries = Vec::new();

    for (key, manifest) in KEYS.iter().zip(expected) {
        if let Some(registered) = registry_default(key)? {
            if !same_path(Path::new(&registered), manifest) {
                return Err(format!("Another product owns Native Messaging key: {key}"));
            }
            verify_manifest(manifest, &host, !key.contains("Mozilla"))?;
            entries.push(((*key).to_owned(), manifest.clone()));
        }
    }
    // A manifest without a key may still be user-owned. Never delete it.
    let mut paths = Vec::new();
    for manifest in [&chrome, &firefox] {
        if entries.iter().any(|(_, owned)| same_path(owned, manifest)) {
            paths.push(manifest.clone());
        }
    }
    Ok(Some(VerifiedHost {
        registry_entries: entries,
        manifest_paths: paths,
        installed_host: host,
    }))
}

/// @dev-id TI-WIN-BROWSER-CLEANUP-002
/// @purpose Remove only keys revalidated against their original manifest,
/// preserving all unrelated registry entries and password vault data.
pub(super) fn remove_verified(verified: VerifiedHost) -> Vec<String> {
    let mut warnings = Vec::new();
    let mut all_removed = true;
    for (key, manifest) in &verified.registry_entries {
        let valid = registry_default(key).ok().flatten()
            .is_some_and(|value| same_path(Path::new(&value), manifest))
            && verify_manifest(manifest, &verified.installed_host, !key.contains("Mozilla")).is_ok();
        if !valid {
            all_removed = false;
            warnings.push(format!("Native Messaging ownership changed; preserved: {key}"));
            continue;
        }
        match reg(&["delete", key, "/f"]) {
            Ok(result) if result.status.success() => {},
            _ => {
                all_removed = false;
                warnings.push(format!("Native Messaging registry could not be removed: {key}"));
            }
        }
    }
    // Do not remove a manifest while any corresponding registry entry is
    // present; a missing key does not authorize deletion of a user's file.
    if all_removed {
        for manifest in verified.manifest_paths {
            if verify_manifest(&manifest, &verified.installed_host, !manifest.ends_with("firefox.json")).is_ok() {
                if let Err(error) = fs::remove_file(&manifest) {
                    warnings.push(format!("Native Messaging manifest preserved: {error}"));
                }
            }
        }
    }
    warnings
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn unknown_packages_never_require_browser_cleanup() {
        assert!(preflight("com.example.other", Path::new(r"C:\Program Files\Example")).unwrap().is_none());
    }
    #[test]
    fn manifest_path_match_is_case_insensitive_on_windows() {
        assert!(same_path(Path::new(r"C:\Apps\Chrome.json"), Path::new(r"c:\apps\chrome.json")));
    }
}
