// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/lib.rs
// Amac: Verified Turkuaz package archive'larini uygulamadan once guvenli staging alanina cikarmak
// Modul - Rust Library
// Version: 0.9.0
// Aciklama: ZIP-slip, Windows path collision, symlink, duplicate entry ve archive bomb sinirlarini uygular
// Bagimli Oldugu Katman: Service | Tool

mod builder;
mod format;
mod setup_bundle;

pub use builder::{PackageBuildError, build_full_package, build_full_package_with_entry_point};
pub use setup_bundle::{
    SetupBundleError, SetupBundleFiles, extract_setup_bundle, write_setup_bundle,
};

use std::{
    collections::HashMap,
    error::Error,
    fmt, fs,
    io::{self, Read, Write},
    path::{Path, PathBuf},
};

use tokio::task;
use turkuaz_installer_artifact_validation::VerifiedArtifact;
use turkuaz_installer_domain::{
    package_id::PackageId, release::PackageRelease, semantic_version::SemanticVersion,
};
use zip::{ZipArchive, result::ZipError};

use crate::format::{
    MAXIMUM_PACKAGE_METADATA_BYTES, PACKAGE_FORMAT_VERSION, PACKAGE_METADATA_ENTRY,
    PACKAGE_PAYLOAD_ROOT, PackageArchiveMetadata,
};

const PACKAGE_EXTENSION: &str = "tzpkg";
const DEFAULT_MAXIMUM_ENTRIES: usize = 250_000;
const DEFAULT_MAXIMUM_FILE_BYTES: u64 = 4 * 1024 * 1024 * 1024;
const DEFAULT_MAXIMUM_TOTAL_BYTES: u64 = 32 * 1024 * 1024 * 1024;

#[derive(Debug)]
pub enum PackageEngineError {
    Io(io::Error),
    Archive(ZipError),
    BlockingTask(task::JoinError),
    InvalidPackage(&'static str),
}

impl fmt::Display for PackageEngineError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Package staging I/O failed: {error}"),
            Self::Archive(error) => write!(formatter, "Package archive is invalid: {error}"),
            Self::BlockingTask(error) => write!(formatter, "Package worker failed: {error}"),
            Self::InvalidPackage(message) => write!(formatter, "Package is invalid: {message}"),
        }
    }
}

impl Error for PackageEngineError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Archive(error) => Some(error),
            Self::BlockingTask(error) => Some(error),
            Self::InvalidPackage(_) => None,
        }
    }
}

#[derive(Debug, Clone, Copy)]
pub struct PackageExtractionLimits {
    maximum_entries: usize,
    maximum_file_bytes: u64,
    maximum_total_bytes: u64,
}

impl PackageExtractionLimits {
    pub const fn new(
        maximum_entries: usize,
        maximum_file_bytes: u64,
        maximum_total_bytes: u64,
    ) -> Self {
        Self {
            maximum_entries,
            maximum_file_bytes,
            maximum_total_bytes,
        }
    }
}

impl Default for PackageExtractionLimits {
    fn default() -> Self {
        Self::new(
            DEFAULT_MAXIMUM_ENTRIES,
            DEFAULT_MAXIMUM_FILE_BYTES,
            DEFAULT_MAXIMUM_TOTAL_BYTES,
        )
    }
}

/// A fully extracted package payload that still has not been made active at its install target.
#[derive(Debug)]
pub struct PackageStage {
    package_id: PackageId,
    version: SemanticVersion,
    payload_directory: PathBuf,
    entry_point: Option<String>,
}

impl PackageStage {
    pub fn package_id(&self) -> &PackageId {
        &self.package_id
    }

    pub fn version(&self) -> &SemanticVersion {
        &self.version
    }

    pub fn payload_directory(&self) -> &Path {
        &self.payload_directory
    }

    pub fn entry_point(&self) -> Option<&str> {
        self.entry_point.as_deref()
    }
}

