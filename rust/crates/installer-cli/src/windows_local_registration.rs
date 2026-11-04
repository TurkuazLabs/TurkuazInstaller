// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_local_registration.rs
// Amac: Imzasiz yerel kurulum icin Windows uygulama kaydi ve sahiplik dosyasi
// Modul - Rust Windows Lifecycle Adapter
// Version: 0.1.7
// Aciklama: HKCU/HKLM Uninstall, harici kaldirici, degismeyen kisayollari koruma
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    fs::{self, File, OpenOptions},
    io::{self, Read, Write},
    os::windows::ffi::OsStrExt,
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use windows::{
    Win32::{
        Foundation::ERROR_SUCCESS,
        Storage::FileSystem::{MOVEFILE_DELAY_UNTIL_REBOOT, MoveFileExW},
        System::Registry::{
            HKEY, HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_QUERY_VALUE, KEY_SET_VALUE,
            REG_CREATE_KEY_DISPOSITION, REG_CREATED_NEW_KEY, REG_DWORD, REG_OPTION_NON_VOLATILE,
            REG_SZ, RegCloseKey, RegCreateKeyExW, RegDeleteKeyW, RegOpenKeyExW, RegQueryValueExW,
            RegSetValueExW,
        },
        UI::Shell::{
            FOLDERID_CommonPrograms, FOLDERID_LocalAppData, FOLDERID_ProgramFiles,
            FOLDERID_Programs,
        },
    },
    core::PCWSTR,
};

use crate::{
    windows_install_scope::{InstallScope, current_process_is_elevated, known_folder},
    windows_shortcuts::{create_owned_uninstaller_link, shortcut_location_is_owned},
};

const UNINSTALL_ROOT: &str = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
const MARKER: &str = "TurkuazOwner";
const MAX_JSON_BYTES: u64 = 256 * 1024;
const MAX_EXECUTABLE_BYTES: u64 = 512 * 1024 * 1024;

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
enum RegisteredScope {
    CurrentUser,
    AllUsers,
}

impl From<InstallScope> for RegisteredScope {
    fn from(scope: InstallScope) -> Self {
        match scope {
            InstallScope::CurrentUser => Self::CurrentUser,
            InstallScope::AllUsers => Self::AllUsers,
        }
    }
}

impl RegisteredScope {
    fn runtime(&self) -> InstallScope {
        match self {
            Self::CurrentUser => InstallScope::CurrentUser,
            Self::AllUsers => InstallScope::AllUsers,
        }
    }
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields, rename_all = "camelCase")]
struct OwnedShortcut {
    path: PathBuf,
    sha256: String,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields, rename_all = "camelCase")]
pub(super) struct LocalRegistration {
    version: u32,
    package_id: String,
    display_version: String,
    target: PathBuf,
    scope: RegisteredScope,
    key: String,
    marker: String,
    uninstaller: PathBuf,
    shortcuts: Vec<OwnedShortcut>,
}

impl LocalRegistration {
    pub(super) fn package_id(&self) -> &str {
        &self.package_id
    }
    pub(super) fn target(&self) -> &Path {
        &self.target
    }
    pub(super) fn scope(&self) -> InstallScope {
        self.scope.runtime()
    }
    pub(super) fn display_version(&self) -> &str {
        &self.display_version
    }
}

fn wide(s: &OsStr) -> Vec<u16> {
    s.encode_wide().chain(std::iter::once(0)).collect()
}

fn failure(action: &str, code: windows::Win32::Foundation::WIN32_ERROR) -> String {
    format!("{action}: Windows hata kodu {}", code.0)
}

struct RegistryKey(HKEY);
impl Drop for RegistryKey {
    fn drop(&mut self) {
        unsafe { RegCloseKey(self.0) };
    }
}

fn hive(scope: InstallScope) -> HKEY {
    match scope {
        InstallScope::CurrentUser => HKEY_CURRENT_USER,
        InstallScope::AllUsers => HKEY_LOCAL_MACHINE,
    }
}

