// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_uninstaller_cleanup.rs
// Amac: Dogrulanmis kaldiriciyi pencere kapandiginda yonetici yetkisiz temizlemek
// Modul - Rust Windows File Adapter
// Version: 0.1.1
// Aciklama: Ayni dosya taniticisinda SHA-256 kontrolu ve POSIX delete disposition
// Bagimli Oldugu Katman: Adapter | Security

use sha2::{Digest, Sha256};
use std::{
    fs::{File, OpenOptions},
    io::Read,
    os::windows::{
        fs::{MetadataExt, OpenOptionsExt},
        io::AsRawHandle,
    },
    path::{Component, Path},
};
use windows::Win32::{
    Foundation::HANDLE,
    Storage::FileSystem::{
        BY_HANDLE_FILE_INFORMATION, DELETE, FILE_ATTRIBUTE_REPARSE_POINT,
        FILE_DISPOSITION_FLAG_DELETE, FILE_DISPOSITION_FLAG_POSIX_SEMANTICS,
        FILE_DISPOSITION_INFO_EX, FILE_DISPOSITION_INFO_EX_FLAGS, FILE_FLAG_BACKUP_SEMANTICS,
        FILE_FLAG_OPEN_REPARSE_POINT, FILE_GENERIC_READ, FILE_READ_ATTRIBUTES, FILE_SHARE_READ,
        FILE_SHARE_WRITE, FileDispositionInfoEx, GetFileInformationByHandle,
        SetFileInformationByHandle,
    },
};

// Field order matters: close the deletion handle before releasing ancestor locks.
pub(super) struct PendingUninstallerRemoval {
    _file: File,
    _ancestors: Vec<File>,
}

