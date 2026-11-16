// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_local_registration.rs
// Amac: Imzasiz yerel kurulum icin Windows uygulama kaydi ve sahiplik dosyasi
// Modul - Rust Windows Lifecycle Adapter
// Version: 0.1.18
// Aciklama: Reparse guvenli envanter okuma ve kayitli kurulum veri koruma denetimi
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    fs::{self, File, OpenOptions},
    io::{self, Read, Write},
    os::windows::{
        ffi::OsStrExt,
        fs::{MetadataExt, OpenOptionsExt},
        io::AsRawHandle,
    },
    path::{Component, Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use turkuaz_installer_package_engine::LOCAL_INSTALL_RECEIPT_FILE_NAME;
use windows::{
    Win32::{
        Foundation::{ERROR_FILE_NOT_FOUND, ERROR_PATH_NOT_FOUND, ERROR_SUCCESS, HANDLE},
        Storage::FileSystem::{
            BY_HANDLE_FILE_INFORMATION, FILE_ATTRIBUTE_DIRECTORY, FILE_ATTRIBUTE_REPARSE_POINT,
            FILE_FLAG_OPEN_REPARSE_POINT, FILE_READ_ATTRIBUTES, FILE_SHARE_DELETE, FILE_SHARE_READ,
            FILE_SHARE_WRITE, GetFileInformationByHandle,
        },
        System::Registry::{
            HKEY, HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, KEY_QUERY_VALUE, KEY_SET_VALUE,
            REG_CREATE_KEY_DISPOSITION, REG_CREATED_NEW_KEY, REG_DWORD, REG_OPTION_NON_VOLATILE,
            REG_SZ, RegCloseKey, RegCreateKeyExW, RegDeleteKeyW, RegOpenKeyExW, RegQueryValueExW,
            RegSetValueExW,
        },
        UI::Shell::{
            FOLDERID_CommonPrograms, FOLDERID_Desktop, FOLDERID_LocalAppData,
            FOLDERID_ProgramFiles, FOLDERID_Programs, FOLDERID_PublicDesktop,
        },
    },
    core::PCWSTR,
};

use crate::{
    windows_install_scope::{InstallScope, current_process_is_elevated, known_folder},
    windows_shortcuts::{
        create_owned_uninstaller_link, notify_shell_directory_removed, notify_shell_removed,
        resolve_installed_executable, shortcut_location_is_owned, valid_display_name,
    },
};

const UNINSTALL_ROOT: &str = "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall";
const MARKER: &str = "TurkuazOwner";
const MAX_JSON_BYTES: u64 = 256 * 1024;
pub(super) const MAX_EXECUTABLE_BYTES: u64 = 512 * 1024 * 1024;

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
    #[serde(default, skip_serializing_if = "Option::is_none")]
    volume_serial_number: Option<u32>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    file_index: Option<u64>,
}

#[derive(Clone, Debug, Serialize, Deserialize)]
#[serde(deny_unknown_fields, rename_all = "camelCase")]
pub(super) struct LocalRegistration {
    version: u32,
    package_id: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    display_name: Option<String>,
    display_version: String,
    target: PathBuf,
    scope: RegisteredScope,
    key: String,
    marker: String,
    uninstaller: PathBuf,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    display_icon: Option<PathBuf>,
    shortcuts: Vec<OwnedShortcut>,
    #[serde(skip)]
    uninstaller_sha256: Option<String>,
}

