// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/turkuaz_av_adapter.rs
// Amac: TurkuazAV rule-scan JSON protokolunu platform bagimsiz Rust scanner'a baglamak
// Modul - Rust Security Adapter
// Version: 0.1.1
// Aciklama: Pinli motor/anahtar, surumlu JSON, timeout, sinirli cikti ve fail-closed
// Bagimli Oldugu Katman: Service | Adapter

use std::{
    fs,
    io::{self, Read},
    path::{Path, PathBuf},
    process::{Command, Stdio},
    thread,
    time::{Duration, Instant},
};

use serde::Deserialize;
use sha2::{Digest, Sha256};

use crate::malware_scan::{MalwareScanner, ScanGateError, ScanPhase, ScanVerdict};

const SCHEMA: &str = "turkuazav.rule-scan.v1";
const COMMAND: &str = "scan-rules-json";
const MAX_OUTPUT: u64 = 64 * 1024;
const TIMEOUT: Duration = Duration::from_secs(45);
const POLL_INTERVAL: Duration = Duration::from_millis(40);

/// A rule no-match is NOT a guarantee of a virus-free file.
/// Default behavior is to deny an unproven allow decision.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum NoMatchPolicy {
    BlockAsUnknown,
    AcceptOnlyForPinnedRuleCoverage,
}

#[derive(Debug)]
pub struct TurkuazAvScanner {
    cli: PathBuf,
    rules: PathBuf,
    signature: PathBuf,
    public_key: PathBuf,
    engine: PathBuf,
    cli_sha256: String,
    public_key_sha256: String,
    no_match: NoMatchPolicy,
}

#[derive(Deserialize)]
#[serde(deny_unknown_fields)]
struct Evidence {
    schema: String,
    status: String,
    sha256: String,
    matches: Vec<String>,
}

fn scanner_failed() -> ScanGateError {
    ScanGateError::ScannerUnavailable
}

fn sha256_file(path: &Path) -> Result<String, ScanGateError> {
    if !path.is_absolute() {
        return Err(scanner_failed());
    }
    let meta = fs::symlink_metadata(path).map_err(|_| scanner_failed())?;
    if meta.file_type().is_symlink() || !meta.is_file() || reparse_point(&meta) {
        return Err(scanner_failed());
    }
    let mut file = fs::File::open(path).map_err(|_| scanner_failed())?;
    let mut hasher = Sha256::new();
    let mut buffer = [0_u8; 64 * 1024];
    loop {
        let count = file.read(&mut buffer).map_err(|_| scanner_failed())?;
        if count == 0 {
            break;
        }
        hasher.update(&buffer[..count]);
    }
    Ok(format!("{:x}", hasher.finalize()))
}

#[cfg(windows)]
fn reparse_point(meta: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    meta.file_attributes() & 0x0400 != 0
}

#[cfg(not(windows))]
fn reparse_point(_meta: &fs::Metadata) -> bool {
    false
}

fn valid_pin(value: &str) -> bool {
    value.len() == 64 && value.bytes().all(|b| b.is_ascii_hexdigit())
}

impl TurkuazAvScanner {
    // BRK-TURKUAZAV-ADAPTER-001: Expected hashes MUST come from independent trusted
    // configuration, never from a scanned archive or command-line report.
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        cli: PathBuf,
        rules: PathBuf,
        signature: PathBuf,
        public_key: PathBuf,
        engine: PathBuf,
        expected_cli_sha256: String,
        expected_public_key_sha256: String,
        no_match: NoMatchPolicy,
    ) -> Result<Self, ScanGateError> {
        if !valid_pin(&expected_cli_sha256) || !valid_pin(&expected_public_key_sha256) {
            return Err(scanner_failed());
        }
        let scanner = Self {
            cli,
            rules,
            signature,
            public_key,
            engine,
            cli_sha256: expected_cli_sha256.to_lowercase(),
            public_key_sha256: expected_public_key_sha256.to_lowercase(),
            no_match,
        };
        scanner.verify_pins()?;
        for path in [&scanner.rules, &scanner.signature, &scanner.engine] {
            sha256_file(path)?;
        }
        Ok(scanner)
    }

    fn verify_pins(&self) -> Result<(), ScanGateError> {
        if sha256_file(&self.cli)? != self.cli_sha256
            || sha256_file(&self.public_key)? != self.public_key_sha256
        {
            return Err(scanner_failed());
        }
        Ok(())
    }

    fn parse(&self, output: &[u8], expected_sha: &str) -> Result<ScanVerdict, ScanGateError> {
        let record: Evidence = serde_json::from_slice(output).map_err(|_| scanner_failed())?;
        if record.schema != SCHEMA || record.sha256 != expected_sha {
            return Err(scanner_failed());
        }
        match record.status.as_str() {
            "match" if !record.matches.is_empty() => Ok(ScanVerdict::Suspicious),
            "no_match" if record.matches.is_empty() => match self.no_match {
                NoMatchPolicy::BlockAsUnknown => Ok(ScanVerdict::Unknown),
                NoMatchPolicy::AcceptOnlyForPinnedRuleCoverage => Ok(ScanVerdict::Clean),
            },
            _ => Err(scanner_failed()),
        }
    }
}

