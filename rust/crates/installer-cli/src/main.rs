// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/main.rs
// Amac: Imzali Turkuaz release metadata alarak dogrulanmis full paketi kurmak veya ileri surume guncellemek
// Modul - Rust Binary
// Version: 0.2.7
// Aciklama: HTTPS release intake, package build, install/update/repair/rollback, paket listesi, deployment dogrulamasi ve kurulu uygulamayi baslatma icin headless CLI'dir
// Bagimli Oldugu Katman: Application | Tool

use std::{
    collections::HashMap,
    error::Error,
    io,
    io::Read,
    path::{Path, PathBuf},
    process,
};

use reqwest::{Client, redirect::Policy};
use sha2::{Digest, Sha256};
use turkuaz_installer_artifact_transfer::DefaultArtifactDownloader;
use turkuaz_installer_artifact_validation::{download_and_verify, verify_staged_artifact};
use turkuaz_installer_deployment_activation::{
    DeploymentActivationOptions, VersionedDeploymentStore,
};
use turkuaz_installer_domain::{
    operation::{InstallerOperationPhase, InstallerOperationType},
    package_id::PackageId,
    semantic_version::SemanticVersion,
};
use turkuaz_installer_install_state_store::{
    JsonInstallStateRepository, JsonInstallStateRepositoryOptions,
};
use turkuaz_installer_install_workflow::{InstallWorkflow, InstallWorkflowOptions};
use turkuaz_installer_manifest_pipeline::read_verified_release;
use turkuaz_installer_manifest_trust_store::YamlManifestTrustPolicyRepository;
use turkuaz_installer_metadata::{MAXIMUM_DETACHED_SIGNATURE_BYTES, MAXIMUM_MANIFEST_BYTES};
use turkuaz_installer_operation_journal::{
    JsonOperationJournalRepository, JsonOperationJournalRepositoryOptions,
};
use turkuaz_installer_operation_lock::FileOperationLockOptions;
use turkuaz_installer_package_engine::{
    PackageExtractionLimits, build_full_package_with_entry_point, create_self_extracting_setup,
    extract_embedded_setup_bundle, extract_setup_bundle, write_setup_bundle,
};
use url::Url;

#[tokio::main]
async fn main() {
    if let Err(error) = run().await {
        eprintln!("Turkuaz Installer failed: {error}");
        process::exit(1);
    }
}

async fn run() -> Result<(), Box<dyn Error>> {
    if std::env::args().nth(1).as_deref() == Some("build-distribution-pair") {
        return build_distribution_pair_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("build-self-extracting-setup") {
        return build_self_extracting_setup_command();
    }
    if std::env::args().nth(1).as_deref() == Some("install-self") {
        return install_self_setup_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("build-setup-bundle") {
        return build_setup_bundle_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("install-bundle") {
        return install_bundle_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("describe-package") {
        return describe_package_command();
    }
    if std::env::args().nth(1).as_deref() == Some("build-package") {
        return build_package_command();
    }
    if std::env::args().nth(1).as_deref() == Some("verify") {
        return verify_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("list") {
        return list_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("launch") {
        return launch_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("recover") {
        return recover_command().await;
    }
    if std::env::args().nth(1).as_deref() == Some("uninstall") {
        return uninstall_command().await;
    }
    let (operation, values) = parse_arguments()?;
    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let manifest_url = parse_https_url(required(&values, "manifest-url")?)?;
    let signature_url = parse_https_url(required(&values, "signature-url")?)?;
    let trust_path = PathBuf::from(required(&values, "trust-store")?);
    let target_path = required(&values, "target")?;
    let data_root = PathBuf::from(required(&values, "data-root")?);
    if target_path.trim().is_empty() || data_root.as_os_str().is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Target and data-root must be non-empty.",
        )
        .into());
    }

    let http_client = Client::builder()
        .timeout(std::time::Duration::from_secs(60))
        .redirect(Policy::custom(|attempt| {
            if attempt.url().scheme() == "https" && attempt.previous().len() < 10 {
                attempt.follow()
            } else {
                attempt.stop()
            }
        }))
        .build()
        .map_err(|_| io::Error::other("Could not initialize HTTPS client."))?;
    let (manifest_bytes, manifest_length) =
        read_bounded_https(&http_client, &manifest_url, MAXIMUM_MANIFEST_BYTES).await?;
    let (signature_bytes, signature_length) = read_bounded_https(
        &http_client,
        &signature_url,
        MAXIMUM_DETACHED_SIGNATURE_BYTES,
    )
    .await?;

    let trust_store = YamlManifestTrustPolicyRepository::new(trust_path)?;
    let mut manifest_source = std::io::Cursor::new(manifest_bytes);
    let mut signature_source = std::io::Cursor::new(signature_bytes);
    let release = read_verified_release(
        &mut manifest_source,
        &mut signature_source,
        Some(manifest_length),
        Some(signature_length),
        &package_id,
        &trust_store,
    )
    .await?;

    let data_root = absolute_path(&data_root)?;
    tokio::fs::create_dir_all(&data_root).await?;
    if operation == InstallerOperationType::Rollback {
        let target_directory = absolute_path(Path::new(target_path))?;
        let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
        let journal = JsonOperationJournalRepository::new(
            JsonOperationJournalRepositoryOptions::new(data_root.join("journal"))?,
        );
        let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
            data_root.join("state"),
        )?);
        let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
            target_directory.join(".turkuaz-installer"),
        )?);
        let options = InstallWorkflowOptions::new(
            lock,
            Some(journal),
            state,
            deployment,
            data_root.join("staging"),
        )?;
        let active = InstallWorkflow::new(options).rollback(release).await?;
        println!(
            "Rolled back to package directory: {}",
            active.directory().display()
        );
        return Ok(());
    }
    let downloader = DefaultArtifactDownloader::new()
        .map_err(|_| io::Error::other("Could not initialize artifact downloader."))?;
    let verified_artifact =
        download_and_verify(&downloader, release.artifact(), data_root.join("downloads"))
            .await
            .map_err(|_| io::Error::other("Artifact download or integrity validation failed."))?;
    let _downloaded_artifact = DownloadedArtifactGuard(verified_artifact.path().to_path_buf());

    let target_directory = absolute_path(Path::new(target_path))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let journal = JsonOperationJournalRepository::new(JsonOperationJournalRepositoryOptions::new(
        data_root.join("journal"),
    )?);
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let options = InstallWorkflowOptions::new(
        lock,
        Some(journal),
        state,
        deployment,
        data_root.join("staging"),
    )?;
    let workflow = InstallWorkflow::new(options);
    if operation == InstallerOperationType::Repair {
        let active = workflow
            .repair(
                verified_artifact,
                release,
                &target_directory.to_string_lossy(),
                PackageExtractionLimits::default(),
            )
            .await?;
        println!(
            "Repaired package directory: {}",
            active.directory().display()
        );
        return Ok(());
    }

    let active = workflow
        .install_or_update(
            verified_artifact,
            release,
            operation,
            &target_directory.to_string_lossy(),
            PackageExtractionLimits::default(),
        )
        .await?;

    println!("Active package directory: {}", active.directory().display());
    Ok(())
}

