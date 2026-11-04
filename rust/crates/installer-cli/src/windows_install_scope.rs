// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_install_scope.rs
// Amac: Windows kurulum kapsamlarini ve guvenilir OS klasorlerini yonetmek
// Modul - Rust Windows Installation Scope Adapter
// Version: 0.1.0
// Aciklama: Kullanici / tum kullanicilar kurulum kapsami, known-folder yolu, yetki korumasi
// Bagimli Oldugu Katman: Adapter | Security

use std::path::{Path, PathBuf};

use windows::{
    Win32::{
        System::Com::{COINIT_APARTMENTTHREADED, CoInitializeEx, CoTaskMemFree, CoUninitialize},
        UI::Shell::{
            FOLDERID_ProgramFiles, FOLDERID_UserProgramFiles, IsUserAnAdmin, KNOWN_FOLDER_FLAG,
            SHGetKnownFolderPath,
        },
    },
    core::GUID,
};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(super) enum InstallScope {
    CurrentUser,
    AllUsers,
}

impl InstallScope {
    pub(super) fn label(self) -> &'static str {
        match self {
            Self::CurrentUser => "Yalnızca bu kullanıcı",
            Self::AllUsers => "Bu bilgisayardaki tüm kullanıcılar",
        }
    }
}

struct ComGuard;
impl ComGuard {
    fn new() -> Result<Self, String> {
        unsafe { CoInitializeEx(None, COINIT_APARTMENTTHREADED) }
            .ok()
            .map_err(|e| format!("Windows klasör çözümlemesi başlatılamadı: {e}"))?;
        Ok(Self)
    }
}
impl Drop for ComGuard {
    fn drop(&mut self) {
        unsafe { CoUninitialize() };
    }
}

/// @dev-id BRK-WIN-SCOPE-001
/// @purpose Resolve OS-owned known folders instead of trusting environment paths.
/// @contract Failure never falls back to a hard-coded absolute directory.
pub(super) fn known_folder(id: &GUID) -> Result<PathBuf, String> {
    let _com = ComGuard::new()?;
    let ptr = unsafe { SHGetKnownFolderPath(id, KNOWN_FOLDER_FLAG(0), None) }
        .map_err(|e| format!("Windows sistem klasörü çözümlenemedi: {e}"))?;
    let text = unsafe { ptr.to_string() };
    unsafe { CoTaskMemFree(Some(ptr.0.cast())) };
    let path = PathBuf::from(text.map_err(|e| format!("Windows klasör yolu geçersiz: {e}"))?);
    if !path.is_absolute() {
        return Err("Windows sistem klasörü mutlak bir yol değil.".into());
    }
    Ok(path)
}

pub(super) fn default_install_directory(
    scope: InstallScope,
    package_id: &str,
) -> Result<PathBuf, String> {
    let known = match scope {
        InstallScope::CurrentUser => &FOLDERID_UserProgramFiles,
        InstallScope::AllUsers => &FOLDERID_ProgramFiles,
    };
    Ok(known_folder(known)?.join(package_id))
}

fn inside_program_files(target: &Path, program_files: &Path) -> bool {
    // All-users installs are deliberately restricted to OS Program Files.
    // Scope cannot become a general-purpose privileged file writer.
    target
        .strip_prefix(program_files)
        .ok()
        .is_some_and(|relative| {
            relative.components().count() >= 1
                && relative
                    .components()
                    .all(|c| matches!(c, std::path::Component::Normal(_)))
        })
}

/// @dev-id BRK-WIN-SCOPE-002
/// @purpose Fail closed until an elevated token and protected install root exist.
/// @contract Never silently downgrade an AllUsers request to CurrentUser.
pub(super) fn validate_install_scope(
    scope: InstallScope,
    target: &Path,
    program_files: &Path,
    elevated: bool,
) -> Result<(), String> {
    if scope == InstallScope::CurrentUser {
        return Ok(());
    }
    if !inside_program_files(target, program_files) {
        return Err("Tüm kullanıcılar kurulumu yalnızca Windows Program Files altında yapılabilir.".into());
    }
    if !elevated {
        return Err(
            "Tüm kullanıcılar kurulumu yönetici yetkisi gerektirir. Kurulum EXE'sini 'Yönetici olarak çalıştır' ile yeniden açın."
                .into(),
        );
    }
    Ok(())
}

pub(super) fn current_process_is_elevated() -> bool {
    // IsUserAnAdmin wraps CheckTokenMembership for the current effective token.
    // Fail closed when not elevated, rather than trying protected writes.
    unsafe { IsUserAnAdmin().as_bool() }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn current_user_is_allowed_without_admin_privileges() {
        assert!(validate_install_scope(
            InstallScope::CurrentUser,
            Path::new(r"C:\Users\Demo\AppData\Local\Programs\demo"),
            Path::new(r"C:\Program Files"),
            false
        )
        .is_ok());
    }

    #[test]
    fn all_users_requires_elevation_and_program_files() {
        let program_files = Path::new(r"C:\Program Files");
        let app = Path::new(r"C:\Program Files\demo");
        assert!(validate_install_scope(InstallScope::AllUsers, app, program_files, false).is_err());
        assert!(validate_install_scope(InstallScope::AllUsers, app, program_files, true).is_ok());
        assert!(validate_install_scope(
            InstallScope::AllUsers,
            Path::new(r"C:\Users\Demo\Desktop\demo"),
            program_files,
            true,
        )
        .is_err());
        assert!(validate_install_scope(
            InstallScope::AllUsers,
            Path::new(r"C:\Program Files\..\Windows"),
            program_files,
            true,
        )
        .is_err());
    }

    #[test]
    fn scope_labels_are_distinct() {
        assert_ne!(InstallScope::CurrentUser.label(), InstallScope::AllUsers.label());
    }
}
