// Dosya Yolu: REPO_ROOT/rust/crates/domain/src/semantic_version.rs
// Amac: SemVer 2.0 surumlerini parse edip precedence kurallarina gore karsilastirmak
// Modul - Rust Domain
// Version: 0.1.0
// Aciklama: Update kararlari icin System.Version yerine contract uyumlu semantic version kullanir
// Bagimli Oldugu Katman: Model

use crate::DomainError;
use std::cmp::Ordering;

#[derive(Debug, Clone, PartialEq, Eq, Hash)]
pub struct SemanticVersion {
    major: i32,
    minor: i32,
    patch: i32,
    pre_release: Option<String>,
    build_metadata: Option<String>,
}

impl SemanticVersion {
    pub fn parse(value: &str) -> Result<Self, DomainError> {
        let value = value.trim();
        if value.is_empty() {
            return Err(DomainError::InvalidSemanticVersion);
        }

        let (without_build, build_metadata) = match value.split_once('+') {
            Some((version, build)) if !build.contains('+') => {
                (version, Some(validate_identifiers(build, false)?))
            }
            Some(_) => return Err(DomainError::InvalidSemanticVersion),
            None => (value, None),
        };

        let (core, pre_release) = match without_build.split_once('-') {
            Some((core, pre)) => (core, Some(validate_identifiers(pre, true)?)),
            None => (without_build, None),
        };

        let mut components = core.split('.');
        let major = parse_numeric_component(components.next())?;
        let minor = parse_numeric_component(components.next())?;
        let patch = parse_numeric_component(components.next())?;
        if components.next().is_some() {
            return Err(DomainError::InvalidSemanticVersion);
        }

        Ok(Self {
            major,
            minor,
            patch,
            pre_release,
            build_metadata,
        })
    }

    pub fn major(&self) -> i32 {
        self.major
    }

    pub fn minor(&self) -> i32 {
        self.minor
    }

    pub fn patch(&self) -> i32 {
        self.patch
    }

    pub fn pre_release(&self) -> Option<&str> {
        self.pre_release.as_deref()
    }

    pub fn build_metadata(&self) -> Option<&str> {
        self.build_metadata.as_deref()
    }

    /// SemVer precedence ignores build metadata, as required by SemVer 2.0.
    pub fn compare_precedence(&self, other: &Self) -> Ordering {
        self.major
            .cmp(&other.major)
            .then_with(|| self.minor.cmp(&other.minor))
            .then_with(|| self.patch.cmp(&other.patch))
            .then_with(|| {
                compare_pre_release(self.pre_release.as_deref(), other.pre_release.as_deref())
            })
    }
}

impl std::fmt::Display for SemanticVersion {
    fn fmt(&self, formatter: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(formatter, "{}.{}.{}", self.major, self.minor, self.patch)?;
        if let Some(pre_release) = &self.pre_release {
            write!(formatter, "-{pre_release}")?;
        }
        if let Some(build_metadata) = &self.build_metadata {
            write!(formatter, "+{build_metadata}")?;
        }
        Ok(())
    }
}

fn parse_numeric_component(value: Option<&str>) -> Result<i32, DomainError> {
    let value = value.ok_or(DomainError::InvalidSemanticVersion)?;
    if value.is_empty()
        || (value.len() > 1 && value.starts_with('0'))
        || !value.bytes().all(|byte| byte.is_ascii_digit())
    {
        return Err(DomainError::InvalidSemanticVersion);
    }
    value
        .parse()
        .map_err(|_| DomainError::InvalidSemanticVersion)
}

fn validate_identifiers(
    value: &str,
    enforce_numeric_leading_zero: bool,
) -> Result<String, DomainError> {
    if value.is_empty() {
        return Err(DomainError::InvalidSemanticVersion);
    }

    let valid = value.split('.').all(|identifier| {
        !identifier.is_empty()
            && identifier
                .bytes()
                .all(|byte| byte.is_ascii_alphanumeric() || byte == b'-')
            && (!enforce_numeric_leading_zero
                || !identifier.bytes().all(|byte| byte.is_ascii_digit())
                || identifier == "0"
                || !identifier.starts_with('0'))
    });

    if valid {
        Ok(value.to_owned())
    } else {
        Err(DomainError::InvalidSemanticVersion)
    }
}

fn compare_pre_release(left: Option<&str>, right: Option<&str>) -> Ordering {
    match (left, right) {
        (None, None) => Ordering::Equal,
        (None, Some(_)) => Ordering::Greater,
        (Some(_), None) => Ordering::Less,
        (Some(left), Some(right)) => {
            let mut left_parts = left.split('.');
            let mut right_parts = right.split('.');
            loop {
                match (left_parts.next(), right_parts.next()) {
                    (None, None) => return Ordering::Equal,
                    (None, Some(_)) => return Ordering::Less,
                    (Some(_), None) => return Ordering::Greater,
                    (Some(left), Some(right)) => {
                        let ordering = compare_identifier(left, right);
                        if ordering != Ordering::Equal {
                            return ordering;
                        }
                    }
                }
            }
        }
    }
}

fn compare_identifier(left: &str, right: &str) -> Ordering {
    let left_numeric = left.bytes().all(|byte| byte.is_ascii_digit());
    let right_numeric = right.bytes().all(|byte| byte.is_ascii_digit());
    match (left_numeric, right_numeric) {
        (true, true) => left.len().cmp(&right.len()).then_with(|| left.cmp(right)),
        (true, false) => Ordering::Less,
        (false, true) => Ordering::Greater,
        (false, false) => left.cmp(right),
    }
}
