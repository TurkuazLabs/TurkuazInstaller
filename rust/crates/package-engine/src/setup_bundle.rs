// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/setup_bundle.rs
// Amac: Imzali release girdileriyle tasinabilir .tzsetup paketini guvenli olusturup acmak
// Modul - Rust Setup Bundle
// Version: 0.1.1
// Aciklama: Manifest, detached imza ve dogrulanmis tzpkg payloadini sabit isimli ZIP bundle'a koyar
// Bagimli Oldugu Katman: Service | Tool

use std::{
    collections::HashMap,
    error::Error,
    fmt, fs, io,
    io::{Read, Write},
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use turkuaz_installer_artifact_validation::VerifiedArtifact;
use turkuaz_installer_domain::release::PackageRelease;
use zip::{CompressionMethod, ZipArchive, ZipWriter, result::ZipError, write::SimpleFileOptions};

const BUNDLE_FORMAT_VERSION: u32 = 1;
const BUNDLE_METADATA_ENTRY: &str = "bundle.json";
const RELEASE_MANIFEST_ENTRY: &str = "release.yaml";
const RELEASE_SIGNATURE_ENTRY: &str = "release.p7s";
const PACKAGE_ENTRY: &str = "application.tzpkg";
const MAXIMUM_BUNDLE_METADATA_BYTES: u64 = 16 * 1024;
const MAXIMUM_MANIFEST_BYTES: u64 = 1024 * 1024;
const MAXIMUM_SIGNATURE_BYTES: u64 = 256 * 1024;
const MAXIMUM_PACKAGE_BYTES: u64 = 32 * 1024 * 1024 * 1024;
const COPY_BUFFER_BYTES: usize = 64 * 1024;

#[derive(Debug)]
pub enum SetupBundleError {
    Io(io::Error),
    Archive(ZipError),
    Json(serde_json::Error),
    InvalidBundle(&'static str),
    ArtifactChanged,
}

impl fmt::Display for SetupBundleError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Setup bundle I/O failed: {error}"),
            Self::Archive(error) => write!(formatter, "Setup bundle archive failed: {error}"),
            Self::Json(error) => write!(formatter, "Setup bundle metadata is invalid: {error}"),
            Self::InvalidBundle(message) => write!(formatter, "Setup bundle is invalid: {message}"),
            Self::ArtifactChanged => formatter
                .write_str("Verified package changed while the setup bundle was being written."),
        }
    }
}

impl Error for SetupBundleError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Archive(error) => Some(error),
            Self::Json(error) => Some(error),
            Self::InvalidBundle(_) | Self::ArtifactChanged => None,
        }
    }
}

#[derive(Debug, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SetupBundleDocument {
    format_version: u32,
    package_id: String,
    version: String,
    manifest_size_bytes: u64,
    manifest_sha256: String,
    signature_size_bytes: u64,
    signature_sha256: String,
    artifact_size_bytes: u64,
    artifact_sha256: String,
}

#[derive(Debug, Clone)]
pub struct SetupBundleFiles {
    root: PathBuf,
    manifest_path: PathBuf,
    signature_path: PathBuf,
    artifact_path: PathBuf,
}

impl SetupBundleFiles {
    pub fn root(&self) -> &Path {
        &self.root
    }

    pub fn manifest_path(&self) -> &Path {
        &self.manifest_path
    }

    pub fn signature_path(&self) -> &Path {
        &self.signature_path
    }

    pub fn artifact_path(&self) -> &Path {
        &self.artifact_path
    }
}