impl LocalRegistration {
    pub(super) fn same_uninstaller(&self, other: &Self) -> bool {
        self.uninstaller == other.uninstaller
            && self.uninstaller_sha256.is_some()
            && self.uninstaller_sha256 == other.uninstaller_sha256
    }
    pub(super) fn package_id(&self) -> &str {
        &self.package_id
    }
    pub(super) fn display_name(&self) -> &str {
        self.display_name.as_deref().unwrap_or(&self.package_id)
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
    fn display_icon(&self) -> &Path {
        self.display_icon.as_deref().unwrap_or(&self.uninstaller)
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
        let _ = unsafe { RegCloseKey(self.0) };
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

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
struct ShortcutFileIdentity {
    volume_serial_number: u32,
    file_index: u64,
}

fn shortcut_file_identity(path: &Path) -> Result<ShortcutFileIdentity, String> {
    let file = OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES.0)
        .share_mode(FILE_SHARE_READ.0 | FILE_SHARE_WRITE.0 | FILE_SHARE_DELETE.0)
        .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT.0)
        .open(path)
        .map_err(|e| e.to_string())?;
    let mut info = BY_HANDLE_FILE_INFORMATION::default();
    unsafe { GetFileInformationByHandle(HANDLE(file.as_raw_handle()), &mut info) }
        .map_err(|e| e.to_string())?;
    if info.dwFileAttributes & (FILE_ATTRIBUTE_REPARSE_POINT.0 | FILE_ATTRIBUTE_DIRECTORY.0) != 0
        || info.nNumberOfLinks != 1
    {
        return Err("Kısayol normal, tek bağlantılı bir Windows dosyası değil.".into());
    }
    Ok(ShortcutFileIdentity {
        volume_serial_number: info.dwVolumeSerialNumber,
        file_index: ((info.nFileIndexHigh as u64) << 32) | info.nFileIndexLow as u64,
    })
}

fn owned_shortcut(path: PathBuf) -> Result<OwnedShortcut, String> {
    let sha256 = digest_file(&path)?;
    let identity = shortcut_file_identity(&path)?;
    Ok(OwnedShortcut {
        path,
        sha256,
        volume_serial_number: Some(identity.volume_serial_number),
        file_index: Some(identity.file_index),
    })
}

fn owned_shortcut_matches(owned: &OwnedShortcut) -> bool {
    match (owned.volume_serial_number, owned.file_index) {
        (Some(volume_serial_number), Some(file_index)) => shortcut_file_identity(&owned.path)
            .is_ok_and(|identity| {
                identity.volume_serial_number == volume_serial_number
                    && identity.file_index == file_index
            }),
        (None, None) => digest_file(&owned.path).ok().as_deref() == Some(&owned.sha256),
        _ => false,
    }
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

/// @dev-id TI-WINREG-001
/// @purpose Refuse reuse of an already registered installation target.
/// @reason Recycling the old target before discovering a registration
/// collision would leave an Installed Apps entry pointing to moved files.
/// @contract Read-only preflight: never touch a prior payload inventory,
/// receipt, executable, shortcut or registry key. Registration remains no-clobber.
/// @tests windows_local_registration::tests::preflight_rejects_existing_registry_and_receipt
pub(super) fn require_unregistered_target(
    package_id: &str,
    target: &Path,
    scope: InstallScope,
) -> Result<(), String> {
    if !target.is_absolute() {
        return Err("Kurulum kaydı için mutlak hedef gerekli.".into());
    }
    let name = registration_name(package_id, target)?;
    // The payload inventory remains even if HKCU/HKLM registration or the
    // independent uninstaller was removed. Do not recycle that installation.
    match fs::symlink_metadata(target.join(LOCAL_INSTALL_RECEIPT_FILE_NAME)) {
        Ok(_) => {
            return Err(
                "Bu klasörde mevcut kurulum envanteri var; önce uygulamayı güvenle kaldırın."
                    .into(),
            );
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => {
            return Err(format!("Mevcut kurulum envanteri denetlenemedi: {error}"));
        }
    }
    let root = owned_root(scope)?;
    for path in [
        root.join(format!("{name}.json")),
        root.join(format!("{name}.exe")),
    ] {
        match fs::symlink_metadata(&path) {
            Ok(_) => {
                return Err(
                    "Bu hedefe ait kaldırıcı zaten kayıtlı; önce mevcut kurulumu kaldırın.".into(),
                );
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => {}
            Err(error) => {
                return Err(format!(
                    "Mevcut kaldırıcı kaydı güvenle denetlenemedi: {error}"
                ));
            }
        }
    }

    let mut key = HKEY::default();
    let path = wide(OsStr::new(&key_path(&name)));
    let status = unsafe {
        RegOpenKeyExW(
            hive(scope),
            PCWSTR(path.as_ptr()),
            None,
            KEY_QUERY_VALUE,
            &mut key,
        )
    };
    if status == ERROR_SUCCESS {
        drop(RegistryKey(key));
        return Err("Bu hedef Windows Yüklü Uygulamalar'a zaten kayıtlı; önce kaldırın.".into());
    }
    if status != ERROR_FILE_NOT_FOUND && status != ERROR_PATH_NOT_FOUND {
        return Err(failure(
            "Mevcut Windows uygulama kaydı denetlenemedi",
            status,
        ));
    }
    Ok(())
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
        || read_value(key.0, "DisplayName")? != record.display_name()
        || read_value(key.0, "DisplayIcon")? != record.display_icon().to_string_lossy()
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

/// @dev-id TI-UNINSTALL-RECEIPT-READ-001
/// @purpose Read the uninstall inventory without following Windows reparse links.
/// @reason The registration path can be passed by an external uninstall caller.
/// @contract Read-only; reject unsafe ancestors, reparse files, oversized data
/// and lexical traversal before reading the receipt. Not a race-free sandbox.
/// @tests windows_local_registration::tests::registration_reader_rejects_unsafe_paths
fn read_registration_bytes(source: &Path) -> Result<Vec<u8>, String> {
    if !source.is_absolute()
        || source
            .components()
            .any(|part| matches!(part, Component::CurDir | Component::ParentDir))
    {
        return Err("Kaldırma envanteri için mutlak ve güvenli yol gerekli.".into());
    }
    let parent = source
        .parent()
        .ok_or("Kaldırma envanteri için üst klasör gerekli.")?;
    for ancestor in parent.ancestors() {
        let meta = fs::symlink_metadata(ancestor).map_err(|e| e.to_string())?;
        if !meta.is_dir()
            || meta.file_type().is_symlink()
            || meta.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT.0 != 0
        {
            return Err("Kaldırma envanteri bağlantılı klasörden okunamaz.".into());
        }
    }
    let file = OpenOptions::new()
        .read(true)
        .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT.0)
        .open(source)
        .map_err(|e| e.to_string())?;
    let meta = file.metadata().map_err(|e| e.to_string())?;
    if !meta.is_file()
        || meta.file_type().is_symlink()
        || meta.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT.0 != 0
        || meta.len() == 0
        || meta.len() > MAX_JSON_BYTES
    {
        return Err("Kaldırma envanter dosyası güvenli değil.".into());
    }
    let mut input = Vec::with_capacity(meta.len() as usize);
    file.take(MAX_JSON_BYTES + 1)
        .read_to_end(&mut input)
        .map_err(|e| e.to_string())?;
    if input.len() as u64 > MAX_JSON_BYTES {
        return Err("Kaldırma envanteri aşırı büyük.".into());
    }
    Ok(input)
}

pub(super) fn read_registration(source: &Path) -> Result<LocalRegistration, String> {
    let input = read_registration_bytes(source)?;
    let mut record: LocalRegistration =
        serde_json::from_slice(&input).map_err(|e| e.to_string())?;
    if record.version != 1
        || !valid_id(&record.package_id)
        || !valid_display_name(record.display_name())
        || record.key != registration_name(&record.package_id, &record.target)?
        || record.shortcuts.len() > 8
        || source != registration_path(&record)?
        || !record.uninstaller.is_absolute()
        || record.uninstaller.parent() != source.parent()
        || record.uninstaller.file_name() != Some(OsStr::new(&format!("{}.exe", record.key)))
        || record.display_icon.as_ref().is_some_and(|icon| {
            !icon.is_absolute() || !icon.starts_with(&record.target) || !icon.is_file()
        })
        || record.shortcuts.iter().any(|shortcut| {
            shortcut.volume_serial_number.is_some() != shortcut.file_index.is_some()
        })
    {
        return Err("Kaldırma envanteri bu Windows kurulumuna ait değil.".into());
    }
    let key = open_key(record.scope(), &record.key)?;
    if read_value(key.0, MARKER)? != record.marker
        || read_value(key.0, "InstallLocation")? != record.target.to_string_lossy()
    {
        return Err("Windows uygulama kaydı/kurulum eşleşmiyor.".into());
    }
    record.uninstaller_sha256 = Some(digest_file(&record.uninstaller)?);
    Ok(record)
}

fn register_key(record: &LocalRegistration, manifest: &Path) -> Result<(), String> {
    let key = create_key(record.scope(), &record.key)?;
    let arguments = uninstall_command(record, manifest);
    let result = (|| {
        write_string(key.0, MARKER, &record.marker)?;
        write_string(key.0, "DisplayName", record.display_name())?;
        write_string(key.0, "DisplayVersion", &record.display_version)?;
        write_string(key.0, "Publisher", "TurkuazInstaller local package")?;
        write_string(key.0, "InstallLocation", &record.target.to_string_lossy())?;
        write_string(key.0, "UninstallString", &arguments)?;
        write_string(
            key.0,
            "DisplayIcon",
            &record.display_icon().to_string_lossy(),
        )?;
        write_u32(key.0, "NoModify", 1)?;
        write_u32(key.0, "NoRepair", 1)?;
        Ok::<(), String>(())
    })();
    drop(key);
    if result.is_err() {
        let path = wide(OsStr::new(&key_path(&record.key)));
        let _ = unsafe { RegDeleteKeyW(hive(record.scope()), PCWSTR(path.as_ptr())) };
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
/// @contract Failed registration removes integration artifacts created by this
/// attempt; the caller rolls back the receipt-owned payload.
pub(super) fn register_local_install(
    source_setup: &Path,
    package_id: &str,
    display_name: &str,
    display_version: &str,
    target: &Path,
    entry_point: Option<&str>,
    scope: InstallScope,
    desktop_shortcut_required: bool,
    previously_created_shortcuts: &[PathBuf],
) -> Result<Vec<String>, String> {
    if scope == InstallScope::AllUsers && !current_process_is_elevated() {
        return Err("Tüm kullanıcılar kaldırıcı kaydı yönetici yetkisi gerektirir.".into());
    }
    if !valid_display_name(display_name) {
        return Err("Windows uygulama kaydı için güvenli bir ürün adı gerekli.".into());
    }
    let display_icon = entry_point
        .map(|entry| {
            resolve_installed_executable(target, entry)?;
            Ok::<PathBuf, String>(target.join(entry))
        })
        .transpose()?;
    let name = registration_name(package_id, target)?;
    let root = owned_root(scope)?;
    ensure_managed_directory(&root)?;
    let uninstaller = root.join(format!("{name}.exe"));
    let manifest = root.join(format!("{name}.json"));
    if uninstaller.starts_with(target) || target.starts_with(&root) || !target.is_absolute() {
        return Err("Kaldırıcı çalışma dizini kurulum hedefiyle çakışıyor.".into());
    }
    if fs::symlink_metadata(&manifest).is_ok() || fs::symlink_metadata(&uninstaller).is_ok() {
        return Err("Bu kuruluma ait kaldırıcı kaydı zaten var; mevcut dosyalar korunuyor.".into());
    }
    if open_key(scope, &name).is_ok() {
        return Err("Mevcut Windows uygulama kaydının üzerine yazılmadı.".into());
    }
    let warnings = Vec::new();
    let mut shortcuts = Vec::new();
    for path in previously_created_shortcuts {
        if shortcut_location_is_owned(path, scope, display_name, false)? {
            shortcuts.push(owned_shortcut(path.clone())?);
        }
    }
    let programs = match scope {
        InstallScope::CurrentUser => &FOLDERID_Programs,
        InstallScope::AllUsers => &FOLDERID_CommonPrograms,
    };
    let group = known_folder(programs)?.join(display_name);
    let expected_application_link = group.join(format!("{display_name}.lnk"));
    let owns_start_group = previously_created_shortcuts
        .iter()
        .any(|path| path == &expected_application_link);
    let owns_selected_desktop = if desktop_shortcut_required {
        let desktop = match scope {
            InstallScope::CurrentUser => &FOLDERID_Desktop,
            InstallScope::AllUsers => &FOLDERID_PublicDesktop,
        };
        let expected = known_folder(desktop)?.join(format!("{display_name}.lnk"));
        previously_created_shortcuts
            .iter()
            .any(|path| path == &expected)
    } else {
        true
    };
    if entry_point.is_some() && (!owns_start_group || !owns_selected_desktop) {
        let _ = remove_owned_shortcuts(&shortcuts, scope, display_name);
        let _ = fs::remove_file(&uninstaller);
        return Err(
            "Zorunlu Başlat menüsü veya seçilmiş masaüstü uygulama kısayolu doğrulanamadı.".into(),
        );
    }
    create_copy_no_clobber(source_setup, &uninstaller)?;
    match create_owned_uninstaller_link(
        &uninstaller,
        &manifest,
        display_name,
        display_icon.as_deref().unwrap_or(&uninstaller),
        scope,
        owns_start_group,
    ) {
        Ok(path) => {
            let owned = owned_shortcut(path)?;
            shortcuts.push(owned);
        }
        Err(error) => {
            let _ = remove_owned_shortcuts(&shortcuts, scope, display_name);
            let _ = fs::remove_file(&uninstaller);
            return Err(format!("Zorunlu Kaldır kısayolu oluşturulamadı: {error}"));
        }
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
        display_name: Some(display_name.to_owned()),
        display_version: display_version.to_owned(),
        target: target.to_path_buf(),
        scope: scope.into(),
        key: name,
        marker,
        uninstaller,
        display_icon,
        shortcuts,
        uninstaller_sha256: None,
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
        let _ = remove_owned_shortcuts(&record.shortcuts, scope, display_name);
        let _ = fs::remove_file(&manifest);
        let _ = fs::remove_file(&record.uninstaller);
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

fn remove_owned_shortcuts(
    shortcuts: &[OwnedShortcut],
    scope: InstallScope,
    display_name: &str,
) -> Vec<String> {
    let mut warnings = Vec::new();
    for owned in shortcuts {
        let allowed =
            shortcut_location_is_owned(&owned.path, scope, display_name, true).unwrap_or(false);
        if !allowed {
            warnings.push(format!(
                "Yabancı kısayol konumu korundu: {}",
                owned.path.display()
            ));
            continue;
        }
        match fs::symlink_metadata(&owned.path) {
            Err(error) if error.kind() == io::ErrorKind::NotFound => continue,
            Err(error) => {
                warnings.push(format!("Kısayol denetlenemedi: {error}"));
                continue;
            }
            Ok(_) => {}
        }
        if !owned_shortcut_matches(owned) {
            warnings.push(format!(
                "Değiştirilmiş veya yabancı kısayol korundu: {}",
                owned.path.display()
            ));
            continue;
        }
        if let Err(error) = fs::remove_file(&owned.path) {
            warnings.push(format!("Kısayol kaldırılamadı: {error}"));
        } else {
            notify_shell_removed(&owned.path);
        }
    }
    let programs = match scope {
        InstallScope::CurrentUser => &windows::Win32::UI::Shell::FOLDERID_Programs,
        InstallScope::AllUsers => &windows::Win32::UI::Shell::FOLDERID_CommonPrograms,
    };
    if let Ok(group) = known_folder(programs).map(|root| root.join(display_name)) {
        if fs::remove_dir(&group).is_ok() {
            notify_shell_directory_removed(&group);
        }
    }
    warnings
}

pub(super) fn cleanup_after_uninstall(record: &LocalRegistration) -> CleanupReport {
    let mut warnings =
        remove_owned_shortcuts(&record.shortcuts, record.scope(), record.display_name());
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
/// @purpose Schedule verified EXE and receipt cleanup after process exit.
/// @contract Called after owned registry removal. Failed preparation preserves
/// both EXE and receipt; no system-wide reboot operation or shell is used.
pub(super) fn finalize_uninstaller(
    record: &LocalRegistration,
    manifest: &Path,
) -> (
    Option<crate::windows_uninstaller_cleanup::PendingUninstallerRemoval>,
    Vec<String>,
) {
    let prepare = || {
        let root = owned_root(record.scope())?;
        if record.key != registration_name(&record.package_id, &record.target)?
            || manifest != registration_path(record)?
            || record.uninstaller != root.join(format!("{}.exe", record.key))
        {
            return Err("TI-UNINSTALL-CLEANUP-001: Kaldırıcı konum doğrulaması başarısız.".into());
        }
        let digest = record
            .uninstaller_sha256
            .as_deref()
            .ok_or("TI-UNINSTALL-CLEANUP-001: Kaldırıcı içerik doğrulaması eksik.")?;
        let target =
            crate::windows_uninstaller_cleanup::prepare_removal(&record.uninstaller, digest)?;
        let receipt =
            crate::windows_uninstaller_cleanup::prepare_removal(manifest, &digest_file(manifest)?)?;
        crate::windows_uninstaller_cleanup::schedule_removal(target, receipt, unsafe {
            windows::Win32::System::Threading::GetCurrentProcess()
        })
    };
    let pending = match prepare() {
        Ok(pending) => pending,
        Err(error) => {
            return (
                None,
                vec![format!(
                    "Kaldırıcı EXE ve envanteri korundu: {} ({error})",
                    record.uninstaller.display()
                )],
            );
        }
    };
    (Some(pending), Vec::new())
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
    fn preflight_rejects_existing_registry_and_receipt() {
        let nonce = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("test clock")
            .as_nanos();
        let id = format!("qa.preflight.{}.{nonce}", std::process::id());
        let target = std::env::temp_dir().join(format!("turkuaz-preflight-{nonce}"));
        let scope = InstallScope::CurrentUser;
        let name = registration_name(&id, &target).expect("registration name");

        require_unregistered_target(&id, &target, scope).expect("new target has no registration");
        let key = create_key(scope, &name).expect("isolated registry key");
        let error = require_unregistered_target(&id, &target, scope)
            .expect_err("existing registry key must prevent recycle");
        assert!(error.contains("zaten kayıtlı"), "{error}");
        drop(key);
        let key_path = wide(OsStr::new(&key_path(&name)));
        assert_eq!(
            unsafe { RegDeleteKeyW(HKEY_CURRENT_USER, PCWSTR(key_path.as_ptr())) },
            ERROR_SUCCESS
        );

        let root = owned_root(scope).expect("owned root");
        fs::create_dir_all(&root).expect("test-owned root");
        let receipt = root.join(format!("{name}.json"));
        let _ = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&receipt)
            .expect("test receipt");
        assert!(
            require_unregistered_target(&id, &target, scope).is_err(),
            "orphan receipt must block replacement"
        );
        assert!(receipt.exists(), "preflight must preserve receipt");
        fs::remove_file(&receipt).expect("fixture cleanup");
        require_unregistered_target(&id, &target, scope).expect("no registry or receipt remains");

        // Simulate another installer publishing its external uninstaller
        // after our first preflight, while our new package is staged.
        let orphan_exe = root.join(format!("{name}.exe"));
        let existing_bytes = b"other installer executable";
        let mut exe_file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&orphan_exe)
            .expect("isolated uninstaller");
        exe_file
            .write_all(existing_bytes)
            .expect("write existing uninstaller");
        drop(exe_file);
        assert!(
            require_unregistered_target(&id, &target, scope).is_err(),
            "a second pre-recycle check must reject a newly installed EXE"
        );
        assert_eq!(
            fs::read(&orphan_exe).expect("existing EXE preserved"),
            existing_bytes
        );
        fs::remove_file(&orphan_exe).expect("fixture EXE cleanup");
        require_unregistered_target(&id, &target, scope)
            .expect("orphan uninstaller removed by fixture");

        // A local payload receipt without the Windows registry or external
        // uninstaller must still block recycling. Never parse or delete it.
        fs::create_dir(&target).expect("isolated install target");
        let local_receipt = target.join(LOCAL_INSTALL_RECEIPT_FILE_NAME);
        let foreign_contents = b"untrusted existing receipt";
        fs::write(&local_receipt, foreign_contents).expect("existing payload inventory");
        let error = require_unregistered_target(&id, &target, scope)
            .expect_err("orphan payload inventory must block replacing an existing install");
        assert!(error.contains("kurulum envanteri"), "{error}");
        assert_eq!(
            fs::read(&local_receipt).expect("old receipt preserved"),
            foreign_contents
        );
        fs::remove_file(&local_receipt).expect("receipt cleanup");
        fs::remove_dir(&target).expect("fixture directory cleanup");
        require_unregistered_target(&id, &target, scope)
            .expect("no prior payload inventory remains");
    }

    #[test]
    fn registration_reader_rejects_unsafe_paths() {
        let nonce = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("test clock")
            .as_nanos();
        let base = std::env::temp_dir().join(format!("turkuaz-receipt-read-{nonce}"));
        fs::create_dir(&base).expect("isolated fixture root");
        let receipt = base.join("registration.json");
        let data = b"safe fixture bytes";
        fs::write(&receipt, data).expect("write receipt bytes");
        assert_eq!(
            read_registration_bytes(&receipt).expect("regular receipt bytes"),
            data.to_vec()
        );
        assert!(
            read_registration_bytes(Path::new("registration.json")).is_err(),
            "relative inventory path must not be read"
        );
        assert!(
            read_registration_bytes(&base.join("..").join("registration.json")).is_err(),
            "lexical parent traversal must be rejected before reading"
        );
        let directory = base.join("directory.json");
        fs::create_dir(&directory).expect("directory fixture");
        assert!(
            read_registration_bytes(&directory).is_err(),
            "a directory is not a regular inventory file"
        );
        assert!(directory.is_dir(), "failed read never modifies directory");
        let oversized = base.join("oversized.json");
        let large_file = OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&oversized)
            .expect("oversized fixture");
        large_file
            .set_len(MAX_JSON_BYTES + 1)
            .expect("set length without allocating memory");
        drop(large_file);
        assert!(
            read_registration_bytes(&oversized).is_err(),
            "oversized inventory must fail closed"
        );
        assert_eq!(
            fs::read(&receipt).expect("valid file remains"),
            data.to_vec()
        );
        fs::remove_dir_all(base).expect("remove isolated fixture");
    }

    #[test]
    fn disposable_hkcu_install_registration_and_receipt_uninstall_roundtrip() {
        use turkuaz_installer_package_engine::uninstall_local_install;

        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("test clock")
            .as_nanos();
        let id = format!("qa.native.{}.{}", std::process::id(), unique);
        let display_name = format!("QA Native {} {unique}", std::process::id());
        let base = std::env::temp_dir().join(format!("turkuaz-local-lifecycle-{unique}"));
        let target = base.join("installed");
        fs::create_dir_all(&target).expect("fixture target");
        let payload = b"installed fixture data";
        let executable_payload = b"MZ fixture executable";
        let executable_name = "QA-Native.exe";
        fs::write(target.join("hello.txt"), payload).expect("fixture payload");
        fs::write(target.join(executable_name), executable_payload).expect("fixture executable");
        let receipt = serde_json::json!({
            "formatVersion": 1,
            "packageId": id,
            "version": "1.0.0",
            "files": [
                {
                    "path": "hello.txt",
                    "sizeBytes": payload.len(),
                    "sha256": hash_bytes(payload),
                },
                {
                    "path": executable_name,
                    "sizeBytes": executable_payload.len(),
                    "sha256": hash_bytes(executable_payload),
                }
            ],
        });
        fs::write(
            target.join(".turkuaz-local-install.json"),
            serde_json::to_vec(&receipt).expect("receipt JSON"),
        )
        .expect("receipt");

        let source = std::env::current_exe().expect("test exe");
        let shortcut_report = crate::windows_shortcuts::create_installed_shortcuts(
            &target,
            executable_name,
            &display_name,
            InstallScope::CurrentUser,
            true,
        )
        .expect("required Start menu and selected Desktop shortcuts");
        assert_eq!(shortcut_report.created.len(), 2);
        let warnings = register_local_install(
            &source,
            &id,
            &display_name,
            "1.0.0",
            &target,
            Some(executable_name),
            InstallScope::CurrentUser,
            true,
            &shortcut_report.created,
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
        assert_eq!(record.display_name(), display_name);
        assert_eq!(record.shortcuts.len(), 3);
        for shortcut in &record.shortcuts {
            let original_digest = shortcut.sha256.clone();
            OpenOptions::new()
                .append(true)
                .open(&shortcut.path)
                .and_then(|mut file| file.write_all(b"windows-shell-metadata"))
                .expect("simulate in-place Windows Shell metadata update");
            assert_ne!(digest_file(&shortcut.path).unwrap(), original_digest);
            assert!(owned_shortcut_matches(shortcut));
        }

        let removed = uninstall_local_install(&target, &id).expect("receipt-guarded removal");
        assert_eq!(removed.removed_files, 2);
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
        for shortcut in &record.shortcuts {
            assert!(
                !shortcut.path.exists(),
                "left shortcut: {:?}",
                shortcut.path
            );
        }
        let start_group = known_folder(&FOLDERID_Programs)
            .unwrap()
            .join(&display_name);
        assert!(
            !start_group.exists(),
            "left Start menu group: {start_group:?}"
        );
        assert!(open_key(InstallScope::CurrentUser, &name).is_err());
        let (rejected, warnings) = finalize_uninstaller(&record, &base.join("foreign.json"));
        assert!(rejected.is_none());
        assert!(
            warnings
                .iter()
                .any(|w| w.contains("TI-UNINSTALL-CLEANUP-001"))
        );
        assert!(manifest.exists());
        let mut stale = record.clone();
        stale.uninstaller_sha256 = Some("0".repeat(64));
        assert!(!record.same_uninstaller(&stale));
        let (rejected, warnings) = finalize_uninstaller(&stale, &manifest);
        assert!(rejected.is_none());
        assert!(
            warnings
                .iter()
                .any(|w| w.contains("TI-UNINSTALL-CLEANUP-001"))
        );
        assert!(manifest.exists());
        assert!(record.uninstaller.exists());
        let (pending, warnings) = finalize_uninstaller(&record, &manifest);
        assert!(warnings.is_empty(), "{warnings:?}");
        assert!(pending.is_some());
        assert!(manifest.exists());
        assert!(record.uninstaller.exists());
        drop(pending);
        // Worker owns both handles and waits for this test process to exit.
        // The live-child test verifies actual post-exit deletion.
        let _ = fs::remove_dir_all(&base);
    }

    #[test]
    fn desktop_opt_out_registers_and_cleans_start_menu_links_only() {
        let unique = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .expect("test clock")
            .as_nanos();
        let id = format!("qa.native.optout.{}.{}", std::process::id(), unique);
        let display_name = format!("QA Native Opt Out {} {unique}", std::process::id());
        let base = std::env::temp_dir().join(format!("turkuaz-local-opt-out-{unique}"));
        let target = base.join("installed");
        fs::create_dir_all(&target).expect("fixture target");
        let executable_name = "QA-Native.exe";
        fs::write(target.join(executable_name), b"MZ fixture executable")
            .expect("fixture executable");

        let shortcut_report = crate::windows_shortcuts::create_installed_shortcuts(
            &target,
            executable_name,
            &display_name,
            InstallScope::CurrentUser,
            false,
        )
        .expect("Start menu shortcut with Desktop opted out");
        assert_eq!(shortcut_report.created.len(), 1);

        let source = std::env::current_exe().expect("test exe");
        let warnings = register_local_install(
            &source,
            &id,
            &display_name,
            "1.0.0",
            &target,
            Some(executable_name),
            InstallScope::CurrentUser,
            false,
            &shortcut_report.created,
        )
        .expect("register Desktop opt-out");
        assert!(warnings.is_empty(), "{warnings:?}");

        let name = registration_name(&id, &target).expect("owner key");
        let root = owned_root(InstallScope::CurrentUser).expect("installer root");
        let manifest = root.join(format!("{name}.json"));
        let record = read_registration(&manifest).expect("registered identity");
        assert_eq!(record.shortcuts.len(), 2, "application and uninstall links");
        let desktop_link = known_folder(&FOLDERID_Desktop)
            .expect("Desktop")
            .join(format!("{display_name}.lnk"));
        assert!(!desktop_link.exists());

        let cleanup = cleanup_after_uninstall(&record);
        assert!(cleanup.registry_removed, "{:?}", cleanup.warnings);
        assert!(cleanup.warnings.is_empty(), "{:?}", cleanup.warnings);
        for shortcut in &record.shortcuts {
            assert!(
                !shortcut.path.exists(),
                "left shortcut: {:?}",
                shortcut.path
            );
        }
        let _ = fs::remove_file(&manifest);
        let _ = fs::remove_file(&record.uninstaller);
        let _ = fs::remove_dir_all(&base);
    }

    #[test]
    fn digest_is_deterministic_for_owned_shortcut_contents() {
        assert_eq!(hash_bytes(b"abc"), hash_bytes(b"abc"));
        assert_ne!(hash_bytes(b"abc"), hash_bytes(b"changed"));
    }
}
