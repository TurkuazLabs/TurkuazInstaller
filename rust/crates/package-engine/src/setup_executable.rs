// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/setup_executable.rs
// Amac: Imzali Turkuaz setup bundle'ini Windows CLI stub'ina guvenli bicimde eklemek ve cikarmak
// Modul - Rust Setup Executable
// Version: 0.2.2
// Aciklama: Authenticode sertifika tablosundan onceki 8 bayt hizalama dolgusunda footer'i guvenle bulur
// Bagimli Oldugu Katman: Service | Tool

use std::{
    error::Error,
    fmt, fs, io,
    io::{Read, Seek, SeekFrom, Write},
    path::Path,
};

use sha2::{Digest, Sha256};

const FOOTER_MAGIC: [u8; 8] = *b"TZSETUP1";
const FOOTER_SIZE_BYTES: u64 = 8 + 8 + 32;
const MAX_AUTHENTICODE_ALIGNMENT_PADDING_BYTES: u64 = 7;
const FOOTER_SCAN_BUFFER_BYTES: usize =
    FOOTER_SIZE_BYTES as usize + MAX_AUTHENTICODE_ALIGNMENT_PADDING_BYTES as usize;
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
            Self::InvalidSetup(message) => {
                write!(formatter, "Self-extracting setup is invalid: {message}")
            }
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
    stub.seek(SeekFrom::Start(0))
        .map_err(SetupExecutableError::Io)?;

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
        output
            .write_all(&FOOTER_MAGIC)
            .map_err(SetupExecutableError::Io)?;
        output
            .write_all(&bundle_metadata.len().to_le_bytes())
            .map_err(SetupExecutableError::Io)?;
        output
            .write_all(&digest)
            .map_err(SetupExecutableError::Io)?;
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
/// @contract Requires a regular executable with the exact footer, bounded payload length, matching SHA-256, at most seven zero alignment bytes, and a new .tzsetup destination.
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
    let opened_metadata = executable.metadata().map_err(SetupExecutableError::Io)?;
    if !opened_metadata.is_file() || opened_metadata.len() != executable_metadata.len() {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable changed while being opened",
        ));
    }
    let payload_end = setup_payload_end(&mut executable, executable_metadata.len())?;
    let footer_start = locate_setup_footer_start(&mut executable, payload_end)?;
    executable
        .seek(SeekFrom::Start(footer_start))
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
    if bundle_length == 0 || bundle_length > MAXIMUM_BUNDLE_BYTES || bundle_length > footer_start {
        return Err(SetupExecutableError::InvalidSetup(
            "embedded bundle length is outside the supported limit",
        ));
    }
    let mut expected_digest = [0_u8; 32];
    executable
        .read_exact(&mut expected_digest)
        .map_err(SetupExecutableError::Io)?;
    let bundle_offset = footer_start - bundle_length;
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

fn locate_setup_footer_start(
    executable: &mut fs::File,
    payload_end: u64,
) -> Result<u64, SetupExecutableError> {
    if payload_end < FOOTER_SIZE_BYTES {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has no space for a bundle footer",
        ));
    }

    let maximum_padding =
        MAX_AUTHENTICODE_ALIGNMENT_PADDING_BYTES.min(payload_end - FOOTER_SIZE_BYTES);
    let scan_start = payload_end - FOOTER_SIZE_BYTES - maximum_padding;
    let scan_length = usize::try_from(payload_end - scan_start).map_err(|_| {
        SetupExecutableError::InvalidSetup("self-extracting setup footer offset is too large")
    })?;
    let mut trailing_bytes = [0_u8; FOOTER_SCAN_BUFFER_BYTES];
    executable
        .seek(SeekFrom::Start(scan_start))
        .map_err(SetupExecutableError::Io)?;
    executable
        .read_exact(&mut trailing_bytes[..scan_length])
        .map_err(SetupExecutableError::Io)?;

    let mut footer_start = None;
    for padding_length in 0..=maximum_padding {
        let footer_end = payload_end - padding_length;
        let candidate_start = footer_end - FOOTER_SIZE_BYTES;
        let candidate_index = usize::try_from(candidate_start - scan_start).map_err(|_| {
            SetupExecutableError::InvalidSetup("self-extracting setup footer offset is too large")
        })?;
        let footer_end_index = candidate_index + FOOTER_SIZE_BYTES as usize;
        let magic_end_index = candidate_index + FOOTER_MAGIC.len();
        let padding_is_zero = trailing_bytes[footer_end_index..scan_length]
            .iter()
            .all(|byte| *byte == 0);

        if trailing_bytes[candidate_index..magic_end_index] == FOOTER_MAGIC[..] && padding_is_zero {
            if footer_start.replace(candidate_start).is_some() {
                return Err(SetupExecutableError::InvalidSetup(
                    "self-extracting setup footer is ambiguous",
                ));
            }
        }
    }

    footer_start.ok_or(SetupExecutableError::InvalidSetup(
        "self-extracting setup footer is missing or unsupported",
    ))
}

