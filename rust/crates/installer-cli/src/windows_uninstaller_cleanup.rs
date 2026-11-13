// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_uninstaller_cleanup.rs
// Amac: Dogrulanmis kaldiriciyi pencere kapandiginda yonetici yetkisiz temizlemek
// Modul - Rust Windows File Adapter
// Version: 0.2.0
// Aciklama: Dogrulanmis taniticilari Rust yardimci surecine aktararak kapanis sonrasi temizlik
// Bagimli Oldugu Katman: Adapter | Security

use sha2::{Digest, Sha256};
use std::{
    fs::{File, OpenOptions},
    io::{Read, Write},
    os::windows::{
        fs::{MetadataExt, OpenOptionsExt},
        io::{AsRawHandle, OwnedHandle},
    },
    path::{Component, Path, PathBuf},
};
use windows::Win32::{
    Foundation::HANDLE,
    Storage::FileSystem::{
        BY_HANDLE_FILE_INFORMATION, DELETE, FILE_ATTRIBUTE_READONLY, FILE_ATTRIBUTE_REPARSE_POINT,
        FILE_FLAG_BACKUP_SEMANTICS, FILE_FLAG_OPEN_REPARSE_POINT, FILE_GENERIC_READ,
        FILE_READ_ATTRIBUTES, FILE_SHARE_READ, FILE_SHARE_WRITE, GetFileInformationByHandle,
    },
};

// Field order matters: close the deletion handle before releasing ancestor locks.
pub(super) struct VerifiedRemoval {
    path: PathBuf,
    _file: File,
    _ancestors: Vec<File>,
}

/// @dev-id TI-UNINSTALL-CLEANUP-001
/// @purpose Pin and verify files before transferring deletion capabilities.
/// @reason Reboot deletion writes system-wide state and fails for standard users.
/// @contract No shell, elevation or reboot. Pin every path ancestor;
/// hash and delete the same regular single-link file with writes/renames denied.
/// Denied access, readonly files and changed content are preserved.
pub(super) fn prepare_removal(
    path: &Path,
    expected_sha256: &str,
) -> Result<VerifiedRemoval, String> {
    verify_file(path, expected_sha256, true)
}

fn verify_file(
    path: &Path,
    expected_sha256: &str,
    delete_access: bool,
) -> Result<VerifiedRemoval, String> {
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
        .access_mode(FILE_GENERIC_READ.0 | if delete_access { DELETE.0 } else { 0 })
        .share_mode(FILE_SHARE_READ.0)
        .custom_flags(FILE_FLAG_OPEN_REPARSE_POINT.0)
        .open(path)
        .map_err(|e| format!("TI-UNINSTALL-CLEANUP-002: Kaldırıcı silme erişimi alınamadı: {e}"))?;
    let meta = file.metadata().map_err(|e| e.to_string())?;
    if !meta.is_file()
        || meta.file_attributes() & (FILE_ATTRIBUTE_REPARSE_POINT.0 | FILE_ATTRIBUTE_READONLY.0)
            != 0
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
    Ok(VerifiedRemoval {
        path: path.to_path_buf(),
        _file: file,
        _ancestors: ancestors,
    })
}

pub(super) struct PendingUninstallerRemoval {
    _target: VerifiedRemoval,
    _receipt: VerifiedRemoval,
    _helper: VerifiedRemoval,
    _worker: OwnedHandle,
    _log: File,
}

const WORKER_BYTES: &[u8] = include_bytes!(concat!(env!("OUT_DIR"), "/turkuaz-cleanup-worker.exe"));