fn key_path(name: &str) -> String {
    format!("{UNINSTALL_ROOT}\\{name}")
}

fn valid_id(package_id: &str) -> bool {
    !package_id.is_empty()
        && package_id.len() <= 120
        && !package_id.starts_with('.')
        && !package_id.ends_with('.')
        && !package_id.contains("..")
        && package_id
            .bytes()
            .all(|b| b.is_ascii_alphanumeric() || matches!(b, b'.' | b'-' | b'_'))
}

fn hash_bytes(bytes: &[u8]) -> String {
    Sha256::digest(bytes)
        .iter()
        .map(|byte| format!("{byte:02x}"))
        .collect()
}

fn digest_file(path: &Path) -> Result<String, String> {
    let meta = fs::symlink_metadata(path).map_err(|e| e.to_string())?;
    use std::os::windows::fs::MetadataExt;
    if !meta.is_file()
        || meta.file_type().is_symlink()
        || meta.file_attributes() & 0x400 != 0
        || meta.len() > MAX_EXECUTABLE_BYTES
    {
        return Err("Dosya normal veya izin verilen boyutta değil.".into());
    }
    let mut file = File::open(path).map_err(|e| e.to_string())?;
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; 64 * 1024];
    loop {
        let count = file.read(&mut buffer).map_err(|e| e.to_string())?;
        if count == 0 {
            break;
        }
        hasher.update(&buffer[..count]);
    }
    Ok(hasher
        .finalize()
        .iter()
        .map(|byte| format!("{byte:02x}"))
        .collect())
}

fn registration_name(package_id: &str, target: &Path) -> Result<String, String> {
    if !valid_id(package_id) {
        return Err("Paket kimliği Windows kayıt defteri için güvenli değil.".into());
    }
    let target_string = target.to_str().ok_or("Kurulum hedefi Unicode olmalıdır.")?;
    let short_hash = hash_bytes(target_string.to_ascii_lowercase().as_bytes());
    Ok(format!(
        "TurkuazInstaller.{package_id}.{}",
        &short_hash[..20]
    ))
}

fn owned_root(scope: InstallScope) -> Result<PathBuf, String> {
    let system_root = match scope {
        InstallScope::CurrentUser => known_folder(&FOLDERID_LocalAppData)?,
        // Use the administrator-protected Program Files root. ProgramData
        // subdirectories are commonly creatable by standard users and must
        // not become an elevated executable planting location.
        InstallScope::AllUsers => known_folder(&FOLDERID_ProgramFiles)?,
    };
    Ok(system_root.join("TurkuazInstaller").join("Uninstallers"))
}

fn ensure_managed_directory(root: &Path) -> Result<(), String> {
    fs::create_dir_all(root).map_err(|e| format!("Kaldırıcı dizini oluşturulamadı: {e}"))?;
    let meta = fs::symlink_metadata(root).map_err(|e| e.to_string())?;
    #[cfg(windows)]
    use std::os::windows::fs::MetadataExt;
    if !meta.is_dir() || meta.file_type().is_symlink() || meta.file_attributes() & 0x400 != 0 {
        return Err("Kaldırıcı klasörü bağlantı/junction içeriyor.".into());
    }
    Ok(())
}

fn read_value(key: HKEY, name: &str) -> Result<String, String> {
    let name_w = wide(OsStr::new(name));
    let mut value_type = REG_SZ;
    let mut needed = 0_u32;
    let first = unsafe {
        RegQueryValueExW(
            key,
            PCWSTR(name_w.as_ptr()),
            None,
            Some(&mut value_type),
            None,
            Some(&mut needed),
        )
    };
    if first != ERROR_SUCCESS || value_type != REG_SZ || needed > 16_384 || needed == 0 {
        return Err(format!("Windows kayıt değeri okunamadı: {name}"));
    }
    let mut bytes = vec![0_u8; needed as usize];
    let second = unsafe {
        RegQueryValueExW(
            key,
            PCWSTR(name_w.as_ptr()),
            None,
            Some(&mut value_type),
            Some(bytes.as_mut_ptr()),
            Some(&mut needed),
        )
    };
    if second != ERROR_SUCCESS
        || needed as usize > bytes.len()
        || needed % 2 != 0
        || value_type != REG_SZ
    {
        return Err(format!("Windows kayıt değeri değişti: {name}"));
    }
    let words = bytes[..needed as usize]
        .chunks_exact(2)
        .map(|chunk| u16::from_le_bytes([chunk[0], chunk[1]]))
        .take_while(|word| *word != 0)
        .collect::<Vec<_>>();
    String::from_utf16(&words).map_err(|e| e.to_string())
}