fn parse_setup_bundle_options(allowed: &[&str]) -> Result<HashMap<String, String>, Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        return Ok(HashMap::new());
    }
    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Options must use --name value form.",
            )
            .into());
        };
        if !allowed.contains(&key) {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Unknown setup bundle option.",
            )
            .into());
        }
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    Ok(values)
}

fn read_bounded_local_file(
    path: &Path,
    maximum_bytes: usize,
) -> Result<(Vec<u8>, u64), Box<dyn Error>> {
    let metadata = std::fs::symlink_metadata(path)?;
    if !metadata.file_type().is_file()
        || metadata.len() == 0
        || metadata.len() > maximum_bytes as u64
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Input must be a bounded, non-empty regular file.",
        )
        .into());
    }
    let file = std::fs::File::open(path)?;
    let mut bytes = Vec::with_capacity(metadata.len() as usize);
    file.take(maximum_bytes as u64 + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() as u64 != metadata.len() || bytes.len() > maximum_bytes {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Input changed or exceeded its size limit while being read.",
        )
        .into());
    }
    Ok((bytes, metadata.len()))
}

async fn build_setup_bundle_command() -> Result<(), Box<dyn Error>> {
    let values = parse_setup_bundle_options(&[
        "package-id",
        "manifest",
        "signature",
        "trust-store",
        "package",
        "output",
    ])?;
    if values.is_empty() {
        println!(
            "Usage: turkuaz-installer build-setup-bundle --package-id ID --manifest FILE.yaml --signature FILE.p7s --trust-store FILE --package FILE.tzpkg --output FILE.tzsetup"
        );
        return Ok(());
    }
    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let manifest_path = absolute_path(Path::new(required(&values, "manifest")?))?;
    let signature_path = absolute_path(Path::new(required(&values, "signature")?))?;
    let package_path = absolute_path(Path::new(required(&values, "package")?))?;
    let output_path = absolute_path(Path::new(required(&values, "output")?))?;
    let (manifest, manifest_length) =
        read_bounded_local_file(&manifest_path, MAXIMUM_MANIFEST_BYTES)?;
    let (signature, signature_length) =
        read_bounded_local_file(&signature_path, MAXIMUM_DETACHED_SIGNATURE_BYTES)?;
    let trust_store =
        YamlManifestTrustPolicyRepository::new(PathBuf::from(required(&values, "trust-store")?))?;
    let mut manifest_source = std::io::Cursor::new(manifest.as_slice());
    let mut signature_source = std::io::Cursor::new(signature.as_slice());
    let release = read_verified_release(
        &mut manifest_source,
        &mut signature_source,
        Some(manifest_length),
        Some(signature_length),
        &package_id,
        &trust_store,
    )
    .await?;
    let artifact = verify_staged_artifact(&package_path, release.artifact()).await?;
    write_setup_bundle(&manifest, &signature, &release, &artifact, &output_path)?;
    println!(
        "Signed offline setup bundle written: {}",
        output_path.display()
    );
    Ok(())
}

