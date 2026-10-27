// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/setup_executable.rs
// Amac: Imzali Turkuaz setup bundle'ini Windows CLI stub'ina guvenli bicimde eklemek ve cikarmak
// Modul - Rust Setup Executable
// Version: 0.1.0
// Aciklama: PE stub sonuna bounded bundle, uzunluk, SHA-256 ve format imzasi ekler
// Bagimli Oldugu Katman: Service | Tool

use std::{
    error::Error,
    fmt, fs, io,
    io::{Read, Seek, SeekFrom, Write},
    path::{Path, PathBuf},
};

use sha2::{Digest, Sha256};

const FOOTER_MAGIC: [u8; 8] = *b"TZSETUP1";
const FOOTER_SIZE_BYTES: u64 = 8 + 8 + 32;
const MAXIMUM_BUNDLE_BYTES: u64 = 33 * 1024 * 1024 * 1024;
const COPY_BUFFER_BYTES: usize = 64 * 1024;

#[derive(Debug)]
pub enum SetupExecutableError {
    Io(io::Error),
    InvalidSetup(&'static str),
}

impl fmt::Display for SetupExecutableError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Self-extracting setup I/O failed: {error}"),
            Self::InvalidSetup(message) => write!(formatter, "Self-extracting setup is invalid: {message}"),
        }
    }
}

impl Error for SetupExecutableError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::InvalidSetup(_) => None,
        }
    }
}

/// @dev-id TIZ-SETUP-EXE-010
/// @purpose Append one bounded .tzsetup archive and an integrity footer to a Windows PE stub.
/// @reason A portable self-extracting executable must carry its exact payload without changing archive trust rules.
/// @contract Requires a regular MZ stub, a regular .tzsetup bundle, a new .exe output path, and never overwrites an existing output.
/// @depends SHA-256, bounded streaming, filesystem create-new semantics, and the signed bundle verifier at install time.
pub fn create_self_extracting_setup(
    stub_path: impl AsRef<Path>,
    bundle_path: impl AsRef<Path>,
    output_path: impl AsRef<Path>,
) -> Result<(), SetupExecutableError> {
    let stub_path = stub_path.as_ref();
    let bundle_path = bundle_path.as_ref();
    let output_path = output_path.as_ref();

    let stub_metadata = regular_file_metadata(stub_path)?;
    if stub_metadata.len() < 2 {
        return Err(SetupExecutableError::InvalidSetup(
            "Windows setup stub is empty or truncated",
        ));
    }
    let mut stub = fs::File::open(stub_path).map_err(SetupExecutableError::Io)?;
    let mut dos_header = [0_u8; 2];
    stub.read_exact(&mut dos_header)
        .map_err(SetupExecutableError::Io)?;
    if dos_header != *b"MZ" {
        return Err(SetupExecutableError::InvalidSetup(
            "Windows setup stub does not have an MZ executable header",
        ));
    }

    if !has_extension(bundle_path, "tzsetup") {
        return Err(SetupExecutableError::InvalidSetup(
            "setup payload must use the .tzsetup extension",
        ));
    }
    if !has_extension(output_path, "exe") {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting setup output must use the .exe extension",
        ));
    }
    let bundle_metadata = regular_file_metadata(bundle_path)?;
    if bundle_metadata.len() == 0 || bundle_metadata.len() > MAXIMUM_BUNDLE_BYTES {
        return Err(SetupExecutableError::InvalidSetup(
            "setup bundle is empty or exceeds the configured size limit",
        ));
    }

    let output_parent = output_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(output_parent).map_err(SetupExecutableError::Io)?;
    let output_name = output_path
        .file_name()
        .ok_or(SetupExecutableError::InvalidSetup(
            "setup executable file name is missing",
        ))?;
    let output_path = output_parent.join(output_name);
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&output_path)
        .map_err(SetupExecutableError::Io)?;

    let result = (|| {
        let copied_stub = io::copy(&mut stub, &mut output).map_err(SetupExecutableError::Io)?;
        if copied_stub != stub_metadata.len() {
            return Err(SetupExecutableError::InvalidSetup(
                "setup stub changed while being copied",
            ));
        }

        let mut bundle = fs::File::open(bundle_path).map_err(SetupExecutableError::Io)?;
        let opened_metadata = bundle.metadata().map_err(SetupExecutableError::Io)?;
        if !opened_metadata.is_file() || opened_metadata.len() != bundle_metadata.len() {
            return Err(SetupExecutableError::InvalidSetup(
                "setup bundle changed while being opened",
            ));
        }
        let digest = copy_and_hash(
            &mut bundle,
            &mut output,
            bundle_metadata.len(),
            "setup bundle changed while being copied",
        )?;
        output.write_all(&FOOTER_MAGIC).map_err(SetupExecutableError::Io)?;
        output
            .write_all(&bundle_metadata.len().to_le_bytes())
            .map_err(SetupExecutableError::Io)?;
        output.write_all(&digest).map_err(SetupExecutableError::Io)?;
        output.sync_all().map_err(SetupExecutableError::Io)
    })();
    if result.is_err() {
        let _ = fs::remove_file(&output_path);
    }
    result
}