fn write_string(key: HKEY, name: &str, value: &str) -> Result<(), String> {
    let name_w = wide(OsStr::new(name));
    let wide_value = wide(OsStr::new(value));
    let bytes = wide_value
        .iter()
        .flat_map(|unit| unit.to_le_bytes())
        .collect::<Vec<u8>>();
    let status =
        unsafe { RegSetValueExW(key, PCWSTR(name_w.as_ptr()), None, REG_SZ, Some(&bytes)) };
    if status != ERROR_SUCCESS {
        return Err(failure("Windows uygulama kaydı yazılamadı", status));
    }
    Ok(())
}

fn write_u32(key: HKEY, name: &str, value: u32) -> Result<(), String> {
    let name_w = wide(OsStr::new(name));
    let value_bytes = value.to_le_bytes();
    let status = unsafe {
        RegSetValueExW(
            key,
            PCWSTR(name_w.as_ptr()),
            None,
            REG_DWORD,
            Some(&value_bytes),
        )
    };
    if status != ERROR_SUCCESS {
        return Err(failure("Windows uygulama bayrağı yazılamadı", status));
    }
    Ok(())
}

fn open_key(scope: InstallScope, name: &str) -> Result<RegistryKey, String> {
    let mut output = HKEY::default();
    let path = wide(OsStr::new(&key_path(name)));
    let status = unsafe {
        RegOpenKeyExW(
            hive(scope),
            PCWSTR(path.as_ptr()),
            None,
            KEY_QUERY_VALUE,
            &mut output,
        )
    };
    if status != ERROR_SUCCESS {
        return Err(failure("Windows uygulama kaydı bulunamadı", status));
    }
    Ok(RegistryKey(output))
}

fn create_key(scope: InstallScope, name: &str) -> Result<RegistryKey, String> {
    let mut output = HKEY::default();
    let mut disposition = REG_CREATE_KEY_DISPOSITION::default();
    let path = wide(OsStr::new(&key_path(name)));
    let status = unsafe {
        RegCreateKeyExW(
            hive(scope),
            PCWSTR(path.as_ptr()),
            None,
            PCWSTR::null(),
            REG_OPTION_NON_VOLATILE,
            KEY_SET_VALUE | KEY_QUERY_VALUE,
            None,
            &mut output,
            Some(&mut disposition),
        )
    };
    if status != ERROR_SUCCESS {
        return Err(failure("Windows uygulama kaydı açılamadı", status));
    }
    let key = RegistryKey(output);
    if disposition != REG_CREATED_NEW_KEY {
        return Err("Windows uygulama kaydı zaten mevcut, üzerine yazılmadı.".into());
    }
    Ok(key)
}

fn uninstall_command(record: &LocalRegistration, manifest: &Path) -> String {
    format!(
        "\"{}\" uninstall-local-gui --registration \"{}\"",
        record.uninstaller.display(),
        manifest.display()
    )
}

