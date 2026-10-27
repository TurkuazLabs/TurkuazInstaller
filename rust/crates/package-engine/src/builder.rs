// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/builder.rs
// Amac: Uygulama payload dizinini Turkuaz'in ozel .tzpkg ZIP paketine donusturmek
// Modul - Rust Package Builder
// Version: 0.1.0
// Aciklama: Symlink ve path collision reddi, sinirli kaynak taramasi, gecici dosya ve no-overwrite publish uygular
// Bagimli Oldugu Katman: Tool | Service

use std::{
    collections::HashMap,
    error::Error,
    fmt, fs, io,
    io::Write,
    path::{Component, Path, PathBuf},
};

use uuid::Uuid;
use zip::{
    CompressionMethod, ZipWriter,
    result::ZipError,
    write::SimpleFileOptions,
};

use crate::{
    PackageExtractionLimits, PackageEngineError, validate_archive_path, validate_entry_conflicts,
};

#[derive(Debug)]
pub enum PackageBuildError {
    Io(io::Error),
    Archive(ZipError),
    InvalidPackage(&'static str),
}

impl fmt::Display for PackageBuildError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(error) => write!(formatter, "Package build I/O failed: {error}"),
            Self::Archive(error) => write!(formatter, "Package archive build failed: {error}"),
            Self::InvalidPackage(message) => write!(formatter, "Package source is invalid: {message}"),
        }
    }
}

impl Error for PackageBuildError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            Self::Archive(error) => Some(error),
            Self::InvalidPackage(_) => None,
        }
    }
}

