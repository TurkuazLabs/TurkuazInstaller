// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_documents.rs
// Amac: Kullaniciya ait gercek Belgeler klasorunu Windows Known Folder API ile bulmak
// Modul - Rust Windows Platform Adapter
// Version: 0.1.0
// Aciklama: OneDrive/redirection destekli FOLDERID_Documents konumunu doner
// Bagimli Oldugu Katman: Tool | Platform

use std::{ffi::OsString, os::windows::ffi::OsStringExt, path::PathBuf};

#[repr(C)]
struct Guid {
    data1: u32,
    data2: u16,
    data3: u16,
    data4: [u8; 8],
}

const DOCUMENTS_ID: Guid = Guid {
    data1: 0xfdd39ad0,
    data2: 0x238f,
    data3: 0x46af,
    data4: [0xad, 0xb4, 0x6c, 0x85, 0x48, 0x03, 0x69, 0xc7],
};
const COINIT_APARTMENTTHREADED: u32 = 2;
const RPC_E_CHANGED_MODE: i32 = -2147417850;

#[link(name = "ole32")]
unsafe extern "system" {
    fn CoInitializeEx(reserved: *mut core::ffi::c_void, coinit: u32) -> i32;
    fn CoUninitialize();
    fn CoTaskMemFree(memory: *mut core::ffi::c_void);
}

#[link(name = "shell32")]
unsafe extern "system" {
    fn SHGetKnownFolderPath(
        folder: *const Guid,
        flags: u32,
        token: *mut core::ffi::c_void,
        result: *mut *mut u16,
    ) -> i32;
}

// BRK-STUDIO-DOCUMENTS-001: Windows Documents may be redirected by OneDrive
// or group policy. Never infer it by appending Documents to USERPROFILE.
pub(super) fn documents_path() -> Result<PathBuf, String> {
    // SAFETY: COM initialization is balanced below. An existing MTA is valid.
    let initialized = unsafe {
        CoInitializeEx(core::ptr::null_mut(), COINIT_APARTMENTTHREADED)
    };
    if initialized < 0 && initialized != RPC_E_CHANGED_MODE {
        return Err(format!("Belgeler klasörü COM başlatılamadı: {initialized:#x}"));
    }
    let owns_com = initialized >= 0;
    let mut pointer: *mut u16 = core::ptr::null_mut();
    // SAFETY: DOCUMENTS_ID is the known Windows GUID, the output is writable.
    let result = unsafe {
        SHGetKnownFolderPath(&DOCUMENTS_ID, 0, core::ptr::null_mut(), &mut pointer)
    };
    let outcome = if result == 0 && !pointer.is_null() {
        let mut length = 0usize;
        // SAFETY: The Shell API returns a NUL-terminated UTF-16 string.
        unsafe {
            while length < 32767 && *pointer.add(length) != 0 {
                length += 1;
            }
        }
        if length >= 32767 {
            Err("Windows Belgeler klasörü yolu beklenenden uzun.".to_owned())
        } else {
            // SAFETY: This slice is valid for the Shell-allocated string.
            let value = unsafe { std::slice::from_raw_parts(pointer, length) };
            Ok(PathBuf::from(OsString::from_wide(value)))
        }
    } else {
        Err(format!(
            "Windows Belgeler klasörü bulunamadı ({result:#x}). Farklı kaydet kullanın."
        ))
    };
    // SAFETY: Windows documents SHGetKnownFolderPath allocation uses CoTaskMemFree.
    unsafe {
        if !pointer.is_null() {
            CoTaskMemFree(pointer.cast());
        }
        if owns_com {
            CoUninitialize();
        }
    }
    outcome
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn documents_folder_is_an_absolute_windows_path() {
        let documents = documents_path().expect("Windows Known Folder Documents");
        assert!(documents.is_absolute());
    }
}