fn remove_key(record: &LocalRegistration) -> Result<(), String> {
    // An unexpected key or a user-modified InstallLocation is never deleted.
    let key = open_key(record.scope(), &record.key)?;
    if read_value(key.0, MARKER)? != record.marker
        || read_value(key.0, "InstallLocation")? != record.target.to_string_lossy()
        || read_value(key.0, "UninstallString")?
            != uninstall_command(record, &registration_path(record)?)
    {
        return Err("Windows uygulama kaydı artık bu kurulumla eşleşmiyor.".into());
    }
    drop(key);
    let path = wide(OsStr::new(&key_path(&record.key)));
    let status = unsafe { RegDeleteKeyW(hive(record.scope()), PCWSTR(path.as_ptr())) };
    if status != ERROR_SUCCESS {
        return Err(failure("Windows uygulama kaydı kaldırılamadı", status));
    }
    Ok(())
}

fn registration_path(record: &LocalRegistration) -> Result<PathBuf, String> {
    let root = owned_root(record.scope())?;
    Ok(root.join(format!("{}.json", record.key)))
}

pub(super) fn read_registration(source: &Path) -> Result<LocalRegistration, String> {
    let meta = fs::symlink_metadata(source).map_err(|e| e.to_string())?;
    if !meta.is_file() || meta.file_type().is_symlink() || meta.len() > MAX_JSON_BYTES {
        return Err("Kaldırma envanter dosyası güvenli değil.".into());
    }
    let mut input = Vec::new();
    File::open(source)
        .map_err(|e| e.to_string())?
        .take(MAX_JSON_BYTES + 1)
        .read_to_end(&mut input)
        .map_err(|e| e.to_string())?;
    if input.len() as u64 > MAX_JSON_BYTES {
        return Err("Kaldırma envanteri aşırı büyük.".into());
    }
    let record: LocalRegistration = serde_json::from_slice(&input).map_err(|e| e.to_string())?;
    if record.version != 1
        || !valid_id(&record.package_id)
        || record.key != registration_name(&record.package_id, &record.target)?
        || record.shortcuts.len() > 8
        || source != registration_path(&record)?
        || !record.uninstaller.is_absolute()
        || record.uninstaller.parent() != source.parent()
        || record.uninstaller.file_name() != Some(OsStr::new(&format!("{}.exe", record.key)))
    {
        return Err("Kaldırma envanteri bu Windows kurulumuna ait değil.".into());
    }
    let key = open_key(record.scope(), &record.key)?;
    if read_value(key.0, MARKER)? != record.marker
        || read_value(key.0, "InstallLocation")? != record.target.to_string_lossy()
    {
        return Err("Windows uygulama kaydı/kurulum eşleşmiyor.".into());
    }
    Ok(record)
}

fn register_key(record: &LocalRegistration, manifest: &Path) -> Result<(), String> {
    let key = create_key(record.scope(), &record.key)?;
    let arguments = uninstall_command(record, manifest);
    let result = (|| {
        write_string(key.0, MARKER, &record.marker)?;
        write_string(key.0, "DisplayName", &record.package_id)?;
        write_string(key.0, "DisplayVersion", &record.display_version)?;
        write_string(key.0, "Publisher", "TurkuazInstaller local package")?;
        write_string(key.0, "InstallLocation", &record.target.to_string_lossy())?;
        write_string(key.0, "UninstallString", &arguments)?;
        write_string(key.0, "DisplayIcon", &record.uninstaller.to_string_lossy())?;
        write_u32(key.0, "NoModify", 1)?;
        write_u32(key.0, "NoRepair", 1)?;
        Ok::<(), String>(())
    })();
    drop(key);
    if result.is_err() {
        let path = wide(OsStr::new(&key_path(&record.key)));
        unsafe { RegDeleteKeyW(hive(record.scope()), PCWSTR(path.as_ptr())) };
    }
    result
}

fn create_copy_no_clobber(source: &Path, destination: &Path) -> Result<(), String> {
    let mut source = File::open(source).map_err(|e| e.to_string())?;
    if source.metadata().map_err(|e| e.to_string())?.len() > MAX_EXECUTABLE_BYTES {
        return Err("Kaldırıcı EXE dosyası boyut sınırını aşıyor.".into());
    }
    let mut dest = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(destination)
        .map_err(|e| format!("Mevcut kaldırıcı üzerine yazılmadı: {e}"))?;
    let result = io::copy(&mut source, &mut dest)
        .and_then(|_| dest.sync_all())
        .map_err(|e| e.to_string());
    drop(dest);
    if result.is_err() {
        let _ = fs::remove_file(destination);
    }
    result
}

