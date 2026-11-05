// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_recycle.rs
// Amac: Kullanici onayli temiz kurulum icin mevcut uygulama klasorunu geri donusume tasimak
// Modul - Rust Windows Shell Adapter
// Version: 0.1.0
// Aciklama: IFileOperation ile recycle-only; silme veya ustune yazma fallback'i yok
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    fs, io,
    os::windows::{ffi::OsStrExt, fs::MetadataExt},
    path::{Component, Path, Prefix},
};

use windows::{
    Win32::{
        System::Com::{
            CLSCTX_INPROC_SERVER, COINIT_APARTMENTTHREADED, CoCreateInstance, CoInitializeEx,
            CoUninitialize,
        },
        UI::Shell::{
            FOF_ALLOWUNDO, FOF_NOCONFIRMATION, FOF_NOERRORUI, FOF_SILENT, FOFX_RECYCLEONDELETE,
            FileOperation, IFileOperation, IFileOperationProgressSink, IShellItem,
            SHCreateItemFromParsingName,
        },
    },
    core::PCWSTR,
};

const FILE_ATTRIBUTE_REPARSE_POINT: u32 = 0x0400;

struct StaApartment;

impl StaApartment {
    fn initialize() -> Result<Self, String> {
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(|error| format!("Windows Geri Dönüşüm Kutusu başlatılamadı: {error}"))?;
        Ok(Self)
    }
}

impl Drop for StaApartment {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

/// @dev-id BRK-RECYCLE-SAFE-PATH-001
/// @purpose Never recycle filesystem roots, arbitrary folder names, or
/// symlink/junction destinations and ancestors.
/// @contract This check is repeated immediately before the Shell operation.
/// It is not a substitute for filesystem ownership/ACL enforcement.
pub(super) fn validate_recycle_target(target: &Path, package_id: &str) -> Result<(), String> {
    if !target.is_absolute()
        || target
            .components()
            .any(|part| matches!(part, Component::CurDir | Component::ParentDir))
    {
        return Err("Geri dönüşüm için mutlak ve geçerli klasör yolu gerekli.".into());
    }
    let first = target.components().next();
    if !matches!(
        first,
        Some(Component::Prefix(prefix))
            if matches!(prefix.kind(), Prefix::Disk(_) | Prefix::VerbatimDisk(_))
    ) {
        return Err("Geri dönüşüm yalnızca yerel Windows sürücülerinde desteklenir.".into());
    }
    if package_id.is_empty()
        || !target
            .file_name()
            .is_some_and(|name| name.to_string_lossy().eq_ignore_ascii_case(package_id))
        || target
            .components()
            .filter(|part| matches!(part, Component::Normal(_)))
            .count()
            < 2
    {
        return Err(
            "Yalnızca uygulama kimliğiyle adlandırılmış alt klasör geri dönüşüme taşınabilir."
                .into(),
        );
    }
    let mut ancestor = target.to_path_buf();
    loop {
        let metadata = fs::symlink_metadata(&ancestor)
            .map_err(|error| format!("Geri dönüşüm klasörü denetlenemedi: {error}"))?;
        if !metadata.is_dir()
            || metadata.file_type().is_symlink()
            || metadata.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT != 0
        {
            return Err("Klasör ya da üst dizin bir sembolik bağlantı/junction olamaz.".into());
        }
        if let Some(parent) = ancestor.parent() {
            ancestor = parent.to_path_buf();
        } else {
            break;
        }
    }
    Ok(())
}

/// @dev-id BRK-RECYCLE-SHELL-001
/// @purpose Move the user-approved original folder through the Windows Shell
/// Recycle Bin; never permanently delete as a fallback.
/// @contract A failed/aborted operation or occupied target prevents new install.
/// This runs on the installation worker's dedicated STA thread.
pub(super) fn recycle_existing_target(target: &Path, package_id: &str) -> Result<(), String> {
    validate_recycle_target(target, package_id)?;
    let _com = StaApartment::initialize()?;
    let operation: IFileOperation =
        unsafe { CoCreateInstance(&FileOperation, None, CLSCTX_INPROC_SERVER) }
            .map_err(|error| format!("Windows Geri Dönüşüm Kutusu kullanılamıyor: {error}"))?;
    let flags =
        FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT | FOFX_RECYCLEONDELETE;
    unsafe { operation.SetOperationFlags(flags) }
        .map_err(|error| format!("Windows recycle-only seçeneği uygulanamadı: {error}"))?;

    let wide: Vec<u16> = target
        .as_os_str()
        .encode_wide()
        .chain(std::iter::once(0))
        .collect();
    let item: IShellItem = unsafe { SHCreateItemFromParsingName(PCWSTR(wide.as_ptr()), None) }
        .map_err(|error| format!("Geri dönüştürülecek klasör bulunamadı: {error}"))?;
    unsafe { operation.DeleteItem(&item, Option::<&IFileOperationProgressSink>::None) }
        .map_err(|error| format!("Geri dönüşüm isteği hazırlanamadı: {error}"))?;
    unsafe { operation.PerformOperations() }
        .map_err(|error| format!("Klasör Geri Dönüşüm Kutusu'na taşınamadı: {error}"))?;
    if unsafe { operation.GetAnyOperationsAborted() }
        .map_err(|error| format!("Geri dönüşüm sonucu okunamadı: {error}"))?
        .as_bool()
    {
        return Err("Windows geri dönüşümü tamamlamadı; kurulum durduruldu.".into());
    }
    match fs::symlink_metadata(target) {
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Ok(_) => Err("Eski klasör hâlâ mevcut; üzerine yazma reddedildi.".into()),
        Err(error) => Err(format!(
            "Geri dönüşüm sonrası klasör doğrulanamadı: {error}"
        )),
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::time::{SystemTime, UNIX_EPOCH};

    #[test]
    fn recycle_path_guard_rejects_unrelated_roots_and_mismatched_leaf() {
        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("clock")
            .as_nanos();
        let root = std::env::temp_dir().join(format!("tz-recycle-test-{stamp}"));
        let target = root.join("qa.recycle");
        fs::create_dir_all(&target).expect("test folder");
        assert!(validate_recycle_target(&target, "qa.recycle").is_ok());
        assert!(validate_recycle_target(&target, "different.app").is_err());
        assert!(validate_recycle_target(&root, "qa.recycle").is_err());
        assert!(validate_recycle_target(Path::new(r"C:\"), "qa.recycle").is_err());
        assert!(
            validate_recycle_target(Path::new(r"\\server\share\qa.recycle"), "qa.recycle").is_err()
        );
        fs::remove_dir_all(root).expect("cleanup fixture");
    }
}
