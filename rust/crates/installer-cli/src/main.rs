// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/main.rs
// Amac: Imzali Turkuaz release metadata alarak dogrulanmis full paketi kurmak veya ileri surume guncellemek
// Modul - Rust Binary
// Version: 0.2.2
// Aciklama: HTTPS release intake, package build, install/update/rollback, paket listesi, deployment dogrulamasi ve kurulu uygulamayi baslatma icin headless CLI'dir
// Bagimli Oldugu Katman: Application | Tool

use std::{
    collections::HashMap,
    error::Error,
    io,
    path::{Path, PathBuf},
    process,
};

use reqwest::{Client, redirect::Policy};
use turkuaz_installer_artifact_transfer::DefaultArtifactDownloader;
use turkuaz_installer_artifact_validation::download_and_verify;
use turkuaz_installer_deployment_activation::{
    DeploymentActivationOptions, VersionedDeploymentStore,
};
use turkuaz_installer_domain::{
    operation::InstallerOperationType, package_id::PackageId, semantic_version::SemanticVersion,
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
use turkuaz_installer_package_engine::{PackageExtractionLimits, build_full_package};
use url::Url;

#[tokio::main]
async fn main() {
    if let Err(error) = run().await {
        eprintln!("Turkuaz Installer failed: {error}");
        process::exit(1);
    }
}

async fn run() -> Result<(), Box<dyn Error>> {
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

fn build_package_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer build-package --package-id ID --version SEMVER --source DIR --output FILE.tzpkg"
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
    let allowed = ["package-id", "version", "source", "output"];
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
    build_full_package(
        source,
        &package_id,
        &version,
        &output,
        PackageExtractionLimits::default(),
    )?;
    println!("Built package: {}", output.display());
    Ok(())
}

async fn launch_command() -> Result<(), Box<dyn Error>> {
    let mut arguments = std::env::args().skip(2).peekable();
    if arguments
        .peek()
        .is_some_and(|argument| matches!(argument.as_str(), "--help" | "-h"))
    {
        println!(
            "Usage: turkuaz-installer launch --package-id ID --target DIR --data-root DIR --executable RELATIVE_PATH [-- APP_ARGS...]"
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
    let target_argument = required(&values, "target")?.trim();
    let target_directory = absolute_path(Path::new(target_argument))?;
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let executable_relative = required(&values, "executable")?;
    let executable_relative_path = Path::new(executable_relative);
    if executable_relative_path.is_absolute()
        || executable_relative.starts_with('/')
        || executable_relative.starts_with('\\')
        || executable_relative.contains(':')
        || executable_relative.contains('\\')
        || executable_relative_path.components().any(|component| {
            !matches!(component, std::path::Component::Normal(_))
        })
    {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Executable must be a safe relative path using forward slashes.",
        )
        .into());
    }

    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let _lock_lease = turkuaz_installer_operation_lock::acquire_package_lock(&lock, &package_id)
        .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;
    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let installed = state.get(&package_id).await?.ok_or_else(|| {
        io::Error::new(io::ErrorKind::NotFound, "Package is not installed.")
    })?;
    if !installed
        .target_path()
        .eq_ignore_ascii_case(target_argument)
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
    let report = deployment.verify_current(&package_id)?.ok_or_else(|| {
        io::Error::new(io::ErrorKind::NotFound, "Active deployment is missing.")
    })?;
    if report.active().version() != installed.version() {
        return Err(io::Error::new(
            io::ErrorKind::InvalidData,
            "Installed state and active deployment version do not match.",
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
    let target_directory = absolute_path(Path::new(required(&values, "target")?))?;
    let data_root = absolute_path(Path::new(required(&values, "data-root")?))?;
    let lock = FileOperationLockOptions::new(data_root.join("locks"))?;
    let _lock_lease = turkuaz_installer_operation_lock::acquire_package_lock(&lock, &package_id)
        .map_err(|_| io::Error::other("Could not acquire package operation lock."))?;

    let state = JsonInstallStateRepository::new(JsonInstallStateRepositoryOptions::new(
        data_root.join("state"),
    )?);
    let installed = state.get(&package_id).await?;
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
        Some("rollback") => InstallerOperationType::Rollback,
        Some("--help" | "-h") => {
            println!(
                "Usage:
  turkuaz-installer <install|update|rollback> --package-id ID --manifest-url HTTPS_URL --signature-url HTTPS_URL --trust-store FILE --target DIR --data-root DIR
  turkuaz-installer verify --package-id ID --target DIR --data-root DIR\n  turkuaz-installer list --data-root DIR\n  turkuaz-installer launch --package-id ID --target DIR --data-root DIR --executable RELATIVE_PATH [-- APP_ARGS...]\n  turkuaz-installer build-package --package-id ID --version SEMVER --source DIR --output FILE.tzpkg"
            );
            process::exit(0);
        }
        _ => {
            return Err(io::Error::new(
                io::ErrorKind::InvalidInput,
                "Expected install, update, or rollback command. Use --help for usage.",
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
