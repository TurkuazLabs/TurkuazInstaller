// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_shortcuts.rs
// Amac: Yerel Rust setup icin kullaniciya ait Windows kisayollari olusturmak
// Modul - Rust Windows Shell Adapter
// Version: 0.1.7
// Aciklama: Zorunlu Baslat grubu, urun ikonlari, Shell yenileme ve yabanci LNK korumasi
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    fs, io,
    os::windows::ffi::OsStrExt,
    path::{Component, Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use crate::windows_install_scope::{InstallScope, known_folder};

use windows::{
    Win32::{
        System::Com::{
            CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED, CoCreateInstance, CoInitializeEx,
            CoUninitialize, IPersistFile,
        },
        UI::Shell::{
            FOLDERID_CommonPrograms, FOLDERID_Desktop, FOLDERID_Programs, FOLDERID_PublicDesktop,
            IShellLinkW, SHCNE_CREATE, SHCNE_DELETE, SHCNE_MKDIR, SHCNE_RMDIR, SHCNE_UPDATEDIR,
            SHCNF_PATHW, SHChangeNotify, ShellLink,
        },
    },
    core::{Interface, PCWSTR},
};

pub(super) fn resolve_installed_executable(root: &Path, relative: &str) -> Result<PathBuf, String> {
    let path = Path::new(relative);
    if relative.is_empty()
        || !path
            .components()
            .all(|part| matches!(part, Component::Normal(_)))
        || !path
            .extension()
            .is_some_and(|extension| extension.eq_ignore_ascii_case("exe"))
    {
        return Err("Kısayol için pakette tanımlı güvenli bir EXE giriş noktası gerekli.".into());
    }
    let root = fs::canonicalize(root).map_err(|e| format!("Kurulum klasörü okunamadı: {e}"))?;
    let file = root.join(path);
    let meta = fs::symlink_metadata(&file).map_err(|e| format!("Ana EXE bulunamadı: {e}"))?;
    if !meta.is_file() || meta.file_type().is_symlink() || windows_reparse(&meta) {
        return Err("Ana EXE normal bir dosya değil; kısayol oluşturulmadı.".into());
    }
    let resolved = fs::canonicalize(&file).map_err(|e| format!("EXE yolu doğrulanamadı: {e}"))?;
    if !resolved.starts_with(&root) {
        return Err("Ana EXE kurulum kökünün dışına çıkıyor.".into());
    }
    Ok(resolved)
}

pub(super) fn valid_display_name(value: &str) -> bool {
    let value = value.trim();
    !value.is_empty()
        && value.chars().count() <= 128
        && value != "."
        && value != ".."
        && !value.ends_with([' ', '.'])
        && !value
            .chars()
            .any(|character| character.is_control() || r#"<>:"/\|?*"#.contains(character))
}

fn windows_reparse(meta: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    meta.file_attributes() & 0x0400 != 0
}

// BRK-WIN-SHORTCUT-001: Common and per-user paths are supplied by
// the OS known-folder adapter, never by the process environment.
// BRK-WIN-SHORTCUT-004: fs::canonicalize returns Win32 verbatim paths;
// keep those for security validation, but pass ordinary paths to ShellLink.
// The Shell API may reject the "\\?\" prefix with E_INVALIDARG.
fn shell_path(path: &Path) -> Result<PathBuf, String> {
    let text = path
        .to_str()
        .ok_or("Kısayol yolu geçerli Unicode içermiyor.")?;
    let ordinary = if let Some(rest) = text.strip_prefix(r"\\?\UNC\") {
        format!(r"\\{rest}")
    } else if let Some(rest) = text.strip_prefix(r"\\?\") {
        // Reject opaque device namespaces; canonicalized disk paths only.
        if rest.len() < 3
            || !rest.as_bytes()[0].is_ascii_alphabetic()
            || rest.as_bytes()[1] != b':'
            || rest.as_bytes()[2] != b'\\'
        {
            return Err("Windows Shell için desteklenmeyen aygıt yolu.".into());
        }
        rest.to_owned()
    } else {
        text.to_owned()
    };
    // Windows ShellLink has limited support for long paths. Never silently
    // truncate a target or change the validated destination.
    if ordinary.encode_utf16().count() >= 260 {
        return Err("Kısayol hedef yolu Windows Shell sınırını aşıyor.".into());
    }
    Ok(PathBuf::from(ordinary))
}

fn wide(value: &OsStr) -> Vec<u16> {
    value.encode_wide().chain(std::iter::once(0)).collect()
}

fn notify_shell_created(path: &Path) {
    let path_w = wide(path.as_os_str());
    unsafe {
        SHChangeNotify(
            SHCNE_CREATE,
            SHCNF_PATHW,
            Some(path_w.as_ptr().cast()),
            None,
        );
    }
    if let Some(parent) = path.parent() {
        let parent_w = wide(parent.as_os_str());
        unsafe {
            SHChangeNotify(
                SHCNE_UPDATEDIR,
                SHCNF_PATHW,
                Some(parent_w.as_ptr().cast()),
                None,
            );
        }
    }
}

fn notify_shell_directory_created(path: &Path) {
    notify_shell_path(path, SHCNE_MKDIR);
}

pub(super) fn notify_shell_directory_removed(path: &Path) {
    notify_shell_path(path, SHCNE_RMDIR);
}

fn notify_shell_path(path: &Path, event: windows::Win32::UI::Shell::SHCNE_ID) {
    let path_w = wide(path.as_os_str());
    unsafe {
        SHChangeNotify(event, SHCNF_PATHW, Some(path_w.as_ptr().cast()), None);
    }
    if let Some(parent) = path.parent() {
        let parent_w = wide(parent.as_os_str());
        unsafe {
            SHChangeNotify(
                SHCNE_UPDATEDIR,
                SHCNF_PATHW,
                Some(parent_w.as_ptr().cast()),
                None,
            );
        }
    }
}

pub(super) fn notify_shell_removed(path: &Path) {
    let path_w = wide(path.as_os_str());
    unsafe {
        SHChangeNotify(
            SHCNE_DELETE,
            SHCNF_PATHW,
            Some(path_w.as_ptr().cast()),
            None,
        );
    }
    if let Some(parent) = path.parent() {
        let parent_w = wide(parent.as_os_str());
        unsafe {
            SHChangeNotify(
                SHCNE_UPDATEDIR,
                SHCNF_PATHW,
                Some(parent_w.as_ptr().cast()),
                None,
            );
        }
    }
}

struct ComApartment;
impl ComApartment {
    fn enter() -> Result<Self, String> {
        // All COM objects remain on the background install worker thread.
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(|e| format!("Windows kısayol servisi başlatılamadı: {e}"))?;
        Ok(Self)
    }
}
impl Drop for ComApartment {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

struct TemporaryShortcut(PathBuf);
impl Drop for TemporaryShortcut {
    fn drop(&mut self) {
        let _ = fs::remove_file(&self.0);
    }
}

/// @dev-id BRK-WIN-SHORTCUT-002
/// @purpose Persist a shell link and atomically publish it only if no
/// existing shortcut occupies the requested path.
/// @contract Never replace third-party links, even after a failed save.
fn create_link_file(
    target: &Path,
    destination: &Path,
    name: &str,
    args: Option<&str>,
    icon: Option<&Path>,
) -> Result<(), String> {
    let parent = destination
        .parent()
        .ok_or("Kısayol hedef klasörü bulunamadı.")?;
    if fs::symlink_metadata(destination).is_ok() {
        return Err("Bu adda kısayol zaten var; mevcut dosya korunuyor.".into());
    }
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_nanos();
    let staged = parent.join(format!(".turkuaz-link-{}-{nonce}.lnk", std::process::id()));
    let reserved = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&staged)
        .map_err(|e| format!("Geçici kısayol dosyası açılamadı: {e}"))?;
    drop(reserved);
    let _cleanup = TemporaryShortcut(staged.clone());

    let link: IShellLinkW = unsafe { CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER) }
        .map_err(|e| format!("Windows ShellLink oluşturulamadı: {e}"))?;
    let shell_target = shell_path(target)?;
    let shell_working = shell_path(
        target
            .parent()
            .ok_or("Uygulama çalışma klasörü bulunamadı.")?,
    )?;
    let target_w = wide(shell_target.as_os_str());
    let working_w = wide(shell_working.as_os_str());
    let description = format!("{name} uygulamasını aç");
    let description_w = wide(OsStr::new(&description));
    let arguments_w = args.map(|args| wide(OsStr::new(args)));
    let icon_path = icon.map(shell_path).transpose()?;
    let icon_w = icon_path.as_ref().map(|path| wide(path.as_os_str()));
    unsafe {
        link.SetPath(PCWSTR(target_w.as_ptr()))
            .map_err(|e| format!("Kısayol EXE hedefi atanamadı: {e}"))?;
        link.SetWorkingDirectory(PCWSTR(working_w.as_ptr()))
            .map_err(|e| format!("Kısayol çalışma klasörü atanamadı: {e}"))?;
        link.SetDescription(PCWSTR(description_w.as_ptr()))
            .map_err(|e| format!("Kısayol açıklaması atanamadı: {e}"))?;
        if let Some(arguments) = &arguments_w {
            link.SetArguments(PCWSTR(arguments.as_ptr()))
                .map_err(|e| format!("Kısayol parametresi atanamadı: {e}"))?;
        }
        if let Some(icon) = &icon_w {
            link.SetIconLocation(PCWSTR(icon.as_ptr()), 0)
                .map_err(|e| format!("Kısayol ikonu atanamadı: {e}"))?;
        }
    }
    let persist: IPersistFile = link
        .cast()
        .map_err(|e| format!("ShellLink kayıt arayüzü açılamadı: {e}"))?;
    let staged_w = wide(staged.as_os_str());
    unsafe { persist.Save(PCWSTR(staged_w.as_ptr()), true) }
        .map_err(|e| format!("Kısayol kaydedilemedi: {e}"))?;

    // NTFS hard link publication is no-clobber. Do not fall back to a rename
    // that could overwrite an unrelated shortcut.
    fs::hard_link(&staged, destination)
        .map_err(|e| format!("Kısayol güvenli yayımlanamadı: {e}"))?;
    notify_shell_created(destination);
    Ok(())
}

#[derive(Default)]
pub(super) struct ShortcutReport {
    pub(super) created: Vec<PathBuf>,
    pub(super) warnings: Vec<String>,
}

/// @dev-id BRK-WIN-SHORTCUT-003
/// @purpose Create the required product Start menu link and opt-in Desktop link.
/// @contract Every executable application gets a dedicated Start menu group;
/// no elevation, no overwrite, and no guessed EXE path.
pub(super) fn create_installed_shortcuts(
    install_root: &Path,
    entry_point: &str,
    display_name: &str,
    desktop: bool,
    scope: InstallScope,
) -> Result<ShortcutReport, String> {
    if !valid_display_name(display_name) {
        return Err("Kısayol için güvenli bir uygulama adı gerekli.".into());
    }
    let exe = resolve_installed_executable(install_root, entry_point)?;
    let _apartment = ComApartment::enter()?;
    let mut report = ShortcutReport::default();
    let folder = match scope {
        InstallScope::CurrentUser => &FOLDERID_Programs,
        InstallScope::AllUsers => &FOLDERID_CommonPrograms,
    };
    let parent = known_folder(folder)?;
    let group = parent.join(display_name);
    let created_group = match fs::create_dir(&group) {
        Ok(()) => {
            notify_shell_directory_created(&group);
            true
        }
        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
            let metadata = fs::symlink_metadata(&group).map_err(|e| e.to_string())?;
            if !metadata.is_dir() || metadata.file_type().is_symlink() || windows_reparse(&metadata)
            {
                return Err("Başlat menüsü uygulama grubu güvenli bir klasör değil.".into());
            }
            if fs::read_dir(&group)
                .map_err(|e| e.to_string())?
                .next()
                .is_some()
            {
                return Err(
                    "Başlat menüsü uygulama grubunda mevcut dosyalar var; üzerine yazılmadı."
                        .into(),
                );
            }
            false
        }
        Err(error) => return Err(format!("Başlat menüsü uygulama grubu: {error}")),
    };
    let shortcut = group.join(format!("{display_name}.lnk"));
    if let Err(error) = create_link_file(&exe, &shortcut, display_name, None, Some(&exe)) {
        if created_group && fs::remove_dir(&group).is_ok() {
            notify_shell_directory_removed(&group);
        }
        return Err(format!("Başlat menüsü uygulama kısayolu: {error}"));
    }
    report.created.push(shortcut);
    if desktop {
        let folder = match scope {
            InstallScope::CurrentUser => &FOLDERID_Desktop,
            InstallScope::AllUsers => &FOLDERID_PublicDesktop,
        };
        let parent = known_folder(folder)?;
        let shortcut = parent.join(format!("{display_name}.lnk"));
        match create_link_file(&exe, &shortcut, display_name, None, Some(&exe)) {
            Ok(()) => report.created.push(shortcut),
            Err(error) => report.warnings.push(format!("Masaüstü: {error}")),
        }
    }
    Ok(report)
}

