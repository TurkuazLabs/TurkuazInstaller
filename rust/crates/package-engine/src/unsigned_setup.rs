// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/unsigned_setup.rs
// Amac: NSIS olmadan yalniz yerel standart kurulum icin .tzpkg gomulu EXE uretmek
// Modul - Rust Package Engine
// Version: 0.3.2
// Aciklama: Imzasiz yerel EXE butunluk kontrolu; imzali release/trust asla atlanmaz
// Bagimli Oldugu Katman: Service | Tool

use std::{
    fs,
    io::{self, Read, Seek, SeekFrom, Write},
    path::{Component, Path, PathBuf},
};

use sha2::{Digest, Sha256};

use crate::{
    MalwareScanner, PackageEngineError, PackageExtractionLimits, ScanPhase, extract_archive,
    format::{
        MAXIMUM_PACKAGE_METADATA_BYTES, PACKAGE_FORMAT_VERSION,
        PACKAGE_FORMAT_VERSION_WITH_ENTRY_POINT, PACKAGE_METADATA_ENTRY, PackageArchiveMetadata,
    },
    local_install_receipt::write_local_install_record,
    scan_directory,
    wizard_config::{MAX_WIZARD_CONFIG_BYTES, WizardConfig},
};
use turkuaz_installer_domain::{package_id::PackageId, semantic_version::SemanticVersion};
use uuid::Uuid;
use zip::ZipArchive;

const MAGIC: [u8; 8] = *b"TZLOCAL1";
const CONFIGURED_MAGIC: [u8; 8] = *b"TZLOCAL2";
const WIZARD_MAGIC: [u8; 8] = *b"TZWIZR01";
const WIZARD_FOOTER_BYTES: u64 = 8 + 8 + 32;
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

fn footer(file: &mut fs::File) -> Result<Option<(u64, [u8; 32], bool)>, PackageEngineError> {
    let len = file.metadata().map_err(PackageEngineError::Io)?.len();
    if len < FOOTER_BYTES {
        return Ok(None);
    }
    file.seek(SeekFrom::End(-(FOOTER_BYTES as i64)))
        .map_err(PackageEngineError::Io)?;
    let mut magic = [0; 8];
    file.read_exact(&mut magic)
        .map_err(PackageEngineError::Io)?;
    if magic != MAGIC && magic != CONFIGURED_MAGIC {
        return Ok(None);
    }
    let mut encoded = [0; 8];
    file.read_exact(&mut encoded)
        .map_err(PackageEngineError::Io)?;
    let package_length = u64::from_le_bytes(encoded);
    if package_length == 0
        || package_length > MAXIMUM_PACKAGE_BYTES
        || package_length >= len - FOOTER_BYTES
    {
        return Err(invalid("local setup package length is invalid"));
    }
    let mut digest = [0; 32];
    file.read_exact(&mut digest)
        .map_err(PackageEngineError::Io)?;
    Ok(Some((package_length, digest, magic == CONFIGURED_MAGIC)))
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
        let count = from
            .read(&mut bytes[..limit])
            .map_err(PackageEngineError::Io)?;
        if count == 0 {
            return Err(invalid("local setup input was truncated"));
        }
        to.write_all(&bytes[..count])
            .map_err(PackageEngineError::Io)?;
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
    create_unsigned_local_setup_inner(stub.as_ref(), package.as_ref(), output.as_ref(), None)
}

/// @dev-id BRK-WIZARD-EMBED-001
/// @purpose Embed bounded wizard options before the TZLOCAL1 package payload,
/// preserving compatibility with existing archive footer and signed setup APIs.
/// @contract Validated EULA text and page order only; unsigned integrity is not
/// publisher authentication. Existing TZLOCAL1 readers can still extract.
pub fn create_unsigned_local_setup_with_wizard(
    stub: impl AsRef<Path>,
    package: impl AsRef<Path>,
    output: impl AsRef<Path>,
    config: &WizardConfig,
) -> Result<(), PackageEngineError> {
    config
        .validate()
        .map_err(|_| invalid("local wizard configuration is invalid"))?;
    create_unsigned_local_setup_inner(
        stub.as_ref(),
        package.as_ref(),
        output.as_ref(),
        Some(config),
    )
}

