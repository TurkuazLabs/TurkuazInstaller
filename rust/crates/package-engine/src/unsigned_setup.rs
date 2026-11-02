// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/unsigned_setup.rs
// Amac: NSIS olmadan yalniz yerel standart kurulum icin .tzpkg gomulu EXE uretmek
// Modul - Rust Package Engine
// Version: 0.1.0
// Aciklama: Imzasiz yerel EXE butunluk kontrolu; imzali release/trust asla atlanmaz
// Bagimli Oldugu Katman: Service | Tool

use std::{
    fs,
    io::{self, Read, Seek, SeekFrom, Write},
    path::Path,
};

use sha2::{Digest, Sha256};

use crate::PackageEngineError;

const MAGIC: [u8; 8] = *b"TZLOCAL1";
const FOOTER_BYTES: u64 = 8 + 8 + 32;
const MAXIMUM_PACKAGE_BYTES: u64 = 32 * 1024 * 1024 * 1024;
const COPY_BYTES: usize = 64 * 1024;

fn invalid(message: &'static str) -> PackageEngineError {
    PackageEngineError::InvalidPackage(message)
}

fn regular_file(path: &Path) -> Result<fs::Metadata, PackageEngineError> {
    let metadata = fs::symlink_metadata(path).map_err(PackageEngineError::Io)?;
    if metadata.file_type().is_symlink() || !metadata.is_file() {
        return Err(invalid("local setup files must be regular files"));
    }
    Ok(metadata)
}

fn footer(file: &mut fs::File) -> Result<Option<(u64, [u8; 32])>, PackageEngineError> {
    let len = file.metadata().map_err(PackageEngineError::Io)?.len();
    if len < FOOTER_BYTES {
        return Ok(None);
    }
    file.seek(SeekFrom::End(-(FOOTER_BYTES as i64)))
        .map_err(PackageEngineError::Io)?;
    let mut magic = [0; 8];
    file.read_exact(&mut magic).map_err(PackageEngineError::Io)?;
    if magic != MAGIC {
        return Ok(None);
    }
    let mut encoded = [0; 8];
    file.read_exact(&mut encoded).map_err(PackageEngineError::Io)?;
    let package_length = u64::from_le_bytes(encoded);
    if package_length == 0
        || package_length > MAXIMUM_PACKAGE_BYTES
        || package_length >= len - FOOTER_BYTES
    {
        return Err(invalid("local setup package length is invalid"));
    }
    let mut digest = [0; 32];
    file.read_exact(&mut digest).map_err(PackageEngineError::Io)?;
    Ok(Some((package_length, digest)))
}

fn copy_hashed<R: Read, W: Write>(
    from: &mut R,
    to: &mut W,
    total: u64,
) -> Result<[u8; 32], PackageEngineError> {
    let mut hasher = Sha256::new();
    let mut left = total;
    let mut bytes = [0; COPY_BYTES];
    while left > 0 {
        let limit = left.min(bytes.len() as u64) as usize;
        let count = from.read(&mut bytes[..limit]).map_err(PackageEngineError::Io)?;
        if count == 0 {
            return Err(invalid("local setup input was truncated"));
        }
        to.write_all(&bytes[..count]).map_err(PackageEngineError::Io)?;
        hasher.update(&bytes[..count]);
        left -= count as u64;
    }
    Ok(hasher.finalize().into())
}

/// @dev-id BRK-LOCAL-SETUP-001
/// @purpose Create a self-contained unsigned Windows setup, not a signed update installer.
/// @contract MZ stub and .tzpkg input, create-new output, bounded copy and SHA-256 footer.
pub fn create_unsigned_local_setup(
    stub: impl AsRef<Path>,
    package: impl AsRef<Path>,
    output: impl AsRef<Path>,
) -> Result<(), PackageEngineError> {
    let stub = stub.as_ref();
    let package = package.as_ref();
    let output = output.as_ref();
    let stub_len = regular_file(stub)?.len();
    let package_len = regular_file(package)?.len();
    if stub_len < 2 || package_len == 0 || package_len > MAXIMUM_PACKAGE_BYTES {
        return Err(invalid("local setup stub or package size is invalid"));
    }
    if package.extension().is_none_or(|x| !x.eq_ignore_ascii_case("tzpkg"))
        || output.extension().is_none_or(|x| !x.eq_ignore_ascii_case("exe"))
    {
        return Err(invalid("local setup requires .tzpkg input and .exe output"));
    }
    let mut stub_reader = fs::File::open(stub).map_err(PackageEngineError::Io)?;
    let mut header = [0; 2];
    stub_reader.read_exact(&mut header).map_err(PackageEngineError::Io)?;
    if header != *b"MZ" || footer(&mut stub_reader)?.is_some() {
        return Err(invalid("local setup stub must be a plain Windows executable"));
    }
    stub_reader.seek(SeekFrom::Start(0)).map_err(PackageEngineError::Io)?;
    let mut pkg_reader = fs::File::open(package).map_err(PackageEngineError::Io)?;
    let mut out = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)
        .map_err(PackageEngineError::Io)?;
    let result = (|| {
        io::copy(&mut stub_reader, &mut out).map_err(PackageEngineError::Io)?;
        let digest = copy_hashed(&mut pkg_reader, &mut out, package_len)?;
        out.write_all(&MAGIC).map_err(PackageEngineError::Io)?;
        out.write_all(&package_len.to_le_bytes()).map_err(PackageEngineError::Io)?;
        out.write_all(&digest).map_err(PackageEngineError::Io)?;
        out.sync_all().map_err(PackageEngineError::Io)?;
        Ok(())
    })();
    if result.is_err() {
        drop(out);
        let _ = fs::remove_file(output);
    }
    result
}