/// @dev-id BRK-WIN-UNINSTALL-001
/// @purpose Register Windows Apps entry and an independent persistent
/// uninstaller outside the package root with no overwrite policy.
/// @contract Failed registration never changes the installed payload and
/// reports partial integration.
pub(super) fn register_local_install(
    source_setup: &Path,
    package_id: &str,
    display_version: &str,
    target: &Path,
    scope: InstallScope,
    previously_created_shortcuts: &[PathBuf],
) -> Result<Vec<String>, String> {
    if scope == InstallScope::AllUsers && !current_process_is_elevated() {
        return Err("Tüm kullanıcılar kaldırıcı kaydı yönetici yetkisi gerektirir.".into());
    }
    let name = registration_name(package_id, target)?;
    let root = owned_root(scope)?;
    ensure_managed_directory(&root)?;
    let uninstaller = root.join(format!("{name}.exe"));
    let manifest = root.join(format!("{name}.json"));
    if uninstaller.starts_with(target)
        || target.starts_with(&root)
        || !target.is_absolute()
    {
        return Err("Kaldırıcı çalışma dizini kurulum hedefiyle çakışıyor.".into());
    }
    if fs::symlink_metadata(&manifest).is_ok() || fs::symlink_metadata(&uninstaller).is_ok() {
        return Err("Bu kuruluma ait kaldırıcı kaydı zaten var; mevcut dosyalar korunuyor.".into());
    }
    if open_key(scope, &name).is_ok() {
        return Err("Mevcut Windows uygulama kaydının üzerine yazılmadı.".into());
    }
    create_copy_no_clobber(source_setup, &uninstaller)?;

    let mut warnings = Vec::new();
    let mut shortcuts = Vec::new();
    let mut uninstall_link = None;
    for path in previously_created_shortcuts {
        if shortcut_location_is_owned(path, scope, package_id, false)? {
            shortcuts.push(OwnedShortcut {
                path: path.clone(),
                sha256: digest_file(path)?,
            });
        }
    }
    let programs = match scope {
        InstallScope::CurrentUser => &FOLDERID_Programs,
        InstallScope::AllUsers => &FOLDERID_CommonPrograms,
    };
    let group = known_folder(programs)?.join(package_id);
    let expected_application_link = group.join(format!("{package_id}.lnk"));
    let owns_start_group = previously_created_shortcuts
        .iter()
        .any(|path| path == &expected_application_link);
    match create_owned_uninstaller_link(
        &uninstaller,
        &manifest,
        package_id,
        scope,
        owns_start_group,
    ) {
        Ok(path) => {
            let digest = digest_file(&path)?;
            uninstall_link = Some((path.clone(), digest.clone()));
            shortcuts.push(OwnedShortcut {
                sha256: digest,
                path,
            });
        }
        Err(error) => warnings.push(format!("Kaldır kısayolu oluşturulamadı: {error}")),
    }
    let marker = hash_bytes(
        format!(
            "{}\n{}\n{}",
            package_id,
            target.display(),
            uninstaller.display()
        )
        .as_bytes(),
    );
    let record = LocalRegistration {
        version: 1,
        package_id: package_id.to_owned(),
        display_version: display_version.to_owned(),
        target: target.to_path_buf(),
        scope: scope.into(),
        key: name,
        marker,
        uninstaller,
        shortcuts,
    };
    let manifest_data = serde_json::to_vec(&record).map_err(|e| e.to_string())?;
    if manifest_data.len() as u64 > MAX_JSON_BYTES {
        return Err("Kaldırma envanteri boyut sınırını aştı.".into());
    }
    let registration_result = (|| {
        let mut file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&manifest)
            .map_err(|e| e.to_string())?;
        file.write_all(&manifest_data).map_err(|e| e.to_string())?;
        file.sync_all().map_err(|e| e.to_string())?;
        register_key(&record, &manifest)
    })();
    if let Err(error) = registration_result {
        if let Some((path, digest)) = uninstall_link {
            if digest_file(&path).ok().as_deref() == Some(&digest) {
                let _ = fs::remove_file(&path);
            }
        }
        let _ = fs::remove_file(&manifest);
        let _ = fs::remove_file(&record.uninstaller);
        // Payload and user-selected shortcuts remain untouched.
        return Err(format!(
            "Windows Yüklü Uygulamalar kaydı tamamlanamadı: {error}"
        ));
    }
    Ok(warnings)
}

