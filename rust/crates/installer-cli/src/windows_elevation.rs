// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_elevation.rs
// Amac: Tum kullanicilar kurulumunu Windows UAC ile yeniden baslatmak
// Modul - Rust Windows Elevation Adapter
// Version: 0.1.2
// Aciklama: runas, komut satiri kacis kodlamasi, onayli yeniden acilis
// Bagimli Oldugu Katman: Adapter | Security

use std::{
    ffi::OsStr,
    os::windows::ffi::OsStrExt,
    path::{Component, Path},
};

use windows::{
    Win32::UI::{Shell::ShellExecuteW, WindowsAndMessaging::SW_SHOWNORMAL},
    core::PCWSTR,
};

use crate::wizard_language::WizardLanguage;

fn wide(value: &OsStr) -> Vec<u16> {
    value.encode_wide().chain(std::iter::once(0)).collect()
}

/// Quote exactly one Windows command-line argument following CommandLineToArgvW
/// escaping rules, never concatenate an untrusted path as command syntax.
fn quote_arg(value: &str) -> Result<String, String> {
    if value.chars().any(char::is_control) {
        return Err("UAC komut satırında kontrol karakterleri geçersiz.".into());
    }
    let mut output = String::from("\"");
    let mut backslashes = 0;
    for character in value.chars() {
        match character {
            '\\' => backslashes += 1,
            '"' => {
                output.push_str(&"\\".repeat(backslashes * 2 + 1));
                output.push('"');
                backslashes = 0;
            }
            _ => {
                output.push_str(&"\\".repeat(backslashes));
                backslashes = 0;
                output.push(character);
            }
        }
    }
    output.push_str(&"\\".repeat(backslashes * 2));
    output.push('"');
    Ok(output)
}

fn resumed_arguments(
    package_id: &str,
    target: &Path,
    start_menu: bool,
    desktop: bool,
    language: WizardLanguage,
) -> Result<String, String> {
    let target = target
        .to_str()
        .ok_or("UAC için hedef klasör yolu Unicode olmalıdır.")?;
    let language = match language {
        WizardLanguage::Turkish => "tr",
        WizardLanguage::English => "en",
    };
    Ok(format!(
        "resume-local-wizard --package-id {} --target {} --start-menu {} --desktop {} --language {}",
        quote_arg(package_id)?,
        quote_arg(target)?,
        if start_menu { "1" } else { "0" },
        if desktop { "1" } else { "0" },
        language
    ))
}

/// @dev-id BRK-UAC-001
/// @purpose Ask the operating system for user-approved elevation; never forge
/// an elevated status, never launch arbitrary commands or silent installation.
/// @contract The new elevated instance re-verifies package identity and shows
/// its Review page, requiring a second explicit Install action.
pub(super) fn request_elevated_setup(
    executable: &Path,
    package_id: &str,
    target: &Path,
    start_menu: bool,
    desktop: bool,
    language: WizardLanguage,
) -> Result<(), String> {
    let args = resumed_arguments(package_id, target, start_menu, desktop, language)?;
    let exe = wide(executable.as_os_str());
    let params = wide(OsStr::new(&args));
    let verb = wide(OsStr::new("runas"));
    let result = unsafe {
        ShellExecuteW(
            None,
            PCWSTR(verb.as_ptr()),
            PCWSTR(exe.as_ptr()),
            PCWSTR(params.as_ptr()),
            PCWSTR::null(),
            SW_SHOWNORMAL,
        )
    };
    if (result.0 as isize) <= 32 {
        return Err(
            "Windows yönetici izni verilmedi veya işlem başlatılamadı. Kurulum yapılmadı."
                .to_string(),
        );
    }
    Ok(())
}

/// The user has already chosen Uninstall in the Windows Apps entry.
/// Reopen the same uninstaller with UAC consent. It still presents its own
/// confirmation screen before touching installed files.
pub(super) fn request_elevated_uninstall(
    executable: &Path,
    registration: &Path,
) -> Result<(), String> {
    let registration = registration
        .to_str()
        .ok_or("Kaldırma envanteri yolu Unicode olmalıdır.")?;
    let params = format!(
        "uninstall-local-gui --registration {}",
        quote_arg(registration)?
    );
    let exe_w = wide(executable.as_os_str());
    let params_w = wide(OsStr::new(&params));
    let verb_w = wide(OsStr::new("runas"));
    let handle = unsafe {
        ShellExecuteW(
            None,
            PCWSTR(verb_w.as_ptr()),
            PCWSTR(exe_w.as_ptr()),
            PCWSTR(params_w.as_ptr()),
            PCWSTR::null(),
            SW_SHOWNORMAL,
        )
    };
    if (handle.0 as isize) <= 32 {
        return Err("Yönetici izni reddedildi. Hiçbir dosya kaldırılmadı.".into());
    }
    Ok(())
}

#[derive(Debug, PartialEq, Eq)]
pub(super) struct ResumeOptions {
    pub(super) package_id: String,
    pub(super) target: String,
    pub(super) start_menu: bool,
    pub(super) desktop: bool,
    pub(super) language: WizardLanguage,
}

pub(super) fn parse_resumed_arguments() -> Result<ResumeOptions, String> {
    parse_resumed_values(&std::env::args().collect::<Vec<_>>())
}