/// @dev-id TIZ-PKG-010
/// @purpose Extract only a previously validated custom Turkuaz ZIP package into a fresh stage directory.
/// @reason Installer payloads are untrusted archive input even after their signed digest is verified.
/// @contract Requires a .tzpkg URI, an exact release/artifact descriptor match, safe Windows paths, regular files only, and bounded entry/expanded sizes.
/// @depends Verified artifact validation, typed release metadata, Tokio worker pool, and zip-rs.
pub async fn stage_full_package(
    verified_artifact: VerifiedArtifact,
    release: &PackageRelease,
    payload_directory: impl AsRef<Path>,
    limits: PackageExtractionLimits,
) -> Result<PackageStage, PackageEngineError> {
    if verified_artifact.descriptor() != release.artifact() {
        return Err(PackageEngineError::InvalidPackage(
            "verified artifact does not match the requested signed release",
        ));
    }
    if verified_artifact
        .descriptor()
        .uri()
        .path()
        .rsplit('/')
        .next()
        .and_then(|name| Path::new(name).extension())
        .is_none_or(|extension| {
            !extension
                .to_string_lossy()
                .eq_ignore_ascii_case(PACKAGE_EXTENSION)
        })
    {
        return Err(PackageEngineError::InvalidPackage(
            "custom package artifacts must use the .tzpkg extension",
        ));
    }
    if limits.maximum_entries == 0
        || limits.maximum_file_bytes == 0
        || limits.maximum_total_bytes == 0
    {
        return Err(PackageEngineError::InvalidPackage(
            "package extraction limits must be positive",
        ));
    }

    let artifact_path = verified_artifact.path().to_path_buf();
    let payload_directory = payload_directory.as_ref().to_path_buf();
    let package_id = release.package_id().clone();
    let version = release.version().clone();

    let worker_directory = payload_directory.clone();
    let worker_limits = limits;
    let expected_package_id = package_id.to_string();
    let expected_version = version.to_string();
    let result = match task::spawn_blocking(move || {
        extract_archive(
            &artifact_path,
            &worker_directory,
            worker_limits,
            &expected_package_id,
            &expected_version,
        )
    })
    .await
    {
        Ok(result) => result,
        Err(error) => return Err(PackageEngineError::BlockingTask(error)),
    };

    let entry_point = result?;

    Ok(PackageStage {
        package_id,
        version,
        payload_directory,
        entry_point,
    })
}

#[derive(Debug)]
struct PlannedEntry {
    index: usize,
    relative_path: PathBuf,
    is_directory: bool,
    uncompressed_size: u64,
}

fn extract_archive(
    archive_path: &Path,
    payload_directory: &Path,
    limits: PackageExtractionLimits,
    expected_package_id: &str,
    expected_version: &str,
) -> Result<Option<String>, PackageEngineError> {
    let parent = payload_directory
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(parent).map_err(PackageEngineError::Io)?;
    fs::create_dir(payload_directory).map_err(PackageEngineError::Io)?;

    let result = extract_archive_contents(
        archive_path,
        payload_directory,
        limits,
        expected_package_id,
        expected_version,
    );
    if result.is_err() {
        let _ = fs::remove_dir_all(payload_directory);
    }
    result
}