/// @dev-id BRK-WIN-UNINSTALL-002
/// @purpose Clean up only verified Windows integration after package engine
/// confirms receipt-owned files have been removed.
/// @contract Foreign or modified LNKs and changed registry values survive.
pub(super) struct CleanupReport {
    pub(super) warnings: Vec<String>,
    pub(super) registry_removed: bool,
}

pub(super) fn cleanup_after_uninstall(record: &LocalRegistration) -> CleanupReport {
    let mut warnings = Vec::new();
    for owned in &record.shortcuts {
        let allowed =
            shortcut_location_is_owned(&owned.path, record.scope(), &record.package_id, true)
                .unwrap_or(false);
        if !allowed || digest_file(&owned.path).ok().as_deref() != Some(&owned.sha256) {
            warnings.push(format!(
                "Değiştirilmiş veya yabancı kısayol korundu: {}",
                owned.path.display()
            ));
            continue;
        }
        if let Err(error) = fs::remove_file(&owned.path) {
            warnings.push(format!("Kısayol kaldırılamadı: {error}"));
        }
        if let Some(folder) = owned.path.parent() {
            // Never delete an empty Desktop, Programs root, or foreign folder.
            let programs = match record.scope() {
                InstallScope::CurrentUser => &windows::Win32::UI::Shell::FOLDERID_Programs,
                InstallScope::AllUsers => &windows::Win32::UI::Shell::FOLDERID_CommonPrograms,
            };
            if known_folder(programs)
                .ok()
                .is_some_and(|root| folder == root.join(&record.package_id))
            {
                let _ = fs::remove_dir(folder);
            }
        }
    }
    let registry_removed = match remove_key(record) {
        Ok(()) => true,
        Err(error) => {
            warnings.push(error);
            false
        }
    };
    CleanupReport {
        warnings,
        registry_removed,
    }
}

