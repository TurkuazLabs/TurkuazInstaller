// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_av_options.rs
// Amac: Setup Studio icin istege bagli coklu antivirus secim ayarlari
// Modul - Rust Config and Security Adapter
// Version: 0.2.1
// Aciklama: Kapali mod, TurkuazAV veya ClamAV secimi, hatada sessiz gecis yok
// Bagimli Oldugu Katman: Config | Service | Adapter

use std::{
    fs,
    path::{Path, PathBuf},
};

use serde::{Deserialize, Serialize};
use turkuaz_installer_package_engine::{
    CLOUDMERSIVE_KEY_ENV, ClamAvScanner, CloudmersiveScanner, MalwareScanner, NoMatchPolicy,
    TurkuazAvScanner, scan_package_archive,
};

#[derive(Debug, Default, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub(super) enum AvProvider {
    #[default]
    TurkuazAv,
    ClamAv,
    Cloudmersive,
}

impl AvProvider {
    pub(super) const ALL: [Self; 3] = [Self::TurkuazAv, Self::ClamAv, Self::Cloudmersive];

    pub(super) const fn label(self) -> &'static str {
        match self {
            Self::TurkuazAv => "TurkuazAV (YARA-X)",
            Self::ClamAv => "ClamAV",
            Self::Cloudmersive => "Cloudmersive (çevrimiçi)",
        }
    }
}

#[derive(Debug, Default, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields, default)]
pub(super) struct AvScanConfig {
    pub(super) enabled: bool,
    pub(super) provider: AvProvider,
    pub(super) executable_path: String,
    pub(super) rules_path: String,
    pub(super) signature_path: String,
    pub(super) public_key_path: String,
    pub(super) engine_path: String,
    pub(super) expected_cli_sha256: String,
    pub(super) expected_public_key_sha256: String,
    // YARA-X rule no-match is not a guarantee of malware-free software.
    // The operator must opt in specifically to accept pinned ruleset coverage.
    pub(super) accept_pinned_no_match: bool,
    // Must be checked explicitly before sending any local file to a third party.
    pub(super) cloud_upload_consent: bool,
}

fn selected_file(value: &str, label: &str) -> Result<PathBuf, String> {
    let path = PathBuf::from(value.trim());
    if !path.is_absolute() {
        return Err(format!("{label}: mutlak dosya yolu seçilmelidir."));
    }
    let metadata =
        fs::symlink_metadata(&path).map_err(|error| format!("{label} bulunamadı: {error}"))?;
    if !metadata.is_file() || metadata.file_type().is_symlink() {
        return Err(format!("{label}: normal bir dosya seçilmelidir."));
    }
    Ok(path)
}

impl AvScanConfig {
    // BRK-AV-OPTIONAL-001: Disabled is an explicit no-scan policy, not an
    // unsuccessful scan. Enabled mode must have a selected, usable scanner.
    pub(super) fn validate(&self) -> Result<(), String> {
        if !self.enabled {
            return Ok(());
        }
        if self.provider == AvProvider::Cloudmersive {
            if !self.cloud_upload_consent {
                return Err("Bulut taraması için dosyaların sağlayıcıya gönderilmesini açıkça onaylamalısınız.".to_owned());
            }
            if std::env::var(CLOUDMERSIVE_KEY_ENV).map_or(true, |key| key.trim().is_empty()) {
                return Err(format!(
                    "Çevrimiçi tarama anahtarı eksik: {} ortam değişkenini ayarlayın.",
                    CLOUDMERSIVE_KEY_ENV
                ));
            }
            return Ok(());
        }
        selected_file(&self.executable_path, "Antivirüs motoru")?;
        if self.provider == AvProvider::TurkuazAv {
            selected_file(&self.rules_path, "YARA-X kural dosyası")?;
            selected_file(&self.signature_path, "Kural imza dosyası")?;
            selected_file(&self.public_key_path, "Güvenilir yayıncı açık anahtarı")?;
            selected_file(&self.engine_path, "YARA-X motoru")?;
            for (name, digest) in [
                ("TurkuazAV program SHA-256", &self.expected_cli_sha256),
                ("Yayıncı anahtarı SHA-256", &self.expected_public_key_sha256),
            ] {
                if digest.len() != 64 || !digest.bytes().all(|b| b.is_ascii_hexdigit()) {
                    return Err(format!("{name}: 64 karakterli SHA-256 gerekli."));
                }
            }
        }
        Ok(())
    }

