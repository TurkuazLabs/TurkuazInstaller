// Dosya Yolu: REPO_ROOT/rust/crates/package-engine/src/setup_theme.rs
// Amac: Dagitilan Turkuaz setup icin sinirli ve dogrulanmis tema ayarini tanimlamak
// Modul - Rust Setup Theme
// Version: 0.2.0
// Aciklama: Hazir setup paletlerini ve marka bazli renk ozellestirmesini guvenli sekilde tasir
// Bagimli Oldugu Katman: Service | Presentation

use std::{error::Error, fmt, str::FromStr};

use serde::{Deserialize, Serialize};

pub const MAXIMUM_SETUP_THEME_BYTES: usize = 16 * 1024;

const DEFAULT_BRAND_NAME: &str = "Turkuaz Installer";
const DEFAULT_SUBTITLE: &str = "Güvenli kurulum ve sürüm yönetimi";
const MINIMUM_TEXT_CONTRAST: f64 = 4.5;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "kebab-case")]
pub enum SetupThemePreset {
    Turkuaz,
    Ocean,
    Forest,
    Sunset,
    Midnight,
    HighContrast,
}

impl SetupThemePreset {
    pub const ALL: [Self; 6] = [
        Self::Turkuaz,
        Self::Ocean,
        Self::Forest,
        Self::Sunset,
        Self::Midnight,
        Self::HighContrast,
    ];

    pub const fn as_str(self) -> &'static str {
        match self {
            Self::Turkuaz => "turkuaz",
            Self::Ocean => "ocean",
            Self::Forest => "forest",
            Self::Sunset => "sunset",
            Self::Midnight => "midnight",
            Self::HighContrast => "high-contrast",
        }
    }

    pub const fn label(self) -> &'static str {
        match self {
            Self::Turkuaz => "Turkuaz",
            Self::Ocean => "Okyanus",
            Self::Forest => "Orman",
            Self::Sunset => "Gün Batımı",
            Self::Midnight => "Gece",
            Self::HighContrast => "Yüksek Kontrast",
        }
    }

    const fn default_palette(self) -> ThemeColorSet {
        match self {
            Self::Turkuaz => ThemeColorSet {
                accent: "#127984",
                light_background: "#F3F8F8",
                light_surface: "#FFFFFF",
                light_text: "#1E3033",
                dark_background: "#11191B",
                dark_surface: "#202A2C",
                dark_text: "#E9F2F3",
            },
            Self::Ocean => ThemeColorSet {
                accent: "#2457A7",
                light_background: "#F1F5FC",
                light_surface: "#FFFFFF",
                light_text: "#1A2940",
                dark_background: "#101827",
                dark_surface: "#1C2A40",
                dark_text: "#EAF0FA",
            },
            Self::Forest => ThemeColorSet {
                accent: "#26734A",
                light_background: "#F1F7F2",
                light_surface: "#FFFFFF",
                light_text: "#1C3023",
                dark_background: "#111B15",
                dark_surface: "#1D2C22",
                dark_text: "#EAF3EC",
            },
            Self::Sunset => ThemeColorSet {
                accent: "#B95533",
                light_background: "#FFF5EF",
                light_surface: "#FFFFFF",
                light_text: "#3A251E",
                dark_background: "#211713",
                dark_surface: "#33231C",
                dark_text: "#FFF0E7",
            },
            Self::Midnight => ThemeColorSet {
                accent: "#9B7BEA",
                light_background: "#F5F2FC",
                light_surface: "#FFFFFF",
                light_text: "#2A2440",
                dark_background: "#15111F",
                dark_surface: "#241D33",
                dark_text: "#F1ECFC",
            },
            Self::HighContrast => ThemeColorSet {
                accent: "#767676",
                light_background: "#FFFFFF",
                light_surface: "#FFFFFF",
                light_text: "#111111",
                dark_background: "#111111",
                dark_surface: "#111111",
                dark_text: "#FFFFFF",
            },
        }
    }
}