/// @dev-id TIZ-PKG-BUNDLE-010
/// @purpose Assemble a signed release and its already verified package into a portable .tzsetup archive.
/// @reason Offline setup media must carry the exact manifest, signature, and bytes described by that manifest.
/// @contract Requires a typed release, exact manifest/signature bytes, and VerifiedArtifact; output is never overwritten and out-of-band trust remains the installer's responsibility.
/// @depends VerifiedArtifact validation, SHA-256, bounded bundle entries, and zip-rs.
pub fn write_setup_bundle(
    manifest: &[u8],
    signature: &[u8],
    release: &PackageRelease,
    verified_artifact: &VerifiedArtifact,
    output_path: impl AsRef<Path>,
) -> Result<(), SetupBundleError> {
    if manifest.is_empty() || manifest.len() as u64 > MAXIMUM_MANIFEST_BYTES {
        return Err(SetupBundleError::InvalidBundle(
            "manifest is empty or exceeds the configured size limit",
        ));
    }
    if signature.is_empty() || signature.len() as u64 > MAXIMUM_SIGNATURE_BYTES {
        return Err(SetupBundleError::InvalidBundle(
            "signature is empty or exceeds the configured size limit",
        ));
    }
    if verified_artifact.descriptor() != release.artifact() {
        return Err(SetupBundleError::InvalidBundle(
            "verified package does not match the signed release",
        ));
    }
    if release.artifact().size_bytes() > MAXIMUM_PACKAGE_BYTES {
        return Err(SetupBundleError::InvalidBundle(
            "package exceeds the setup bundle size limit",
        ));
    }

    let output_path = output_path.as_ref();
    if output_path
        .extension()
        .is_none_or(|extension| !extension.to_string_lossy().eq_ignore_ascii_case("tzsetup"))
    {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle output must use the .tzsetup extension",
        ));
    }
    let output_parent = output_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(output_parent).map_err(SetupBundleError::Io)?;
    let output_name = output_path
        .file_name()
        .ok_or(SetupBundleError::InvalidBundle(
            "setup bundle output file name is missing",
        ))?;
    let output_path = output_parent.join(output_name);
    match fs::symlink_metadata(&output_path) {
        Ok(_) => {
            return Err(SetupBundleError::InvalidBundle(
                "setup bundle output already exists",
            ));
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(SetupBundleError::Io(error)),
    }

    let artifact_path = verified_artifact.path();
    let artifact_metadata = fs::symlink_metadata(artifact_path).map_err(SetupBundleError::Io)?;
    if !artifact_metadata.file_type().is_file()
        || artifact_metadata.len() != release.artifact().size_bytes()
    {
        return Err(SetupBundleError::ArtifactChanged);
    }

    let document = SetupBundleDocument {
        format_version: BUNDLE_FORMAT_VERSION,
        package_id: release.package_id().to_string(),
        version: release.version().to_string(),
        manifest_size_bytes: manifest.len() as u64,
        manifest_sha256: sha256_hex(manifest),
        signature_size_bytes: signature.len() as u64,
        signature_sha256: sha256_hex(signature),
        artifact_size_bytes: release.artifact().size_bytes(),
        artifact_sha256: release.artifact().digest().as_str().to_owned(),
    };
    let metadata = serde_json::to_vec(&document).map_err(SetupBundleError::Json)?;
    if metadata.len() as u64 > MAXIMUM_BUNDLE_METADATA_BYTES {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle metadata exceeds the configured size limit",
        ));
    }

    let file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&output_path)
        .map_err(SetupBundleError::Io)?;
    let result = (|| {
        let mut archive = ZipWriter::new(file);
        let options = SimpleFileOptions::default()
            .compression_method(CompressionMethod::Deflated)
            .unix_permissions(0o600);
        write_entry(&mut archive, BUNDLE_METADATA_ENTRY, &metadata, options)?;
        write_entry(&mut archive, RELEASE_MANIFEST_ENTRY, manifest, options)?;
        write_entry(&mut archive, RELEASE_SIGNATURE_ENTRY, signature, options)?;
        let package_options = SimpleFileOptions::default()
            .compression_method(CompressionMethod::Stored)
            .unix_permissions(0o600);
        write_artifact_entry(
            &mut archive,
            artifact_path,
            release.artifact().size_bytes(),
            release.artifact().digest().as_str(),
            package_options,
        )?;
        let file = archive.finish().map_err(SetupBundleError::Archive)?;
        file.sync_all().map_err(SetupBundleError::Io)
    })();
    if result.is_err() {
        let _ = fs::remove_file(&output_path);
    }
    result
}