/// @dev-id BRK-WIN-UNINSTALL-LNK-001
/// @purpose Verify that a shortcut belongs to the expected Windows shell
/// locations without accepting arbitrary file paths from JSON.
pub(super) fn shortcut_location_is_owned(
    path: &Path,
    scope: InstallScope,
    display_name: &str,
    allow_uninstall: bool,
) -> Result<bool, String> {
    let programs = match scope {
        InstallScope::CurrentUser => &FOLDERID_Programs,
        InstallScope::AllUsers => &FOLDERID_CommonPrograms,
    };
    let desktop = match scope {
        InstallScope::CurrentUser => &FOLDERID_Desktop,
        InstallScope::AllUsers => &FOLDERID_PublicDesktop,
    };
    let start_dir = known_folder(programs)?.join(display_name);
    let app_link = start_dir.join(format!("{display_name}.lnk"));
    let desktop_link = known_folder(desktop)?.join(format!("{display_name}.lnk"));
    let uninstall_link = start_dir.join("Kaldır.lnk");
    Ok(path == app_link || path == desktop_link || (allow_uninstall && path == uninstall_link))
}

/// @dev-id BRK-WIN-UNINSTALL-LNK-002
/// @purpose Provide a no-overwrite Start menu uninstall shortcut pointing to
/// the independent registered uninstaller, never to an installed payload EXE.
pub(super) fn create_owned_uninstaller_link(
    executable: &Path,
    manifest: &Path,
    display_name: &str,
    display_icon: &Path,
    scope: InstallScope,
    owns_existing_group: bool,
) -> Result<PathBuf, String> {
    let metadata = fs::symlink_metadata(executable).map_err(|e| e.to_string())?;
    if !metadata.is_file() || metadata.file_type().is_symlink() || windows_reparse(&metadata) {
        return Err("Kaldırıcı EXE normal bir dosya değil.".into());
    }
    let programs = match scope {
        InstallScope::CurrentUser => &FOLDERID_Programs,
        InstallScope::AllUsers => &FOLDERID_CommonPrograms,
    };
    if !valid_display_name(display_name) {
        return Err("Kaldır kısayolu için güvenli bir uygulama adı gerekli.".into());
    }
    let directory = known_folder(programs)?.join(display_name);
    match fs::create_dir(&directory) {
        Ok(()) => notify_shell_directory_created(&directory),
        Err(error) if error.kind() == io::ErrorKind::AlreadyExists && owns_existing_group => {}
        Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
            return Err("Başlat menüsündeki mevcut yabancı gruba dokunulmadı.".into());
        }
        Err(error) => return Err(format!("Kaldır kısayolu klasörü: {error}")),
    }
    let folder_meta = fs::symlink_metadata(&directory).map_err(|e| e.to_string())?;
    if !folder_meta.is_dir()
        || folder_meta.file_type().is_symlink()
        || windows_reparse(&folder_meta)
    {
        return Err("Kaldır kısayolu klasörü bir bağlantı veya junction içeriyor.".into());
    }
    let link = directory.join("Kaldır.lnk");
    let arguments = format!(
        "uninstall-local-gui --registration \"{}\"",
        manifest.display()
    );
    if manifest.to_string_lossy().contains('"') {
        return Err("Kaldırıcı parametre yolu geçersiz.".into());
    }
    let _com = ComApartment::enter()?;
    create_link_file(
        executable,
        &link,
        &format!("{display_name} uygulamasını kaldır"),
        Some(&arguments),
        Some(display_icon),
    )?;
    Ok(link)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reject_missing_exe_and_escaping_paths() {
        let root = std::env::temp_dir();
        for relative in [
            "../escape.exe",
            "C:\\escape.exe",
            "app.bat",
            "",
            "/absolute.exe",
        ] {
            assert!(
                resolve_installed_executable(&root, relative).is_err(),
                "{relative}"
            );
        }
    }

    #[test]
    fn converts_only_known_verbatim_prefixes_for_shell() {
        assert_eq!(
            shell_path(Path::new(r"\\?\C:\Program Files\Uygulama.exe")).expect("disk"),
            PathBuf::from(r"C:\Program Files\Uygulama.exe")
        );
        assert_eq!(
            shell_path(Path::new(r"\\?\UNC\host\share\uygulama.exe")).expect("UNC"),
            PathBuf::from(r"\\host\share\uygulama.exe")
        );
        assert!(shell_path(Path::new(r"\\?\GLOBALROOT\Device\Harddisk")).is_err());
    }

    #[test]
    fn creates_shell_link_without_overwriting_foreign_file() {
        let root =
            std::env::temp_dir().join(format!("turkuaz-shortcut-test-{}", std::process::id()));
        fs::create_dir(&root).expect("fixture");
        let exe = std::env::current_exe().expect("running Windows test executable");
        let _com = ComApartment::enter().expect("COM");
        let link = root.join("Kisayol.lnk");
        create_link_file(&exe, &link, "Test", None, None).expect("create shell link");
        let bytes = fs::read(&link).expect("read LNK");
        assert!(bytes.len() > 76);
        assert_eq!(&bytes[..4], &[0x4c, 0x00, 0x00, 0x00]);
        assert!(create_link_file(&exe, &link, "Test", None, None).is_err());
        assert_eq!(fs::read(&link).expect("unchanged"), bytes);
        fs::remove_dir_all(&root).expect("fixture cleanup");
    }

    #[test]
    fn display_name_rejects_windows_path_syntax() {
        assert!(valid_display_name("PixelTone"));
        for name in ["", ".", "..", "Bad/Name", "Bad\\Name", "Bad:", "Bad. "] {
            assert!(!valid_display_name(name), "{name:?}");
        }
    }
}
