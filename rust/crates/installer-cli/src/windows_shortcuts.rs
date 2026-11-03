// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_shortcuts.rs
// Amac: Yerel Rust setup icin kullaniciya ait Windows kisayollari olusturmak
// Modul - Rust Windows Shell Adapter
// Version: 0.1.0
// Aciklama: OS known folder, guvenli entry point ve yabanci LNK korumasi
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    fs, io,
    os::windows::ffi::OsStrExt,
    path::{Component, Path, PathBuf},
    time::{SystemTime, UNIX_EPOCH},
};

use windows::{
    Win32::{
        System::Com::{
            CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED, CoCreateInstance, CoInitializeEx,
            CoTaskMemFree, CoUninitialize, IPersistFile,
        },
        UI::Shell::{
            FOLDERID_Desktop, FOLDERID_Programs, IShellLinkW, KNOWN_FOLDER_FLAG,
            SHGetKnownFolderPath, ShellLink,
        },
    },
    core::{GUID, PCWSTR},
};

fn safe_relative_executable(root: &Path, relative: &str) -> Result<PathBuf, String> {
    let path = Path::new(relative);
    if relative.is_empty()
        || !path.components().all(|part| matches!(part, Component::Normal(_)))
        || !path
            .extension()
            .is_some_and(|extension| extension.eq_ignore_ascii_case("exe"))
    {
        return Err("Kisayol icin pakette tanimli guvenli bir EXE giris noktasi gerekli.".into());
    }
    let root = fs::canonicalize(root).map_err(|e| format!("Kurulum klasoru okunamadi: {e}"))?;
    let file = root.join(path);
    let meta = fs::symlink_metadata(&file).map_err(|e| format!("Ana EXE bulunamadi: {e}"))?;
    if !meta.is_file() || meta.file_type().is_symlink() || windows_reparse(&meta) {
        return Err("Ana EXE normal bir dosya degil; kisayol olusturulmadi.".into());
    }
    let resolved = fs::canonicalize(&file).map_err(|e| format!("EXE yolu dogrulanamadi: {e}"))?;
    if !resolved.starts_with(&root) {
        return Err("Ana EXE kurulum kokunun disina cikiyor.".into());
    }
    Ok(resolved)
}

fn windows_reparse(meta: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    meta.file_attributes() & 0x0400 != 0
}

fn folder_path(id: &GUID) -> Result<PathBuf, String> {
    // BRK-WIN-SHORTCUT-001: Resolve actual redirected Windows folders through
    // known-folder API; never compose paths from USERPROFILE or process CWD.
    let wide = unsafe { SHGetKnownFolderPath(id, KNOWN_FOLDER_FLAG(0), None) }
        .map_err(|e| format!("Windows sistem klasoru bulunamadi: {e}"))?;
    let text = unsafe { wide.to_string() };
    unsafe { CoTaskMemFree(Some(wide.0.cast())) };
    let folder = text.map_err(|e| format!("Windows klasor yolu gecersiz: {e}"))?;
    let path = PathBuf::from(folder);
    fs::canonicalize(path).map_err(|e| format!("Windows klasoru erisilemiyor: {e}"))
}

fn wide(value: &OsStr) -> Vec<u16> {
    value.encode_wide().chain(std::iter::once(0)).collect()
}

