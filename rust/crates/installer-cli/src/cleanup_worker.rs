// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/cleanup_worker.rs
// Amac: Ust surec bittikten sonra yalniz aktarilan dosya taniticilarini temizlemek
// Modul - Rust Windows Cleanup Worker
// Version: 0.1.0
// Aciklama: Dosya yolu/komut kabul etmez; parent, EXE, envanter ve log handle kullanir
// Bagimli Oldugu Katman: Adapter | Security
#![windows_subsystem = "windows"]

use std::{
    ffi::c_void,
    fs::File,
    io::Write,
    os::windows::io::{AsRawHandle, FromRawHandle},
    thread,
    time::Duration,
};

#[link(name = "kernel32")]
unsafe extern "system" {
    fn WaitForSingleObject(handle: *mut c_void, milliseconds: u32) -> u32;
    fn SetFileInformationByHandle(
        handle: *mut c_void,
        class: u32,
        info: *const c_void,
        size: u32,
    ) -> i32;
    fn CloseHandle(handle: *mut c_void) -> i32;
}

fn delete_verified(file: &File) -> std::io::Result<()> {
    // FILE_DISPOSITION_INFO / FileDispositionInfo: BOOLEAN DeleteFile, class 4.
    // Parent process has exited, so no executable image mapping remains.
    let delete: u8 = 1;
    for attempt in 0..100 {
        // SAFETY: File owns an inherited DELETE-capable handle; buffer is a BOOLEAN.
        if unsafe {
            SetFileInformationByHandle(
                file.as_raw_handle(),
                4,
                &delete as *const _ as *const c_void,
                1,
            )
        } != 0
        {
            return Ok(());
        }
        let error = std::io::Error::last_os_error();
        if attempt == 99 {
            return Err(error);
        }
        thread::sleep(Duration::from_millis(50));
    }
    unreachable!()
}

fn main() {
    let args: Vec<_> = std::env::args().skip(1).collect();
    if args.len() != 4 {
        std::process::exit(2);
    }
    let parsed: Option<Vec<usize>> = args
        .iter()
        .map(|s| {
            s.parse::<usize>()
                .ok()
                .filter(|n| *n > 0 && *n <= isize::MAX as usize)
        })
        .collect();
    let Some(handles) = parsed else {
        std::process::exit(2);
    };
    if handles
        .iter()
        .enumerate()
        .any(|(i, h)| handles[..i].contains(h))
    {
        std::process::exit(2);
    }
    // SAFETY: launcher transfers exactly these four handles using HANDLE_LIST.
    // No paths, shell text, PID lookup or directory traversal occurs here.
    let target = unsafe { File::from_raw_handle(handles[0] as *mut c_void) };
    let receipt = unsafe { File::from_raw_handle(handles[1] as *mut c_void) };
    let parent = handles[2] as *mut c_void;
    let mut log = unsafe { File::from_raw_handle(handles[3] as *mut c_void) };
    // SAFETY: inherited real wait handle; never a pseudo handle or PID.
    let waited = unsafe { WaitForSingleObject(parent, u32::MAX) };
    unsafe {
        CloseHandle(parent);
    }
    let result = if waited != 0 {
        Err(format!(
            "TI-UNINSTALL-CLEANUP-004: parent wait failed: {waited}"
        ))
    } else {
        delete_verified(&target)
            .map_err(|e| format!("TI-UNINSTALL-CLEANUP-003: EXE preserved: {e}"))
    };
    if let Err(error) = result {
        let _ = writeln!(log, "{error}");
        let _ = log.sync_all();
        std::process::exit(3);
    }
    drop(target);
    if let Err(error) = delete_verified(&receipt) {
        let _ = writeln!(log, "TI-UNINSTALL-CLEANUP-003: receipt preserved: {error}");
        let _ = log.sync_all();
        std::process::exit(4);
    }
    drop(receipt);
    let _ = writeln!(
        log,
        "TI-UNINSTALL-CLEANUP-OK: executable and receipt removed after parent exit"
    );
    let _ = log.sync_all();
}