/// @dev-id TIZ-SETUP-EXE-011
/// @purpose Recover a bounded appended .tzsetup payload from a self-extracting Windows executable.
/// @reason The setup stub must locate its own payload without trusting a path or length supplied by that payload.
/// @contract Requires a regular executable with the exact footer, bounded payload length, matching SHA-256, and a new .tzsetup destination.
/// @depends SHA-256, bounded streaming, fixed footer format, and downstream signature/trust verification.
pub fn extract_embedded_setup_bundle(
    executable_path: impl AsRef<Path>,
    bundle_output_path: impl AsRef<Path>,
) -> Result<(), SetupExecutableError> {
    let executable_path = executable_path.as_ref();
    let bundle_output_path = bundle_output_path.as_ref();
    let executable_metadata = regular_file_metadata(executable_path)?;
    if executable_metadata.len() <= FOOTER_SIZE_BYTES {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable is too small to contain a bundle",
        ));
    }
    if !has_extension(bundle_output_path, "tzsetup") {
        return Err(SetupExecutableError::InvalidSetup(
            "extracted setup bundle output must use the .tzsetup extension",
        ));
    }

    let mut executable = fs::File::open(executable_path).map_err(SetupExecutableError::Io)?;
    let opened_metadata = executable
        .metadata()
        .map_err(SetupExecutableError::Io)?;
    if !opened_metadata.is_file() || opened_metadata.len() != executable_metadata.len() {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable changed while being opened",
        ));
    }
    executable
        .seek(SeekFrom::End(-(FOOTER_SIZE_BYTES as i64)))
        .map_err(SetupExecutableError::Io)?;

    let mut magic = [0_u8; 8];
    executable
        .read_exact(&mut magic)
        .map_err(SetupExecutableError::Io)?;
    if magic != FOOTER_MAGIC {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting setup footer is missing or unsupported",
        ));
    }
    let mut length_bytes = [0_u8; 8];
    executable
        .read_exact(&mut length_bytes)
        .map_err(SetupExecutableError::Io)?;
    let bundle_length = u64::from_le_bytes(length_bytes);
    if bundle_length == 0
        || bundle_length > MAXIMUM_BUNDLE_BYTES
        || bundle_length > executable_metadata.len() - FOOTER_SIZE_BYTES
    {
        return Err(SetupExecutableError::InvalidSetup(
            "embedded bundle length is outside the supported limit",
        ));
    }
    let mut expected_digest = [0_u8; 32];
    executable
        .read_exact(&mut expected_digest)
        .map_err(SetupExecutableError::Io)?;
    let bundle_offset = executable_metadata.len() - FOOTER_SIZE_BYTES - bundle_length;
    executable
        .seek(SeekFrom::Start(bundle_offset))
        .map_err(SetupExecutableError::Io)?;

    let output_parent = bundle_output_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(output_parent).map_err(SetupExecutableError::Io)?;
    let output_name = bundle_output_path
        .file_name()
        .ok_or(SetupExecutableError::InvalidSetup(
            "extracted bundle file name is missing",
        ))?;
    let bundle_output_path = output_parent.join(output_name);
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&bundle_output_path)
        .map_err(SetupExecutableError::Io)?;

    let result = (|| {
        let digest = copy_and_hash(
            &mut executable,
            &mut output,
            bundle_length,
            "embedded setup bundle ended before its declared size",
        )?;
        if digest != expected_digest {
            return Err(SetupExecutableError::InvalidSetup(
                "embedded setup bundle SHA-256 does not match its footer",
            ));
        }
        output.sync_all().map_err(SetupExecutableError::Io)
    })();
    if result.is_err() {
        let _ = fs::remove_file(&bundle_output_path);
    }
    result
}

fn regular_file_metadata(path: &Path) -> Result<fs::Metadata, SetupExecutableError> {
    let metadata = fs::symlink_metadata(path).map_err(SetupExecutableError::Io)?;
    if !metadata.file_type().is_file() {
        return Err(SetupExecutableError::InvalidSetup(
            "setup inputs must be regular files, not symlinks",
        ));
    }
    Ok(metadata)
}

fn has_extension(path: &Path, expected: &str) -> bool {
    path.extension()
        .is_some_and(|extension| extension.to_string_lossy().eq_ignore_ascii_case(expected))
}

fn copy_and_hash(
    input: &mut fs::File,
    output: &mut fs::File,
    expected_length: u64,
    changed_message: &'static str,
) -> Result<[u8; 32], SetupExecutableError> {
    let mut hasher = Sha256::new();
    let mut copied = 0_u64;
    let mut buffer = [0_u8; COPY_BUFFER_BYTES];
    loop {
        let count = input.read(&mut buffer).map_err(SetupExecutableError::Io)?;
        if count == 0 {
            break;
        }
        copied = copied
            .checked_add(count as u64)
            .ok_or(SetupExecutableError::InvalidSetup(changed_message))?;
        if copied > expected_length {
            return Err(SetupExecutableError::InvalidSetup(changed_message));
        }
        output
            .write_all(&buffer[..count])
            .map_err(SetupExecutableError::Io)?;
        hasher.update(&buffer[..count]);
    }
    if copied != expected_length {
        return Err(SetupExecutableError::InvalidSetup(changed_message));
    }
    Ok(hasher.finalize().into())
}
