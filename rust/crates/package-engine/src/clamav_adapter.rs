// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/clamav_adapter.rs
// Amac: ClamAV CLI taramasini ortak MalwareScanner servis sinirina baglamak
// Modul - Rust Security Adapter
// Version: 0.1.0
// Aciklama: Kullanici sectigi motor, timeout, limit ve islem hata kontrolu
// Bagimli Oldugu Katman: Service | Adapter

use std::{
    fs,
    io::{self, Read},
    path::{Path, PathBuf},
    process::{Command, ExitStatus, Stdio},
    thread,
    time::{Duration, Instant},
};

use crate::{MalwareScanner, ScanGateError, ScanPhase, ScanVerdict};

const SCAN_TIMEOUT: Duration = Duration::from_secs(120);
const POLL_INTERVAL: Duration = Duration::from_millis(50);
const MAXIMUM_OUTPUT: u64 = 64 * 1024;
const MAXIMUM_INPUT_SIZE: u64 = 2_000 * 1024 * 1024;

// BRK-CLAMAV-ADAPTER-001: ClamAV clamscan exit status contract:
// 0 no reported detection, 1 detection, 2 or anything else an engine error.
// The scanner must never delete, move or overwrite source files.
#[derive(Debug, Clone)]
pub struct ClamAvScanner {
    executable: PathBuf,
}

fn unavailable() -> ScanGateError {
    ScanGateError::ScannerUnavailable
}

#[cfg(windows)]
fn is_reparse_point(meta: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    meta.file_attributes() & 0x0400 != 0
}

#[cfg(not(windows))]
fn is_reparse_point(_meta: &fs::Metadata) -> bool {
    false
}

fn ensure_regular_file(path: &Path) -> Result<fs::Metadata, ScanGateError> {
    if !path.is_absolute() {
        return Err(unavailable());
    }
    let metadata = fs::symlink_metadata(path).map_err(|_| unavailable())?;
    if !metadata.is_file() || metadata.file_type().is_symlink() || is_reparse_point(&metadata) {
        return Err(unavailable());
    }
    Ok(metadata)
}

fn bounded_output(mut reader: impl Read) -> Result<Vec<u8>, io::Error> {
    let mut bytes = Vec::new();
    reader
        .by_ref()
        .take(MAXIMUM_OUTPUT + 1)
        .read_to_end(&mut bytes)?;
    if bytes.len() as u64 > MAXIMUM_OUTPUT {
        return Err(io::Error::other(
            "ClamAV output exceeded the supported limit",
        ));
    }
    Ok(bytes)
}

fn interpret(status: ExitStatus, stdout: &[u8]) -> Result<ScanVerdict, ScanGateError> {
    match status.code() {
        Some(0) => Ok(ScanVerdict::Clean),
        Some(1)
            if stdout
                .windows(b"Heuristics.Limits.Exceeded".len())
                .any(|window| window == b"Heuristics.Limits.Exceeded") =>
        {
            Ok(ScanVerdict::Suspicious)
        }
        Some(1) => Ok(ScanVerdict::Threat),
        _ => Err(unavailable()),
    }
}

impl ClamAvScanner {
    pub fn new(executable: PathBuf) -> Result<Self, ScanGateError> {
        ensure_regular_file(&executable)?;
        Ok(Self { executable })
    }
}

impl MalwareScanner for ClamAvScanner {
    fn scan_file(&self, file: &Path, _phase: ScanPhase) -> Result<ScanVerdict, ScanGateError> {
        ensure_regular_file(&self.executable)?;
        let metadata = ensure_regular_file(file)?;
        // ClamAV may otherwise skip files exceeding its limits and report success.
        if metadata.len() > MAXIMUM_INPUT_SIZE {
            return Err(unavailable());
        }

        let mut child = Command::new(&self.executable)
            .args([
                "--stdout",
                "--no-summary",
                "--infected",
                "--alert-exceeds-max=yes",
                "--max-filesize=2000M",
                "--max-scansize=4000M",
                "--follow-file-symlinks=0",
                "--follow-dir-symlinks=0",
            ])
            .arg(file)
            .stdin(Stdio::null())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .map_err(|_| unavailable())?;

        let out = child.stdout.take().ok_or_else(unavailable)?;
        let err = child.stderr.take().ok_or_else(unavailable)?;
        let reader_out = thread::spawn(move || bounded_output(out));
        let reader_err = thread::spawn(move || bounded_output(err));
        let start = Instant::now();

        let status = loop {
            match child.try_wait() {
                Ok(Some(status)) => break Ok(status),
                Ok(None) if start.elapsed() < SCAN_TIMEOUT => thread::sleep(POLL_INTERVAL),
                Ok(None) | Err(_) => {
                    let _ = child.kill();
                    let _ = child.wait();
                    break Err(unavailable());
                }
            }
        };
        let stdout = reader_out
            .join()
            .map_err(|_| unavailable())?
            .map_err(|_| unavailable())?;
        reader_err
            .join()
            .map_err(|_| unavailable())?
            .map_err(|_| unavailable())?;
        interpret(status?, &stdout)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::process::Command;

    #[test]
    fn clamscan_exit_codes_fail_closed() {
        // ExitStatus is platform specific; only test on a platform when a
        // standard process is available. No antivirus is required for this test.
        let mut command = if cfg!(windows) {
            let mut command = Command::new("cmd");
            command.args(["/C", "exit", "2"]);
            command
        } else {
            let mut command = Command::new("sh");
            command.args(["-c", "exit 2"]);
            command
        };
        let status = command.status().expect("test command");
        assert!(interpret(status, b"").is_err());
    }

    #[test]
    fn clamscan_missing_binary_is_an_error() {
        assert!(ClamAvScanner::new(PathBuf::from("clamscan")).is_err());
    }
}
