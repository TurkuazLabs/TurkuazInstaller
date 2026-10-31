// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/web_setup.rs
// Amac: Tek dosyali web kurulum EXE'sine imzali HTTPS yayin ayarlarini gommek
// Modul - Rust Tool | Application
// Version: 0.1.0
// Aciklama: Sinirli JSON, SHA-256 footer ve Authenticode PE siniri ile web installer konfigunu okur
// Bagimli Oldugu Katman: Tool | Domain

use std::{
    fs,
    io::{self, Read, Seek, SeekFrom, Write},
    path::Path,
};

use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use turkuaz_installer_domain::package_id::PackageId;
use turkuaz_installer_package_engine::{SetupThemeConfig, pe_unsigned_payload_end};
use url::Url;

const MAGIC: [u8; 8] = *b"TZWEB001";
const FOOTER_BYTES: u64 = 8 + 8 + 32;
const MAXIMUM_CONFIG_BYTES: usize = 1200 * 1024;
const MAXIMUM_STUB_BYTES: u64 = 128 * 1024 * 1024;
const MAXIMUM_ALIGNMENT_PADDING: u64 = 7;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct WebSetupConfig {
    pub schema_version: u32,
    pub package_id: String,
    pub manifest_url: String,
    pub signature_url: String,
    pub install_target: String,
    pub trust_policy: String,
    pub theme_json: String,
}

impl WebSetupConfig {
    pub fn validate(&self) -> io::Result<()> {
        if self.schema_version != 1 {
            return Err(invalid("Unsupported WebSetup configuration version."));
        }
        PackageId::parse(&self.package_id)
            .map_err(|_| invalid("Invalid WebSetup package identity."))?;
        for address in [&self.manifest_url, &self.signature_url] {
            let url = Url::parse(address.trim())
                .map_err(|_| invalid("WebSetup release URL is invalid."))?;
            if url.scheme() != "https"
                || url.host_str().is_none()
                || !url.username().is_empty()
                || url.password().is_some()
                || url.fragment().is_some()
            {
                return Err(invalid("WebSetup release URLs must be credential-free HTTPS."));
            }
        }
        if self.install_target.trim().is_empty()
            || self.install_target.chars().any(char::is_control)
            || self.trust_policy.trim().is_empty()
        {
            return Err(invalid("WebSetup target or publisher trust policy is missing."));
        }
        if self.trust_policy.len() > 1024 * 1024 {
            return Err(invalid("WebSetup publisher trust policy exceeds 1 MiB."));
        }
        SetupThemeConfig::from_json(self.theme_json.as_bytes())
            .map_err(|_| invalid("WebSetup theme is invalid."))?;
        Ok(())
    }

    fn to_bytes(&self) -> io::Result<Vec<u8>> {
        self.validate()?;
        let bytes = serde_json::to_vec(self)?;
        if bytes.is_empty() || bytes.len() > MAXIMUM_CONFIG_BYTES {
            return Err(invalid("WebSetup configuration exceeds the size limit."));
        }
        Ok(bytes)
    }
}

fn invalid(message: &str) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidData, message.to_owned())
}

fn regular_file(path: &Path) -> io::Result<fs::Metadata> {
    let metadata = fs::symlink_metadata(path)?;
    if !metadata.is_file() || metadata.file_type().is_symlink() {
        return Err(invalid("WebSetup input is not a regular file."));
    }
    Ok(metadata)
}

/// @dev-id TIZ-WEB-001
/// @purpose Append a bounded signed-release configuration, never an application payload.
/// @contract Requires an unsigned Windows PE stub and a new .exe destination; no overwrite.
pub fn create_web_setup(
    stub_path: &Path,
    config: &WebSetupConfig,
    output_path: &Path,
) -> io::Result<()> {
    let bytes = config.to_bytes()?;
    let metadata = regular_file(stub_path)?;
    if metadata.len() < 512 || metadata.len() > MAXIMUM_STUB_BYTES {
        return Err(invalid("WebSetup stub size is invalid."));
    }
    let unsigned_end = pe_unsigned_payload_end(stub_path)
        .map_err(|_| invalid("WebSetup stub does not have a supported PE layout."))?;
    if unsigned_end != metadata.len() {
        return Err(invalid("WebSetup stub must be unsigned before attaching configuration."));
    }
    if !output_path
        .extension()
        .is_some_and(|value| value.eq_ignore_ascii_case("exe"))
    {
        return Err(invalid("WebSetup output must have the .exe extension."));
    }

    let mut input = fs::File::open(stub_path)?;
    let mut output = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(output_path)?;
    let result = (|| {
        if io::copy(&mut input, &mut output)? != metadata.len() {
            return Err(invalid("WebSetup stub changed during copy."));
        }
        output.write_all(&bytes)?;
        output.write_all(&MAGIC)?;
        output.write_all(&(bytes.len() as u64).to_le_bytes())?;
        output.write_all(&Sha256::digest(&bytes))?;
        output.sync_all()
    })();
    if result.is_err() {
        let _ = fs::remove_file(output_path);
    }
    result
}