struct ComApartment;
impl ComApartment {
    fn enter() -> Result<Self, String> {
        // All COM objects remain on the background install worker thread.
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(|e| format!("Windows kisayol servisi baslatilamadi: {e}"))?;
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
fn create_link_file(target: &Path, destination: &Path, name: &str) -> Result<(), String> {
    let parent = destination
        .parent()
        .ok_or("Kisayol hedef klasoru bulunamadi.")?;
    if fs::symlink_metadata(destination).is_ok() {
        return Err("Bu adda kisayol zaten var; mevcut dosya korunuyor.".into());
    }
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_nanos();
    let staged = parent.join(format!(
        ".turkuaz-link-{}-{nonce}.lnk",
        std::process::id()
    ));
    let reserved = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&staged)
        .map_err(|e| format!("Gecici kisayol dosyasi acilamadi: {e}"))?;
    drop(reserved);
    let _cleanup = TemporaryShortcut(staged.clone());

    let link: IShellLinkW =
        unsafe { CoCreateInstance(&ShellLink, None, CLSCTX_INPROC_SERVER) }
            .map_err(|e| format!("Windows ShellLink olusturulamadi: {e}"))?;
    let target_w = wide(target.as_os_str());
    let working_w = wide(
        target
            .parent()
            .ok_or("Uygulama calisma klasoru bulunamadi.")?
            .as_os_str(),
    );
    let description = format!("{name} uygulamasini ac");
    let description_w = wide(OsStr::new(&description));
    unsafe {
        link.SetPath(PCWSTR(target_w.as_ptr()))
            .map_err(|e| format!("Kisayol EXE hedefi atanamadi: {e}"))?;
        link.SetWorkingDirectory(PCWSTR(working_w.as_ptr()))
            .map_err(|e| format!("Kisayol calisma klasoru atanamadi: {e}"))?;
        link.SetDescription(PCWSTR(description_w.as_ptr()))
            .map_err(|e| format!("Kisayol aciklamasi atanamadi: {e}"))?;
    }
    let persist: IPersistFile = link
        .cast()
        .map_err(|e| format!("ShellLink kayit arayuzu acilamadi: {e}"))?;
    let staged_w = wide(staged.as_os_str());
    unsafe { persist.Save(PCWSTR(staged_w.as_ptr()), true) }
        .map_err(|e| format!("Kisayol kaydedilemedi: {e}"))?;

    // NTFS hard link publication is no-clobber. Do not fall back to a rename
    // that could overwrite an unrelated shortcut.
    fs::hard_link(&staged, destination)
        .map_err(|e| format!("Kisayol guvenli yayinlanamadi: {e}"))
}

#[derive(Default)]
pub(super) struct ShortcutReport {
    pub(super) created: Vec<PathBuf>,
    pub(super) warnings: Vec<String>,
}

/// @dev-id BRK-WIN-SHORTCUT-003
/// @purpose Create per-user Start menu and opt-in Desktop links after install.
/// @contract No elevation; no HKLM; never replace shortcuts or guess EXE paths.
/// This does not represent a complete uninstall/ownership protocol.
pub(super) fn create_installed_shortcuts(
    install_root: &Path,
    entry_point: &str,
    package_id: &str,
    start_menu: bool,
    desktop: bool,
) -> Result<ShortcutReport, String> {
    let exe = safe_relative_executable(install_root, entry_point)?;
    let _apartment = ComApartment::enter()?;
    let mut report = ShortcutReport::default();
    if start_menu {
        let parent = folder_path(&FOLDERID_Programs)?;
        let group = parent.join(package_id);
        match fs::create_dir(&group) {
            Ok(()) => {
                let shortcut = group.join(format!("{package_id}.lnk"));
                match create_link_file(&exe, &shortcut, package_id) {
                    Ok(()) => report.created.push(shortcut),
                    Err(error) => {
                        let _ = fs::remove_dir(&group);
                        report.warnings.push(format!("Baslat menusu: {error}"));
                    }
                }
            }
            Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {
                report.warnings.push(
                    "Baslat menusu grubunda mevcut dosyalar var; uzerine yazilmadi.".into(),
                );
            }
            Err(error) => report.warnings.push(format!("Baslat menusu: {error}")),
        }
    }
    if desktop {
        let parent = folder_path(&FOLDERID_Desktop)?;
        let shortcut = parent.join(format!("{package_id}.lnk"));
        match create_link_file(&exe, &shortcut, package_id) {
            Ok(()) => report.created.push(shortcut),
            Err(error) => report.warnings.push(format!("Masaustu: {error}")),
        }
    }
    Ok(report)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reject_missing_exe_and_escaping_paths() {
        let root = std::env::temp_dir();
        for relative in ["../escape.exe", "C:\\escape.exe", "app.bat", "", "/absolute.exe"] {
            assert!(safe_relative_executable(&root, relative).is_err(), "{relative}");
        }
    }

    #[test]
    fn creates_shell_link_without_overwriting_foreign_file() {
        let root = std::env::temp_dir().join(format!(
            "turkuaz-shortcut-test-{}",
            std::process::id()
        ));
        fs::create_dir(&root).expect("fixture");
        let exe = root.join("uygulama.exe");
        fs::write(&exe, b"MZ-fixture").expect("fixture EXE");
        let exe = safe_relative_executable(&root, "uygulama.exe").expect("target");
        let _com = ComApartment::enter().expect("COM");
        let link = root.join("Kisayol.lnk");
        create_link_file(&exe, &link, "Test").expect("create shell link");
        let bytes = fs::read(&link).expect("read LNK");
        assert!(bytes.len() > 76);
        assert_eq!(&bytes[..4], &[0x4c, 0x00, 0x00, 0x00]);
        assert!(create_link_file(&exe, &link, "Test").is_err());
        assert_eq!(fs::read(&link).expect("unchanged"), bytes);
        fs::remove_dir_all(&root).expect("fixture cleanup");
    }
}
