// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_owned_browser_cleanup.rs
// Amac: Bilinen urun icin native messaging HKCU registry sahiplik temizligi
// Modul - Rust Windows Adapter
// Version: 0.1.7
// Aciklama: Yalniz sabit TPM package ID, manifest ve registry sahipligi; script calistirilmaz
// Bagimli Oldugu Katman: Tool | Security | Windows Uninstall

use serde_json::Value;
use std::{
    ffi::OsStr,
    fs,
    os::windows::ffi::OsStrExt,
    path::{Path, PathBuf},
};
use windows::{
    Win32::{
        Foundation::{ERROR_FILE_NOT_FOUND, ERROR_PATH_NOT_FOUND, ERROR_SUCCESS},
        System::Registry::{
            HKEY, HKEY_CURRENT_USER, KEY_QUERY_VALUE, REG_SZ, RegCloseKey, RegDeleteKeyW,
            RegOpenKeyExW, RegQueryValueExW,
        },
    },
    core::PCWSTR,
};

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
    left.to_string_lossy()
        .eq_ignore_ascii_case(&right.to_string_lossy())
}

fn wide(value: &OsStr) -> Vec<u16> {
    value.encode_wide().chain(std::iter::once(0)).collect()
}

struct OpenRegistryKey(HKEY);
impl Drop for OpenRegistryKey {
    fn drop(&mut self) {
        unsafe { RegCloseKey(self.0) };
    }
}

fn hkcu_path(key: &str) -> Result<&str, String> {
    key.strip_prefix("HKCU\\")
        .ok_or_else(|| "Only HKCU native messaging entries are allowed".to_owned())
}

fn open_registry_key(key: &str) -> Result<Option<OpenRegistryKey>, String> {
    let path = wide(OsStr::new(hkcu_path(key)?));
    let mut handle = HKEY::default();
    let status = unsafe {
        RegOpenKeyExW(
            HKEY_CURRENT_USER,
            PCWSTR(path.as_ptr()),
            None,
            KEY_QUERY_VALUE,
            &mut handle,
        )
    };
    if status == ERROR_FILE_NOT_FOUND || status == ERROR_PATH_NOT_FOUND {
        return Ok(None);
    }
    if status != ERROR_SUCCESS {
        return Err(format!(
            "Could not read Native Messaging registry key: {key} ({})",
            status.0
        ));
    }
    Ok(Some(OpenRegistryKey(handle)))
}

fn registry_default(key: &str) -> Result<Option<String>, String> {
    let Some(handle) = open_registry_key(key)? else {
        return Ok(None);
    };
    let mut value_type = REG_SZ;
    let mut needed = 0_u32;
    let status = unsafe {
        RegQueryValueExW(
            handle.0,
            PCWSTR::null(),
            None,
            Some(&mut value_type),
            None,
            Some(&mut needed),
        )
    };
    if status != ERROR_SUCCESS
        || value_type != REG_SZ
        || needed < 2
        || needed > 32_768
        || needed % 2 != 0
    {
        return Err(format!(
            "Native Messaging default is missing or invalid: {key}"
        ));
    }
    let mut bytes = vec![0_u8; needed as usize];
    let read = unsafe {
        RegQueryValueExW(
            handle.0,
            PCWSTR::null(),
            None,
            Some(&mut value_type),
            Some(bytes.as_mut_ptr()),
            Some(&mut needed),
        )
    };
    if read != ERROR_SUCCESS
        || value_type != REG_SZ
        || needed as usize > bytes.len()
        || needed % 2 != 0
    {
        return Err(format!("Native Messaging registry value changed: {key}"));
    }
    decode_registry_utf16(&bytes[..needed as usize])
        .map(Some)
        .map_err(|error| format!("Invalid Native Messaging registry value for {key}: {error}"))
}

