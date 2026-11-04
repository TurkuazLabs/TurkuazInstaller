// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/wizard_config.rs
// Amac: Yerel kurulum sihirbazi sayfa sirasi, zorunlu adimlari ve EULA kontrati
// Modul - Rust Package Configuration Service
// Version: 0.1.0
// Aciklama: Surumlu, sinirli ve eski projeleri bozmayan kurulum sayfasi ayarlari
// Bagimli Oldugu Katman: Service | Model | Security

use serde::{Deserialize, Serialize};

pub const WIZARD_CONFIG_VERSION: u32 = 1;
pub const MAX_WIZARD_CONFIG_BYTES: usize = 128 * 1024;
pub const MAX_LICENSE_BYTES: usize = 48 * 1024;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum WizardPageId {
    Welcome,
    License,
    Destination,
    Shortcuts,
    Review,
    Installing,
    Finished,
}

impl WizardPageId {
    pub const ALL: [Self; 7] = [
        Self::Welcome,
        Self::License,
        Self::Destination,
        Self::Shortcuts,
        Self::Review,
        Self::Installing,
        Self::Finished,
    ];

    pub const fn label(self) -> &'static str {
        match self {
            Self::Welcome => "Hoş geldiniz",
            Self::License => "Lisans sözleşmesi (EULA)",
            Self::Destination => "Kurulum konumu",
            Self::Shortcuts => "Kısayol seçenekleri",
            Self::Review => "Kuruluma hazır",
            Self::Installing => "Kuruluyor",
            Self::Finished => "Tamamlandı",
        }
    }

    pub const fn required(self) -> bool {
        matches!(self, Self::Review | Self::Installing | Self::Finished)
    }

    pub const fn movable(self) -> bool {
        !self.required()
    }

    pub const fn title_en(self) -> &'static str {
        match self {
            Self::Welcome => "Welcome",
            Self::License => "End-User License Agreement",
            Self::Destination => "Installation folder",
            Self::Shortcuts => "Additional tasks",
            Self::Review => "Ready to install",
            Self::Installing => "Installing",
            Self::Finished => "Installation result",
        }
    }

    pub const fn description_tr(self) -> &'static str {
        match self {
            Self::Welcome => "Kurulum sihirbazına hoş geldiniz.",
            Self::License => "Lütfen son kullanıcı lisans sözleşmesini dikkatle okuyun.",
            Self::Destination => "Uygulamanın kurulacağı klasörü seçin.",
            Self::Shortcuts => "Oluşturulacak kısayolları seçin.",
            Self::Review => "Kurulumu başlatmadan önce seçimleri kontrol edin.",
            Self::Installing => "Dosyalar güvenli şekilde kuruluyor.",
            Self::Finished => "Kurulum işleminin sonucu.",
        }
    }

    pub const fn description_en(self) -> &'static str {
        match self {
            Self::Welcome => "Welcome to the setup wizard.",
            Self::License => "Please read the license agreement carefully.",
            Self::Destination => "Choose an installation folder.",
            Self::Shortcuts => "Choose shortcuts to create.",
            Self::Review => "Review your selections before installing.",
            Self::Installing => "Application files are being installed.",
            Self::Finished => "Installation result.",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct WizardPageSettings {
    pub page: WizardPageId,
    pub enabled: bool,
    pub title_tr: String,
    pub title_en: String,
    pub description_tr: String,
    pub description_en: String,
}

impl WizardPageSettings {
    pub fn default_for(page: WizardPageId) -> Self {
        Self {
            page,
            enabled: page != WizardPageId::License,
            title_tr: page.label().to_owned(),
            title_en: page.title_en().to_owned(),
            description_tr: page.description_tr().to_owned(),
            description_en: page.description_en().to_owned(),
        }
    }

    pub fn title(&self, english: bool) -> &str {
        if english {
            &self.title_en
        } else {
            &self.title_tr
        }
    }

    pub fn description(&self, english: bool) -> &str {
        if english {
            &self.description_en
        } else {
            &self.description_tr
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct WizardConfig {
    pub format_version: u32,
    pub pages: Vec<WizardPageSettings>,
    pub license_text_tr: String,
    pub license_text_en: String,
}

impl Default for WizardConfig {
    fn default() -> Self {
        Self {
            format_version: WIZARD_CONFIG_VERSION,
            pages: WizardPageId::ALL
                .iter()
                .copied()
                .map(WizardPageSettings::default_for)
                .collect(),
            license_text_tr: String::new(),
            license_text_en: String::new(),
        }
    }
}

impl WizardConfig {
    pub fn page(&self, page: WizardPageId) -> Option<&WizardPageSettings> {
        self.pages.iter().find(|item| item.page == page)
    }

    pub fn page_mut(&mut self, page: WizardPageId) -> Option<&mut WizardPageSettings> {
        self.pages.iter_mut().find(|item| item.page == page)
    }

    pub fn is_enabled(&self, page: WizardPageId) -> bool {
        self.page(page).is_some_and(|item| item.enabled)
    }

    pub fn license_text(&self, english: bool) -> &str {
        if english {
            &self.license_text_en
        } else {
            &self.license_text_tr
        }
    }

    /// @dev-id BRK-WIZARD-CONFIG-001
    /// @purpose Keep UI customization outside the security-sensitive install phases.
    /// @contract Review, Installing and Finished can never be disabled, removed
    /// or moved ahead of pre-install pages. The EULA must have real supplied text
    /// in both exposed languages before the wizard can be packaged.
    pub fn validate(&self) -> Result<(), String> {
        if self.format_version != WIZARD_CONFIG_VERSION {
            return Err("Sihirbaz yapılandırma sürümü desteklenmiyor.".into());
        }
        if self.pages.len() != WizardPageId::ALL.len() {
            return Err("Sihirbaz bütün tanımlı sayfaları içermelidir.".into());
        }
        let mut seen = std::collections::HashSet::new();
        for (index, item) in self.pages.iter().enumerate() {
            if !seen.insert(item.page) {
                return Err("Sihirbazda yinelenen sayfa bulundu.".into());
            }
            if item.page.required() != (index >= 4) {
                return Err("Zorunlu kurulum aşamalarının sırası değiştirilemez.".into());
            }
            if item.page.required() && !item.enabled {
                return Err("Kurulum özeti, ilerleme ve sonuç sayfaları zorunludur.".into());
            }
            for value in [
                &item.title_tr,
                &item.title_en,
                &item.description_tr,
                &item.description_en,
            ] {
                if value.contains('\0') || value.chars().any(|ch| ch.is_control() && ch != '\n')
                {
                    return Err("Sihirbaz başlıklarında geçersiz kontrol karakteri var.".into());
                }
            }
            if item.title_tr.trim().is_empty()
                || item.title_en.trim().is_empty()
                || item.title_tr.chars().count() > 100
                || item.title_en.chars().count() > 100
                || item.description_tr.chars().count() > 450
                || item.description_en.chars().count() > 450
            {
                return Err("Sihirbaz başlık veya açıklama uzunluğu geçersiz.".into());
            }
        }
        if self.pages[4].page != WizardPageId::Review
            || self.pages[5].page != WizardPageId::Installing
            || self.pages[6].page != WizardPageId::Finished
        {
            return Err("Kuruluma hazır / Kuruluyor / Tamamlandı sırası sabittir.".into());
        }
        for text in [&self.license_text_tr, &self.license_text_en] {
            if text.len() > MAX_LICENSE_BYTES || text.contains('\0') {
                return Err("EULA metni 48 KiB sınırını aşamaz veya NUL içeremez.".into());
            }
        }
        if self.is_enabled(WizardPageId::License)
            && (self.license_text_tr.trim().is_empty()
                || self.license_text_en.trim().is_empty())
        {
            return Err("EULA açıkken Türkçe ve İngilizce sözleşme metinleri zorunludur.".into());
        }
        Ok(())
    }

    pub fn move_optional(&mut self, page: WizardPageId, delta: isize) -> Result<(), String> {
        if !page.movable() {
            return Err("Bu güvenlik aşamasının yeri değiştirilemez.".into());
        }
        let index = self
            .pages
            .iter()
            .position(|p| p.page == page)
            .ok_or("Sihirbaz sayfası bulunamadı.")?;
        let next = index as isize + delta;
        if !(0..4).contains(&next) {
            return Err("İsteğe bağlı sayfa güvenli kurulum aşamalarını geçemez.".into());
        }
        self.pages.swap(index, next as usize);
        self.validate()
    }

    /// The runtime filters optional pages, but never skips the review/install
    /// security boundary, even when metadata was authored by older Studio.
    pub fn visible_pages(&self, has_exe: bool) -> Vec<WizardPageId> {
        self.pages
            .iter()
            .filter(|item| {
                item.enabled
                    && (item.page != WizardPageId::Shortcuts || has_exe)
            })
            .map(|item| item.page)
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn legacy_default_is_valid_without_eula() {
        let config = WizardConfig::default();
        config.validate().expect("valid legacy pages");
        assert!(!config.is_enabled(WizardPageId::License));
        assert_eq!(config.visible_pages(false).len(), 5);
    }

    #[test]
    fn enabled_eula_requires_explicit_bilingual_text() {
        let mut config = WizardConfig::default();
        config.page_mut(WizardPageId::License).unwrap().enabled = true;
        assert!(config.validate().is_err());
        config.license_text_tr = "Gerçek koşullar".into();
        config.license_text_en = "Actual conditions".into();
        assert!(config.validate().is_ok());
    }

    #[test]
    fn only_optional_pages_can_reorder_or_be_disabled() {
        let mut config = WizardConfig::default();
        config.move_optional(WizardPageId::Destination, -1).unwrap();
        assert_eq!(config.pages[1].page, WizardPageId::Destination);
        assert!(config.move_optional(WizardPageId::Review, -1).is_err());
        config.page_mut(WizardPageId::Review).unwrap().enabled = false;
        assert!(config.validate().is_err());
    }

    #[test]
    fn config_roundtrips_with_unknown_fields_rejected() {
        let config = WizardConfig::default();
        let bytes = serde_json::to_vec(&config).unwrap();
        let parsed: WizardConfig = serde_json::from_slice(&bytes).unwrap();
        assert_eq!(parsed, config);
        let mut json: serde_json::Value = serde_json::from_slice(&bytes).unwrap();
        json["unexpected"] = serde_json::Value::Bool(true);
        assert!(serde_json::from_value::<WizardConfig>(json).is_err());
    }
}