    fn scanner(&self) -> Result<Box<dyn MalwareScanner>, String> {
        self.validate()?;
        if !self.enabled {
            return Err("Antivirüs taraması kapalıdır.".to_owned());
        }
        if self.provider == AvProvider::Cloudmersive {
            return CloudmersiveScanner::new(self.cloud_upload_consent)
                .map(|scanner| Box::new(scanner) as Box<dyn MalwareScanner>)
                .map_err(|error| format!("Cloudmersive başlatılamadı: {error}"));
        }
        let binary = selected_file(&self.executable_path, "Antivirüs motoru")?;
        match self.provider {
            AvProvider::ClamAv => ClamAvScanner::new(binary)
                .map(|scanner| Box::new(scanner) as Box<dyn MalwareScanner>)
                .map_err(|error| format!("ClamAV başlatılamadı: {error}")),
            AvProvider::Cloudmersive => unreachable!("online provider handled above"),
            AvProvider::TurkuazAv => TurkuazAvScanner::new(
                binary,
                selected_file(&self.rules_path, "YARA-X kural dosyası")?,
                selected_file(&self.signature_path, "Kural imza dosyası")?,
                selected_file(&self.public_key_path, "Yayıncı anahtarı")?,
                selected_file(&self.engine_path, "YARA-X motoru")?,
                self.expected_cli_sha256.clone(),
                self.expected_public_key_sha256.clone(),
                if self.accept_pinned_no_match {
                    NoMatchPolicy::AcceptOnlyForPinnedRuleCoverage
                } else {
                    NoMatchPolicy::BlockAsUnknown
                },
            )
            .map(|scanner| Box::new(scanner) as Box<dyn MalwareScanner>)
            .map_err(|error| format!("TurkuazAV başlatılamadı: {error}")),
        }
    }

    // BRK-AV-OPTIONAL-002: Scan the extracted bytes of the new .tzpkg before
    // any build outputs are published. No silent fallback to another provider.
    pub(super) fn scan_package(
        &self,
        package_path: &Path,
        staging: &Path,
        id: &str,
        version: &str,
    ) -> Result<usize, String> {
        let scanner = self.scanner()?;
        let summary = scan_package_archive(package_path, staging, id, version, scanner.as_ref())
            .map_err(|error| format!("{} taraması başarısız: {error}", self.provider.label()))?;
        Ok(summary.files_scanned)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn old_project_defaults_to_no_scan() {
        let config: AvScanConfig = serde_json::from_str("{}").expect("legacy defaults");
        assert!(!config.enabled);
        assert_eq!(config.provider, AvProvider::TurkuazAv);
        assert!(config.validate().is_ok());
        let encoded = serde_json::to_value(config).expect("serialize");
        assert_eq!(encoded["enabled"], false);
    }

    #[test]
    fn selected_unavailable_scanner_fails_closed() {
        let config = AvScanConfig {
            enabled: true,
            provider: AvProvider::ClamAv,
            ..AvScanConfig::default()
        };
        assert!(config.validate().is_err());
        assert!(config.scanner().is_err());
    }

    #[test]
    fn cloud_provider_requires_explicit_consent_without_local_motor() {
        let mut config = AvScanConfig {
            enabled: true,
            provider: AvProvider::Cloudmersive,
            cloud_upload_consent: false,
            ..AvScanConfig::default()
        };
        assert!(config.validate().is_err());
        config.cloud_upload_consent = true;
        // A missing API key still blocks. No network requests in unit tests.
        // Never modify environment variables in parallel tests.
        assert_eq!(config.provider.label(), "Cloudmersive (çevrimiçi)");
        assert!(config.executable_path.is_empty());
    }

    #[test]
    fn provider_roundtrips_and_does_not_change_off_state() {
        let config = AvScanConfig {
            enabled: false,
            provider: AvProvider::ClamAv,
            executable_path: "not-installed".to_owned(),
            ..AvScanConfig::default()
        };
        let saved = serde_json::to_string(&config).expect("save");
        let loaded: AvScanConfig = serde_json::from_str(&saved).expect("load");
        assert_eq!(config, loaded);
        assert!(loaded.validate().is_ok());
    }
}