/// @dev-id TIZ-WEB-002
/// @purpose Read and hash-check the bounded embedded config (also after Authenticode signing).
/// @contract Missing marker means ordinary offline GUI; corrupt marked content is rejected.
pub fn read_web_setup(path: &Path) -> io::Result<Option<WebSetupConfig>> {
    let metadata = regular_file(path)?;
    let payload_end = pe_unsigned_payload_end(path)
        .map_err(|_| invalid("WebSetup executable PE layout is invalid."))?;
    if payload_end < FOOTER_BYTES {
        return Ok(None);
    }
    let mut file = fs::File::open(path)?;
    if file.metadata()?.len() != metadata.len() {
        return Err(invalid("WebSetup executable changed during open."));
    }
    for padding in 0..=MAXIMUM_ALIGNMENT_PADDING {
        let Some(footer_start) = payload_end.checked_sub(FOOTER_BYTES + padding) else {
            continue;
        };
        if padding > 0 {
            file.seek(SeekFrom::Start(payload_end - padding))?;
            let mut padding_bytes = vec![0; padding as usize];
            file.read_exact(&mut padding_bytes)?;
            if padding_bytes.iter().any(|byte| *byte != 0) {
                continue;
            }
        }
        file.seek(SeekFrom::Start(footer_start))?;
        let mut marker = [0_u8; 8];
        file.read_exact(&mut marker)?;
        if marker != MAGIC {
            continue;
        }
        let mut length = [0_u8; 8];
        file.read_exact(&mut length)?;
        let length = u64::from_le_bytes(length);
        if length == 0 || length > MAXIMUM_CONFIG_BYTES as u64 || length > footer_start {
            return Err(invalid("Embedded WebSetup configuration length is invalid."));
        }
        let mut expected_digest = [0_u8; 32];
        file.read_exact(&mut expected_digest)?;
        file.seek(SeekFrom::Start(footer_start - length))?;
        let mut bytes = vec![0; length as usize];
        file.read_exact(&mut bytes)?;
        let digest: [u8; 32] = Sha256::digest(&bytes).into();
        if digest != expected_digest {
            return Err(invalid("Embedded WebSetup configuration SHA-256 mismatch."));
        }
        let config: WebSetupConfig = serde_json::from_slice(&bytes)?;
        config.validate()?;
        return Ok(Some(config));
    }
    Ok(None)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sample() -> WebSetupConfig {
        WebSetupConfig {
            schema_version: 1,
            package_id: "example.product".to_owned(),
            manifest_url: "https://example.test/stable.yaml".to_owned(),
            signature_url: "https://example.test/stable.yaml.p7s".to_owned(),
            install_target: "C:\\Program Files\\Example".to_owned(),
            trust_policy: "schemaVersion: 1".to_owned(),
            theme_json: String::from_utf8(SetupThemeConfig::default().to_json().unwrap())
                .unwrap(),
        }
    }

    #[test]
    fn online_web_setup_rejects_http_credentials_and_empty_trust() {
        let original = sample();
        assert!(original.to_bytes().is_ok());
        let mut changed = original.clone();
        changed.manifest_url = "http://example.test/stable.yaml".to_owned();
        assert!(changed.to_bytes().is_err());
        changed = original.clone();
        changed.signature_url = "https://user:pass@example.test/stable.yaml.p7s".to_owned();
        assert!(changed.to_bytes().is_err());
        changed = original;
        changed.trust_policy.clear();
        assert!(changed.to_bytes().is_err());
    }
}