impl FromStr for SetupThemePreset {
    type Err = SetupThemeError;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        Self::ALL
            .into_iter()
            .find(|preset| preset.as_str() == value)
            .ok_or(SetupThemeError::Invalid("unsupported setup theme preset"))
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
pub struct SetupThemeConfig {
    schema_version: u32,
    preset: SetupThemePreset,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    brand_name: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    subtitle: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    accent_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    light_background_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    light_surface_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    light_text_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    dark_background_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    dark_surface_color: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    dark_text_color: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct SetupThemePalette {
    pub accent: [u8; 3],
    pub background: [u8; 3],
    pub surface: [u8; 3],
    pub text: [u8; 3],
}

#[derive(Debug)]
pub enum SetupThemeError {
    Invalid(&'static str),
    Json(serde_json::Error),
}

impl fmt::Display for SetupThemeError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Invalid(message) => write!(formatter, "Setup theme is invalid: {message}"),
            Self::Json(error) => write!(formatter, "Setup theme JSON is invalid: {error}"),
        }
    }
}

impl Error for SetupThemeError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Invalid(_) => None,
            Self::Json(error) => Some(error),
        }
    }
}

impl SetupThemeConfig {
    pub fn for_preset(preset: SetupThemePreset) -> Self {
        Self {
            schema_version: 1,
            preset,
            brand_name: None,
            subtitle: None,
            accent_color: None,
            light_background_color: None,
            light_surface_color: None,
            light_text_color: None,
            dark_background_color: None,
            dark_surface_color: None,
            dark_text_color: None,
        }
    }

    pub fn from_json(bytes: &[u8]) -> Result<Self, SetupThemeError> {
        if bytes.is_empty() || bytes.len() > MAXIMUM_SETUP_THEME_BYTES {
            return Err(SetupThemeError::Invalid(
                "configuration is empty or exceeds the size limit",
            ));
        }
        let config: Self = serde_json::from_slice(bytes).map_err(SetupThemeError::Json)?;
        config.validate()?;
        Ok(config)
    }

    pub fn to_json(&self) -> Result<Vec<u8>, SetupThemeError> {
        self.validate()?;
        let bytes = serde_json::to_vec(self).map_err(SetupThemeError::Json)?;
        if bytes.len() > MAXIMUM_SETUP_THEME_BYTES {
            return Err(SetupThemeError::Invalid(
                "configuration exceeds the size limit",
            ));
        }
        Ok(bytes)
    }

    pub fn validate(&self) -> Result<(), SetupThemeError> {
        if self.schema_version != 1 {
            return Err(SetupThemeError::Invalid("unsupported schema version"));
        }
        validate_label(self.brand_name.as_deref(), 64, "brand name")?;
        validate_label(self.subtitle.as_deref(), 128, "subtitle")?;

        for color in [
            self.accent_color.as_deref(),
            self.light_background_color.as_deref(),
            self.light_surface_color.as_deref(),
            self.light_text_color.as_deref(),
            self.dark_background_color.as_deref(),
            self.dark_surface_color.as_deref(),
            self.dark_text_color.as_deref(),
        ]
        .into_iter()
        .flatten()
        {
            parse_color(color)?;
        }

        let defaults = self.preset.default_palette();
        for (background, surface, text) in [
            (
                self.light_background_color
                    .as_deref()
                    .unwrap_or(defaults.light_background),
                self.light_surface_color
                    .as_deref()
                    .unwrap_or(defaults.light_surface),
                self.light_text_color
                    .as_deref()
                    .unwrap_or(defaults.light_text),
            ),
            (
                self.dark_background_color
                    .as_deref()
                    .unwrap_or(defaults.dark_background),
                self.dark_surface_color
                    .as_deref()
                    .unwrap_or(defaults.dark_surface),
                self.dark_text_color
                    .as_deref()
                    .unwrap_or(defaults.dark_text),
            ),
        ] {
            let background = parse_color(background)?;
            let surface = parse_color(surface)?;
            let text = parse_color(text)?;
            if contrast_ratio(text, background) < MINIMUM_TEXT_CONTRAST
                || contrast_ratio(text, surface) < MINIMUM_TEXT_CONTRAST
            {
                return Err(SetupThemeError::Invalid(
                    "text and background colors do not have enough contrast",
                ));
            }
        }
        Ok(())
    }