async fn build_distribution_pair_command() -> Result<(), Box<dyn Error>> {
    let values = parse_setup_bundle_options(&[
        "package-id",
        "stub",
        "bundle",
        "trust-store",
        "output-dir",
        "makensis",
    ])?;
    if values.is_empty() {
        println!(
            "Usage: turkuaz-installer build-distribution-pair --package-id ID --stub FILE.exe --bundle FILE.tzsetup --trust-store FILE --output-dir DIR [--makensis PATH]"
        );
        return Ok(());
    }

    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let stub_path = absolute_path(Path::new(required(&values, "stub")?))?;
    let bundle_path = absolute_path(Path::new(required(&values, "bundle")?))?;
    let trust_path = absolute_path(Path::new(required(&values, "trust-store")?))?;
    let output_directory = absolute_path(Path::new(required(&values, "output-dir")?))?;
    let trust_metadata = std::fs::symlink_metadata(&trust_path)?;
    if !trust_metadata.file_type().is_file() {
        return Err(io::Error::new(io::ErrorKind::InvalidInput, "Trust store must be a regular file.").into());
    }
    if std::fs::symlink_metadata(&output_directory).is_ok() {
        return Err(io::Error::new(io::ErrorKind::AlreadyExists, "Output directory already exists.").into());
    }
    let parent = output_directory.parent().ok_or_else(|| {
        io::Error::new(io::ErrorKind::InvalidInput, "Output directory must have a parent.")
    })?;
    if !parent.is_dir() {
        return Err(io::Error::new(io::ErrorKind::NotFound, "Output parent directory does not exist.").into());
    }

    let nonce = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)?
        .as_nanos();
    let staging_directory = parent.join(format!(".turkuaz-pair-{}-{nonce}", process::id()));
    std::fs::create_dir(&staging_directory)?;
    let mut staging_guard = DistributionStagingGuard(staging_directory.clone());
    let verification_directory = staging_directory.join(".bundle-validation");
    let verified_files = extract_setup_bundle(bundle_path.clone(), verification_directory)?;
    let verification_guard = SetupBundleExtractionGuard(verified_files.root().to_path_buf());
    let (manifest, manifest_length) =
        read_bounded_local_file(verified_files.manifest_path(), MAXIMUM_MANIFEST_BYTES)?;
    let (signature, signature_length) = read_bounded_local_file(
        verified_files.signature_path(),
        MAXIMUM_DETACHED_SIGNATURE_BYTES,
    )?;
    let trust_store = YamlManifestTrustPolicyRepository::new(trust_path.clone())?;
    let mut manifest_source = std::io::Cursor::new(manifest.as_slice());
    let mut signature_source = std::io::Cursor::new(signature.as_slice());
    let release = read_verified_release(
        &mut manifest_source,
        &mut signature_source,
        Some(manifest_length),
        Some(signature_length),
        &package_id,
        &trust_store,
    )
    .await?;
    let _verified_artifact =
        verify_staged_artifact(verified_files.artifact_path(), release.artifact()).await?;
    drop(verification_guard);

    let primary_setup = staging_directory.join("TurkuazSetup.exe");
    create_self_extracting_setup(stub_path, bundle_path, &primary_setup)?;

    const MAX_TRUST_STORE_BYTES: usize = 1024 * 1024;
    let (trust_bytes, _) = read_bounded_local_file(&trust_path, MAX_TRUST_STORE_BYTES)?;
    let staged_trust = staging_directory.join("manifest-trust.yaml");
    std::fs::write(&staged_trust, trust_bytes)?;
    let backup_path = staging_directory.join("TurkuazInstallerBackup.exe");
    let script_path = staging_directory.join("TurkuazInstallerBackup.nsi");
    std::fs::write(
        &script_path,
        include_str!("../../../../packaging/TurkuazInstallerBackup.nsi"),
    )?;

    let makensis = values.get("makensis").map(String::as_str).unwrap_or("makensis");
    let output = process::Command::new(makensis)
        .arg(format!("/DPACKAGE_ID={package_id}"))
        .arg(format!("/DPRIMARY_SETUP_PATH={}", primary_setup.display()))
        .arg(format!("/DTRUST_STORE_PATH={}", staged_trust.display()))
        .arg(format!("/DOUTPUT_PATH={}", backup_path.display()))
        .arg(&script_path)
        .output()?;
    if !output.status.success() {
        return Err(io::Error::other(format!(
            "NSIS backup build failed ({}): {}",
            output.status,
            String::from_utf8_lossy(&output.stderr)
        ))
        .into());
    }
    let backup_metadata = std::fs::symlink_metadata(&backup_path)?;
    if !backup_metadata.file_type().is_file() || backup_metadata.len() == 0 {
        return Err(io::Error::new(io::ErrorKind::InvalidData, "NSIS did not produce a backup executable.").into());
    }

    std::fs::rename(&staging_directory, &output_directory)?;
    staging_guard.0.clear();
    println!("Distribution pair written to: {}", output_directory.display());
    println!("Primary (share this): {}", output_directory.join("TurkuazSetup.exe").display());
    println!("NSIS backup: {}", output_directory.join("TurkuazInstallerBackup.exe").display());
    println!("Both executables must be Authenticode-signed after generation.");
    Ok(())
}

struct DistributionStagingGuard(PathBuf);

impl Drop for DistributionStagingGuard {
    fn drop(&mut self) {
        if !self.0.as_os_str().is_empty() {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }
}

fn build_self_extracting_setup_command() -> Result<(), Box<dyn Error>> {
    let values = parse_setup_bundle_options(&["stub", "bundle", "output"])?;
    if values.is_empty() {
        println!(
            "Usage: turkuaz-installer build-self-extracting-setup --stub FILE.exe --bundle FILE.tzsetup --output TurkuazSetup.exe"
        );
        return Ok(());
    }
    let stub_path = absolute_path(Path::new(required(&values, "stub")?))?;
    let bundle_path = absolute_path(Path::new(required(&values, "bundle")?))?;
    let output_path = absolute_path(Path::new(required(&values, "output")?))?;
    create_self_extracting_setup(stub_path, bundle_path, &output_path)?;
    println!("Self-extracting setup written: {}", output_path.display());
    Ok(())
}

async fn install_self_setup_command() -> Result<(), Box<dyn Error>> {
    let mut values = parse_setup_bundle_options(&[
        "package-id",
        "trust-store",
        "target",
        "data-root",
        "operation",
    ])?;
    if values.is_empty() {
        println!(
            "Usage: TurkuazSetup.exe install-self --package-id ID --trust-store FILE --target DIR --data-root DIR --operation install|update|repair|rollback"
        );
        return Ok(());
    }
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    std::fs::create_dir_all(&data_root)?;
    let nonce = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)?
        .as_nanos();
    let embedded_bundle_path =
        data_root.join(format!(".setup-self-{}-{nonce}.tzsetup", process::id()));
    let executable_path = std::env::current_exe()?;
    let executable_for_worker = executable_path.clone();
    let bundle_for_worker = embedded_bundle_path.clone();
    tokio::task::spawn_blocking(move || {
        extract_embedded_setup_bundle(executable_for_worker, bundle_for_worker)
    })
    .await??;
    let _bundle_guard = SetupBundleFileGuard(embedded_bundle_path.clone());
    values.insert(
        "bundle".to_owned(),
        embedded_bundle_path.to_string_lossy().into_owned(),
    );
    install_bundle_with_options(values).await
}

