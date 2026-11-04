// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/wizard_language.rs
// Amac: Standart yerel kurulum sihirbazi icin kucuk ve test edilebilir dil sozlugu
// Modul - Rust Windows Wizard Language
// Version: 0.1.0
// Aciklama: Turkce ve Ingilizce arayuz, guvenli varsayilan ve yerel degistirme
// Bagimli Oldugu Katman: Presentation | Language

/// @dev-id BRK-WIZARD-LANG-001
/// @purpose Choose a UI language without changing package content or scope.
/// @contract Never translate package identifiers, filesystem paths, or engine errors.
#[derive(Clone, Copy, Debug, Default, Eq, PartialEq)]
pub(super) enum WizardLanguage {
    #[default]
    Turkish,
    English,
}

impl WizardLanguage {
    pub(super) fn label(self) -> &'static str {
        match self {
            Self::Turkish => "Türkçe",
            Self::English => "English",
        }
    }

    pub(super) fn text(self, tr: &'static str, en: &'static str) -> &'static str {
        match self {
            Self::Turkish => tr,
            Self::English => en,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn language_can_be_switched_without_losing_turkish_default() {
        assert_eq!(WizardLanguage::default(), WizardLanguage::Turkish);
        assert_eq!(WizardLanguage::Turkish.text("Kurulum", "Installation"), "Kurulum");
        assert_eq!(WizardLanguage::English.text("Kurulum", "Installation"), "Installation");
        assert_ne!(WizardLanguage::Turkish.label(), WizardLanguage::English.label());
    }
}