/// @dev-id TIZ-PKG-BUNDLE-011
/// @purpose Extract only the fixed, bounded entries in a Turkuaz .tzsetup archive.
/// @reason Bundle paths are untrusted archive input and must never choose their own extraction destinations.
/// @contract Accepts exactly the four version-1 entries, rejects duplicate/unsafe/special entries and bounded-size violations, and creates files beneath a new extraction directory.
/// @depends ZIP archive validation, bounded streaming, and caller-provided trusted staging root.
pub fn extract_setup_bundle(
    bundle_path: impl AsRef<Path>,
    extraction_directory: impl AsRef<Path>,
) -> Result<SetupBundleFiles, SetupBundleError> {
    let bundle_path = bundle_path.as_ref();
    let bundle_metadata = fs::symlink_metadata(bundle_path).map_err(SetupBundleError::Io)?;
    if !bundle_metadata.file_type().is_file() {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle must be a regular file, not a symlink",
        ));
    }
    let extraction_directory = extraction_directory.as_ref().to_path_buf();
    match fs::symlink_metadata(&extraction_directory) {
        Ok(_) => {
            return Err(SetupBundleError::InvalidBundle(
                "setup extraction directory already exists",
            ));
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(SetupBundleError::Io(error)),
    }
    fs::create_dir(&extraction_directory).map_err(SetupBundleError::Io)?;

    let result = (|| {
        let file = fs::File::open(bundle_path).map_err(SetupBundleError::Io)?;
        let mut archive = ZipArchive::new(file).map_err(SetupBundleError::Archive)?;
        if archive.len() != 4 {
            return Err(SetupBundleError::InvalidBundle(
                "setup bundle must contain exactly four entries",
            ));
        }

        let allowed = [
            BUNDLE_METADATA_ENTRY,
            RELEASE_MANIFEST_ENTRY,
            RELEASE_SIGNATURE_ENTRY,
            PACKAGE_ENTRY,
        ];
        let mut indices = HashMap::new();
        for index in 0..archive.len() {
            let entry = archive.by_index(index).map_err(SetupBundleError::Archive)?;
            let name = entry.name();
            if !allowed.contains(&name) || entry.is_dir() {
                return Err(SetupBundleError::InvalidBundle(
                    "setup bundle contains an unknown entry or directory",
                ));
            }
            if entry
                .unix_mode()
                .is_some_and(|mode| mode & 0o170000 == 0o120000)
            {
                return Err(SetupBundleError::InvalidBundle(
                    "setup bundle contains a symbolic link",
                ));
            }
            if indices.insert(name.to_owned(), index).is_some() {
                return Err(SetupBundleError::InvalidBundle(
                    "setup bundle contains duplicate entry names",
                ));
            }
        }
        if allowed.iter().any(|name| !indices.contains_key(*name)) {
            return Err(SetupBundleError::InvalidBundle(
                "setup bundle is missing a required entry",
            ));
        }

        let mut metadata_bytes = read_bounded_entry(
            &mut archive,
            indices[BUNDLE_METADATA_ENTRY],
            MAXIMUM_BUNDLE_METADATA_BYTES,
        )?;
        let document: SetupBundleDocument =
            serde_json::from_slice(&metadata_bytes).map_err(SetupBundleError::Json)?;
        metadata_bytes.fill(0);
        if document.format_version != BUNDLE_FORMAT_VERSION
            || document.manifest_size_bytes == 0
            || document.manifest_size_bytes > MAXIMUM_MANIFEST_BYTES
            || document.signature_size_bytes == 0
            || document.signature_size_bytes > MAXIMUM_SIGNATURE_BYTES
            || document.artifact_size_bytes == 0
            || document.artifact_size_bytes > MAXIMUM_PACKAGE_BYTES
            || !is_sha256(&document.manifest_sha256)
            || !is_sha256(&document.signature_sha256)
            || !is_sha256(&document.artifact_sha256)
        {
            return Err(SetupBundleError::InvalidBundle(
                "setup bundle metadata is outside the supported contract",
            ));
        }

        let manifest_path = extraction_directory.join(RELEASE_MANIFEST_ENTRY);
        let signature_path = extraction_directory.join(RELEASE_SIGNATURE_ENTRY);
        let artifact_path = extraction_directory.join(PACKAGE_ENTRY);
        extract_entry(
            &mut archive,
            indices[RELEASE_MANIFEST_ENTRY],
            &manifest_path,
            document.manifest_size_bytes,
        )?;
        extract_entry(
            &mut archive,
            indices[RELEASE_SIGNATURE_ENTRY],
            &signature_path,
            document.signature_size_bytes,
        )?;
        extract_entry(
            &mut archive,
            indices[PACKAGE_ENTRY],
            &artifact_path,
            document.artifact_size_bytes,
        )?;

        if hash_file(&manifest_path)? != document.manifest_sha256
            || hash_file(&signature_path)? != document.signature_sha256
            || hash_file(&artifact_path)? != document.artifact_sha256
        {
            return Err(SetupBundleError::InvalidBundle(
                "setup bundle entry digest does not match its metadata",
            ));
        }

        Ok(SetupBundleFiles {
            root: extraction_directory.clone(),
            manifest_path,
            signature_path,
            artifact_path,
        })
    })();

    if result.is_err() {
        let _ = fs::remove_dir_all(&extraction_directory);
    }
    result
}

fn write_entry(
    archive: &mut ZipWriter<fs::File>,
    name: &str,
    contents: &[u8],
    options: SimpleFileOptions,
) -> Result<(), SetupBundleError> {
    archive
        .start_file(name, options)
        .map_err(SetupBundleError::Archive)?;
    archive.write_all(contents).map_err(SetupBundleError::Io)
}