fn create_unsigned_local_setup_inner(
    stub: &Path,
    package: &Path,
    output: &Path,
    config: Option<&WizardConfig>,
) -> Result<(), PackageEngineError> {
    let wizard_bytes = match config {
        Some(config) => {
            let bytes = serde_json::to_vec(config)
                .map_err(|_| invalid("local wizard JSON cannot be serialized"))?;
            if bytes.is_empty() || bytes.len() > MAX_WIZARD_CONFIG_BYTES {
                return Err(invalid("local wizard configuration size is invalid"));
            }
            Some(bytes)
        }
        None => None,
    };
    let stub_len = regular_file(stub)?.len();
    let package_len = regular_file(package)?.len();
    if stub_len < 2 || package_len == 0 || package_len > MAXIMUM_PACKAGE_BYTES {
        return Err(invalid("local setup stub or package size is invalid"));
    }
    if package
        .extension()
        .is_none_or(|x| !x.eq_ignore_ascii_case("tzpkg"))
        || output
            .extension()
            .is_none_or(|x| !x.eq_ignore_ascii_case("exe"))
    {
        return Err(invalid("local setup requires .tzpkg input and .exe output"));
    }
    let mut stub_reader = fs::File::open(stub).map_err(PackageEngineError::Io)?;
    let mut header = [0; 2];
    stub_reader
        .read_exact(&mut header)
        .map_err(PackageEngineError::Io)?;
    if header != *b"MZ" || footer(&mut stub_reader)?.is_some() {
        return Err(invalid(
            "local setup stub must be a plain Windows executable",
        ));
    }
    stub_reader
        .seek(SeekFrom::Start(0))
        .map_err(PackageEngineError::Io)?;
    read_identity(package)?;
    let mut pkg_reader = fs::File::open(package).map_err(PackageEngineError::Io)?;
    let mut out = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output)
        .map_err(PackageEngineError::Io)?;
    let result = (|| {
        io::copy(&mut stub_reader, &mut out).map_err(PackageEngineError::Io)?;
        if let Some(bytes) = &wizard_bytes {
            out.write_all(bytes).map_err(PackageEngineError::Io)?;
            out.write_all(&WIZARD_MAGIC)
                .map_err(PackageEngineError::Io)?;
            out.write_all(&(bytes.len() as u64).to_le_bytes())
                .map_err(PackageEngineError::Io)?;
            let config_digest: [u8; 32] = Sha256::digest(bytes).into();
            out.write_all(&config_digest)
                .map_err(PackageEngineError::Io)?;
        }
        let digest = copy_hashed(&mut pkg_reader, &mut out, package_len)?;
        out.write_all(if wizard_bytes.is_some() {
            &CONFIGURED_MAGIC
        } else {
            &MAGIC
        })
        .map_err(PackageEngineError::Io)?;
        out.write_all(&package_len.to_le_bytes())
            .map_err(PackageEngineError::Io)?;
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
    if output
        .extension()
        .is_none_or(|x| !x.eq_ignore_ascii_case("tzpkg"))
    {
        return Err(invalid("unsigned local payload output must be .tzpkg"));
    }
    let mut input = fs::File::open(executable).map_err(PackageEngineError::Io)?;
    let (pkg_len, expected, _) =
        footer(&mut input)?.ok_or(invalid("executable is not an unsigned local setup"))?;
    let start = metadata
        .len()
        .checked_sub(FOOTER_BYTES + pkg_len)
        .ok_or(invalid("unsigned local setup offsets are invalid"))?;
    input
        .seek(SeekFrom::Start(start))
        .map_err(PackageEngineError::Io)?;
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

/// @dev-id BRK-WIZARD-EMBED-002
/// @purpose Read optional wizard settings from an unsigned native setup.
/// @contract Legacy EXEs without a wizard footer retain the existing defaults.
/// Damaged or unsupported settings fail closed, never silently skip a license.
pub fn read_unsigned_local_wizard_config(
    executable: impl AsRef<Path>,
) -> Result<WizardConfig, PackageEngineError> {
    let mut file = fs::File::open(executable).map_err(PackageEngineError::Io)?;
    let len = file.metadata().map_err(PackageEngineError::Io)?.len();
    let (package_len, _, has_config) =
        footer(&mut file)?.ok_or(invalid("executable is not an unsigned local setup"))?;
    if !has_config {
        return Ok(WizardConfig::default());
    }
    let package_start = len
        .checked_sub(FOOTER_BYTES + package_len)
        .ok_or(invalid("local setup package offsets are invalid"))?;
    if package_start < WIZARD_FOOTER_BYTES {
        return Err(invalid("configured setup has no wizard footer"));
    }
    file.seek(SeekFrom::Start(package_start - WIZARD_FOOTER_BYTES))
        .map_err(PackageEngineError::Io)?;
    let mut magic = [0u8; 8];
    file.read_exact(&mut magic)
        .map_err(PackageEngineError::Io)?;
    if magic != WIZARD_MAGIC {
        return Err(invalid("configured setup wizard marker is missing"));
    }
    let mut size_bytes = [0u8; 8];
    file.read_exact(&mut size_bytes)
        .map_err(PackageEngineError::Io)?;
    let config_len = u64::from_le_bytes(size_bytes);
    if config_len == 0
        || config_len > MAX_WIZARD_CONFIG_BYTES as u64
        || config_len > package_start - WIZARD_FOOTER_BYTES
    {
        return Err(invalid("local wizard data size is invalid"));
    }
    let mut expected = [0u8; 32];
    file.read_exact(&mut expected)
        .map_err(PackageEngineError::Io)?;
    let start = package_start - WIZARD_FOOTER_BYTES - config_len;
    file.seek(SeekFrom::Start(start))
        .map_err(PackageEngineError::Io)?;
    let mut bytes = vec![0u8; config_len as usize];
    file.read_exact(&mut bytes)
        .map_err(PackageEngineError::Io)?;
    let actual: [u8; 32] = Sha256::digest(&bytes).into();
    if actual != expected {
        return Err(invalid("local wizard configuration digest mismatch"));
    }
    let config: WizardConfig = serde_json::from_slice(&bytes)
        .map_err(|_| invalid("local wizard configuration JSON is invalid"))?;
    config
        .validate()
        .map_err(|_| invalid("local wizard pages or EULA are invalid"))?;
    Ok(config)
}

/// Only local installer metadata; no publisher authentication is asserted.
#[derive(Debug, Clone)]
pub struct UnsignedLocalIdentity {
    pub package_id: String,
    pub version: String,
    /// Verified against package payload paths during the extraction step.
    /// UI shortcut creation must additionally validate the installed file.
    pub entry_point: Option<String>,
}

fn read_identity(package: &Path) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    let file = fs::File::open(package).map_err(PackageEngineError::Io)?;
    let mut zip = ZipArchive::new(file).map_err(PackageEngineError::Archive)?;
    let mut entry = zip
        .by_name(PACKAGE_METADATA_ENTRY)
        .map_err(PackageEngineError::Archive)?;
    if !entry.is_file() || entry.is_symlink() || entry.size() > MAXIMUM_PACKAGE_METADATA_BYTES {
        return Err(invalid(
            "unsigned local package metadata is missing or invalid",
        ));
    }
    let size = entry.size();
    let mut bytes = Vec::with_capacity(size as usize);
    (&mut entry)
        .take(MAXIMUM_PACKAGE_METADATA_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(PackageEngineError::Io)?;
    if bytes.len() as u64 != size {
        return Err(invalid("unsigned local metadata length mismatch"));
    }
    let metadata: PackageArchiveMetadata = serde_json::from_slice(&bytes)
        .map_err(|_| invalid("unsigned local package metadata JSON is invalid"))?;
    if !match metadata.format_version {
        PACKAGE_FORMAT_VERSION => metadata.entry_point.is_none(),
        PACKAGE_FORMAT_VERSION_WITH_ENTRY_POINT => metadata.entry_point.is_some(),
        _ => false,
    } {
        return Err(invalid("unsigned local package format version is invalid"));
    }
    PackageId::parse(&metadata.package_id)
        .map_err(|_| invalid("unsigned local package id is invalid"))?;
    SemanticVersion::parse(&metadata.version)
        .map_err(|_| invalid("unsigned local package version is invalid"))?;
    Ok(UnsignedLocalIdentity {
        package_id: metadata.package_id,
        version: metadata.version,
        entry_point: metadata.entry_point,
    })
}

struct TemporaryFiles(Vec<PathBuf>);
impl Drop for TemporaryFiles {
    fn drop(&mut self) {
        for path in &self.0 {
            let metadata = fs::symlink_metadata(path);
            if let Ok(meta) = metadata {
                if meta.is_dir() && !meta.file_type().is_symlink() {
                    let _ = fs::remove_dir_all(path);
                } else {
                    let _ = fs::remove_file(path);
                }
            }
        }
    }
}

fn ensure_safe_directory(path: &Path) -> Result<(), PackageEngineError> {
    if !path.is_absolute() || path.components().any(|c| matches!(c, Component::ParentDir)) {
        return Err(invalid(
            "unsigned local install directory must be an absolute path without traversal",
        ));
    }
    let mut visited = PathBuf::new();
    for component in path.components() {
        visited.push(component.as_os_str());
        if matches!(component, Component::Prefix(_) | Component::RootDir) {
            continue;
        }
        let meta = match fs::symlink_metadata(&visited) {
            Ok(meta) => meta,
            Err(err) if err.kind() == io::ErrorKind::NotFound => {
                match fs::create_dir(&visited) {
                    Ok(()) => {}
                    Err(err) if err.kind() == io::ErrorKind::AlreadyExists => {}
                    Err(err) => return Err(PackageEngineError::Io(err)),
                }
                fs::symlink_metadata(&visited).map_err(PackageEngineError::Io)?
            }
            Err(err) => return Err(PackageEngineError::Io(err)),
        };
        if !meta.is_dir() || meta.file_type().is_symlink() || windows_reparse_point(&meta) {
            return Err(invalid(
                "unsigned local install parent contains a file, symlink, or junction",
            ));
        }
    }
    Ok(())
}

#[cfg(windows)]
fn windows_reparse_point(meta: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    meta.file_attributes() & 0x0400 != 0
}
#[cfg(not(windows))]
fn windows_reparse_point(_meta: &fs::Metadata) -> bool {
    false
}

fn embedded_to_temporary_package(
    executable: &Path,
    parent: &Path,
    guard: &mut TemporaryFiles,
) -> Result<PathBuf, PackageEngineError> {
    let package = parent.join(format!(".turkuaz-native-{}.tzpkg", Uuid::new_v4()));
    // Guard before creation to ensure cleanup on all errors.
    guard.0.push(package.clone());
    extract_unsigned_local_package(executable, &package)?;
    Ok(package)
}

/// @dev-id BRK-LOCAL-SETUP-004
/// @purpose Display the embedded unsigned package identity in the local setup UI.
pub fn unsigned_local_identity(
    executable: impl AsRef<Path>,
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    let mut guard = TemporaryFiles(Vec::new());
    let base = std::env::temp_dir();
    let package = embedded_to_temporary_package(executable.as_ref(), &base, &mut guard)?;
    read_identity(&package)
}

/// @dev-id BRK-LOCAL-SETUP-005
/// @purpose Install a local unsigned package to a new folder without modifying signed install state.
/// @contract No existing targets, validated ZIP paths, bounded extraction, symlink-safe ancestors.
/// @warning Local integrity is NOT publisher authenticity; no signed update/repair/rollback support.
pub fn install_unsigned_local_setup(
    executable: impl AsRef<Path>,
    target: impl AsRef<Path>,
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    install_unsigned_local_setup_inner(executable.as_ref(), target.as_ref(), None, None, None)
}

/// Named milestones from the real installer operations; not a percentage or
/// an estimate of bytes transferred.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum LocalInstallPhase {
    VerifyingPackage,
    ExtractingPayload,
    PublishingInstall,
}

/// @dev-id BRK-LOCAL-SETUP-PROGRESS-001
/// @purpose Let the Windows wizard show actual installer operations.
/// @contract Callbacks are informational and never bypass install validation,
/// scanning policy, receipt creation, or no-overwrite publishing.
pub fn install_unsigned_local_setup_with_progress(
    executable: impl AsRef<Path>,
    target: impl AsRef<Path>,
    on_phase: &mut dyn FnMut(LocalInstallPhase),
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    install_unsigned_local_setup_inner(executable.as_ref(), target.as_ref(), None, Some(on_phase), None)
}

/// @dev-id BRK-LOCAL-SETUP-RECYCLE-001
/// @purpose Prepare and validate the new payload before a caller-approved
/// platform adapter clears a pre-existing destination.
/// @contract Standard install APIs still reject occupied targets. The adapter
/// must move the old folder to recoverable storage; the engine never deletes it.
/// A failed adapter or still-occupied path aborts before publishing.
pub fn install_unsigned_local_setup_with_replacement(
    executable: impl AsRef<Path>,
    target: impl AsRef<Path>,
    on_phase: &mut dyn FnMut(LocalInstallPhase),
    on_existing_target: &mut dyn FnMut(&Path) -> Result<(), PackageEngineError>,
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    install_unsigned_local_setup_inner(
        executable.as_ref(),
        target.as_ref(),
        None,
        Some(on_phase),
        Some(on_existing_target),
    )
}

/// @dev-id BRK-LOCAL-SETUP-006
/// @purpose Fail-closed local install with caller-provided platform-neutral AV scanner.
/// @contract Scan all staged files before publishing; deny threat, unknown, or engine failure.
/// @warning The legacy unsigned install entry point remains separate until a real AV
/// adapter and explicit product policy are configured. Do not claim it scans malware.
pub fn install_unsigned_local_setup_scanned(
    executable: impl AsRef<Path>,
    target: impl AsRef<Path>,
    scanner: &dyn MalwareScanner,
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    install_unsigned_local_setup_inner(executable.as_ref(), target.as_ref(), Some(scanner), None, None)
}

fn install_unsigned_local_setup_inner(
    executable: &Path,
    target: &Path,
    scanner: Option<&dyn MalwareScanner>,
    mut on_phase: Option<&mut dyn FnMut(LocalInstallPhase)>,
    mut on_existing_target: Option<&mut dyn FnMut(&Path) -> Result<(), PackageEngineError>>,
) -> Result<UnsignedLocalIdentity, PackageEngineError> {
    let parent = target
        .parent()
        .ok_or(invalid("local install target has no parent"))?;
    ensure_safe_directory(parent)?;
    match fs::symlink_metadata(target) {
        Ok(_) if on_existing_target.is_none() => {
            return Err(invalid(
                "local install target already exists; overwrite refused",
            ));
        }
        Ok(_) => {} // Opt-in callback runs only after staged payload validation.
        Err(err) if err.kind() == io::ErrorKind::NotFound => {}
        Err(err) => return Err(PackageEngineError::Io(err)),
    }
    if let Some(callback) = &mut on_phase {
        callback(LocalInstallPhase::VerifyingPackage);
    }
    let mut guard = TemporaryFiles(Vec::new());
    let package = embedded_to_temporary_package(executable, parent, &mut guard)?;
    let identity = read_identity(&package)?;
    let stage = parent.join(format!(".turkuaz-native-stage-{}", Uuid::new_v4()));
    guard.0.push(stage.clone());
    if let Some(callback) = &mut on_phase {
        callback(LocalInstallPhase::ExtractingPayload);
    }
    extract_archive(
        &package,
        &stage,
        PackageExtractionLimits::default(),
        &identity.package_id,
        &identity.version,
    )?;
    if let Some(scanner) = scanner {
        scan_directory(&stage, ScanPhase::BeforeInstall, scanner)
            .map_err(|_| invalid("malware scan did not return a clean result; install refused"))?;
    }
    // Record the exact staged payload before it becomes an installed tree.
    // Untracked older installs have no receipt and are never auto-uninstalled.
    write_local_install_record(&stage, &identity.package_id, &identity.version)?;
    if let Some(callback) = &mut on_phase {
        callback(LocalInstallPhase::PublishingInstall);
    }
    // Never overwrite in place. An explicitly opted-in platform adapter may
    // recycle an occupied folder only after staged files and receipt are ready.
    match fs::symlink_metadata(target) {
        Ok(_) => match &mut on_existing_target {
            Some(recycle) => recycle(target)?,
            None => return Err(invalid("local install target became occupied")),
        },
        Err(err) if err.kind() == io::ErrorKind::NotFound => {}
        Err(err) => return Err(PackageEngineError::Io(err)),
    }
    match fs::symlink_metadata(target) {
        Err(err) if err.kind() == io::ErrorKind::NotFound => {}
        Ok(_) => return Err(invalid("local install target is still occupied")),
        Err(err) => return Err(PackageEngineError::Io(err)),
    }
    fs::rename(&stage, target).map_err(PackageEngineError::Io)?;
    Ok(identity)
}

#[cfg(test)]
mod tests {
    use super::*;
    use uuid::Uuid;

    struct ScannerBlocker;
    impl MalwareScanner for ScannerBlocker {
        fn scan_file(
            &self,
            _file: &Path,
            _phase: ScanPhase,
        ) -> Result<crate::ScanVerdict, crate::ScanGateError> {
            Ok(crate::ScanVerdict::Threat)
        }
    }

    #[test]
    fn scanned_local_install_refuses_malware_before_target_is_published() {
        let root = std::env::temp_dir().join(format!("tz-malware-gate-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("fixture root");
        let source = root.join("source");
        fs::create_dir(&source).expect("fixture source");
        fs::write(source.join("demo.txt"), b"fixture").expect("fixture payload");
        let package = root.join("demo.tzpkg");
        let stub = root.join("studio.exe");
        let setup = root.join("setup.exe");
        let target = root.join("installed");
        let package_id = PackageId::parse("qa.malware").expect("id");
        let version = SemanticVersion::parse("1.0.0").expect("version");
        crate::build_full_package(
            &source,
            &package_id,
            &version,
            &package,
            PackageExtractionLimits::default(),
        )
        .expect("build package");
        fs::write(&stub, b"MZ-test-stub").expect("stub");
        create_unsigned_local_setup(&stub, &package, &setup).expect("setup");
        assert!(install_unsigned_local_setup_scanned(&setup, &target, &ScannerBlocker).is_err());
        assert!(!target.exists(), "unsafe target must not be published");
        fs::remove_dir_all(&root).expect("fixture cleanup");
    }

    #[test]
    fn local_setup_does_not_overwrite_existing_install_directory() {
        let root = std::env::temp_dir().join(format!("tz-local-existing-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("create");
        let output = root.join("existing");
        fs::create_dir(&output).expect("create existing");
        let err = install_unsigned_local_setup(root.join("nothing.exe"), &output);
        assert!(err.is_err());
        assert!(output.is_dir());
        fs::remove_dir_all(root).expect("cleanup");
    }

    #[test]
    fn local_setup_roundtrip_rejects_corruption_and_overwrites() {
        let root = std::env::temp_dir().join(format!("tz-native-test-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("test directory");
        let stub = root.join("stub.exe");
        let package = root.join("app.tzpkg");
        let installer = root.join("setup.exe");
        let extracted = root.join("unpacked.tzpkg");
        fs::write(&stub, b"MZ-example-rust-binary").expect("stub");
        let source = root.join("source");
        fs::create_dir(&source).expect("create source");
        fs::write(source.join("app.txt"), b"local-package-example").expect("source");
        let id = PackageId::parse("qa.local").expect("id");
        let version = SemanticVersion::parse("1.0.0").expect("version");
        crate::build_full_package(
            &source,
            &id,
            &version,
            &package,
            PackageExtractionLimits::default(),
        )
        .expect("package");
        create_unsigned_local_setup(&stub, &package, &installer).expect("create");
        assert!(has_unsigned_local_setup(&installer).expect("marker"));
        extract_unsigned_local_package(&installer, &extracted).expect("extract");
        assert_eq!(
            fs::read(&extracted).expect("read"),
            fs::read(&package).expect("original")
        );
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
    fn optional_eula_embeds_and_roundtrips_without_changing_payload_extraction() {
        let root = std::env::temp_dir().join(format!("tz-wizard-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("root");
        let stub = root.join("stub.exe");
        let pkg = root.join("app.tzpkg");
        let setup = root.join("setup.exe");
        let extracted = root.join("extracted.tzpkg");
        fs::write(&stub, b"MZ-local-setup-test").expect("stub");
        let source = root.join("source");
        fs::create_dir(&source).expect("source");
        fs::write(source.join("hello.txt"), b"safe fixture").expect("payload");
        let id = PackageId::parse("qa.wizard").expect("id");
        let version = SemanticVersion::parse("1.0.0").expect("version");
        crate::build_full_package(
            &source,
            &id,
            &version,
            &pkg,
            PackageExtractionLimits::default(),
        )
        .expect("package");
        let mut config = WizardConfig::default();
        config
            .page_mut(crate::WizardPageId::License)
            .unwrap()
            .enabled = true;
        config.license_text_tr = "Test sözleşmesidir".into();
        config.license_text_en = "Test agreement".into();
        create_unsigned_local_setup_with_wizard(&stub, &pkg, &setup, &config)
            .expect("configured installer");
        assert_eq!(read_unsigned_local_wizard_config(&setup).unwrap(), config);
        extract_unsigned_local_package(&setup, &extracted).expect("extract");
        assert_eq!(fs::read(&pkg).unwrap(), fs::read(&extracted).unwrap());
        let mut bytes = fs::read(&setup).expect("bytes");
        let start =
            bytes.len() - FOOTER_BYTES as usize - fs::metadata(&pkg).unwrap().len() as usize;
        let mut tampered_marker = bytes.clone();
        tampered_marker[start - WIZARD_FOOTER_BYTES as usize] ^= 1;
        fs::write(&setup, tampered_marker).expect("tamper marker");
        assert!(read_unsigned_local_wizard_config(&setup).is_err());
        bytes[start - WIZARD_FOOTER_BYTES as usize - 1] ^= 1;
        fs::write(&setup, bytes).expect("tamper");
        assert!(read_unsigned_local_wizard_config(&setup).is_err());
        fs::remove_dir_all(root).expect("clean");
    }

    #[test]
    fn native_unsigned_install_exercises_real_package_extraction_and_no_overwrite() {
        let root = std::env::temp_dir().join(format!("tz-local-e2e-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("test directory");
        let stub = root.join("studio.exe");
        let package = root.join("sample.tzpkg");
        let installer = root.join("standalone.exe");
        let source = root.join("source");
        fs::create_dir(&source).expect("source directory");
        fs::write(source.join("hello.txt"), "Türkçe test payload".as_bytes())
            .expect("test payload");
        let id = PackageId::parse("qa.native").expect("package id");
        let version = SemanticVersion::parse("1.0.0").expect("package version");
        crate::build_full_package(
            &source,
            &id,
            &version,
            &package,
            PackageExtractionLimits::default(),
        )
        .expect("build package");
        fs::write(&stub, b"MZ-rust-studio-stub").expect("stub");
        create_unsigned_local_setup(&stub, &package, &installer).expect("create setup");
        let inspected = unsigned_local_identity(&installer).expect("inspect setup");
        assert_eq!(inspected.package_id, "qa.native");
        assert_eq!(inspected.version, "1.0.0");

        let target = root.join("installed");
        let mut phases = Vec::new();
        let installed =
            install_unsigned_local_setup_with_progress(&installer, &target, &mut |phase| {
                phases.push(phase)
            })
            .expect("install with real phases");
        assert_eq!(
            phases,
            [
                LocalInstallPhase::VerifyingPackage,
                LocalInstallPhase::ExtractingPayload,
                LocalInstallPhase::PublishingInstall,
            ]
        );
        assert_eq!(installed.package_id, "qa.native");
        assert_eq!(
            fs::read(target.join("hello.txt")).expect("read installed payload"),
            "Türkçe test payload".as_bytes()
        );
        let mut retry_phases = Vec::new();
        assert!(
            install_unsigned_local_setup_with_progress(&installer, &target, &mut |phase| {
                retry_phases.push(phase)
            },)
            .is_err()
        );
        assert!(retry_phases.is_empty(), "no events when target is occupied");
        assert_eq!(
            fs::read(target.join("hello.txt")).expect("still preserved"),
            "Türkçe test payload".as_bytes()
        );
        fs::remove_dir_all(root).expect("cleanup");
    }

    /// @dev-id BRK-LOCAL-SETUP-RECYCLE-TEST-001
    /// @purpose Ensure the existing-target hook runs only after full staging.
    /// @contract Test adapter simulates recycling; production recycle is Windows-only.
    #[test]
    fn optional_replacement_runs_after_staging_and_denial_preserves_old_tree() {
        let root = std::env::temp_dir().join(format!("tz-local-replace-{}", Uuid::new_v4()));
        fs::create_dir(&root).expect("create root");
        let source = root.join("source");
        fs::create_dir(&source).expect("create source");
        fs::write(source.join("new.txt"), b"new payload").expect("new payload");
        let id = PackageId::parse("qa.recycle").expect("id");
        let version = SemanticVersion::parse("1.0.0").expect("version");
        let package = root.join("app.tzpkg");
        crate::build_full_package(
            &source,
            &id,
            &version,
            &package,
            PackageExtractionLimits::default(),
        )
        .expect("package");
        let stub = root.join("stub.exe");
        let executable = root.join("installer.exe");
        fs::write(&stub, b"MZ-rust-stub").expect("stub");
        create_unsigned_local_setup(&stub, &package, &executable).expect("setup");

        let target = root.join("qa.recycle");
        fs::create_dir(&target).expect("old folder");
        fs::write(target.join("old.txt"), b"previous user data").expect("old content");
        let mut called = false;
        let mut phases = Vec::new();
        let rejected = install_unsigned_local_setup_with_replacement(
            &executable,
            &target,
            &mut |phase| phases.push(phase),
            &mut |path| {
                called = true;
                assert_eq!(path, target);
                assert_eq!(fs::read(path.join("old.txt")).unwrap(), b"previous user data");
                assert!(fs::read_dir(&root)
                    .unwrap()
                    .filter_map(Result::ok)
                    .any(|item| item.file_name().to_string_lossy().starts_with(".turkuaz-native-stage-")));
                Err(invalid("recycle request denied"))
            },
        );
        assert!(rejected.is_err());
        assert!(called);
        assert!(target.join("old.txt").exists());
        assert!(!target.join("new.txt").exists());
        assert_eq!(
            phases,
            [
                LocalInstallPhase::VerifyingPackage,
                LocalInstallPhase::ExtractingPayload,
                LocalInstallPhase::PublishingInstall,
            ]
        );

        let mut recycled = false;
        let replaced = install_unsigned_local_setup_with_replacement(
            &executable,
            &target,
            &mut |_| {},
            &mut |path| {
                let old = root.join("simulated-recycle");
                fs::rename(path, &old).map_err(PackageEngineError::Io)?;
                recycled = true;
                Ok(())
            },
        )
        .expect("replacement");
        assert_eq!(replaced.package_id, "qa.recycle");
        assert!(recycled);
        assert_eq!(fs::read(target.join("new.txt")).unwrap(), b"new payload");
        assert_eq!(
            fs::read(root.join("simulated-recycle/old.txt")).unwrap(),
            b"previous user data"
        );
        fs::remove_dir_all(root).expect("cleanup");
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