fn decode_registry_utf16(bytes: &[u8]) -> Result<String, &'static str> {
    if bytes.len() < 2 || bytes.len() % 2 != 0 {
        return Err("Invalid UTF-16 byte count");
    }
    let units = bytes
        .chunks_exact(2)
        .map(|pair| u16::from_le_bytes([pair[0], pair[1]]))
        .collect::<Vec<_>>();
    let Some(end) = units.iter().position(|unit| *unit == 0) else {
        return Err("Missing UTF-16 terminator");
    };
    if end == 0 || units[end..].iter().any(|unit| *unit != 0) {
        return Err("Empty path or unexpected data after terminator");
    }
    String::from_utf16(&units[..end]).map_err(|_| "Malformed UTF-16")
}

fn delete_owned_key(key: &str) -> Result<(), String> {
    let path = wide(OsStr::new(hkcu_path(key)?));
    let status = unsafe { RegDeleteKeyW(HKEY_CURRENT_USER, PCWSTR(path.as_ptr())) };
    if status != ERROR_SUCCESS {
        return Err(format!(
            "Native Messaging key could not be deleted: {key} ({})",
            status.0
        ));
    }
    Ok(())
}

fn verify_manifest_directory(root: &Path) -> Result<(), String> {
    // Never traverse a user-controlled junction when deleting a manifest.
    // Missing folders are safe only when no existing registry entry refers
    // to them, which the manifest preflight handles separately.
    let metadata = match fs::symlink_metadata(root) {
        Ok(metadata) => metadata,
        Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(()),
        Err(error) => return Err(error.to_string()),
    };
    use std::os::windows::fs::MetadataExt;
    if !metadata.is_dir()
        || metadata.file_type().is_symlink()
        || metadata.file_attributes() & 0x400 != 0
    {
        return Err(
            "Native Messaging manifest root is a reparse point or is not a directory".into(),
        );
    }
    Ok(())
}

fn verify_regular_file(path: &Path) -> Result<(), String> {
    let meta = fs::symlink_metadata(path).map_err(|e| e.to_string())?;
    #[cfg(windows)]
    use std::os::windows::fs::MetadataExt;
    if !meta.is_file()
        || meta.file_type().is_symlink()
        || meta.file_attributes() & 0x400 != 0
        || meta.len() > MAX_MANIFEST_BYTES
    {
        return Err("Native messaging manifest is not a bounded regular file".into());
    }
    Ok(())
}

fn validate_manifest_value(manifest: &Value, host: &Path, chromium: bool) -> Result<(), String> {
    let object = manifest
        .as_object()
        .ok_or("Native Messaging manifest must be a JSON object")?;
    const COMMON_FIELDS: [&str; 4] = ["name", "description", "path", "type"];
    let permission_field = if chromium {
        "allowed_origins"
    } else {
        "allowed_extensions"
    };
    if object.len() != COMMON_FIELDS.len() + 1
        || !object
            .keys()
            .all(|key| COMMON_FIELDS.contains(&key.as_str()) || key == permission_field)
    {
        return Err("Native Messaging manifest contains unexpected fields".into());
    }
    if manifest.get("name").and_then(Value::as_str) != Some(HOST_ID)
        || manifest.get("type").and_then(Value::as_str) != Some("stdio")
        || manifest.get("description").and_then(Value::as_str)
            != Some("Turkuaz Password Manager Native Messaging Host")
        || !manifest
            .get("path")
            .and_then(Value::as_str)
            .is_some_and(|value| same_path(Path::new(value), host))
    {
        return Err("Foreign or modified native messaging manifest".into());
    }
    let expected = if chromium {
        format!("chrome-extension://{CHROME_ID}/")
    } else {
        FIREFOX_ID.to_owned()
    };
    if manifest
        .get(permission_field)
        .and_then(Value::as_array)
        .is_none_or(|values| values.len() != 1 || values[0].as_str() != Some(expected.as_str()))
    {
        return Err("Native messaging extension permissions mismatch".into());
    }
    Ok(())
}