/// @dev-id TI-UNINSTALL-CLEANUP-001
/// @purpose Hold verified deletion until the result window releases this object.
/// @reason Reboot deletion writes system-wide state and fails for standard users.
/// @contract No shell, helper EXE, elevation or reboot. Pin every path ancestor;
/// hash and delete the same regular single-link file with writes/renames denied.
/// Unsupported filesystems or denied deletion preserve the file and return an error.
pub(super) fn prepare_removal(
    path: &Path,
    expected_sha256: &str,
) -> Result<PendingUninstallerRemoval, String> {
    if !path.is_absolute()
        || path
            .components()
            .any(|part| matches!(part, Component::ParentDir | Component::CurDir))
        || expected_sha256.len() != 64
        || !expected_sha256.bytes().all(|b| b.is_ascii_hexdigit())
    {
        return Err("TI-UNINSTALL-CLEANUP-001: Geçersiz kaldırıcı kimliği veya yolu.".into());
    }
    let parent = path
        .parent()
        .ok_or("TI-UNINSTALL-CLEANUP-001: Kaldırıcı dizini yok.")?;
    let mut ancestors = Vec::new();
    for directory in parent.ancestors().collect::<Vec<_>>().into_iter().rev() {
        let handle = OpenOptions::new()
            .access_mode(FILE_READ_ATTRIBUTES.0)
            .share_mode(FILE_SHARE_READ.0 | FILE_SHARE_WRITE.0)
            .custom_flags(FILE_FLAG_BACKUP_SEMANTICS.0 | FILE_FLAG_OPEN_REPARSE_POINT.0)
            .open(directory)
            .map_err(|e| format!("TI-UNINSTALL-CLEANUP-001: Dizin sabitlenemedi: {e}"))?;
        let meta = handle.metadata().map_err(|e| e.to_string())?;
        if !meta.is_dir() || meta.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT.0 != 0 {
            return Err("TI-UNINSTALL-CLEANUP-001: Bağlantılı kaldırıcı dizini korunuyor.".into());
        }
        ancestors.push(handle);
    }
    let mut file = OpenOptions::new()
        .access_mode(FILE_GENERIC_READ.0 | DELETE.0)
        .share_mode(FILE_SHARE_READ.0)
        .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT.0)
        .open(path)
        .map_err(|e| format!("TI-UNINSTALL-CLEANUP-002: Kaldırıcı silme erişimi alınamadı: {e}"))?;
    let meta = file.metadata().map_err(|e| e.to_string())?;
    if !meta.is_file()
        || meta.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT.0 != 0
        || meta.len() > super::windows_local_registration::MAX_EXECUTABLE_BYTES
    {
        return Err(
            "TI-UNINSTALL-CLEANUP-001: Güvenli olmayan kaldırıcı dosyası korunuyor.".into(),
        );
    }
    let handle = HANDLE(file.as_raw_handle());
    let mut info = BY_HANDLE_FILE_INFORMATION::default();
    // SAFETY: the live File owns this handle; info is a correctly sized output buffer.
    unsafe { GetFileInformationByHandle(handle, &mut info) }.map_err(|e| e.to_string())?;
    if info.nNumberOfLinks != 1 {
        return Err("TI-UNINSTALL-CLEANUP-001: Birden çok bağlantısı olan dosya korunuyor.".into());
    }
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; 64 * 1024];
    loop {
        let count = file.read(&mut buffer).map_err(|e| e.to_string())?;
        if count == 0 {
            break;
        }
        hasher.update(&buffer[..count]);
    }
    let actual = format!("{:x}", hasher.finalize());
    if actual != expected_sha256 {
        return Err("TI-UNINSTALL-CLEANUP-001: Kaldırıcı içeriği değişti; dosya korunuyor.".into());
    }
    let disposition = FILE_DISPOSITION_INFO_EX {
        Flags: FILE_DISPOSITION_INFO_EX_FLAGS(
            FILE_DISPOSITION_FLAG_DELETE.0 | FILE_DISPOSITION_FLAG_POSIX_SEMANTICS.0,
        ),
    };
    // SAFETY: DELETE access was obtained for this verified File; the structure
    // remains live for the synchronous call. Do not force-clear read-only flags.
    unsafe {
        SetFileInformationByHandle(
            handle,
            FileDispositionInfoEx,
            &disposition as *const _ as *const _,
            std::mem::size_of_val(&disposition) as u32,
        )
    }
    .map_err(|e| {
        format!("TI-UNINSTALL-CLEANUP-003: Windows dosya temizliğini kabul etmedi: {e}")
    })?;
    Ok(PendingUninstallerRemoval {
        _file: file,
        _ancestors: ancestors,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::{
        fs,
        process::{Child, Command},
        sync::atomic::{AtomicU64, Ordering},
        thread,
        time::{Duration, SystemTime, UNIX_EPOCH},
    };

    static NEXT_FIXTURE: AtomicU64 = AtomicU64::new(0);

    struct Fixture(std::path::PathBuf);
    impl Fixture {
        fn new() -> Self {
            let nonce = SystemTime::now()
                .duration_since(UNIX_EPOCH)
                .unwrap()
                .as_nanos();
            let sequence = NEXT_FIXTURE.fetch_add(1, Ordering::Relaxed);
            let path = std::env::temp_dir().join(format!(
                "turkuaz-cleanup-{}-{nonce}-{sequence}",
                std::process::id()
            ));
            fs::create_dir(&path).unwrap();
            Self(path)
        }
    }
    impl Drop for Fixture {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }
    struct ChildGuard(Child);
    impl Drop for ChildGuard {
        fn drop(&mut self) {
            let _ = self.0.kill();
            let _ = self.0.wait();
        }
    }
    fn digest(path: &Path) -> String {
        format!("{:x}", Sha256::digest(fs::read(path).unwrap()))
    }

    #[test]
    fn cleanup_waits_for_handle_drop_and_preserves_neighbours() {
        let fixture = Fixture::new();
        let owned = fixture.0.join("owned.exe");
        let foreign = fixture.0.join("user.txt");
        fs::write(&owned, b"owned fixture").unwrap();
        fs::write(&foreign, b"keep").unwrap();
        let pending = prepare_removal(&owned, &digest(&owned)).unwrap();
        assert!(
            owned.exists(),
            "must remain until the result window releases its handle"
        );
        drop(pending);
        assert!(!owned.exists());
        assert_eq!(fs::read(foreign).unwrap(), b"keep");
    }

    #[test]
    fn cleanup_rejects_changed_readonly_and_hardlinked_files() {
        let fixture = Fixture::new();
        let owned = fixture.0.join("owned.exe");
        fs::write(&owned, b"original").unwrap();
        let expected = digest(&owned);
        fs::write(&owned, b"changed").unwrap();
        assert!(prepare_removal(&owned, &expected).is_err());
        assert_eq!(fs::read(&owned).unwrap(), b"changed");
        let expected = digest(&owned);
        let alias = fixture.0.join("alias.exe");
        fs::hard_link(&owned, &alias).unwrap();
        assert!(prepare_removal(&owned, &expected).is_err());
        fs::remove_file(alias).unwrap();
        let mut permissions = fs::metadata(&owned).unwrap().permissions();
        permissions.set_readonly(true);
        fs::set_permissions(&owned, permissions.clone()).unwrap();
        assert!(prepare_removal(&owned, &expected).is_err());
        assert!(owned.exists());
        permissions.set_readonly(false);
        fs::set_permissions(&owned, permissions).unwrap();
    }

    #[test]
    fn cleanup_can_release_a_running_executable_without_reboot() {
        let fixture = Fixture::new();
        let original = std::env::current_exe().unwrap();
        let copy = fixture.0.join("running-test.exe");
        fs::copy(&original, &copy).unwrap();
        let expected = digest(&copy);
        let mut child = ChildGuard(
            Command::new(&copy)
                .args([
                    "--exact",
                    "windows_uninstaller_cleanup::tests::cleanup_child_process",
                    "--ignored",
                ])
                .env("TURKUAZ_CLEANUP_TEST_ROOT", &fixture.0)
                .spawn()
                .unwrap(),
        );
        for _ in 0..100 {
            if fixture.0.join("ready").exists() {
                break;
            }
            thread::sleep(Duration::from_millis(100));
        }
        assert!(fixture.0.join("ready").exists(), "child did not start");
        assert!(child.0.try_wait().unwrap().is_none());
        let pending = prepare_removal(&copy, &expected).expect("live image cleanup");
        assert!(copy.exists());
        drop(pending);
        assert!(
            !copy.exists(),
            "uninstaller name should be removed without reboot"
        );
        fs::write(fixture.0.join("finish"), b"exit").unwrap();
        assert!(child.0.wait().unwrap().success());
        assert!(original.exists(), "the parent test executable must survive");
    }

    // This is a child-process entry point, exercised by the test above.
    #[test]
    #[ignore = "spawned by cleanup_can_release_a_running_executable_without_reboot"]
    fn cleanup_child_process() {
        let root = std::path::PathBuf::from(
            std::env::var_os("TURKUAZ_CLEANUP_TEST_ROOT").expect("child root"),
        );
        fs::write(root.join("ready"), b"running").unwrap();
        for _ in 0..300 {
            if root.join("finish").exists() {
                return;
            }
            thread::sleep(Duration::from_millis(100));
        }
        panic!("parent did not finish cleanup test");
    }
}