fn read_capped(mut pipe: impl Read) -> Result<Vec<u8>, io::Error> {
    let mut output = Vec::new();
    pipe.by_ref()
        .take(MAX_OUTPUT + 1)
        .read_to_end(&mut output)?;
    if output.len() as u64 > MAX_OUTPUT {
        return Err(io::Error::other("AV output exceeds protocol limit"));
    }
    Ok(output)
}

impl MalwareScanner for TurkuazAvScanner {
    // BRK-TURKUAZAV-ADAPTER-002: Run the pinned AV process directly with bounded
    // stdout/stderr and a deadline; never shell-expand file paths or trust exit code alone.
    fn scan_file(&self, file: &Path, _phase: ScanPhase) -> Result<ScanVerdict, ScanGateError> {
        self.verify_pins()?;
        let expected = sha256_file(file)?;
        let mut child = Command::new(&self.cli)
            .arg(COMMAND)
            .arg(file)
            .arg(&self.rules)
            .arg(&self.signature)
            .arg(&self.public_key)
            .arg(&self.engine)
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .map_err(|_| scanner_failed())?;

        let stdout = child.stdout.take().ok_or_else(scanner_failed)?;
        let stderr = child.stderr.take().ok_or_else(scanner_failed)?;
        let output_thread = thread::spawn(move || read_capped(stdout));
        let error_thread = thread::spawn(move || read_capped(stderr));
        let started = Instant::now();

        let status = loop {
            match child.try_wait() {
                Ok(Some(exit)) => break Ok(exit),
                Ok(None) if started.elapsed() < TIMEOUT => thread::sleep(POLL_INTERVAL),
                Ok(None) | Err(_) => {
                    let _ = child.kill();
                    let _ = child.wait();
                    break Err(scanner_failed());
                }
            }
        };
        let output = output_thread
            .join()
            .map_err(|_| scanner_failed())?
            .map_err(|_| scanner_failed())?;
        error_thread
            .join()
            .map_err(|_| scanner_failed())?
            .map_err(|_| scanner_failed())?;

        if !status?.success() || sha256_file(file)? != expected {
            return Err(scanner_failed());
        }
        self.parse(&output, &expected)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn scanner(policy: NoMatchPolicy) -> TurkuazAvScanner {
        TurkuazAvScanner {
            cli: PathBuf::new(),
            rules: PathBuf::new(),
            signature: PathBuf::new(),
            public_key: PathBuf::new(),
            engine: PathBuf::new(),
            cli_sha256: String::new(),
            public_key_sha256: String::new(),
            no_match: policy,
        }
    }

    #[test]
    fn av_contract_default_is_fail_closed_for_no_match() {
        let hash = "0".repeat(64);
        let response = format!(
            "{{\"schema\":\"{SCHEMA}\",\"status\":\"no_match\",\"sha256\":\"{hash}\",\"matches\":[]}}"
        );
        assert_eq!(
            scanner(NoMatchPolicy::BlockAsUnknown)
                .parse(response.as_bytes(), &hash)
                .unwrap(),
            ScanVerdict::Unknown
        );
        assert_eq!(
            scanner(NoMatchPolicy::AcceptOnlyForPinnedRuleCoverage)
                .parse(response.as_bytes(), &hash)
                .unwrap(),
            ScanVerdict::Clean
        );
    }

    #[test]
    fn av_contract_match_blocks_and_rejects_spoofed_hash() {
        let hash = "a".repeat(64);
        let response = format!(
            "{{\"schema\":\"{SCHEMA}\",\"status\":\"match\",\"sha256\":\"{hash}\",\"matches\":[\"TEST_RULE\"]}}"
        );
        let scanner = scanner(NoMatchPolicy::BlockAsUnknown);
        assert_eq!(
            scanner.parse(response.as_bytes(), &hash).unwrap(),
            ScanVerdict::Suspicious
        );
        assert!(scanner.parse(response.as_bytes(), &"b".repeat(64)).is_err());
        let forged = response.replace("match", "no_match");
        assert!(scanner.parse(forged.as_bytes(), &hash).is_err());
    }

    #[test]
    fn av_contract_rejects_malformed_and_unknown_status() {
        let scanner = scanner(NoMatchPolicy::BlockAsUnknown);
        for input in [b"{}".as_slice(), b"not-json".as_slice(),
            b"{\"schema\":\"turkuazav.rule-scan.v2\",\"status\":\"no_match\",\"sha256\":\"00\",\"matches\":[]}".as_slice()]
        {
            assert!(scanner.parse(input, "00").is_err());
        }
        assert!(!valid_pin("a"));
        assert!(valid_pin(&"f".repeat(64)));
    }
}
