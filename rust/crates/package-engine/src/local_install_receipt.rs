// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/local_install_receipt.rs
// Amac: Imzasiz yerel kurulumun sahip oldugu payload dosyalarini guvenli kaydetmek ve kaldirmak
// Modul - Rust Package Lifecycle Service
// Version: 0.1.5
// Aciklama: Windows handle ile sahipli dosya silme, SHA-256 ve veri koruma
// Bagimli Oldugu Katman: Service | Security

use std::{
    collections::HashSet,
    fs,
    io::{self, Read, Write},
    path::{Component, Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

use crate::{PackageEngineError, validate_archive_path};

/// Name of the owned payload inventory. Consumers may use it for
/// read-only collision checks; they must not infer ownership from its presence.
pub const LOCAL_INSTALL_RECEIPT_FILE_NAME: &str = ".turkuaz-local-install.json";
const RECORD_NAME: &str = LOCAL_INSTALL_RECEIPT_FILE_NAME;
const RECORD_VERSION: u32 = 1;
const MAX_RECORD_BYTES: u64 = 32 * 1024 * 1024;
const MAX_OWNED_FILES: usize = 100_000;

// Windows file sharing/open flags from the platform SDK. Keep the verified
// payload receipt stable while uninstalling and reject reparse files.
#[cfg(windows)]
const WINDOWS_FILE_SHARE_READ: u32 = 0x0000_0001;
#[cfg(windows)]
const WINDOWS_FILE_FLAG_OPEN_REPARSE_POINT: u32 = 0x0020_0000;
#[cfg(all(windows, test))]
const WINDOWS_FILE_SHARE_ALL: u32 = 0x0000_0007;
#[cfg(windows)]
const WINDOWS_FILE_SHARE_DELETE: u32 = 0x0000_0004;
#[cfg(windows)]
const WINDOWS_DELETE_ACCESS: u32 = 0x0001_0000;
#[cfg(windows)]
const WINDOWS_GENERIC_READ_ACCESS: u32 = 0x8000_0000;
#[cfg(windows)]
const WINDOWS_FILE_DISPOSITION_INFO_CLASS: i32 = 4;

#[cfg(windows)]
#[repr(C)]
struct WindowsFileDispositionInfo {
    delete_file: u8,
}

#[cfg(windows)]
#[link(name = "kernel32")]
unsafe extern "system" {
    #[link_name = "SetFileInformationByHandle"]
    fn set_file_information_by_handle(
        file: *mut std::ffi::c_void,
        information_class: i32,
        information: *const std::ffi::c_void,
        information_size: u32,
    ) -> i32;
}

fn invalid(reason: &'static str) -> PackageEngineError {
    PackageEngineError::InvalidPackage(reason)
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct OwnedFile {
    path: String,
    size_bytes: u64,
    sha256: String,
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct InstallRecord {
    format_version: u32,
    package_id: String,
    version: String,
    files: Vec<OwnedFile>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct LocalUninstallSummary {
    pub removed_files: usize,
    pub retained_unknown_files: bool,
}

fn is_reparse(metadata: &fs::Metadata) -> bool {
    #[cfg(windows)]
    {
        use std::os::windows::fs::MetadataExt;
        metadata.file_attributes() & 0x0400 != 0
    }
    #[cfg(not(windows))]
    {
        let _ = metadata;
        false
    }
}

fn safe_regular(path: &Path) -> Result<fs::Metadata, PackageEngineError> {
    let metadata = fs::symlink_metadata(path).map_err(PackageEngineError::Io)?;
    if !metadata.is_file() || metadata.file_type().is_symlink() || is_reparse(&metadata) {
        return Err(invalid("local install entry is not a regular file"));
    }
    Ok(metadata)
}

fn safe_root(path: &Path) -> Result<(), PackageEngineError> {
    if !path.is_absolute()
        || path.components().count() < 3
        || path
            .components()
            .any(|c| matches!(c, Component::ParentDir | Component::CurDir))
    {
        return Err(invalid(
            "local install target must be a non-root absolute path",
        ));
    }
    let mut current = PathBuf::new();
    for part in path.components() {
        current.push(part.as_os_str());
        let meta = match fs::symlink_metadata(&current) {
            Ok(meta) => meta,
            Err(e) if e.kind() == io::ErrorKind::NotFound => return Err(PackageEngineError::Io(e)),
            Err(e) => return Err(PackageEngineError::Io(e)),
        };
        if !meta.is_dir() || meta.file_type().is_symlink() || is_reparse(&meta) {
            return Err(invalid("local install target contains a junction or link"));
        }
    }
    Ok(())
}

fn digest_reader(mut reader: impl Read) -> Result<String, PackageEngineError> {
    let mut hash = Sha256::new();
    let mut block = [0u8; 64 * 1024];
    loop {
        let count = reader.read(&mut block).map_err(PackageEngineError::Io)?;
        if count == 0 {
            break;
        }
        hash.update(&block[..count]);
    }
    Ok(hash
        .finalize()
        .iter()
        .map(|b| format!("{b:02x}"))
        .collect::<String>())
}

fn digest_file(path: &Path) -> Result<String, PackageEngineError> {
    safe_regular(path)?;
    digest_reader(fs::File::open(path).map_err(PackageEngineError::Io)?)
}

fn relative_name(root: &Path, file: &Path) -> Result<String, PackageEngineError> {
    let relative = file
        .strip_prefix(root)
        .map_err(|_| invalid("owned file escaped local install root"))?;
    let mut parts = Vec::new();
    for part in relative.components() {
        let Component::Normal(name) = part else {
            return Err(invalid("owned file path is not relative"));
        };
        parts.push(
            name.to_str()
                .ok_or(invalid("owned file name must be Unicode"))?,
        );
    }
    let text = parts.join("/");
    validate_archive_path(&text)?;
    if text.eq_ignore_ascii_case(RECORD_NAME) {
        return Err(invalid("payload must not contain reserved install receipt"));
    }
    Ok(text)
}

/// @dev-id BRK-LOCAL-RECEIPT-001
/// @purpose Record a verified package's exact staged files before publishing.
/// @contract Receipt exists only for new installs; no overwrites or foreign files.
pub(crate) fn write_local_install_record(
    stage: &Path,
    package_id: &str,
    version: &str,
) -> Result<(), PackageEngineError> {
    let metadata = fs::symlink_metadata(stage).map_err(PackageEngineError::Io)?;
    if !metadata.is_dir() || metadata.file_type().is_symlink() || is_reparse(&metadata) {
        return Err(invalid("local install stage is unsafe"));
    }
    let mut directories = vec![stage.to_path_buf()];
    let mut files = Vec::new();
    while let Some(dir) = directories.pop() {
        for entry in fs::read_dir(dir).map_err(PackageEngineError::Io)? {
            let entry = entry.map_err(PackageEngineError::Io)?;
            let path = entry.path();
            let meta = fs::symlink_metadata(&path).map_err(PackageEngineError::Io)?;
            if meta.file_type().is_symlink() || is_reparse(&meta) {
                return Err(invalid("local install contains a link or junction"));
            }
            if meta.is_dir() {
                directories.push(path);
            } else if meta.is_file() {
                if files.len() >= MAX_OWNED_FILES {
                    return Err(invalid("local install receipt file limit exceeded"));
                }
                let name = relative_name(stage, &path)?;
                files.push(OwnedFile {
                    path: name,
                    size_bytes: meta.len(),
                    sha256: digest_file(&path)?,
                });
            } else {
                return Err(invalid("local install contains a special file"));
            }
        }
    }
    if files.is_empty() {
        return Err(invalid("local install has no regular payload files"));
    }
    files.sort_by(|a, b| a.path.cmp(&b.path));
    let record = InstallRecord {
        format_version: RECORD_VERSION,
        package_id: package_id.to_owned(),
        version: version.to_owned(),
        files,
    };
    let data = serde_json::to_vec(&record)
        .map_err(|_| invalid("local install receipt could not be serialized"))?;
    if data.len() as u64 > MAX_RECORD_BYTES {
        return Err(invalid("local install receipt exceeds size limit"));
    }
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(stage.join(RECORD_NAME))
        .map_err(PackageEngineError::Io)?;
    output.write_all(&data).map_err(PackageEngineError::Io)?;
    output.sync_all().map_err(PackageEngineError::Io)
}

/// @dev-id BRK-LOCAL-RECEIPT-003
/// @purpose Pin a local payload receipt without following Windows reparse links.
/// @contract On Windows, deny concurrent write/delete access while owned files
/// are checked and removed; refuse an existing writer before any deletion.
/// @tests local_install_receipt::tests::concurrent_receipt_writer_blocks_uninstall
fn open_local_receipt(path: &Path) -> Result<fs::File, PackageEngineError> {
    // Retain the cross-platform no-symlink policy. On Windows, the
    // checked open handle adds protection against replacement after this.
    safe_regular(path)?;
    let mut options = fs::OpenOptions::new();
    options.read(true);
    #[cfg(windows)]
    {
        use std::os::windows::fs::OpenOptionsExt;
        options
            .share_mode(WINDOWS_FILE_SHARE_READ)
            .custom_flags(WINDOWS_FILE_FLAG_OPEN_REPARSE_POINT);
    }
    let file = options.open(path).map_err(PackageEngineError::Io)?;
    let meta = file.metadata().map_err(PackageEngineError::Io)?;
    if !meta.is_file() || meta.file_type().is_symlink() || is_reparse(&meta) {
        return Err(invalid("local install receipt is not a regular file"));
    }
    Ok(file)
}

fn checked_record(
    target: &Path,
    expected_id: &str,
) -> Result<(InstallRecord, fs::File), PackageEngineError> {
    safe_root(target)?;
    let path = target.join(RECORD_NAME);
    let mut receipt_handle = open_local_receipt(&path)?;
    let metadata = receipt_handle.metadata().map_err(PackageEngineError::Io)?;
    if metadata.len() == 0 || metadata.len() > MAX_RECORD_BYTES {
        return Err(invalid("local install receipt size invalid"));
    }
    let mut raw = Vec::with_capacity(metadata.len() as usize);
    (&mut receipt_handle)
        .take(MAX_RECORD_BYTES + 1)
        .read_to_end(&mut raw)
        .map_err(PackageEngineError::Io)?;
    if raw.len() as u64 > MAX_RECORD_BYTES {
        return Err(invalid("local install receipt grew during read"));
    }
    let record: InstallRecord =
        serde_json::from_slice(&raw).map_err(|_| invalid("local install receipt JSON invalid"))?;
    if record.format_version != RECORD_VERSION
        || record.package_id != expected_id
        || record.files.is_empty()
        || record.files.len() > MAX_OWNED_FILES
        || record.version.is_empty()
    {
        return Err(invalid(
            "local install receipt identity or version mismatch",
        ));
    }
    let mut seen = HashSet::new();
    for entry in &record.files {
        let relative = validate_archive_path(&entry.path)?;
        if entry.path.eq_ignore_ascii_case(RECORD_NAME)
            || relative.components().count() == 0
            || !seen.insert(entry.path.to_lowercase())
            || entry.sha256.len() != 64
            || !entry.sha256.bytes().all(|b| b.is_ascii_hexdigit())
        {
            return Err(invalid("local install receipt contains invalid file entry"));
        }
    }
    Ok((record, receipt_handle))
}

fn checked_file_path(target: &Path, item: &OwnedFile) -> Result<PathBuf, PackageEngineError> {
    let relative = validate_archive_path(&item.path)?;
    let full = target.join(relative);
    let mut parent = full.parent();
    while let Some(dir) = parent {
        if dir == target {
            break;
        }
        let meta = fs::symlink_metadata(dir).map_err(PackageEngineError::Io)?;
        if !meta.is_dir() || meta.file_type().is_symlink() || is_reparse(&meta) {
            return Err(invalid("local install receipt traverses a directory link"));
        }
        parent = dir.parent();
    }
    Ok(full)
}

fn preflight_file(target: &Path, item: &OwnedFile) -> Result<(), PackageEngineError> {
    let full = checked_file_path(target, item)?;
    match fs::symlink_metadata(&full) {
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(()),
        Err(error) => Err(PackageEngineError::Io(error)),
        Ok(meta) => {
            if !meta.is_file() || meta.file_type().is_symlink() || is_reparse(&meta) {
                return Err(invalid(
                    "local install file was replaced with a special file",
                ));
            }
            if meta.len() != item.size_bytes || digest_file(&full)? != item.sha256 {
                return Err(invalid("local install file was changed; uninstall refused"));
            }
            Ok(())
        }
    }
}

/// @dev-id BRK-LOCAL-WIN-DELETE-001
/// @purpose Delete a verified Windows file object, never its replacement path.
/// @contract Open with DELETE+read access and no write sharing; reject reparse
/// files and SHA-256 mismatch; mark only the verified handle for deletion.
/// @tests local_install_receipt::tests::windows_verified_handle_preserves_replacement
#[cfg(windows)]
fn open_verified_windows_file(
    path: &Path,
    item: &OwnedFile,
) -> Result<Option<fs::File>, PackageEngineError> {
    use std::os::windows::fs::OpenOptionsExt;

    let mut options = fs::OpenOptions::new();
    options
        .access_mode(WINDOWS_GENERIC_READ_ACCESS | WINDOWS_DELETE_ACCESS)
        .share_mode(WINDOWS_FILE_SHARE_READ | WINDOWS_FILE_SHARE_DELETE)
        .custom_flags(WINDOWS_FILE_FLAG_OPEN_REPARSE_POINT);
    let mut handle = match options.open(path) {
        Ok(file) => file,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(None),
        Err(error) => return Err(PackageEngineError::Io(error)),
    };
    let meta = handle.metadata().map_err(PackageEngineError::Io)?;
    if !meta.is_file() || meta.file_type().is_symlink() || is_reparse(&meta) {
        return Err(invalid("local install file is not a regular file"));
    }
    if meta.len() != item.size_bytes || digest_reader(&mut handle)? != item.sha256 {
        return Err(invalid("local install file was changed; uninstall refused"));
    }
    Ok(Some(handle))
}

#[cfg(windows)]
fn remove_verified_windows_handle(handle: fs::File) -> Result<(), PackageEngineError> {
    use std::os::windows::io::AsRawHandle;

    let disposition = WindowsFileDispositionInfo { delete_file: 1 };
    // SAFETY: File owns a DELETE-capable handle. The repr(C) buffer
    // is valid for the duration of this synchronous Win32 call.
    let status = unsafe {
        set_file_information_by_handle(
            handle.as_raw_handle(),
            WINDOWS_FILE_DISPOSITION_INFO_CLASS,
            (&raw const disposition).cast(),
            std::mem::size_of::<WindowsFileDispositionInfo>() as u32,
        )
    };
    if status == 0 {
        return Err(PackageEngineError::Io(io::Error::last_os_error()));
    }
    drop(handle);
    Ok(())
}

/// @dev-id BRK-LOCAL-RECEIPT-002
/// @purpose Remove only unchanged package-owned files, preserve everything else.
/// @contract No receipt => no deletion; changed files => abort before deletion;
/// file-by-file check and no symlink traversal. Only empty folders are removed.
/// Note: local unsigned receipt is not cryptographically authenticated.
pub fn uninstall_local_install(
    target: impl AsRef<Path>,
    expected_package_id: &str,
) -> Result<LocalUninstallSummary, PackageEngineError> {
    let target = target.as_ref();
    // Hold the inventory read handle through owned-file removal so Windows
    // writers cannot change the receipt after initial validation.
    let (record, receipt_handle) = checked_record(target, expected_package_id)?;
    // Preflight all entries so a changed file fails without partial deletion.
    for item in &record.files {
        preflight_file(target, item)?;
    }
    let mut removed = 0;
    let mut dirs = HashSet::new();
    for item in &record.files {
        // Recheck ancestors before opening the exact file object.
        let file = checked_file_path(target, item)?;
        #[cfg(windows)]
        let removed_this = match open_verified_windows_file(&file, item)? {
            Some(handle) => {
                remove_verified_windows_handle(handle)?;
                true
            }
            None => false,
        };
        #[cfg(not(windows))]
        let removed_this = {
            preflight_file(target, item)?;
            match fs::symlink_metadata(&file) {
                Ok(_) => {
                    fs::remove_file(&file).map_err(PackageEngineError::Io)?;
                    true
                }
                Err(error) if error.kind() == io::ErrorKind::NotFound => false,
                Err(error) => return Err(PackageEngineError::Io(error)),
            }
        };
        if removed_this {
            removed += 1;
        }
        let mut parent = file.parent();
        while let Some(dir) = parent {
            if dir == target {
                break;
            }
            dirs.insert(dir.to_path_buf());
            parent = dir.parent();
        }
    }
    let receipt = target.join(RECORD_NAME);
    safe_regular(&receipt)?;
    drop(receipt_handle);
    fs::remove_file(receipt).map_err(PackageEngineError::Io)?;
    let mut dirs: Vec<_> = dirs.into_iter().collect();
    dirs.sort_by_key(|dir| std::cmp::Reverse(dir.components().count()));
    for dir in dirs {
        let _ = fs::remove_dir(dir); // only empty directories disappear
    }
    let retained_unknown_files = match fs::remove_dir(target) {
        Ok(()) => false,
        Err(error) if error.kind() == io::ErrorKind::DirectoryNotEmpty => true,
        Err(error) => return Err(PackageEngineError::Io(error)),
    };
    Ok(LocalUninstallSummary {
        removed_files: removed,
        retained_unknown_files,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    fn fixture() -> (PathBuf, PathBuf) {
        let base = std::env::temp_dir().join(format!("turkuaz-owned-{}", Uuid::new_v4()));
        let target = base.join("kurulu uygulama");
        fs::create_dir_all(target.join("bin")).expect("create nested test folder");
        fs::write(target.join("bin/hello.txt"), b"trusted contents").expect("fixture");
        write_local_install_record(&target, "qa.native", "1.0.0").expect("inventory");
        (base, target)
    }

    #[test]
    fn records_and_removes_only_owned_files() {
        let (base, target) = fixture();
        fs::write(target.join("user-notes.txt"), b"never delete").expect("user file");
        let result = uninstall_local_install(&target, "qa.native").expect("uninstall");
        assert_eq!(result.removed_files, 1);
        assert!(result.retained_unknown_files);
        assert!(!target.join("bin/hello.txt").exists());
        assert_eq!(
            fs::read(target.join("user-notes.txt")).unwrap(),
            b"never delete"
        );
        fs::remove_dir_all(base).expect("cleanup");
    }

    #[test]
    fn modified_files_and_unknown_installs_cannot_be_removed() {
        let (base, target) = fixture();
        fs::write(target.join("bin/hello.txt"), b"user modified").expect("modify");
        assert!(uninstall_local_install(&target, "qa.native").is_err());
        assert!(target.join("bin/hello.txt").exists());
        assert!(uninstall_local_install(&target, "other.package").is_err());
        assert!(target.join(RECORD_NAME).exists());
        fs::remove_dir_all(base).expect("cleanup");
    }

    #[cfg(windows)]
    #[test]
    fn windows_verified_handle_preserves_replacement() {
        let (base, target) = fixture();
        let original = target.join("bin/hello.txt");
        let record = checked_record(&target, "qa.native")
            .expect("validated inventory")
            .0;
        let verified = open_verified_windows_file(&original, &record.files[0])
            .expect("verified file open")
            .expect("owned file present");
        let moved = target.join("bin/renamed-owned.txt");
        fs::rename(&original, &moved).expect("rename while verified handle is open");
        fs::write(&original, b"unrelated user data").expect("new path occupant");
        remove_verified_windows_handle(verified).expect("delete only the original file");
        assert!(!moved.exists(), "original object must be deleted");
        assert_eq!(
            fs::read(&original).expect("new occupant preserved"),
            b"unrelated user data"
        );
        fs::remove_dir_all(base).expect("cleanup isolated fixture");
    }

    #[cfg(unix)]
    #[test]
    fn receipt_symlink_is_refused_and_foreign_bytes_survive() {
        use std::os::unix::fs::symlink;

        let (base, target) = fixture();
        let receipt = target.join(RECORD_NAME);
        let original = fs::read(&receipt).expect("read isolated fixture inventory");
        let foreign = base.join("foreign-inventory.json");
        fs::write(&foreign, &original).expect("create external inventory");
        fs::remove_file(&receipt).expect("replace receipt with symlink");
        symlink(&foreign, &receipt).expect("create symlink receipt");
        assert!(
            uninstall_local_install(&target, "qa.native").is_err(),
            "linked receipt must not authorize payload removal"
        );
        assert!(target.join("bin/hello.txt").exists());
        assert_eq!(
            fs::read(&foreign).expect("foreign bytes preserved"),
            original
        );
        fs::remove_dir_all(base).expect("remove isolated fixture");
    }

    #[cfg(windows)]
    #[test]
    fn concurrent_receipt_writer_blocks_uninstall() {
        use std::os::windows::fs::OpenOptionsExt;

        let (base, target) = fixture();
        let receipt = target.join(RECORD_NAME);
        let writer = fs::OpenOptions::new()
            .write(true)
            .share_mode(WINDOWS_FILE_SHARE_ALL)
            .open(&receipt)
            .expect("open writable test receipt");
        assert!(
            uninstall_local_install(&target, "qa.native").is_err(),
            "concurrently writable receipt cannot authorize removal"
        );
        assert!(
            target.join("bin/hello.txt").exists(),
            "payload must survive a contested inventory"
        );
        assert!(receipt.exists(), "receipt must remain");
        drop(writer);
        let removed =
            uninstall_local_install(&target, "qa.native").expect("uninstall after writer closes");
        assert_eq!(removed.removed_files, 1);
        assert!(!removed.retained_unknown_files);
        assert!(!receipt.exists());
        fs::remove_dir(base).expect("remove test root");
    }

    #[test]
    fn forged_escape_in_receipt_is_rejected() {
        let (base, target) = fixture();
        let receipt = target.join(RECORD_NAME);
        let mut record: InstallRecord =
            serde_json::from_slice(&fs::read(&receipt).unwrap()).unwrap();
        record.files[0].path = "../outside.txt".into();
        fs::write(receipt, serde_json::to_vec(&record).unwrap()).expect("test mutation");
        assert!(uninstall_local_install(&target, "qa.native").is_err());
        assert!(target.join("bin/hello.txt").exists());
        fs::remove_dir_all(base).expect("cleanup");
    }
}