struct SetupBundleFileGuard(PathBuf);

impl Drop for SetupBundleFileGuard {
    fn drop(&mut self) {
        let _ = std::fs::remove_file(&self.0);
    }
}

async fn install_bundle_command() -> Result<(), Box<dyn Error>> {
    let values = parse_setup_bundle_options(&[
        "package-id",
        "bundle",
        "trust-store",
        "target",
        "data-root",
        "operation",
    ])?;
    if values.is_empty() {
        println!(
            "Usage: turkuaz-installer install-bundle --package-id ID --bundle FILE.tzsetup --trust-store FILE --target DIR --data-root DIR --operation install|update|repair|rollback"
        );
        return Ok(());
    }
    install_bundle_with_options(values).await
}

async fn install_bundle_with_options(
    values: HashMap<String, String>,
) -> Result<(), Box<dyn Error>> {
    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let operation = match required(&values, "operation")? {
        "install" => InstallerOperationType::Install,
        "update" => InstallerOperationType::Update,
        "repair" => InstallerOperationType::Repair,
        "rollback" => InstallerOperationType::Rollback,
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Bundle operation must be install, update, repair, or rollback.",
            )
            .into());
        }
    };
    let bundle_path = absolute_path(Path::new(required(&values, "bundle")?))?;
    let trust_path = absolute_path(Path::new(required(&values, "trust-store")?))?;
    let target_directory = absolute_path(Path::new(required(&values, "target")?))?;
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    std::fs::create_dir_all(&data_root)?;
    let nonce = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)?
        .as_nanos();
    let extraction_directory = data_root.join(format!(".setup-bundle-{}-{nonce}", process::id()));
    let extraction_path = extraction_directory.clone();
    let bundle_path_for_worker = bundle_path.clone();
    let files = tokio::task::spawn_blocking(move || {
        extract_setup_bundle(bundle_path_for_worker, extraction_path)
    })
    .await??;
    let _extraction_guard = SetupBundleExtractionGuard(files.root().to_path_buf());

    let manifest = std::fs::read(files.manifest_path())?;
    let signature = std::fs::read(files.signature_path())?;
    let trust_store = YamlManifestTrustPolicyRepository::new(trust_path)?;
    let mut manifest_source = std::io::Cursor::new(manifest.as_slice());
    let mut signature_source = std::io::Cursor::new(signature.as_slice());
    let release = read_verified_release(
        &mut manifest_source,
        &mut signature_source,
        Some(manifest.len() as u64),
        Some(signature.len() as u64),
        &package_id,
        &trust_store,
    )
    .await?;
    let verified_artifact =
        verify_staged_artifact(files.artifact_path(), release.artifact()).await?;

    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let journal = JsonOperationJournalRepository::new(JsonOperationJournalRepositoryOptions::new(
        data_root.join("journal"),
    )?);
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let workflow = InstallWorkflow::new(InstallWorkflowOptions::new(
        lock,
        Some(journal),
        state,
        deployment,
        data_root.join("staging"),
    )?);
    if operation == InstallerOperationType::Rollback {
        let active = workflow.rollback(release).await?;
        println!(
            "Rolled back to package directory: {}",
            active.directory().display()
        );
    } else if operation == InstallerOperationType::Repair {
        let active = workflow
            .repair(
                verified_artifact,
                release,
                &target_directory.to_string_lossy(),
                PackageExtractionLimits::default(),
            )
            .await?;
        println!(
            "Repaired package directory: {}",
            active.directory().display()
        );
    } else {
        let active = workflow
            .install_or_update(
                verified_artifact,
                release,
                operation,
                &target_directory.to_string_lossy(),
                PackageExtractionLimits::default(),
            )
            .await?;
        println!("Active package directory: {}", active.directory().display());
    }
    Ok(())
}

struct SetupBundleExtractionGuard(PathBuf);

impl Drop for SetupBundleExtractionGuard {
    fn drop(&mut self) {
        let _ = std::fs::remove_dir_all(&self.0);
    }
}

