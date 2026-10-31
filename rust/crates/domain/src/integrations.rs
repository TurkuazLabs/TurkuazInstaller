// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/integrations.rs
// Amac: Signed manifest shortcut ve URL protocol eylemlerini guvenli typed domain modelleri olarak temsil etmek
// Modul - Rust Domain
// Version: 0.2.0
// Aciklama: Relative EXE path, Windows isim kurallari, scheme formatlari ve duplicate action kontrollerini uygular
// Bagimli Oldugu Katman: Model | Service | Tool

use std::collections::HashSet;

use crate::DomainError;

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
#[repr(i32)]
pub enum WindowsShortcutLocation {
    Desktop = 0,
    StartMenu = 1,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WindowsShortcutIntegration {
    id: String,
    name: String,
    location: WindowsShortcutLocation,
    executable_relative_path: String,
}

impl WindowsShortcutIntegration {
    pub fn new(
        id: &str,
        name: &str,
        location: WindowsShortcutLocation,
        executable_relative_path: &str,
    ) -> Result<Self, DomainError> {
        let id = normalize_identifier(id, 64)?;
        let name = name.trim();
        if name.is_empty()
            || name.len() > 100
            || matches!(name, "." | "..")
            || name.chars().any(|character| {
                character < ' '
                    || matches!(
                        character,
                        '<' | '>' | ':' | '"' | '/' | '\\' | '|' | '?' | '*'
                    )
            })
        {
            return Err(DomainError::InvalidWindowsIntegration);
        }
        Ok(Self {
            id,
            name: name.to_owned(),
            location,
            executable_relative_path: validate_relative_exe(executable_relative_path)?,
        })
    }

    pub fn id(&self) -> &str {
        &self.id
    }
    pub fn name(&self) -> &str {
        &self.name
    }
    pub const fn location(&self) -> WindowsShortcutLocation {
        self.location
    }
    pub fn executable_relative_path(&self) -> &str {
        &self.executable_relative_path
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WindowsProtocolIntegration {
    scheme: String,
    executable_relative_path: String,
}

impl WindowsProtocolIntegration {
    pub fn new(scheme: &str, executable_relative_path: &str) -> Result<Self, DomainError> {
        let scheme = scheme.trim().to_lowercase();
        if !(2..=32).contains(&scheme.len())
            || !scheme.chars().next().is_some_and(char::is_alphabetic)
            || !scheme.chars().all(|character| {
                character.is_alphanumeric() || matches!(character, '+' | '-' | '.')
            })
        {
            return Err(DomainError::InvalidWindowsIntegration);
        }
        Ok(Self {
            scheme,
            executable_relative_path: validate_relative_exe(executable_relative_path)?,
        })
    }

    pub fn scheme(&self) -> &str {
        &self.scheme
    }
    pub fn executable_relative_path(&self) -> &str {
        &self.executable_relative_path
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct WindowsIntegrationPolicy {
    shortcuts: Vec<WindowsShortcutIntegration>,
    protocols: Vec<WindowsProtocolIntegration>,
}

impl WindowsIntegrationPolicy {
    pub const MAXIMUM_ACTIONS_PER_TYPE: usize = 32;

    pub fn new(
        shortcuts: Vec<WindowsShortcutIntegration>,
        protocols: Vec<WindowsProtocolIntegration>,
    ) -> Result<Self, DomainError> {
        if shortcuts.len() > Self::MAXIMUM_ACTIONS_PER_TYPE
            || protocols.len() > Self::MAXIMUM_ACTIONS_PER_TYPE
        {
            return Err(DomainError::InvalidWindowsIntegration);
        }
        let mut shortcut_ids = HashSet::new();
        if !shortcuts
            .iter()
            .all(|item| shortcut_ids.insert(item.id.to_lowercase()))
        {
            return Err(DomainError::InvalidWindowsIntegration);
        }
        let mut schemes = HashSet::new();
        if !protocols
            .iter()
            .all(|item| schemes.insert(item.scheme.to_lowercase()))
        {
            return Err(DomainError::InvalidWindowsIntegration);
        }
        Ok(Self {
            shortcuts,
            protocols,
        })
    }

    pub fn empty() -> Self {
        Self {
            shortcuts: Vec::new(),
            protocols: Vec::new(),
        }
    }

    pub fn shortcuts(&self) -> &[WindowsShortcutIntegration] {
        &self.shortcuts
    }
    pub fn protocols(&self) -> &[WindowsProtocolIntegration] {
        &self.protocols
    }
    pub fn has_actions(&self) -> bool {
        !self.shortcuts.is_empty() || !self.protocols.is_empty()
    }
}

fn normalize_identifier(value: &str, maximum: usize) -> Result<String, DomainError> {
    let normalized = value.trim().to_lowercase();
    if normalized.is_empty()
        || normalized.len() > maximum
        || !normalized.chars().next().is_some_and(char::is_alphanumeric)
        || !normalized
            .chars()
            .all(|character| character.is_alphanumeric() || matches!(character, '.' | '_' | '-'))
    {
        return Err(DomainError::InvalidWindowsIntegration);
    }
    Ok(normalized)
}

fn validate_relative_exe(value: &str) -> Result<String, DomainError> {
    let normalized = value.trim().replace('\\', "/");
    let segments: Vec<_> = normalized
        .split('/')
        .filter(|segment| !segment.is_empty())
        .collect();
    if normalized.starts_with('/')
        || normalized.contains(':')
        || segments.is_empty()
        || !normalized.to_ascii_lowercase().ends_with(".exe")
        || segments
            .iter()
            .any(|segment| matches!(*segment, "." | ".."))
    {
        return Err(DomainError::InvalidWindowsIntegration);
    }
    Ok(normalized)
}
