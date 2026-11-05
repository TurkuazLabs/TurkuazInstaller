// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/package_scan.rs
// Amac: Istege bagli AV taramasini uretilen paketin gercek icerigine uygulamak
// Modul - Rust Package Security Service
// Version: 0.1.0
// Aciklama: ZIP path denetimli staging, AV taramasi, cleanup ve fail-closed
// Bagimli Oldugu Katman: Service | Adapter

use std::{
    error::Error,
    fmt, fs, io,
    path::{Path, PathBuf},
};

use uuid::Uuid;

use crate::{
    MalwareScanner, PackageEngineError, PackageExtractionLimits, ScanGateError, ScanPhase,
    ScanSummary, extract_archive, scan_directory,
};

#[derive(Debug)]
pub enum PackageScanError {
    Io(io::Error),
    Archive(PackageEngineError),
    Malware(ScanGateError),
}

impl fmt::Display for PackageScanError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Io(e) => write!(f, "Scan staging error: {e}"),
            Self::Archive(e) => write!(f, "Package extraction rejected: {e}"),
            Self::Malware(e) => write!(f, "Antivirus scan refused package: {e}"),
        }
    }
}
impl Error for PackageScanError {}

struct TemporaryDirectory(PathBuf);
impl Drop for TemporaryDirectory {
    fn drop(&mut self) {
        // This path is random and is created inside the Studio's private
        // new build staging directory. Never remove the parent directory.
        if fs::symlink_metadata(&self.0).is_ok_and(|meta| meta.is_dir()) {
            let _ = fs::remove_dir_all(&self.0);
        }
    }
}

/// @dev-id BRK-PACKAGE-AV-001
/// @purpose Scan the bytes that will actually be distributed, not a mutable
/// source tree that can change between a scan and package creation.
/// @contract Require every extracted file to have a clean AV verdict;
/// remove the scan directory on all exit paths; never overwrite target.
pub fn scan_package_archive(
    archive: &Path,
    staging_parent: &Path,
    package_id: &str,
    version: &str,
    scanner: &dyn MalwareScanner,
) -> Result<ScanSummary, PackageScanError> {
    let parent = fs::symlink_metadata(staging_parent).map_err(PackageScanError::Io)?;
    if !parent.is_dir() || parent.file_type().is_symlink() {
        return Err(PackageScanError::Io(io::Error::other(
            "AV scan staging parent must be a regular directory",
        )));
    }
    let temporary = staging_parent.join(format!(".av-review-{}", Uuid::new_v4()));
    let guard = TemporaryDirectory(temporary.clone());
    extract_archive(
        archive,
        &temporary,
        PackageExtractionLimits::default(),
        package_id,
        version,
    )
    .map_err(PackageScanError::Archive)?;
    let result = scan_directory(&temporary, ScanPhase::BeforePackaging, scanner)
        .map_err(PackageScanError::Malware);
    drop(guard);
    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::ScanVerdict;
    use turkuaz_installer_domain::{package_id::PackageId, semantic_version::SemanticVersion};

    struct FixedVerdict(ScanVerdict);
    impl MalwareScanner for FixedVerdict {
        fn scan_file(&self, _: &Path, _: ScanPhase) -> Result<ScanVerdict, ScanGateError> {
            Ok(self.0)
        }
    }

    #[test]
    fn optional_package_scan_accepts_clean_and_denies_threat() {
        let folder = std::env::temp_dir().join(format!("tz-av-test-{}", Uuid::new_v4()));
        fs::create_dir(&folder).expect("root");
        let source = folder.join("source");
        fs::create_dir(&source).expect("source");
        fs::write(source.join("sample.txt"), b"harmless test sample").expect("fixture");
        let id = PackageId::parse("qa.multiscan").expect("id");
        let version = SemanticVersion::parse("1.0.0").expect("version");
        let archive = folder.join("application.tzpkg");
        crate::build_full_package(
            &source,
            &id,
            &version,
            &archive,
            PackageExtractionLimits::default(),
        )
        .expect("package");
        let ok = scan_package_archive(
            &archive,
            &folder,
            "qa.multiscan",
            "1.0.0",
            &FixedVerdict(ScanVerdict::Clean),
        )
        .expect("clean");
        assert_eq!(ok.files_scanned, 1);
        let denied = scan_package_archive(
            &archive,
            &folder,
            "qa.multiscan",
            "1.0.0",
            &FixedVerdict(ScanVerdict::Threat),
        );
        assert!(matches!(denied, Err(PackageScanError::Malware(_))));
        assert!(
            !fs::read_dir(&folder)
                .expect("root files")
                .filter_map(Result::ok)
                .any(|x| x.file_name().to_string_lossy().starts_with(".av-review-"))
        );
        fs::remove_dir_all(folder).expect("cleanup");
    }
}