fn describe_package_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer describe-package --package-id ID --version SEMVER --channel stable|beta --package FILE.tzpkg --uri HTTPS_URL --target TARGET --output MANIFEST.yaml"
        );
        return Ok(());
    }
    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Package description options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = [
        "package-id",
        "version",
        "channel",
        "package",
        "uri",
        "target",
        "output",
    ];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown package description option was provided.",
        )
        .into());
    }
    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let version = SemanticVersion::parse(required(&values, "version")?)?;
    let channel = required(&values, "channel")?.trim().to_ascii_lowercase();
    if !matches!(channel.as_str(), "stable" | "beta") {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Channel must be stable or beta.",
        )
        .into());
    }
    let artifact_uri = Url::parse(required(&values, "uri")?)
        .map_err(|_| io::Error::new(io::ErrorKind::InvalidInput, "Artifact URI is invalid."))?;
    if !matches!(artifact_uri.scheme(), "https" | "file")
        || (artifact_uri.scheme() == "https" && artifact_uri.host_str().is_none())
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Artifact URI must use HTTPS or file scheme.",
        )
        .into());
    }
    let package_path = absolute_path(Path::new(required(&values, "package")?))?;
    let metadata = std::fs::symlink_metadata(&package_path)?;
    if !metadata.file_type().is_file() || metadata.len() == 0 {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Package must be a non-empty regular file, not a symlink.",
        )
        .into());
    }
    let mut file = std::fs::File::open(&package_path)?;
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; 64 * 1024];
    let mut actual_size = 0_u64;
    loop {
        use std::io::Read;
        let count = file.read(&mut buffer)?;
        if count == 0 {
            break;
        }
        actual_size = actual_size.checked_add(count as u64).ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidData, "Package size overflowed.")
        })?;
        hasher.update(&buffer[..count]);
    }
    if actual_size != metadata.len() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Package changed while its descriptor was being generated.",
        )
        .into());
    }
    let digest = encode_sha256(&hasher.finalize());
    let target = required(&values, "target")?;
    if target.chars().any(char::is_control) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Target must not contain control characters.",
        )
        .into());
    }
    let output = absolute_path(Path::new(required(&values, "output")?))?;
    let manifest = format!(
        "schema_version: 1\npackage:\n  id: {}\n  version: {}\n  channel: {}\nartifact:\n  uri: {}\n  sha256: {}\n  size_bytes: {}\ninstall:\n  mode: full\n  target: {}\nrollback:\n  supported: false\n  previous_version_required: true\n",
        package_id,
        version,
        channel,
        quote_yaml(artifact_uri.as_str())?,
        digest,
        actual_size,
        quote_yaml(target)?
    );
    turkuaz_installer_manifest_parser::parse_manifest(&manifest).map_err(|_| {
        io::Error::new(
            io::ErrorKind::InvalidData,
            "Generated manifest draft failed schema validation.",
        )
    })?;
    let mut output_file = std::fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(&output)?;
    use std::io::Write;
    output_file.write_all(manifest.as_bytes())?;
    output_file.sync_all()?;
    println!("Manifest draft written: {} (not signed)", output.display());
    Ok(())
}

fn encode_sha256(bytes: &[u8]) -> String {
    const HEX: &[u8; 16] = b"0123456789abcdef";
    let mut output = String::with_capacity(bytes.len() * 2);
    for byte in bytes {
        output.push(HEX[(byte >> 4) as usize] as char);
        output.push(HEX[(byte & 0x0f) as usize] as char);
    }
    output
}

fn quote_yaml(value: &str) -> Result<String, io::Error> {
    if value.chars().any(char::is_control) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Manifest scalar must not contain control characters.",
        ));
    }
    Ok(format!("'{}'", value.replace("'", "''")))
}

fn build_package_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer build-package --package-id ID --version SEMVER --source DIR --output FILE.tzpkg [--executable RELATIVE_PATH]"
        );
        return Ok(());
    }
    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Package options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = ["package-id", "version", "source", "output", "executable"];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown package option was provided.",
        )
        .into());
    }
    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let version = SemanticVersion::parse(required(&values, "version")?)?;
    let source = absolute_path(Path::new(required(&values, "source")?))?;
    let output = absolute_path(Path::new(required(&values, "output")?))?;
    build_full_package_with_entry_point(
        source,
        &package_id,
        &version,
        &output,
        values.get("executable").map(String::as_str),
        PackageExtractionLimits::default(),
    )?;
    println!("Built package: {}", output.display());
    Ok(())
}

