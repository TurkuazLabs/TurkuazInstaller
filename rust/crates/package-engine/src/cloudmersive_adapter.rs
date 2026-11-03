// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/cloudmersive_adapter.rs
// Amac: Istege bagli bulut antivirus taramasini ortak motor arayuzune eklemek
// Modul - Rust Security Adapter
// Version: 0.1.0
// Aciklama: Acik onayla dosya yukleme, sinirli HTTPS cevaplari, fail-closed sonuc
// Bagimli Oldugu Katman: Service | Adapter

use std::{
    fs,
    io::Read,
    path::Path,
    time::Duration,
};

use reqwest::{
    blocking::{Client, multipart},
    redirect::Policy,
};
use serde::Deserialize;

use crate::{MalwareScanner, ScanGateError, ScanPhase, ScanVerdict};

const CLOUDMERSIVE_ENDPOINT: &str = "https://api.cloudmersive.com/virus/scan/file";
pub const CLOUDMERSIVE_KEY_ENV: &str = "CLOUDMERSIVE_API_KEY";
pub const MAX_CLOUD_SCAN_FILE_BYTES: u64 = 32 * 1024 * 1024;
const MAX_RESPONSE_BYTES: u64 = 64 * 1024;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct CloudmersiveResponse {
    clean_result: bool,
    #[serde(default)]
    found_viruses: Option<Vec<CloudmersiveDetection>>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct CloudmersiveDetection {
    file_name: Option<String>,
    virus_name: Option<String>,
}

pub struct CloudmersiveScanner {
    client: Client,
    api_key: String,
}

fn unavailable() -> ScanGateError {
    ScanGateError::ScannerUnavailable
}

#[cfg(windows)]
fn is_reparse_point(metadata: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    metadata.file_attributes() & 0x0400 != 0
}
#[cfg(not(windows))]
fn is_reparse_point(_metadata: &fs::Metadata) -> bool {
    false
}

fn response_verdict(body: &[u8]) -> Result<ScanVerdict, ScanGateError> {
    let record: CloudmersiveResponse =
        serde_json::from_slice(body).map_err(|_| unavailable())?;
    let findings = record.found_viruses.unwrap_or_default();
    // Unexpected, contradictory results are not silently accepted.
    if record.clean_result && !findings.is_empty() {
        return Err(unavailable());
    }
    // Parse fields without reflecting potentially sensitive cloud data to logs.
    let _ = findings.iter().any(|d| d.file_name.is_some() || d.virus_name.is_some());
    if record.clean_result {
        Ok(ScanVerdict::Clean)
    } else if findings.is_empty() {
        Ok(ScanVerdict::Suspicious)
    } else {
        Ok(ScanVerdict::Threat)
    }
}

impl CloudmersiveScanner {
    /// @dev-id BRK-CLOUD-AV-001
    /// @purpose Create a scanner only after explicit upload consent.
    /// @contract Key comes from environment, never persisted in a .tzproj.
    pub fn new(explicit_consent: bool) -> Result<Self, ScanGateError> {
        if !explicit_consent {
            return Err(unavailable());
        }
        let api_key = std::env::var(CLOUDMERSIVE_KEY_ENV).map_err(|_| unavailable())?;
        if api_key.is_empty()
            || api_key.len() > 1024
            || api_key.chars().any(char::is_control)
        {
            return Err(unavailable());
        }
        let client = Client::builder()
            .timeout(Duration::from_secs(120))
            .connect_timeout(Duration::from_secs(10))
            .redirect(Policy::none())
            .build()
            .map_err(|_| unavailable())?;
        Ok(Self { client, api_key })
    }
}

impl MalwareScanner for CloudmersiveScanner {
    /// @dev-id BRK-CLOUD-AV-002
    /// @purpose Upload one explicitly authorized staged payload for AV scan.
    /// @contract Never upload without consent, accept redirects, or ignore API failures.
    fn scan_file(&self, file: &Path, _phase: ScanPhase) -> Result<ScanVerdict, ScanGateError> {
        if !file.is_absolute() {
            return Err(unavailable());
        }
        let metadata = fs::symlink_metadata(file).map_err(|_| unavailable())?;
        if !metadata.is_file()
            || metadata.file_type().is_symlink()
            || is_reparse_point(&metadata)
            || metadata.len() > MAX_CLOUD_SCAN_FILE_BYTES
        {
            return Err(unavailable());
        }

        let form = multipart::Form::new()
            .file("inputFile", file)
            .map_err(|_| unavailable())?;
        let mut response = self
            .client
            .post(CLOUDMERSIVE_ENDPOINT)
            .header("Apikey", &self.api_key)
            .header("Accept", "application/json")
            .multipart(form)
            .send()
            .map_err(|_| unavailable())?;
        if !response.status().is_success() {
            return Err(unavailable());
        }

        let mut body = Vec::new();
        response
            .by_ref()
            .take(MAX_RESPONSE_BYTES + 1)
            .read_to_end(&mut body)
            .map_err(|_| unavailable())?;
        if body.len() as u64 > MAX_RESPONSE_BYTES {
            return Err(unavailable());
        }
        response_verdict(&body)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clean_and_threat_contracts_are_distinct() {
        assert_eq!(
            response_verdict(br#"{"CleanResult":true,"FoundViruses":null}"#).unwrap(),
            ScanVerdict::Clean
        );
        assert_eq!(
            response_verdict(
                br#"{"CleanResult":false,"FoundViruses":[{"FileName":"test","VirusName":"TEST"}]}"#
            ).unwrap(),
            ScanVerdict::Threat
        );
        assert_eq!(
            response_verdict(br#"{"CleanResult":false,"FoundViruses":[]}"#).unwrap(),
            ScanVerdict::Suspicious
        );
    }

    #[test]
    fn missing_or_conflicting_cloud_data_fails_closed() {
        for response in [
            br#"{}"#.as_slice(),
            br#"{"CleanResult":"yes"}"#.as_slice(),
            br#"{"CleanResult":true,"FoundViruses":[{"VirusName":"test"}]}"#.as_slice(),
        ] {
            assert!(response_verdict(response).is_err());
        }
        assert!(CloudmersiveScanner::new(false).is_err());
    }
}