/// @dev-id TIZ-PKG-BUILD-010
/// @purpose Build a custom .tzpkg archive from a trusted release payload directory.
/// @reason The Rust installer must own both ends of its package format without invoking Velopack.
/// @contract Rejects symlinks, special files, Windows-invalid/colliding paths, output inside source, and source size/count overages; publishes without overwriting an existing package.
/// @depends Shared package path validation, zip-rs, and UUID temporary names.
pub fn build_full_package(
    source_directory: impl AsRef<Path>,
    output_file: impl AsRef<Path>,
    limits: PackageExtractionLimits,
) -> Result<(), PackageBuildError> {
    if limits.maximum_entries == 0
        || limits.maximum_file_bytes == 0
        || limits.maximum_total_bytes == 0
    {
        return Err(PackageBuildError::InvalidPackage(
            "package build limits must be positive",
        ));
    }

    let source_directory = source_directory.as_ref();
    let source_metadata =
        fs::symlink_metadata(source_directory).map_err(PackageBuildError::Io)?;
    if source_metadata.file_type().is_symlink() || !source_metadata.is_dir() {
        return Err(PackageBuildError::InvalidPackage(
            "package source must be a non-symlink directory",
        ));
    }
    let source_directory = fs::canonicalize(source_directory).map_err(PackageBuildError::Io)?;

    let output_file = output_file.as_ref();
    if output_file
        .extension()
        .is_none_or(|extension| !extension.to_string_lossy().eq_ignore_ascii_case("tzpkg"))
    {
        return Err(PackageBuildError::InvalidPackage(
            "custom package output must use the .tzpkg extension",
        ));
    }

    let output_parent = output_file
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .unwrap_or_else(|| Path::new("."));
    fs::create_dir_all(output_parent).map_err(PackageBuildError::Io)?;
    let output_parent = fs::canonicalize(output_parent).map_err(PackageBuildError::Io)?;
    let output_name = output_file.file_name().ok_or(PackageBuildError::InvalidPackage(
        "package output file name is missing",
    ))?;
    let output_file = output_parent.join(output_name);
    if output_file.starts_with(&source_directory) {
        return Err(PackageBuildError::InvalidPackage(
            "package output cannot be inside the source directory",
        ));
    }
    match fs::symlink_metadata(&output_file) {
        Ok(_) => {
            return Err(PackageBuildError::InvalidPackage(
                "package output already exists",
            ));
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(PackageBuildError::Io(error)),
    }

    let temporary_file = output_parent.join(format!(
        ".{}.partial-{}",
        output_name.to_string_lossy(),
        Uuid::new_v4()
    ));

    let mut entries = Vec::new();
    let mut entry_kinds = HashMap::new();
    let mut total_bytes = 0_u64;
    collect_entries(
        &source_directory,
        &source_directory,
        &mut entries,
        &mut entry_kinds,
        &mut total_bytes,
        limits,
    )?;
    if entries.iter().all(|entry| entry.is_directory) {
        return Err(PackageBuildError::InvalidPackage(
            "package source contains no regular files",
        ));
    }
    validate_entry_conflicts(&entry_kinds)
        .map_err(|_| PackageBuildError::InvalidPackage("package source paths conflict"))?;
    entries.sort_by(|left, right| left.archive_path.cmp(&right.archive_path));

    let build_result = build_archive(&temporary_file, &entries);
    if let Err(error) = build_result {
        let _ = fs::remove_file(&temporary_file);
        return Err(error);
    }

    if let Err(error) = fs::hard_link(&temporary_file, &output_file) {
        let _ = fs::remove_file(&temporary_file);
        return Err(PackageBuildError::Io(error));
    }
    fs::remove_file(&temporary_file).map_err(PackageBuildError::Io)?;
    Ok(())
}

#[derive(Debug)]
struct BuildEntry {
    source_path: PathBuf,
    archive_path: String,
    is_directory: bool,
    size_bytes: u64,
}

fn collect_entries(
    source_directory: &Path,
    current_directory: &Path,
    entries: &mut Vec<BuildEntry>,
    entry_kinds: &mut HashMap<String, bool>,
    total_bytes: &mut u64,
    limits: PackageExtractionLimits,
) -> Result<(), PackageBuildError> {
    let mut children = fs::read_dir(current_directory)
        .map_err(PackageBuildError::Io)?
        .map(|entry| entry.map(|entry| entry.path()))
        .collect::<Result<Vec<_>, _>>()
        .map_err(PackageBuildError::Io)?;
    children.sort();

    for child in children {
        if entries.len() >= limits.maximum_entries {
            return Err(PackageBuildError::InvalidPackage(
                "package source exceeds the configured entry count limit",
            ));
        }
        let metadata = fs::symlink_metadata(&child).map_err(PackageBuildError::Io)?;
        if metadata.file_type().is_symlink() || (!metadata.is_dir() && !metadata.is_file()) {
            return Err(PackageBuildError::InvalidPackage(
                "package source contains a symlink or special file",
            ));
        }

        let relative_path = child
            .strip_prefix(source_directory)
            .map_err(|_| PackageBuildError::InvalidPackage("source path escaped its root"))?;
        let archive_path = archive_path(relative_path)?;
        validate_archive_path(&archive_path)
            .map_err(|_| PackageBuildError::InvalidPackage("package source path is unsafe"))?;

        let is_directory = metadata.is_dir();
        let normalized_path = archive_path.to_lowercase();
        if entry_kinds
            .insert(normalized_path, is_directory)
            .is_some()
        {
            return Err(PackageBuildError::InvalidPackage(
                "package source contains duplicate or case-colliding paths",
            ));
        }

        let size_bytes = if is_directory { 0 } else { metadata.len() };
        if size_bytes > limits.maximum_file_bytes {
            return Err(PackageBuildError::InvalidPackage(
                "package source file exceeds the configured size limit",
            ));
        }
        *total_bytes = total_bytes.checked_add(size_bytes).ok_or(
            PackageBuildError::InvalidPackage("package source expanded size overflowed"),
        )?;
        if *total_bytes > limits.maximum_total_bytes {
            return Err(PackageBuildError::InvalidPackage(
                "package source exceeds the configured total size limit",
            ));
        }

        entries.push(BuildEntry {
            source_path: child.clone(),
            archive_path: archive_path.clone(),
            is_directory,
            size_bytes,
        });
        if is_directory {
            collect_entries(
                source_directory,
                &child,
                entries,
                entry_kinds,
                total_bytes,
                limits,
            )?;
        }
    }
    Ok(())
}

fn archive_path(path: &Path) -> Result<String, PackageBuildError> {
    let mut segments = Vec::new();
    for component in path.components() {
        match component {
            Component::Normal(segment) => segments.push(
                segment
                    .to_str()
                    .ok_or(PackageBuildError::InvalidPackage(
                        "package source path is not valid Unicode",
                    ))?,
            ),
            _ => {
                return Err(PackageBuildError::InvalidPackage(
                    "package source path contains a non-relative component",
                ));
            }
        }
    }
    if segments.is_empty() {
        return Err(PackageBuildError::InvalidPackage(
            "package source contains an empty path",
        ));
    }
    Ok(segments.join("/"))
}

fn build_archive(path: &Path, entries: &[BuildEntry]) -> Result<(), PackageBuildError> {
    let output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(PackageBuildError::Io)?;
    let mut archive = ZipWriter::new(output);
    let options = SimpleFileOptions::default()
        .compression_method(CompressionMethod::Deflated)
        .large_file(true);

    for entry in entries {
        if entry.is_directory {
            archive
                .add_directory(format!("{}/", entry.archive_path), options)
                .map_err(PackageBuildError::Archive)?;
            continue;
        }

        archive
            .start_file(&entry.archive_path, options)
            .map_err(PackageBuildError::Archive)?;
        let mut source = fs::File::open(&entry.source_path).map_err(PackageBuildError::Io)?;
        let copied_bytes =
            io::copy(&mut source, &mut archive).map_err(PackageBuildError::Io)?;
        if copied_bytes != entry.size_bytes {
            return Err(PackageBuildError::InvalidPackage(
                "package source changed while it was being archived",
            ));
        }
    }

    let output = archive.finish().map_err(PackageBuildError::Archive)?;
    output.sync_all().map_err(PackageBuildError::Io)?;
    Ok(())
}
