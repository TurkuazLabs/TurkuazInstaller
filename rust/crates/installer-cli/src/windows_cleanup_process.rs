// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_cleanup_process.rs
// Amac: Rust temizleyiciye yalniz gerekli yerel taniticilari aktarmak
// Modul - Rust Windows Process Adapter
// Version: 0.1.0
// Aciklama: Acik HANDLE_LIST, sinirli erisim, shell olmadan dogrudan CreateProcessW
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    os::windows::{
        ffi::OsStrExt,
        io::{AsRawHandle, FromRawHandle, OwnedHandle},
    },
    path::Path,
};
use windows::{
    Win32::{
        Foundation::{DUPLICATE_HANDLE_OPTIONS, DuplicateHandle, HANDLE},
        Storage::FileSystem::{DELETE, FILE_GENERIC_WRITE},
        System::Threading::*,
    },
    core::{PCWSTR, PWSTR},
};

fn duplicate(handle: HANDLE, access: u32) -> Result<OwnedHandle, String> {
    let mut inherited = HANDLE::default();
    // SAFETY: duplicate into this process; the resulting handle has one RAII owner.
    unsafe {
        DuplicateHandle(
            GetCurrentProcess(),
            handle,
            GetCurrentProcess(),
            &mut inherited,
            access,
            true,
            DUPLICATE_HANDLE_OPTIONS(0),
        )
    }
    .map_err(|e| e.to_string())?;
    Ok(unsafe { OwnedHandle::from_raw_handle(inherited.0) })
}

struct Attributes {
    list: LPPROC_THREAD_ATTRIBUTE_LIST,
    _storage: Vec<usize>,
}
impl Drop for Attributes {
    fn drop(&mut self) {
        unsafe {
            DeleteProcThreadAttributeList(self.list);
        }
    }
}

/// @dev-id TI-UNINSTALL-CLEANUP-004
/// @contract Four explicit inherited handles, no environment commands or path arguments.
pub(super) fn spawn_worker(
    executable: &Path,
    target: HANDLE,
    receipt: HANDLE,
    parent: HANDLE,
    log: HANDLE,
) -> Result<OwnedHandle, String> {
    let handles = [
        duplicate(target, DELETE.0)?,
        duplicate(receipt, DELETE.0)?,
        duplicate(parent, PROCESS_SYNCHRONIZE.0)?,
        duplicate(log, FILE_GENERIC_WRITE.0)?,
    ];
    let raw: Vec<HANDLE> = handles.iter().map(|h| HANDLE(h.as_raw_handle())).collect();
    let mut size = 0;
    // SAFETY: documented sizing call, followed by aligned owned storage.
    let _ = unsafe { InitializeProcThreadAttributeList(None, 1, None, &mut size) };
    if size == 0 {
        return Err("attribute list size missing".into());
    }
    let mut storage = vec![0usize; size.div_ceil(std::mem::size_of::<usize>())];
    let list = LPPROC_THREAD_ATTRIBUTE_LIST(storage.as_mut_ptr().cast());
    unsafe { InitializeProcThreadAttributeList(Some(list), 1, None, &mut size) }
        .map_err(|e| e.to_string())?;
    let attributes = Attributes {
        list,
        _storage: storage,
    };
    unsafe {
        UpdateProcThreadAttribute(
            attributes.list,
            0,
            PROC_THREAD_ATTRIBUTE_HANDLE_LIST as usize,
            Some(raw.as_ptr().cast()),
            std::mem::size_of_val(raw.as_slice()),
            None,
            None,
        )
    }
    .map_err(|e| e.to_string())?;
    let mut startup = STARTUPINFOEXW::default();
    startup.StartupInfo.cb = std::mem::size_of::<STARTUPINFOEXW>() as u32;
    startup.lpAttributeList = attributes.list;
    let application: Vec<u16> = executable
        .as_os_str()
        .encode_wide()
        .chain(Some(0))
        .collect();
    // argv[0] is constant; executable is supplied separately, never interpreted by a shell.
    let command = format!(
        "turkuaz-cleanup-worker {} {} {} {}",
        raw[0].0 as usize, raw[1].0 as usize, raw[2].0 as usize, raw[3].0 as usize
    );
    let mut command: Vec<u16> = OsStr::new(&command).encode_wide().chain(Some(0)).collect();
    let mut process = PROCESS_INFORMATION::default();
    // SAFETY: application, command, startup, attribute storage and handle list stay
    // alive through synchronous CreateProcessW. Only explicitly listed handles inherit.
    unsafe {
        CreateProcessW(
            PCWSTR(application.as_ptr()),
            Some(PWSTR(command.as_mut_ptr())),
            None,
            None,
            true,
            CREATE_NO_WINDOW | EXTENDED_STARTUPINFO_PRESENT,
            None,
            PCWSTR::null(),
            &startup.StartupInfo,
            &mut process,
        )
    }
    .map_err(|e| e.to_string())?;
    let thread = unsafe { OwnedHandle::from_raw_handle(process.hThread.0) };
    drop(thread);
    Ok(unsafe { OwnedHandle::from_raw_handle(process.hProcess.0) })
}