/// @dev-id BRK-UAC-RESUME-PARSER-001
/// @purpose Reject malformed/extra UAC resume arguments before reading the
/// embedded package and starting the privileged review window.
/// @contract No implicit consent flags, path traversal or downgrade from
/// AllUsers; the elevated wizard must re-ask EULA and recycle consent.
fn parse_resumed_values(values: &[String]) -> Result<ResumeOptions, String> {
    // No optional unknown switches, no implicit defaults for privileged state.
    if values.len() != 12 || values[1] != "resume-local-wizard" {
        return Err("UAC yeniden başlatma parametreleri geçersiz.".into());
    }
    let expected = [
        "--package-id",
        "--target",
        "--start-menu",
        "--desktop",
        "--language",
    ];
    // 5 named flags and values -> executable + command + 10 = 12.
    for (index, flag) in expected.iter().enumerate() {
        if values[2 + index * 2] != *flag {
            return Err("UAC parametre sırası geçersiz.".into());
        }
    }
    fn yes_no(value: &str) -> Result<bool, String> {
        match value {
            "1" => Ok(true),
            "0" => Ok(false),
            _ => Err("Geçersiz UAC tercih değeri.".into()),
        }
    }
    let language = match values[11].as_str() {
        "tr" => WizardLanguage::Turkish,
        "en" => WizardLanguage::English,
        _ => return Err("Geçersiz UAC dili.".into()),
    };
    let target = Path::new(&values[5]);
    if values[3].trim().is_empty()
        || !target.is_absolute()
        || target
            .components()
            .any(|part| matches!(part, Component::CurDir | Component::ParentDir))
        || target.parent().is_none()
    {
        return Err("UAC paket kimliği veya hedef klasör yolu geçersiz.".into());
    }
    Ok(ResumeOptions {
        package_id: values[3].clone(),
        target: values[5].clone(),
        start_menu: yes_no(&values[7])?,
        desktop: yes_no(&values[9])?,
        language,
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn quoted_windows_arguments_never_break_out_of_one_value() {
        assert_eq!(
            quote_arg(r#"C:\Program Files\Türkçe"#).unwrap(),
            r#""C:\Program Files\Türkçe""#
        );
        assert_eq!(quote_arg("a\"b").unwrap(), r#""a\"b""#);
        assert_eq!(quote_arg("ends\\").unwrap(), r#""ends\\""#);
        assert!(quote_arg("evil\0arg").is_err());
    }

    /// @dev-id BRK-UAC-RESUME-TEST-001
    /// @purpose Verify that UAC resume never accepts inherited consent,
    /// unrecognized flags, relative/traversal targets or invalid booleans.
    #[test]
    fn privileged_resume_rejects_extra_consent_flags_and_unsafe_targets() {
        let base = [
            "setup.exe",
            "resume-local-wizard",
            "--package-id",
            "qa.uac",
            "--target",
            r"C:\Program Files\qa.uac",
            "--start-menu",
            "1",
            "--desktop",
            "0",
            "--language",
            "tr",
        ];
        let make = || base.iter().map(|arg| (*arg).to_owned()).collect::<Vec<_>>();
        let valid = make();
        let options = parse_resumed_values(&valid).expect("valid UAC resume");
        assert_eq!(options.package_id, "qa.uac");
        assert_eq!(options.target, r"C:\Program Files\qa.uac");
        assert!(options.start_menu);
        assert!(!options.desktop);
        assert_eq!(options.language, WizardLanguage::Turkish);

        for flag in ["--recycle-approved", "--accept-license", "--quiet", "--scope"] {
            let mut args = make();
            args.push(flag.to_owned());
            args.push("1".to_owned());
            assert!(parse_resumed_values(&args).is_err(), "accepted {flag}");
        }
        for invalid in ["0", "yes", "2", ""] {
            let mut args = make();
            args[7] = invalid.into();
            if invalid == "0" {
                assert!(!parse_resumed_values(&args).expect("explicit false").start_menu);
            } else {
                assert!(parse_resumed_values(&args).is_err(), "accepted {invalid}");
            }
        }
        for invalid in ["relative\\qa.uac", r"C:\Program Files\..\qa.uac", r"C:qa.uac"] {
            let mut args = make();
            args[5] = invalid.into();
            assert!(parse_resumed_values(&args).is_err(), "accepted {invalid}");
        }
        let mut swapped = make();
        swapped[2] = "--target".into();
        assert!(parse_resumed_values(&swapped).is_err());
        let mut invalid_language = make();
        invalid_language[11] = "de".into();
        assert!(parse_resumed_values(&invalid_language).is_err());
    }

    #[test]
    fn uac_command_argument_encoder_rejects_controls_and_keeps_unicode() {
        for invalid in ["bad\rvalue", "bad\nvalue", "bad\0value", "bad\tvalue"] {
            assert!(quote_arg(invalid).is_err());
        }
        assert_eq!(
            quote_arg(r"C:\Program Files\İş Uygulaması\").unwrap(),
            r#""C:\Program Files\İş Uygulaması\\""#
        );
    }

    #[test]
    fn elevation_parameters_are_explicit_and_preserve_scope_choices() {
        let args = resumed_arguments(
            "ornek.uygulamam",
            Path::new(r"C:\Program Files\örnek"),
            true,
            false,
            WizardLanguage::English,
        )
        .unwrap();
        assert!(args.starts_with("resume-local-wizard --package-id "));
        assert!(args.contains(r#"--target "C:\Program Files\örnek""#));
        assert!(args.contains("--start-menu 1 --desktop 0 --language en"));
    }
}