/// @dev-id TI-UNINSTALL-CLEANUP-005
/// @purpose Launch a small shared Rust worker, which waits for the real parent handle.
/// @contract Reuse only the embedded worker's content hash. Keep cache/log files;
/// delete only verified target and receipt handles after process exit.
pub(super) fn schedule_removal(
    target: VerifiedRemoval,
    receipt: VerifiedRemoval,
    parent: HANDLE,
) -> Result<PendingUninstallerRemoval, String> {
    let root = target.path.parent().ok_or("cleanup root missing")?;
    let cache = root.join("Cleanup");
    match std::fs::create_dir(&cache) {
        Ok(()) => (),
        Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
        Err(e) => return Err(format!("TI-UNINSTALL-CLEANUP-005: {e}")),
    }
    // Pin and reject a reparse cache directory before any writes into it.
    let cache_lock = OpenOptions::new()
        .access_mode(FILE_READ_ATTRIBUTES.0)
        .share_mode(FILE_SHARE_READ.0 | FILE_SHARE_WRITE.0)
        .custom_flags(FILE_FLAG_BACKUP_SEMANTICS.0 | FILE_FLAG_OPEN_REPARSE_POINT.0)
        .open(&cache)
        .map_err(|e| e.to_string())?;
    let meta = cache_lock.metadata().map_err(|e| e.to_string())?;
    if !meta.is_dir() || meta.file_attributes() & FILE_ATTRIBUTE_REPARSE_POINT.0 != 0 {
        return Err("TI-UNINSTALL-CLEANUP-005: Güvensiz temizleyici önbelleği.".into());
    }
    let digest = format!("{:x}", Sha256::digest(WORKER_BYTES));
    let executable = cache.join(format!("turkuaz-cleanup-{digest}.exe"));
    match OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&executable)
    {
        Ok(mut file) => {
            file.write_all(WORKER_BYTES)
                .and_then(|_| file.sync_all())
                .map_err(|e| e.to_string())?;
        }
        Err(e) if e.kind() == std::io::ErrorKind::AlreadyExists => (),
        Err(e) => return Err(format!("TI-UNINSTALL-CLEANUP-005: {e}")),
    }
    let helper = verify_file(&executable, &digest, false)?;
    let nonce = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map_err(|e| e.to_string())?
        .as_nanos();
    let log = OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(cache.join(format!("cleanup-{}-{nonce}.log", std::process::id())))
        .map_err(|e| format!("TI-UNINSTALL-CLEANUP-005: {e}"))?;
    let worker = crate::windows_cleanup_process::spawn_worker(
        &executable,
        HANDLE(target._file.as_raw_handle()),
        HANDLE(receipt._file.as_raw_handle()),
        parent,
        HANDLE(log.as_raw_handle()),
    )
    .map_err(|e| format!("TI-UNINSTALL-CLEANUP-004: {e}"))?;
    Ok(PendingUninstallerRemoval {
        _target: target,
        _receipt: receipt,
        _helper: helper,
        _worker: worker,
        _log: log,
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
    fn cleanup_waits_for_parent_signal_and_preserves_neighbours() {
        let fixture = Fixture::new();
        let owned = fixture.0.join("owned.exe");
        let foreign = fixture.0.join("user.txt");
        fs::write(&owned, b"owned fixture").unwrap();
        fs::write(&foreign, b"keep").unwrap();
        let receipt = fixture.0.join("receipt.json");
        fs::write(&receipt, b"receipt").unwrap();
        use std::os::windows::io::FromRawHandle;
        use windows::Win32::System::Threading::{CreateEventW, SetEvent};
        let event = unsafe { CreateEventW(None, true, false, None) }.unwrap();
        let event_owner = unsafe { OwnedHandle::from_raw_handle(event.0) };
        let pending = schedule_removal(
            prepare_removal(&owned, &digest(&owned)).unwrap(),
            prepare_removal(&receipt, &digest(&receipt)).unwrap(),
            event,
        )
        .unwrap();
        assert!(owned.exists());
        assert!(receipt.exists());
        unsafe { SetEvent(event) }.unwrap();
        // Release parent file references, but retain the process handle for verification.
        let worker = pending._worker;
        drop(pending._target);
        drop(pending._receipt);
        drop(pending._helper);
        drop(pending._log);
        wait_worker(&worker);
        drop(event_owner);
        assert!(!owned.exists());
        assert!(!receipt.exists());
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
    fn cleanup_removes_executable_only_after_process_exit() {
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
        let receipt = fixture.0.join("receipt.json");
        fs::write(&receipt, b"receipt").unwrap();
        let pending = schedule_removal(
            prepare_removal(&copy, &expected).unwrap(),
            prepare_removal(&receipt, &digest(&receipt)).unwrap(),
            HANDLE(child.0.as_raw_handle()),
        )
        .unwrap();
        assert!(copy.exists());
        assert!(receipt.exists());
        fs::write(fixture.0.join("finish"), b"exit").unwrap();
        assert!(child.0.wait().unwrap().success());
        let worker = pending._worker;
        drop(pending._target);
        drop(pending._receipt);
        drop(pending._helper);
        drop(pending._log);
        wait_worker(&worker);
        assert!(
            !copy.exists(),
            "uninstaller must be removed after process exit"
        );
        assert!(!receipt.exists());
        assert!(original.exists(), "the parent test executable must survive");
    }

    #[test]
    fn modified_cached_worker_is_never_launched_and_receipts_survive() {
        let fixture = Fixture::new();
        let target = fixture.0.join("owned.exe");
        let receipt = fixture.0.join("receipt.json");
        fs::write(&target, b"target").unwrap();
        fs::write(&receipt, b"receipt").unwrap();
        let cache = fixture.0.join("Cleanup");
        fs::create_dir(&cache).unwrap();
        let name = format!("turkuaz-cleanup-{:x}.exe", Sha256::digest(WORKER_BYTES));
        fs::write(cache.join(name), b"foreign contents").unwrap();
        let result = schedule_removal(
            prepare_removal(&target, &digest(&target)).unwrap(),
            prepare_removal(&receipt, &digest(&receipt)).unwrap(),
            unsafe { windows::Win32::System::Threading::GetCurrentProcess() },
        );
        assert!(result.is_err());
        assert_eq!(fs::read(&target).unwrap(), b"target");
        assert_eq!(fs::read(&receipt).unwrap(), b"receipt");
    }

    fn wait_worker(worker: &OwnedHandle) {
        use windows::Win32::{
            Foundation::WAIT_OBJECT_0,
            System::Threading::{GetExitCodeProcess, WaitForSingleObject},
        };
        assert_eq!(
            unsafe { WaitForSingleObject(HANDLE(worker.as_raw_handle()), 15000) },
            WAIT_OBJECT_0
        );
        let mut code = 99;
        unsafe { GetExitCodeProcess(HANDLE(worker.as_raw_handle()), &mut code) }.unwrap();
        assert_eq!(code, 0, "cleanup worker failed");
    }

    // This is a child-process entry point, exercised by the test above.
    #[test]
    #[ignore = "spawned by cleanup_removes_executable_only_after_process_exit"]
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