fn verify_manifest(path: &Path, host: &Path, chromium: bool) -> Result<(), String> {
    verify_regular_file(path)?;
    let body = fs::read(path).map_err(|e| e.to_string())?;
    let manifest: Value = serde_json::from_slice(&body).map_err(|e| e.to_string())?;
    validate_manifest_value(&manifest, host, chromium)
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
    let product_root = PathBuf::from(local_app_data).join("TurkuazPasswordManager");
    verify_manifest_directory(&product_root)?;
    let root = product_root.join("native-messaging");
    verify_manifest_directory(&root)?;
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

/// Check every browser owner, not only the keys observed during preflight.
/// Missing keys are safe; unreadable or newly registered keys prevent cleanup.
fn manifest_is_unreferenced<F>(manifest: &Path, mut lookup: F) -> Result<bool, String>
where
    F: FnMut(&str) -> Result<Option<String>, String>,
{
    for key in KEYS {
        if let Some(value) = lookup(key)? {
            if same_path(Path::new(&value), manifest) {
                return Ok(false);
            }
        }
    }
    Ok(true)
}

/// @dev-id TI-WIN-BROWSER-CLEANUP-002
/// @purpose Remove only keys revalidated against their original manifest,
/// preserving all unrelated registry entries and password vault data.
pub(super) fn remove_verified(verified: VerifiedHost) -> Vec<String> {
    let mut warnings = Vec::new();
    let mut all_removed = true;
    for (key, manifest) in &verified.registry_entries {
        let valid = registry_default(key)
            .ok()
            .flatten()
            .is_some_and(|value| same_path(Path::new(&value), manifest))
            && verify_manifest(manifest, &verified.installed_host, !key.contains("Mozilla"))
                .is_ok();
        if !valid {
            all_removed = false;
            warnings.push(format!(
                "Native Messaging ownership changed; preserved: {key}"
            ));
            continue;
        }
        if let Err(error) = delete_owned_key(key) {
            all_removed = false;
            warnings.push(error);
        }
    }
    // Recheck ALL three browsers immediately before deleting any manifest.
    // In particular, a second process might register a key during uninstall.
    // User-controlled manifest directories must still be ordinary directories.
    if all_removed {
        for manifest in verified.manifest_paths {
            let safe_root = manifest.parent().is_some_and(|folder| {
                verify_manifest_directory(folder).is_ok()
                    && folder
                        .parent()
                        .is_some_and(|parent| verify_manifest_directory(parent).is_ok())
            });
            if !safe_root {
                warnings.push(format!(
                    "Native Messaging directory changed; preserved: {}",
                    manifest.display()
                ));
                continue;
            }
            match manifest_is_unreferenced(&manifest, registry_default) {
                Ok(true) => {}
                Ok(false) => {
                    warnings.push(format!(
                        "Native Messaging manifest is still registered; preserved: {}",
                        manifest.display()
                    ));
                    continue;
                }
                Err(error) => {
                    warnings.push(format!(
                        "Cannot confirm Native Messaging ownership; preserved: {error}"
                    ));
                    continue;
                }
            }
            if verify_manifest(
                &manifest,
                &verified.installed_host,
                !manifest.ends_with("firefox.json"),
            )
            .is_ok()
            {
                if let Err(error) = fs::remove_file(&manifest) {
                    warnings.push(format!("Native Messaging manifest preserved: {error}"));
                }
            } else {
                warnings.push(format!(
                    "Native Messaging manifest changed; preserved: {}",
                    manifest.display()
                ));
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
        assert!(
            preflight("com.example.other", Path::new(r"C:\Program Files\Example"))
                .unwrap()
                .is_none()
        );
    }
    #[test]
    fn cleanup_registry_paths_reject_non_hkcu_roots() {
        assert_eq!(
            hkcu_path(KEYS[0]).unwrap(),
            r"Software\Google\Chrome\NativeMessagingHosts\com.turkuazlabs.passwordmanager"
        );
        assert!(hkcu_path(r"HKLM\Software\Microsoft").is_err());
        assert!(hkcu_path(r"Software\Google").is_err());
    }
    #[test]
    fn registry_utf16_decoder_accepts_unicode_install_path() {
        let path = r"C:\Users\Çağrı\AppData\Local\Türkçe\chrome.json";
        let bytes = path
            .encode_utf16()
            .chain(std::iter::once(0))
            .flat_map(u16::to_le_bytes)
            .collect::<Vec<u8>>();
        assert_eq!(decode_registry_utf16(&bytes).unwrap(), path);
    }

    #[test]
    fn registry_utf16_decoder_rejects_embedded_nul_and_odd_bytes() {
        assert!(decode_registry_utf16(&[0x41]).is_err());
        assert!(decode_registry_utf16(&[0x41, 0, 0, 0, 0x42, 0, 0, 0]).is_err());
        assert!(decode_registry_utf16(&[0x41, 0]).is_err());
    }

    #[test]
    fn manifest_rejects_extra_extension_permissions() {
        let host = Path::new(r"C:\TurkuazPM\tpm-native-host.exe");
        let mut manifest = serde_json::json!({
            "name": HOST_ID,
            "description": "Turkuaz Password Manager Native Messaging Host",
            "path": host.to_string_lossy(),
            "type": "stdio",
            "allowed_origins": [format!("chrome-extension://{CHROME_ID}/")]
        });
        assert!(validate_manifest_value(&manifest, host, true).is_ok());
        manifest["allowed_extensions"] = serde_json::json!(["foreign@extension"]);
        assert!(validate_manifest_value(&manifest, host, true).is_err());
    }

    #[test]
    fn manifest_rejects_unknown_fields_and_modified_host_path() {
        let host = Path::new(r"C:\TurkuazPM\tpm-native-host.exe");
        let mut manifest = serde_json::json!({
            "name": HOST_ID,
            "description": "Turkuaz Password Manager Native Messaging Host",
            "path": host.to_string_lossy(),
            "type": "stdio",
            "allowed_extensions": [FIREFOX_ID]
        });
        assert!(validate_manifest_value(&manifest, host, false).is_ok());
        manifest["path"] = serde_json::json!(r"C:\Other\tpm-native-host.exe");
        assert!(validate_manifest_value(&manifest, host, false).is_err());
    }

    #[test]
    fn newly_registered_browser_key_blocks_manifest_deletion() {
        let manifest = Path::new(r"C:\Users\Test\chrome.json");
        let mut checked = 0;
        let can_delete = manifest_is_unreferenced(manifest, |key| {
            checked += 1;
            Ok((key == KEYS[1]).then(|| r"c:\users\test\chrome.json".to_owned()))
        })
        .unwrap();
        assert!(!can_delete);
        assert_eq!(checked, 2);
    }

    #[test]
    fn unreadable_registry_prevents_manifest_deletion() {
        let manifest = Path::new(r"C:\Users\Test\chrome.json");
        let decision = manifest_is_unreferenced(manifest, |_key| Err("denied".to_owned()));
        assert!(decision.is_err());
    }

    #[test]
    fn manifest_deletion_requires_all_browser_keys_absent() {
        let manifest = Path::new(r"C:\Users\Test\chrome.json");
        let mut checked = 0;
        assert!(
            manifest_is_unreferenced(manifest, |_key| {
                checked += 1;
                Ok(None)
            })
            .unwrap()
        );
        assert_eq!(checked, KEYS.len());
    }

    #[test]
    fn manifest_path_match_is_case_insensitive_on_windows() {
        assert!(same_path(
            Path::new(r"C:\Apps\Chrome.json"),
            Path::new(r"c:\apps\chrome.json")
        ));
    }
}