async fn recover_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!("Usage: turkuaz-installer recover --package-id ID --target DIR --data-root DIR");
        return Ok(());
    }

    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Recover options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = ["package-id", "target", "data-root"];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown recover option was provided.",
        )
        .into());
    }

    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let target_directory = absolute_path(Path::new(required(&values, "target")?.trim()))?;
    let target_argument = target_directory.to_string_lossy().into_owned();
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let _lock_lease = turkuaz_installer_operation_lock::acquire_package_lock(&lock, &package_id)
        .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;

    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let journal = JsonOperationJournalRepository::new(JsonOperationJournalRepositoryOptions::new(
        data_root.join("journal"),
    )?);
    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let installed = state.get(&package_id).await?;
    let active = deployment.current(&package_id)?;
    let Some(entry) = journal.get(&package_id).await? else {
        match (installed.as_ref(), active.as_ref()) {
            (None, None) => println!("No installed package or pending operation."),
            (Some(installed), Some(active))
                if installed.version() == active.version()
                    && installed
                        .target_path()
                        .eq_ignore_ascii_case(&target_argument) =>
            {
                deployment.verify_current(&package_id)?.ok_or_else(|| {
                    io::Error::new(io::ErrorKind::InvalidData, "Active deployment is missing.")
                })?;
                println!(
                    "Installed state and active deployment are consistent; no journal exists."
                );
            }
            _ => {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "State and active deployment disagree, but no recovery journal exists. Manual recovery is required.",
                )
                .into());
            }
        }
        return Ok(());
    };

    if !entry
        .target_path
        .trim()
        .eq_ignore_ascii_case(&target_argument)
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Recovery target does not match the operation journal.",
        )
        .into());
    }

    if entry.operation == InstallerOperationType::Repair {
        match (installed.as_ref(), active.as_ref()) {
            (Some(installed), Some(active))
                if installed.version() == active.version()
                    && installed
                        .target_path()
                        .eq_ignore_ascii_case(&target_argument) =>
            {
                journal.delete(&package_id).await?;
                println!(
                    "Cleared interrupted repair record; run repair again if deployment files remain damaged."
                );
            }
            _ => {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "Interrupted repair state is ambiguous; no files or state were changed.",
                )
                .into());
            }
        }
        return Ok(());
    }

    if entry.operation == InstallerOperationType::Uninstall {
        let has_quarantine = deployment.has_uninstall_quarantine(&package_id)?;
        match (installed.as_ref(), active.as_ref(), has_quarantine) {
            (Some(installed), Some(active), false)
                if installed.version() == active.version()
                    && installed
                        .target_path()
                        .eq_ignore_ascii_case(&target_argument) =>
            {
                deployment.verify_current(&package_id)?.ok_or_else(|| {
                    io::Error::new(io::ErrorKind::InvalidData, "Active deployment is missing.")
                })?;
                journal.delete(&package_id).await?;
                println!("Uninstall did not move the managed deployment; pending record cleared.");
            }
            (Some(installed), None, true)
                if installed
                    .target_path()
                    .eq_ignore_ascii_case(&target_argument) =>
            {
                state.delete(&package_id).await?;
                deployment.finish_uninstall(&package_id)?;
                journal.delete(&package_id).await?;
                println!("Recovered interrupted uninstall and removed managed package files.");
            }
            (None, None, true) => {
                deployment.finish_uninstall(&package_id)?;
                journal.delete(&package_id).await?;
                println!("Recovered uninstall cleanup and removed the pending record.");
            }
            (None, None, false) => {
                journal.delete(&package_id).await?;
                println!("Package is already absent; stale uninstall record removed.");
            }
            _ => {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "Interrupted uninstall state is ambiguous; no files or state were changed.",
                )
                .into());
            }
        }
        return Ok(());
    }

    match (installed, active) {
        (Some(installed), Some(active)) => {
            if !installed
                .target_path()
                .eq_ignore_ascii_case(&target_argument)
            {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidInput,
                    "Recovery target does not match installed package state.",
                )
                .into());
            }
            if installed.version() == active.version() {
                deployment.verify_current(&package_id)?.ok_or_else(|| {
                    io::Error::new(io::ErrorKind::InvalidData, "Active deployment is missing.")
                })?;
                journal.delete(&package_id).await?;
                println!(
                    "Package state is consistent at version {}; stale journal removed.",
                    installed.version()
                );
                return Ok(());
            }

            let recoverable_phase = matches!(
                entry.phase,
                InstallerOperationPhase::Applying
                    | InstallerOperationPhase::SavingState
                    | InstallerOperationPhase::Failed
            );
            let target_version = entry
                .version
                .as_deref()
                .map(SemanticVersion::parse)
                .transpose()?;
            if !recoverable_phase
                || !matches!(
                    entry.operation,
                    InstallerOperationType::Update | InstallerOperationType::Rollback
                )
                || target_version.as_ref() != Some(active.version())
                || active.previous_version() != Some(installed.version())
            {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "The journal does not prove a safe update/rollback recovery. No files or state were changed.",
                )
                .into());
            }

            let report = deployment.verify_current(&package_id)?.ok_or_else(|| {
                io::Error::new(io::ErrorKind::InvalidData, "Active deployment is missing.")
            })?;
            if report.active().version() != active.version() {
                return Err(io::Error::new(
                    io::ErrorKind::InvalidData,
                    "Active deployment changed during recovery.",
                )
                .into());
            }
            let recovered_state = turkuaz_installer_domain::state::InstalledPackageState::new(
                package_id.clone(),
                active.version().clone(),
                installed.channel(),
                installed.target_path(),
            );
            state.save(&recovered_state).await?;
            journal.delete(&package_id).await?;
            println!(
                "Recovered operation by completing activation: package={} version={}",
                package_id,
                active.version()
            );
        }
        (None, None) if entry.operation == InstallerOperationType::Install => {
            journal.delete(&package_id).await?;
            println!(
                "Install did not activate a deployment; stale journal removed. Any abandoned staging directory was left untouched."
            );
        }
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "This interrupted operation cannot be recovered safely from its current state. No files or state were changed.",
            )
            .into());
        }
    }
    Ok(())
}

async fn uninstall_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer uninstall --package-id ID --target DIR --data-root DIR --confirm-uninstall YES"
        );
        return Ok(());
    }
    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Uninstall options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = ["package-id", "target", "data-root", "confirm-uninstall"];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown uninstall option was provided.",
        )
        .into());
    }
    if required(&values, "confirm-uninstall")? != "YES" {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Uninstall requires --confirm-uninstall YES.",
        )
        .into());
    }

    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let target_directory = absolute_path(Path::new(required(&values, "target")?.trim()))?;
    let target_path = target_directory.to_string_lossy().into_owned();
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let journal = JsonOperationJournalRepository::new(JsonOperationJournalRepositoryOptions::new(
        data_root.join("journal"),
    )?);
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let workflow = InstallWorkflow::new(InstallWorkflowOptions::new(
        lock,
        Some(journal),
        state,
        deployment,
        data_root.join("staging"),
    )?);
    workflow.uninstall(&package_id, &target_path).await?;
    println!("Uninstalled package={package_id}");
    Ok(())
}