fn write_artifact_entry(
    archive: &mut ZipWriter<fs::File>,
    artifact_path: &Path,
    expected_size: u64,
    expected_sha256: &str,
    options: SimpleFileOptions,
) -> Result<(), SetupBundleError> {
    let metadata = fs::symlink_metadata(artifact_path).map_err(SetupBundleError::Io)?;
    if !metadata.file_type().is_file() || metadata.len() != expected_size {
        return Err(SetupBundleError::ArtifactChanged);
    }
    let mut input = fs::File::open(artifact_path).map_err(SetupBundleError::Io)?;
    let opened_metadata = input.metadata().map_err(SetupBundleError::Io)?;
    if !opened_metadata.is_file() || opened_metadata.len() != expected_size {
        return Err(SetupBundleError::ArtifactChanged);
    }

    archive
        .start_file(PACKAGE_ENTRY, options)
        .map_err(SetupBundleError::Archive)?;
    let mut hasher = Sha256::new();
    let mut copied = 0_u64;
    let mut buffer = [0_u8; COPY_BUFFER_BYTES];
    loop {
        let count = input.read(&mut buffer).map_err(SetupBundleError::Io)?;
        if count == 0 {
            break;
        }
        copied = copied
            .checked_add(count as u64)
            .ok_or(SetupBundleError::ArtifactChanged)?;
        if copied > expected_size {
            return Err(SetupBundleError::ArtifactChanged);
        }
        hasher.update(&buffer[..count]);
        archive
            .write_all(&buffer[..count])
            .map_err(SetupBundleError::Io)?;
    }
    if copied != expected_size || hex(&hasher.finalize()) != expected_sha256 {
        return Err(SetupBundleError::ArtifactChanged);
    }
    Ok(())
}

fn read_bounded_entry(
    archive: &mut ZipArchive<fs::File>,
    index: usize,
    maximum_bytes: u64,
) -> Result<Vec<u8>, SetupBundleError> {
    let entry = archive.by_index(index).map_err(SetupBundleError::Archive)?;
    let declared_size = entry.size();
    if declared_size == 0 || declared_size > maximum_bytes {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle metadata entry has an invalid size",
        ));
    }
    let mut bytes = Vec::with_capacity(declared_size as usize);
    entry
        .take(maximum_bytes + 1)
        .read_to_end(&mut bytes)
        .map_err(SetupBundleError::Io)?;
    if bytes.len() as u64 != declared_size || bytes.len() as u64 > maximum_bytes {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle metadata entry changed size while being read",
        ));
    }
    Ok(bytes)
}

fn extract_entry(
    archive: &mut ZipArchive<fs::File>,
    index: usize,
    destination: &Path,
    expected_size: u64,
) -> Result<(), SetupBundleError> {
    let mut entry = archive.by_index(index).map_err(SetupBundleError::Archive)?;
    if entry.size() != expected_size {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle entry size disagrees with bundle metadata",
        ));
    }
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(destination)
        .map_err(SetupBundleError::Io)?;
    let copied = io::copy(&mut entry, &mut output).map_err(SetupBundleError::Io)?;
    if copied != expected_size {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle entry ended before its declared size",
        ));
    }
    output.sync_all().map_err(SetupBundleError::Io)
}

fn hash_file(path: &Path) -> Result<String, SetupBundleError> {
    let mut file = fs::File::open(path).map_err(SetupBundleError::Io)?;
    let metadata = file.metadata().map_err(SetupBundleError::Io)?;
    if !metadata.is_file() {
        return Err(SetupBundleError::InvalidBundle(
            "setup bundle extracted a non-regular file",
        ));
    }
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; COPY_BUFFER_BYTES];
    loop {
        let count = file.read(&mut buffer).map_err(SetupBundleError::Io)?;
        if count == 0 {
            break;
        }
        hasher.update(&buffer[..count]);
    }
    Ok(hex(&hasher.finalize()))
}

fn sha256_hex(bytes: &[u8]) -> String {
    hex(&Sha256::digest(bytes))
}

fn is_sha256(value: &str) -> bool {
    value.len() == 64 && value.bytes().all(|byte| byte.is_ascii_hexdigit())
}

fn hex(bytes: &[u8]) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut output = String::with_capacity(bytes.len() * 2);
    for byte in bytes {
        output.push(HEX[(byte >> 4) as usize] as char);
        output.push(HEX[(byte & 0x0f) as usize] as char);
    }
    output
}