/// @dev-id BRK-WIN-UNINSTALL-003
/// @purpose Dispose of the owner receipt and request deferred uninstaller
/// removal once the program is no longer running. No untrusted commands.
/// @contract Only called after the owned registry entry was removed.
pub(super) fn finalize_uninstaller(record: &LocalRegistration, manifest: &Path) -> Vec<String> {
    let mut warnings = Vec::new();
    if manifest
        != registration_path(record)
            .ok()
            .as_deref()
            .unwrap_or(Path::new(""))
    {
        return vec!["Kaldırıcı konum doğrulaması başarısız oldu.".into()];
    }
    if let Err(error) = fs::remove_file(manifest) {
        warnings.push(format!("Kaldırıcı envanteri silinemedi: {error}"));
    }
    let path_w = wide(record.uninstaller.as_os_str());
    if let Err(error) = unsafe {
        MoveFileExW(
            PCWSTR(path_w.as_ptr()),
            PCWSTR::null(),
            MOVEFILE_DELAY_UNTIL_REBOOT,
        )
    } {
        // On non-elevated per-user installs Windows can deny the system-wide
        // delayed-delete registry operation. Preserve rather than force delete.
        warnings.push(format!(
            "Kaldırıcı EXE daha sonra temizlenmelidir: {} ({error})",
            record.uninstaller.display()
        ));
    }
    warnings
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn registry_names_are_package_scoped_and_not_user_controlled_paths() {
        let a = registration_name("qa.native", Path::new(r"C:\Apps\QA 1")).unwrap();
        let b = registration_name("qa.native", Path::new(r"C:\Apps\QA 2")).unwrap();
        assert!(a.starts_with("TurkuazInstaller.qa.native."));
        assert_ne!(a, b);
        assert!(registration_name("../bad", Path::new(r"C:\Apps")).is_err());
        assert!(registration_name("name\\bad", Path::new(r"C:\Apps")).is_err());
    }

    #[test]
    fn hkcu_record_is_created_read_and_deleted_without_touching_other_keys() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos();
        let name = format!("TurkuazInstaller.qa.ci.{}.{}", std::process::id(), unique);
        let key = create_key(InstallScope::CurrentUser, &name).expect("new CI-only key");
        write_string(key.0, MARKER, "test-owner").expect("write marker");
        assert_eq!(read_value(key.0, MARKER).unwrap(), "test-owner");
        assert!(create_key(InstallScope::CurrentUser, &name).is_err());
        drop(key);
        let path = wide(OsStr::new(&key_path(&name)));
        let result = unsafe { RegDeleteKeyW(HKEY_CURRENT_USER, PCWSTR(path.as_ptr())) };
        assert_eq!(result, ERROR_SUCCESS);
    }

    #[test]
    fn disposable_hkcu_install_registration_and_receipt_uninstall_roundtrip() {
        use turkuaz_installer_package_engine::uninstall_local_install;

        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("test clock")
            .as_nanos();
        let id = format!("qa.native.{}.{}", std::process::id(), unique);
        let base = std::env::temp_dir().join(format!("turkuaz-local-lifecycle-{unique}"));
        let target = base.join("installed");
        fs::create_dir_all(&target).expect("fixture target");
        let payload = b"installed fixture data";
        fs::write(target.join("hello.txt"), payload).expect("fixture payload");
        let receipt = serde_json::json!({
            "formatVersion": 1,
            "packageId": id,
            "version": "1.0.0",
            "files": [{
                "path": "hello.txt",
                "sizeBytes": payload.len(),
                "sha256": hash_bytes(payload),
            }],
        });
        fs::write(
            target.join(".turkuaz-local-install.json"),
            serde_json::to_vec(&receipt).expect("receipt JSON"),
        )
        .expect("receipt");

        let source = std::env::current_exe().expect("test exe");
        let warnings = register_local_install(
            &source,
            &id,
            "1.0.0",
            &target,
            InstallScope::CurrentUser,
            &[],
        )
        .expect("register standalone install");
        assert!(
            warnings.is_empty(),
            "Start menu uninstall link: {warnings:?}"
        );
        let name = registration_name(&id, &target).expect("owner key");
        let root = owned_root(InstallScope::CurrentUser).expect("installer root");
        let manifest = root.join(format!("{name}.json"));
        let record = read_registration(&manifest).expect("registered identity");
        assert_eq!(record.target(), target.as_path());
        assert_eq!(record.package_id(), id);

        let removed = uninstall_local_install(&target, &id).expect("receipt-guarded removal");
        assert_eq!(removed.removed_files, 1);
        assert!(!removed.retained_unknown_files);
        let cleanup = cleanup_after_uninstall(&record);
        assert!(
            cleanup.registry_removed,
            "Registry cleanup: {:?}",
            cleanup.warnings
        );
        assert!(
            cleanup.warnings.is_empty(),
            "Shortcut cleanup: {:?}",
            cleanup.warnings
        );
        assert!(open_key(InstallScope::CurrentUser, &name).is_err());
        fs::remove_file(&manifest).expect("remove fixture manifest");
        fs::remove_file(&record.uninstaller).expect("remove unused fixture binary");
        let _ = fs::remove_dir_all(&base);
    }

    #[test]
    fn digest_is_deterministic_for_owned_shortcut_contents() {
        assert_eq!(hash_bytes(b"abc"), hash_bytes(b"abc"));
        assert_ne!(hash_bytes(b"abc"), hash_bytes(b"changed"));
    }
}