async fn launch_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer launch --package-id ID --target DIR --data-root DIR [--executable RELATIVE_PATH] [-- APP_ARGS...]"
        );
        return Ok(());
    }

    let mut values = HashMap::new();
    let mut application_arguments = Vec::new();
    while let Some(flag) = arguments.next() {
        if flag == "--" {
            application_arguments.extend(arguments);
            break;
        }
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Launch options must use --name value form; use -- before application arguments.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = ["package-id", "target", "data-root", "executable"];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown launch option was provided.",
        )
        .into());
    }

    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let target_directory = absolute_path(Path::new(required(&values, "target")?.trim()))?;
    let target_argument = target_directory.to_string_lossy().into_owned();
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let _lock_lease = turkuaz_installer_operation_lock::acquire_package_lock(&lock, &package_id)
        .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let installed = state
        .get(&package_id)
        .await?
        .ok_or_else(|| io::Error::new(io::ErrorKind::NotFound, "Package is not installed."))?;
    if !installed
        .target_path()
        .eq_ignore_ascii_case(&target_argument)
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Target path does not match the installed package state.",
        )
        .into());
    }

    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let report = deployment
        .verify_current(&package_id)?
        .ok_or_else(|| io::Error::new(io::ErrorKind::NotFound, "Active deployment is missing."))?;
    if report.active().version() != installed.version() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Installed state and active deployment version do not match.",
        )
        .into());
    }

    let executable_relative = values
        .get("executable")
        .map(String::as_str)
        .or(report.active().entry_point())
        .ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::InvalidInput,
                "Package has no declared entry point; provide --executable RELATIVE_PATH.",
            )
        })?;
    let executable_relative_path = Path::new(executable_relative);
    if executable_relative_path.is_absolute()
        || executable_relative.starts_with('/')
        || executable_relative.starts_with('\\')
        || executable_relative.contains(':')
        || executable_relative.contains('\\')
        || executable_relative_path
            .components()
            .any(|component| !matches!(component, std::path::Component::Normal(_)))
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Executable must be a safe relative path using forward slashes.",
        )
        .into());
    }

    let canonical_deployment = std::fs::canonicalize(report.active().directory())?;
    let executable_path = report.active().directory().join(executable_relative_path);
    let canonical_executable = std::fs::canonicalize(&executable_path)?;
    if !canonical_executable.starts_with(&canonical_deployment)
        || !std::fs::metadata(&canonical_executable)?.is_file()
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Executable must resolve to a regular file inside the active deployment.",
        )
        .into());
    }

    let child = process::Command::new(&canonical_executable)
        .args(application_arguments)
        .current_dir(&canonical_deployment)
        .spawn()?;
    println!(
        "Started package={} version={} pid={}",
        package_id,
        report.active().version(),
        child.id()
    );
    Ok(())
}

async fn list_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!("Usage: turkuaz-installer list --data-root DIR");
        return Ok(());
    }

    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "List options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    if values.keys().any(|key| key != "data-root") {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown list option was provided.",
        )
        .into());
    }

    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let mut installed = state.list().await?;
    installed.sort_by(|left, right| left.package_id().as_str().cmp(right.package_id().as_str()));

    if installed.is_empty() {
        println!("No installed packages.");
        return Ok(());
    }
    for package in installed {
        let _lock_lease =
            turkuaz_installer_operation_lock::acquire_package_lock(&lock, package.package_id())
                .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;
        let Some(package) = state.get(package.package_id()).await? else {
            continue;
        };
        let target_directory = absolute_path(Path::new(package.target_path()))?;
        let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
            target_directory.join(".turkuaz-installer"),
        )?);
        let active = deployment.current(package.package_id())?.ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::InvalidData,
                format!(
                    "Installed state exists but active deployment is missing for package {}.",
                    package.package_id()
                ),
            )
        })?;
        if active.version() != package.version() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                format!(
                    "Installed state and active deployment disagree for package {}.",
                    package.package_id()
                ),
            )
            .into());
        }
        println!(
            "{}\t{}\t{:?}\t{}",
            package.package_id(),
            package.version(),
            package.channel(),
            package.target_path()
        );
    }
    Ok(())
}

async fn verify_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!("Usage: turkuaz-installer verify --package-id ID --target DIR --data-root DIR");
        return Ok(());
    }

    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Verify options must use --name value form.",
            )
            .into());
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            )
            .into());
        }
    }
    let allowed = ["package-id", "target", "data-root"];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown verify option was provided.",
        )
        .into());
    }

    let package_id = PackageId::parse(required(&values, "package-id")?)?;
    let target_directory = absolute_path(Path::new(required(&values, "target")?.trim()))?;
    let target_argument = target_directory.to_string_lossy().into_owned();
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let _lock_lease = turkuaz_installer_operation_lock::acquire_package_lock(&lock, &package_id)
        .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;

    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let installed = state.get(&package_id).await?;
    if installed.as_ref().is_some_and(|installed| {
        !installed
            .target_path()
            .eq_ignore_ascii_case(&target_argument)
    }) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Target path does not match the installed package state.",
        )
        .into());
    }
    let deployment = VersionedDeploymentStore::new(DeploymentActivationOptions::new(
        target_directory.join(".turkuaz-installer"),
    )?);
    let report = deployment.verify_current(&package_id)?;

    let (installed, report) = match (installed, report) {
        (Some(installed), Some(report)) => (installed, report),
        (None, None) => {
            return Err(
                io::Error::new(io::ErrorKind::NotFound, "Package is not installed.").into(),
            );
        }
        (Some(_), None) => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "Installed state exists but the active deployment is missing.",
            )
            .into());
        }
        (None, Some(_)) => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "An active deployment exists without installed state.",
            )
            .into());
        }
    };
    if report.active().version() != installed.version() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Installed state and verified active deployment version do not match.",
        )
        .into());
    }

    println!(
        "Deployment files match the recorded inventory: package={} version={} files={} directory={}",
        package_id,
        report.active().version(),
        report.verified_file_count(),
        report.active().directory().display()
    );
    Ok(())
}