/// @dev-id BRK-LOCAL-SETUP-002
/// @purpose Recognize the unsigned-local marker separately from signed .tzsetup.
pub fn has_unsigned_local_setup(path: impl AsRef<Path>) -> Result<bool, PackageEngineError> {
    regular_file(path.as_ref())?;
    let mut file = fs::File::open(path).map_err(PackageEngineError::Io)?;
    Ok(footer(&mut file)?.is_some())
}

/// @dev-id BRK-LOCAL-SETUP-003
/// @purpose Extract the locally embedded archive with exact length and SHA-256.
/// @contract Integrity only, never a publisher authenticity or trusted update check.
pub fn extract_unsigned_local_package(
    executable: impl AsRef<Path>,
    output: impl AsRef<Path>,
) -> Result<(), PackageEngineError> {
    let executable = executable.as_ref();
    let output = output.as_ref();
    let metadata = regular_file(executable)?;
    if output.extension().is_none_or(|x| !x.eq_ignore_ascii_case("tzpkg")) {
        return Err(invalid("unsigned local payload output must be .tzpkg"));
    }
    let mut input = fs::File::open(executable).map_err(PackageEngineError::Io)?;
    let (pkg_len, expected) = footer(&mut input)?.ok_or(invalid(
        "executable is not an unsigned local setup",
    ))?;
    let start = metadata
        .len()
        .checked_sub(FOOTER_BYTES + pkg_len)
        .ok_or(invalid("unsigned local setup offsets are invalid"))?;
    input.seek(SeekFrom::Start(start)).map_err(PackageEngineError::Io)?;
    let mut out = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)
        .map_err(PackageEngineError::Io)?;
    let result = (|| {
        let actual = copy_hashed(&mut input, &mut out, pkg_len)?;
        if actual != expected {
            return Err(invalid("unsigned local setup payload SHA-256 mismatch"));
        }
        out.sync_all().map_err(PackageEngineError::Io)?;
        Ok(())
    })();
    if result.is_err() {
        drop(out);
        let _ = fs::remove_file(output);
    }
    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    #[test]
    fn local_setup_roundtrip_rejects_corruption_and_overwrites() {
        let root = std::env::temp_dir().join(format!("tz-native-test-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("test directory");
        let stub = root.join("stub.exe");
        let package = root.join("app.tzpkg");
        let installer = root.join("setup.exe");
        let extracted = root.join("unpacked.tzpkg");
        fs::write(&stub, b"MZ-example-rust-binary").expect("stub");
        fs::write(&package, b"local-package-example").expect("package");
        create_unsigned_local_setup(&stub, &package, &installer).expect("create");
        assert!(has_unsigned_local_setup(&installer).expect("marker"));
        extract_unsigned_local_package(&installer, &extracted).expect("extract");
        assert_eq!(fs::read(&extracted).expect("read"), fs::read(&package).expect("original"));
        assert!(create_unsigned_local_setup(&stub, &package, &installer).is_err());
        fs::remove_file(&extracted).expect("remove extracted");
        let mut bytes = fs::read(&installer).expect("read installer");
        let i = bytes.len() - FOOTER_BYTES as usize - 1;
        bytes[i] ^= 1;
        fs::write(&installer, bytes).expect("tamper test fixture");
        assert!(extract_unsigned_local_package(&installer, &extracted).is_err());
        assert!(!extracted.exists());
        fs::remove_dir_all(root).expect("remove fixture");
    }

    #[test]
    fn local_setup_rejects_invalid_stub_and_extension() {
        let root = std::env::temp_dir().join(format!("tz-local-negative-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("test directory");
        let stub = root.join("stub.exe");
        let package = root.join("app.tzpkg");
        fs::write(&stub, b"not executable").expect("stub");
        fs::write(&package, b"payload").expect("package");
        assert!(create_unsigned_local_setup(&stub, &package, root.join("bad.exe")).is_err());
        fs::remove_dir_all(root).expect("remove fixture");
    }
}