    pub fn preset(&self) -> SetupThemePreset {
        self.preset
    }

    pub fn brand_name(&self) -> &str {
        self.brand_name.as_deref().unwrap_or(DEFAULT_BRAND_NAME)
    }

    pub fn subtitle(&self) -> &str {
        self.subtitle.as_deref().unwrap_or(DEFAULT_SUBTITLE)
    }

    pub fn palette(&self, dark: bool) -> SetupThemePalette {
        let defaults = self.preset.default_palette();
        let (background, surface, text) = if dark {
            (
                self.dark_background_color
                    .as_deref()
                    .unwrap_or(defaults.dark_background),
                self.dark_surface_color
                    .as_deref()
                    .unwrap_or(defaults.dark_surface),
                self.dark_text_color
                    .as_deref()
                    .unwrap_or(defaults.dark_text),
            )
        } else {
            (
                self.light_background_color
                    .as_deref()
                    .unwrap_or(defaults.light_background),
                self.light_surface_color
                    .as_deref()
                    .unwrap_or(defaults.light_surface),
                self.light_text_color
                    .as_deref()
                    .unwrap_or(defaults.light_text),
            )
        };

        SetupThemePalette {
            accent: self
                .accent_color
                .as_deref()
                .and_then(|color| parse_color(color).ok())
                .or_else(|| parse_color(defaults.accent).ok())
                .unwrap_or([18, 121, 132]),
            background: parse_color(background).unwrap_or([243, 248, 248]),
            surface: parse_color(surface).unwrap_or([255, 255, 255]),
            text: parse_color(text).unwrap_or([30, 48, 51]),
        }
    }
}

impl Default for SetupThemeConfig {
    fn default() -> Self {
        Self::for_preset(SetupThemePreset::Turkuaz)
    }
}

#[derive(Clone, Copy)]
struct ThemeColorSet {
    accent: &'static str,
    light_background: &'static str,
    light_surface: &'static str,
    light_text: &'static str,
    dark_background: &'static str,
    dark_surface: &'static str,
    dark_text: &'static str,
}

fn validate_label(
    value: Option<&str>,
    maximum_characters: usize,
    field_name: &'static str,
) -> Result<(), SetupThemeError> {
    let Some(value) = value else {
        return Ok(());
    };
    if value.trim().is_empty()
        || value.chars().count() > maximum_characters
        || value.chars().any(char::is_control)
    {
        return Err(SetupThemeError::Invalid(field_name));
    }
    Ok(())
}

fn parse_color(value: &str) -> Result<[u8; 3], SetupThemeError> {
    let digits = value
        .strip_prefix('#')
        .filter(|digits| digits.len() == 6 && digits.bytes().all(|byte| byte.is_ascii_hexdigit()))
        .ok_or(SetupThemeError::Invalid("colors must use #RRGGBB notation"))?;
    let parse_channel = |start| {
        u8::from_str_radix(&digits[start..start + 2], 16)
            .map_err(|_| SetupThemeError::Invalid("color channel is invalid"))
    };
    Ok([parse_channel(0)?, parse_channel(2)?, parse_channel(4)?])
}

fn contrast_ratio(first: [u8; 3], second: [u8; 3]) -> f64 {
    let first_luminance = luminance(first);
    let second_luminance = luminance(second);
    let lighter = first_luminance.max(second_luminance);
    let darker = first_luminance.min(second_luminance);
    (lighter + 0.05) / (darker + 0.05)
}

fn luminance(color: [u8; 3]) -> f64 {
    let channel = |value: u8| {
        let value = f64::from(value) / 255.0;
        if value <= 0.04045 {
            value / 12.92
        } else {
            ((value + 0.055) / 1.055).powf(2.4)
        }
    };
    0.2126 * channel(color[0]) + 0.7152 * channel(color[1]) + 0.0722 * channel(color[2])
}