fn parse_arguments() -> Result<(InstallerOperationType, HashMap<String, String>), io::Error> {
    let mut arguments = std::env::args().skip(1);
    let operation = match arguments.next().as_deref() {
        Some("install") => InstallerOperationType::Install,
        Some("update") => InstallerOperationType::Update,
        Some("repair") => InstallerOperationType::Repair,
        Some("rollback") => InstallerOperationType::Rollback,
        Some("--help" | "-h") => {
            println!(
                "Usage:
  turkuaz-installer <install|update|repair|rollback> --package-id ID --manifest-url HTTPS_URL --signature-url HTTPS_URL --trust-store FILE --target DIR --data-root DIR
  turkuaz-installer verify --package-id ID --target DIR --data-root DIR\n  turkuaz-installer list --data-root DIR\n  turkuaz-installer launch --package-id ID --target DIR --data-root DIR --executable RELATIVE_PATH [-- APP_ARGS...]\n  turkuaz-installer recover --package-id ID --target DIR --data-root DIR\n  turkuaz-installer uninstall --package-id ID --target DIR --data-root DIR --confirm-uninstall YES\n  turkuaz-installer build-package --package-id ID --version SEMVER --source DIR --output FILE.tzpkg\n  turkuaz-installer build-setup-bundle --package-id ID --manifest FILE --signature FILE --trust-store FILE --package FILE.tzpkg --output FILE.tzsetup\n  turkuaz-installer build-self-extracting-setup --stub FILE.exe --bundle FILE.tzsetup --output FILE.exe\n  turkuaz-installer build-distribution-pair --package-id ID --stub FILE.exe --bundle FILE.tzsetup --trust-store FILE --output-dir DIR [--makensis PATH]\n  TurkuazSetup.exe install-self --package-id ID --trust-store FILE --target DIR --data-root DIR --operation install|update|repair|rollback\n  turkuaz-installer install-bundle --package-id ID --bundle FILE.tzsetup --trust-store FILE --target DIR --data-root DIR --operation install|update|repair|rollback\n  turkuaz-installer describe-package --package-id ID --version SEMVER --channel stable|beta --package FILE.tzpkg --uri HTTPS_URL --target TARGET --output MANIFEST.yaml"
            );
            process::exit(0);
        }
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Expected install, update, repair, or rollback command. Use --help for usage.",
            ));
        }
    };

    let mut values = HashMap::new();
    while let Some(flag) = arguments.next() {
        let Some(key) = flag.strip_prefix("--") else {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Arguments after the command must use --name value form.",
            ));
        };
        let value = arguments.next().ok_or_else(|| {
            io::Error::new(io::ErrorKind::InvalidInput, "An option value is missing.")
        })?;
        if values.insert(key.to_owned(), value).is_some() {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "An option was provided more than once.",
            ));
        }
    }

    let allowed = [
        "package-id",
        "manifest-url",
        "signature-url",
        "trust-store",
        "target",
        "data-root",
    ];
    if values.keys().any(|key| !allowed.contains(&key.as_str())) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "An unknown option was provided.",
        ));
    }
    for key in allowed {
        required(&values, key)?;
    }
    Ok((operation, values))
}

fn required<'a>(values: &'a HashMap<String, String>, key: &str) -> Result<&'a str, io::Error> {
    values
        .get(key)
        .map(String::as_str)
        .filter(|value| !value.trim().is_empty())
        .ok_or_else(|| {
            io::Error::new(
                io::ErrorKind::InvalidInput,
                format!("Required option --{key} is missing."),
            )
        })
}

fn parse_https_url(value: &str) -> Result<Url, io::Error> {
    let url = Url::parse(value)
        .map_err(|_| io::Error::new(io::ErrorKind::InvalidInput, "A source URL is invalid."))?;
    if url.scheme() != "https" || url.host_str().is_none() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Manifest and signature URLs must use HTTPS.",
        ));
    }
    Ok(url)
}

async fn read_bounded_https(
    client: &Client,
    url: &Url,
    maximum_bytes: usize,
) -> Result<(Vec<u8>, u64), io::Error> {
    let mut response = client
        .get(url.clone())
        .send()
        .await
        .map_err(|_| io::Error::other("HTTPS source request failed."))?
        .error_for_status()
        .map_err(|_| io::Error::other("HTTPS source returned an unsuccessful status."))?;
    if response
        .content_length()
        .is_some_and(|length| length > maximum_bytes as u64)
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "HTTPS source exceeds its configured size limit.",
        ));
    }
    let declared_length = response.content_length();
    let mut bytes = Vec::new();
    while let Some(chunk) = response
        .chunk()
        .await
        .map_err(|_| io::Error::other("HTTPS source response could not be read."))?
    {
        let next_length = (bytes.len() as u64)
            .checked_add(chunk.len() as u64)
            .ok_or_else(|| io::Error::new(io::ErrorKind::InvalidData, "Source size overflowed."))?;
        if next_length > maximum_bytes as u64 {
            return Err(io::Error::new(
                io::ErrorKind::InvalidData,
                "HTTPS source exceeds its configured size limit.",
            ));
        }
        bytes.extend_from_slice(&chunk);
    }
    let actual_length = bytes.len() as u64;
    if declared_length.is_some_and(|length| length != actual_length) {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "HTTPS source length does not match its Content-Length.",
        ));
    }
    Ok((bytes, actual_length))
}

fn absolute_path(path: &Path) -> io::Result<PathBuf> {
    if path.as_os_str().is_empty() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "A configured path cannot be empty.",
        ));
    }
    if path.is_absolute() {
        Ok(path.to_path_buf())
    } else {
        Ok(std::env::current_dir()?.join(path))
    }
}
struct DownloadedArtifactGuard(PathBuf);

impl Drop for DownloadedArtifactGuard {
    fn drop(&mut self) {
        if let Some(download_directory) = self.0.parent() {
            let _ = std::fs::remove_file(&self.0);
            let _ = std::fs::remove_dir(download_directory);
        }
    }
}