fn extract_archive_contents(
    archive_path: &Path,
    payload_directory: &Path,
    limits: PackageExtractionLimits,
    expected_package_id: &str,
    expected_version: &str,
) -> Result<Option<String>, PackageEngineError> {
    let archive_file = fs::File::open(archive_path).map_err(PackageEngineError::Io)?;
    let mut archive = ZipArchive::new(archive_file).map_err(PackageEngineError::Archive)?;
    let (metadata_index, entry_point) =
        read_and_validate_package_metadata(&mut archive, expected_package_id, expected_version)?;
    if archive.len() > limits.maximum_entries {
        return Err(PackageEngineError::InvalidPackage(
            "package entry count exceeds the configured limit",
        ));
    }

    let mut entries = Vec::with_capacity(archive.len());
    let mut entry_kinds = HashMap::<String, bool>::with_capacity(archive.len());
    let mut total_declared_bytes = 0_u64;
    let mut payload_root_seen = false;

    for index in 0..archive.len() {
        if index == metadata_index {
            continue;
        }
        let entry = archive
            .by_index(index)
            .map_err(PackageEngineError::Archive)?;
        let raw_name = entry.name();
        if entry.enclosed_name().is_none() {
            return Err(PackageEngineError::InvalidPackage(
                "package contains a path outside its archive root",
            ));
        }
        if raw_name == "payload/" {
            if payload_root_seen || !entry.is_dir() || entry.size() != 0 {
                return Err(PackageEngineError::InvalidPackage(
                    "package payload root entry is missing, duplicated, or invalid",
                ));
            }
            payload_root_seen = true;
            continue;
        }
        let payload_name = raw_name
            .strip_prefix(PACKAGE_PAYLOAD_ROOT)
            .filter(|name| !name.is_empty())
            .ok_or(PackageEngineError::InvalidPackage(
                "package entries must be stored beneath payload/",
            ))?;
        let relative_path = validate_archive_path(payload_name)?;
        let is_directory = entry.is_dir();

        if entry.is_symlink() || (!is_directory && !entry.is_file()) {
            return Err(PackageEngineError::InvalidPackage(
                "symbolic links and special files are not supported in Turkuaz packages",
            ));
        }
        if is_directory && entry.size() != 0 {
            return Err(PackageEngineError::InvalidPackage(
                "directory entries cannot contain file data",
            ));
        }
        if !is_directory && entry.size() > limits.maximum_file_bytes {
            return Err(PackageEngineError::InvalidPackage(
                "package file exceeds the configured uncompressed size limit",
            ));
        }
        total_declared_bytes = total_declared_bytes.checked_add(entry.size()).ok_or(
            PackageEngineError::InvalidPackage("package expanded size overflowed"),
        )?;
        if total_declared_bytes > limits.maximum_total_bytes {
            return Err(PackageEngineError::InvalidPackage(
                "package exceeds the configured total uncompressed size limit",
            ));
        }

        let normalized_path = relative_path
            .to_string_lossy()
            .replace('\\', "/")
            .to_lowercase();
        if entry_kinds
            .insert(normalized_path.clone(), is_directory)
            .is_some()
        {
            return Err(PackageEngineError::InvalidPackage(
                "package contains duplicate or case-colliding paths",
            ));
        }
        entries.push(PlannedEntry {
            index,
            relative_path,
            is_directory,
            uncompressed_size: entry.size(),
        });
    }

    if !payload_root_seen {
        return Err(PackageEngineError::InvalidPackage(
            "package payload root directory is missing",
        ));
    }
    validate_entry_conflicts(&entry_kinds)?;
    if let Some(entry_point) = entry_point.as_deref() {
        if !entry_kinds
            .get(&entry_point.to_lowercase())
            .is_some_and(|is_directory| !*is_directory)
        {
            return Err(PackageEngineError::InvalidPackage(
                "package entry point must name a regular payload file",
            ));
        }
    }
    if entries.is_empty() {
        return Err(PackageEngineError::InvalidPackage(
            "package payload does not contain any entries",
        ));
    }

    for planned in &entries {
        let destination = payload_directory.join(&planned.relative_path);
        if planned.is_directory {
            fs::create_dir_all(&destination).map_err(PackageEngineError::Io)?;
            continue;
        }

        let parent = destination
            .parent()
            .ok_or(PackageEngineError::InvalidPackage(
                "package file path has no parent directory",
            ))?;
        fs::create_dir_all(parent).map_err(PackageEngineError::Io)?;

        let mut source = archive
            .by_index(planned.index)
            .map_err(PackageEngineError::Archive)?;
        let mut target = fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&destination)
            .map_err(PackageEngineError::Io)?;
        let mut bounded_source = (&mut source).take(limits.maximum_file_bytes.saturating_add(1));
        let copied_bytes =
            io::copy(&mut bounded_source, &mut target).map_err(PackageEngineError::Io)?;
        if copied_bytes != planned.uncompressed_size || copied_bytes > limits.maximum_file_bytes {
            return Err(PackageEngineError::InvalidPackage(
                "package entry size differs from its declared size",
            ));
        }
        target.flush().map_err(PackageEngineError::Io)?;
        target.sync_all().map_err(PackageEngineError::Io)?;
    }

    Ok(entry_point)
}