fn setup_payload_end(
    executable: &mut fs::File,
    file_length: u64,
) -> Result<u64, SetupExecutableError> {
    if file_length < 0x40 {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has a truncated DOS header",
        ));
    }

    executable
        .seek(SeekFrom::Start(0x3c))
        .map_err(SetupExecutableError::Io)?;
    let mut pe_offset_bytes = [0_u8; 4];
    executable
        .read_exact(&mut pe_offset_bytes)
        .map_err(SetupExecutableError::Io)?;
    let pe_offset = u32::from_le_bytes(pe_offset_bytes) as u64;
    if pe_offset
        .checked_add(24)
        .is_none_or(|header_end| header_end > file_length)
    {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has an invalid PE header offset",
        ));
    }

    executable
        .seek(SeekFrom::Start(pe_offset))
        .map_err(SetupExecutableError::Io)?;
    let mut pe_signature = [0_u8; 4];
    executable
        .read_exact(&mut pe_signature)
        .map_err(SetupExecutableError::Io)?;
    if pe_signature != *b"PE\0\0" {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has no valid PE signature",
        ));
    }
    let mut coff_header = [0_u8; 20];
    executable
        .read_exact(&mut coff_header)
        .map_err(SetupExecutableError::Io)?;
    let optional_header_size = u16::from_le_bytes([coff_header[16], coff_header[17]]) as u64;
    let optional_header_start = pe_offset + 24;
    let optional_header_end = optional_header_start
        .checked_add(optional_header_size)
        .filter(|end| *end <= file_length)
        .ok_or(SetupExecutableError::InvalidSetup(
            "self-extracting executable has a truncated PE optional header",
        ))?;

    executable
        .seek(SeekFrom::Start(optional_header_start))
        .map_err(SetupExecutableError::Io)?;
    let mut optional_magic = [0_u8; 2];
    executable
        .read_exact(&mut optional_magic)
        .map_err(SetupExecutableError::Io)?;
    let (directory_start, directory_count_offset) = match u16::from_le_bytes(optional_magic) {
        0x10b => (96_u64, 92_u64),
        0x20b => (112_u64, 108_u64),
        _ => {
            return Err(SetupExecutableError::InvalidSetup(
                "self-extracting executable uses an unsupported PE optional header",
            ));
        }
    };

    let directory_count_end = optional_header_start + directory_count_offset + 4;
    if directory_count_end > optional_header_end {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has an incomplete PE data directory",
        ));
    }
    executable
        .seek(SeekFrom::Start(
            optional_header_start + directory_count_offset,
        ))
        .map_err(SetupExecutableError::Io)?;
    let mut directory_count_bytes = [0_u8; 4];
    executable
        .read_exact(&mut directory_count_bytes)
        .map_err(SetupExecutableError::Io)?;
    let directory_count = u32::from_le_bytes(directory_count_bytes);
    if directory_count <= 4 {
        return Ok(file_length);
    }

    let certificate_entry_offset = optional_header_start + directory_start + 4 * 8;
    if certificate_entry_offset + 8 > optional_header_end {
        return Err(SetupExecutableError::InvalidSetup(
            "self-extracting executable has a truncated certificate directory entry",
        ));
    }
    executable
        .seek(SeekFrom::Start(certificate_entry_offset))
        .map_err(SetupExecutableError::Io)?;
    let mut certificate_entry = [0_u8; 8];
    executable
        .read_exact(&mut certificate_entry)
        .map_err(SetupExecutableError::Io)?;
    let certificate_offset = u32::from_le_bytes(
        certificate_entry[..4]
            .try_into()
            .map_err(|_| SetupExecutableError::InvalidSetup("PE certificate offset is invalid"))?,
    ) as u64;
    let certificate_size = u32::from_le_bytes(
        certificate_entry[4..]
            .try_into()
            .map_err(|_| SetupExecutableError::InvalidSetup("PE certificate size is invalid"))?,
    ) as u64;

    if certificate_offset == 0 && certificate_size == 0 {
        return Ok(file_length);
    }
    let certificate_end = certificate_offset.checked_add(certificate_size).ok_or(
        SetupExecutableError::InvalidSetup("PE certificate table length overflows"),
    )?;
    if certificate_offset < optional_header_end
        || certificate_offset % 8 != 0
        || certificate_size < 8
        || certificate_end != file_length
    {
        return Err(SetupExecutableError::InvalidSetup(
            "PE certificate table is outside the supported file layout",
        ));
    }
    Ok(certificate_offset)
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
    while copied < expected_length {
        let remaining = (expected_length - copied).min(buffer.len() as u64) as usize;
        let count = input
            .read(&mut buffer[..remaining])
            .map_err(SetupExecutableError::Io)?;
        if count == 0 {
            break;
        }
        copied = copied
            .checked_add(count as u64)
            .ok_or(SetupExecutableError::InvalidSetup(changed_message))?;
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

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    struct TestDirectory(PathBuf);

    impl TestDirectory {
        fn new() -> Self {
            let path = std::env::temp_dir().join(format!(
                "turkuaz-setup-executable-test-{}",
                uuid::Uuid::new_v4()
            ));
            fs::create_dir(&path).expect("create temporary test directory");
            Self(path)
        }

        fn join(&self, name: &str) -> PathBuf {
            self.0.join(name)
        }
    }

    impl Drop for TestDirectory {
        fn drop(&mut self) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }

    fn signed_setup_executable(bundle: &[u8]) -> (Vec<u8>, usize) {
        const PE_HEADER_OFFSET: u32 = 0x80;
        const OPTIONAL_HEADER_SIZE: u16 = 0xf0;
        const CERTIFICATE_SIZE: u32 = 8;

        let mut executable = vec![0_u8; 0x200];
        executable[..2].copy_from_slice(b"MZ");
        executable[0x3c..0x40].copy_from_slice(&PE_HEADER_OFFSET.to_le_bytes());

        let pe_header_offset = PE_HEADER_OFFSET as usize;
        executable[pe_header_offset..pe_header_offset + 4].copy_from_slice(b"PE\0\0");
        let coff_header_offset = pe_header_offset + 4;
        executable[coff_header_offset + 16..coff_header_offset + 18]
            .copy_from_slice(&OPTIONAL_HEADER_SIZE.to_le_bytes());

        let optional_header_offset = coff_header_offset + 20;
        executable[optional_header_offset..optional_header_offset + 2]
            .copy_from_slice(&0x20b_u16.to_le_bytes());
        executable[optional_header_offset + 108..optional_header_offset + 112]
            .copy_from_slice(&16_u32.to_le_bytes());
        let certificate_entry_offset = optional_header_offset + 112 + 4 * 8;

        executable.extend_from_slice(bundle);
        executable.extend_from_slice(&FOOTER_MAGIC);
        executable.extend_from_slice(&(bundle.len() as u64).to_le_bytes());
        let digest: [u8; 32] = Sha256::digest(bundle).into();
        executable.extend_from_slice(&digest);

        let padding_length = (8 - (executable.len() % 8)) % 8;
        executable.extend_from_slice(&[0_u8; 7][..padding_length]);

        let certificate_offset =
            u32::try_from(executable.len()).expect("test PE fixture fits in u32");
        executable[certificate_entry_offset..certificate_entry_offset + 4]
            .copy_from_slice(&certificate_offset.to_le_bytes());
        executable[certificate_entry_offset + 4..certificate_entry_offset + 8]
            .copy_from_slice(&CERTIFICATE_SIZE.to_le_bytes());
        executable.extend_from_slice(&[0_u8; CERTIFICATE_SIZE as usize]);

        (executable, padding_length)
    }

    #[test]
    fn extracts_signed_setup_with_authenticode_alignment_padding() {
        let directory = TestDirectory::new();

        for bundle_length in 1..=8 {
            let bundle = (0..bundle_length)
                .map(|byte| byte as u8)
                .collect::<Vec<_>>();
            let (executable, padding_length) = signed_setup_executable(&bundle);
            assert_eq!(padding_length, (8 - (bundle_length % 8)) % 8);

            let executable_path = directory.join(&format!("{bundle_length}.exe"));
            let bundle_path = directory.join(&format!("{bundle_length}.tzsetup"));
            fs::write(&executable_path, executable).expect("write signed PE fixture");

            extract_embedded_setup_bundle(&executable_path, &bundle_path)
                .expect("extract bundle before Authenticode alignment padding");
            assert_eq!(
                fs::read(bundle_path).expect("read extracted bundle"),
                bundle
            );
        }
    }

    #[test]
    fn rejects_nonzero_bytes_between_footer_and_certificate_table() {
        let directory = TestDirectory::new();
        let bundle = b"bundle-data!!";
        let (mut executable, padding_length) = signed_setup_executable(bundle);
        assert!(padding_length > 0);

        let certificate_offset = executable.len() - 8;
        executable[certificate_offset - 1] = 0xa5;

        let executable_path = directory.join("nonzero-padding.exe");
        let bundle_path = directory.join("nonzero-padding.tzsetup");
        fs::write(&executable_path, executable).expect("write signed PE fixture");

        let error = extract_embedded_setup_bundle(&executable_path, &bundle_path)
            .expect_err("nonzero signer alignment data must not be accepted");
        assert!(
            error
                .to_string()
                .contains("footer is missing or unsupported")
        );
        assert!(!bundle_path.exists());
    }
}