fn read_and_validate_package_metadata(
    archive: &mut ZipArchive<fs::File>,
    expected_package_id: &str,
    expected_version: &str,
) -> Result<(usize, Option<String>), PackageEngineError> {
    let metadata_index = archive.index_for_name(PACKAGE_METADATA_ENTRY).ok_or(
        PackageEngineError::InvalidPackage("package metadata entry is missing"),
    )?;
    let mut entry = archive
        .by_index(metadata_index)
        .map_err(PackageEngineError::Archive)?;
    if !entry.is_file() || entry.is_symlink() || entry.size() > MAXIMUM_PACKAGE_METADATA_BYTES {
        return Err(PackageEngineError::InvalidPackage(
            "package metadata entry is not a bounded regular file",
        ));
    }

    let declared_size = entry.size();
    let mut bytes = Vec::with_capacity(declared_size as usize);
    (&mut entry)
        .take(MAXIMUM_PACKAGE_METADATA_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(PackageEngineError::Io)?;
    if bytes.len() as u64 != declared_size || bytes.len() as u64 > MAXIMUM_PACKAGE_METADATA_BYTES {
        return Err(PackageEngineError::InvalidPackage(
            "package metadata size differs from its declared size",
        ));
    }
    drop(entry);

    let metadata: PackageArchiveMetadata = serde_json::from_slice(&bytes)
        .map_err(|_| PackageEngineError::InvalidPackage("package metadata JSON is invalid"))?;
    let metadata_version_is_supported = match metadata.format_version {
        PACKAGE_FORMAT_VERSION => metadata.entry_point.is_none(),
        crate::format::PACKAGE_FORMAT_VERSION_WITH_ENTRY_POINT => metadata.entry_point.is_some(),
        _ => false,
    };
    if !metadata_version_is_supported
        || metadata.package_id != expected_package_id
        || metadata.version != expected_version
    {
        return Err(PackageEngineError::InvalidPackage(
            "package metadata does not match the requested signed release",
        ));
    }
    let entry_point = metadata.entry_point;
    if let Some(entry_point) = entry_point.as_deref() {
        let relative = validate_archive_path(entry_point)?;
        let entry_name = format!(
            "{PACKAGE_PAYLOAD_ROOT}{}",
            relative.to_string_lossy().replace('\\', "/")
        );
        let index =
            archive
                .index_for_name(&entry_name)
                .ok_or(PackageEngineError::InvalidPackage(
                    "package entry point file is missing",
                ))?;
        let entry = archive
            .by_index(index)
            .map_err(PackageEngineError::Archive)?;
        if !entry.is_file() || entry.is_symlink() {
            return Err(PackageEngineError::InvalidPackage(
                "package entry point must be a regular payload file",
            ));
        }
    }
    Ok((metadata_index, entry_point))
}

fn validate_archive_path(raw_name: &str) -> Result<PathBuf, PackageEngineError> {
    if raw_name.is_empty()
        || raw_name.starts_with('/')
        || raw_name.starts_with('\\')
        || raw_name.contains('\\')
        || raw_name.contains(':')
        || raw_name.contains('\0')
    {
        return Err(PackageEngineError::InvalidPackage(
            "package contains an absolute or platform-specific path",
        ));
    }

    let trimmed_name = raw_name.strip_suffix('/').unwrap_or(raw_name);
    if trimmed_name.is_empty() {
        return Err(PackageEngineError::InvalidPackage(
            "package contains an empty path",
        ));
    }

    let mut relative_path = PathBuf::new();
    for segment in trimmed_name.split('/') {
        if segment.is_empty()
            || segment == "."
            || segment == ".."
            || segment.ends_with('.')
            || segment.ends_with(' ')
            || segment.chars().any(|character| {
                character < ' ' || matches!(character, '<' | '>' | '"' | '|' | '?' | '*')
            })
        {
            return Err(PackageEngineError::InvalidPackage(
                "package path contains an unsafe Windows path segment",
            ));
        }
        reject_reserved_windows_name(segment)?;
        relative_path.push(segment);
    }

    Ok(relative_path)
}

fn reject_reserved_windows_name(segment: &str) -> Result<(), PackageEngineError> {
    let stem = segment
        .split('.')
        .next()
        .unwrap_or_default()
        .trim_end_matches(|character| matches!(character, ' ' | '.'))
        .to_ascii_uppercase();
    let reserved = matches!(stem.as_str(), "CON" | "PRN" | "AUX" | "NUL")
        || ["COM", "LPT"].iter().any(|prefix| {
            stem.strip_prefix(prefix).is_some_and(|suffix| {
                matches!(
                    suffix,
                    "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" | "¹" | "²" | "³"
                )
            })
        });
    if reserved {
        return Err(PackageEngineError::InvalidPackage(
            "package path uses a reserved Windows device name",
        ));
    }
    Ok(())
}

fn validate_entry_conflicts(entry_kinds: &HashMap<String, bool>) -> Result<(), PackageEngineError> {
    for (path, _) in entry_kinds {
        let mut parent = path.as_str();
        while let Some((ancestor, _)) = parent.rsplit_once('/') {
            if entry_kinds
                .get(ancestor)
                .is_some_and(|ancestor_is_directory| !ancestor_is_directory)
            {
                return Err(PackageEngineError::InvalidPackage(
                    "package file path conflicts with a parent file",
                ));
            }
            parent = ancestor;
        }
    }
    Ok(())
}
