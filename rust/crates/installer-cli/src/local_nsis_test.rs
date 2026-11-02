// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/local_nsis_test.rs
// Amac: Community Studio icin imzasiz yerel NSIS test EXE olusturmak
// Modul - Rust Packaging Tool
// Version: 0.1.0
// Aciklama: Kaynak dosya taramasi, NSIS script guvenligi ve test-only cikti
// Bagimli Oldugu Katman: Tool | QA

use std::{
    fs, io,
    path::{Path, PathBuf},
    process::Command,
    time::{SystemTime, UNIX_EPOCH},
};

use super::{
    TemporaryDirectoryGuard, is_windows_reparse_point, regular_file_exists, require_regular_file,
    run_child, validate_or_create_output_parent, write_new_file,
};

fn validate_define(value: &str) -> Result<(), String> {
    if value.is_empty()
        || value
            .chars()
            .any(|ch| ch.is_control() || matches!(ch, '"' | '
    {
        return Err("NSIS yolu veya ürün adı geçersiz özel karakter içeriyor.".to_owned());
    }
    Ok(())
}

fn validate_source(source: &Path, output_root: &Path) -> Result<(), String> {
    validate_or_create_output_parent(source, false)?;
    let source =
        fs::canonicalize(source).map_err(|error| format!("Kaynak klasörü okunamadı: {error}"))?;
    let output_root = fs::canonicalize(output_root)
        .map_err(|error| format!("Paket klasörü okunamadı: {error}"))?;
    if source.starts_with(&output_root) || output_root.starts_with(&source) {
        return Err("Kaynak ve çıktı klasörleri iç içe olamaz.".to_owned());
    }
    let mut dirs = vec![source];
    let mut count = 0usize;
    while let Some(dir) = dirs.pop() {
        for entry in
            fs::read_dir(&dir).map_err(|error| format!("Kaynak klasörü listelenemedi: {error}"))?
        {
            let entry = entry.map_err(|error| format!("Kaynak dosya okunamadı: {error}"))?;
            count += 1;
            if count > 100_000 {
                return Err("Kaynak dosya sayısı sınırı aşıldı.".to_owned());
            }
            let metadata = fs::symlink_metadata(entry.path())
                .map_err(|error| format!("Kaynak dosyası doğrulanamadı: {error}"))?;
            if metadata.file_type().is_symlink() || is_windows_reparse_point(&metadata) {
                return Err("NSIS test kaynağında symlink/junction reddedildi.".to_owned());
            }
            if metadata.is_dir() {
                dirs.push(entry.path());
            } else if !metadata.is_file() {
                return Err("NSIS test kaynağında normal olmayan dosya var.".to_owned());
            }
        }
    }
    if count == 0 {
        return Err("NSIS test kaynağı boş olamaz.".to_owned());
    }
    Ok(())
}

fn makensis_path(configured: &str) -> Result<PathBuf, String> {
    if !configured.trim().is_empty() {
        return require_regular_file("NSIS derleyici", configured);
    }
    for variable in ["ProgramFiles(x86)", "ProgramFiles"] {
        if let Some(root) = std::env::var_os(variable) {
            let candidate = PathBuf::from(root).join("NSIS").join("makensis.exe");
            if regular_file_exists(&candidate) {
                return Ok(candidate);
            }
        }
    }
    Ok(PathBuf::from("makensis"))
}

pub(super) fn build(
    source: &Path,
    package_root: &Path,
    configured_nsis: &str,
    product_name: &str,
    package_id: &str,
) -> Result<String, String> {
    validate_source(source, package_root)?;
    // Name is shown by NSIS only. It cannot be a script expression.
    let product = super::safe_project_folder_name(product_name);
    let destination = package_root.join("local-test");
    match fs::symlink_metadata(&destination) {
        Ok(_) => return Err("Yerel test klasörü zaten var; üzerine yazılmaz.".to_owned()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(format!("Yerel test klasörü denetlenemedi: {error}")),
    }
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| format!("Sistem saati okunamadı: {error}"))?
        .as_nanos();
    let staging = package_root.join(format!(".nsis-local-test-{}-{nonce}", std::process::id()));
    fs::create_dir(&staging).map_err(|error| format!("NSIS test klasörü açılamadı: {error}"))?;
    let _guard = TemporaryDirectoryGuard(staging.clone());
    let script = staging.join("local-test.nsi");
    write_new_file(
        &script,
        include_str!("../../../../packaging/TurkuazLocalTest.nsi").as_bytes(),
    )?;
    let executable = staging.join("LOCAL-TEST-ONLY.exe");
    for value in [
        source.to_string_lossy().as_ref(),
        executable.to_string_lossy().as_ref(),
        &product,
        package_id,
    ] {
        validate_define(value)?;
    }
    let mut command = Command::new(makensis_path(configured_nsis)?);
    command
        .arg(format!("/DSOURCE_DIR={}", source.display()))
        .arg(format!("/DOUTPUT_PATH={}", executable.display()))
        .arg(format!("/DAPP_NAME={product}"))
        .arg(format!("/DPACKAGE_ID={package_id}"))
        .arg(&script);
    run_child(command, "İmzasız NSIS yerel test derlemesi")
        .map_err(|error| format!("{error}. NSIS kurulu ve erişilebilir olmalıdır."))?;
    if !regular_file_exists(&executable) {
        return Err("NSIS yerel test EXE'si oluşturulamadı.".to_owned());
    }
    write_new_file(
        &staging.join("LOCAL-TEST-NOTICE.txt"),
        b"LOCAL TEST ONLY - UNSIGNED NSIS DEMO. NOT A PRODUCTION INSTALLER.\nNo signed manifest verification or rollback.\nDefault target: per-user LocalAppData/TurkuazInstaller/LocalTests.\n",
    )?;
    fs::remove_file(&script).map_err(|error| format!("Geçici NSIS scripti silinemedi: {error}"))?;
    fs::rename(&staging, &destination)
        .map_err(|error| format!("Test çıktısı taşınamadı: {error}"))?;
    Ok(format!(
        "İmzasız yerel TEST EXE hazır: {}. Dağıtım için kullanmayın.",
        destination.join("LOCAL-TEST-ONLY.exe").display(),
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_or_output_cannot_overlap() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let dir = std::env::temp_dir().join(format!(
            "turkuaz-local-nsis-test-{}-{nonce}",
            std::process::id()
        ));
        let source = dir.join("source");
        let output = source.join("output");
        fs::create_dir_all(&output).expect("create test fixture");
        fs::write(source.join("game.exe"), b"fixture").expect("create source fixture");
        assert!(validate_source(&source, &output).is_err());
        fs::remove_dir_all(dir).expect("remove fixture");
    }

    #[test]
    fn macro_values_reject_script_expression_characters() {
        for value in ["", "bad!name", "dollar$sign", "line\nline", "bad\"quote"] {
            assert!(validate_define(value).is_err());
        }
        assert!(validate_define("Oyun Türkçe").is_ok());
    }
}
 | '!' | '`'))
    {
        return Err("NSIS yolu veya ürün adı geçersiz özel karakter içeriyor.".to_owned());
    }
    Ok(())
}

fn validate_source(source: &Path, output_root: &Path) -> Result<(), String> {
    validate_or_create_output_parent(source, false)?;
    let source = fs::canonicalize(source)
        .map_err(|error| format!("Kaynak klasörü okunamadı: {error}"))?;
    let output_root = fs::canonicalize(output_root)
        .map_err(|error| format!("Paket klasörü okunamadı: {error}"))?;
    if source.starts_with(&output_root) || output_root.starts_with(&source) {
        return Err("Kaynak ve çıktı klasörleri iç içe olamaz.".to_owned());
    }
    let mut dirs = vec![source];
    let mut count = 0usize;
    while let Some(dir) = dirs.pop() {
        for entry in fs::read_dir(&dir)
            .map_err(|error| format!("Kaynak klasörü listelenemedi: {error}"))?
        {
            let entry = entry.map_err(|error| format!("Kaynak dosya okunamadı: {error}"))?;
            count += 1;
            if count > 100_000 {
                return Err("Kaynak dosya sayısı sınırı aşıldı.".to_owned());
            }
            let metadata = fs::symlink_metadata(entry.path())
                .map_err(|error| format!("Kaynak dosyası doğrulanamadı: {error}"))?;
            if metadata.file_type().is_symlink() || is_windows_reparse_point(&metadata) {
                return Err("NSIS test kaynağında symlink/junction reddedildi.".to_owned());
            }
            if metadata.is_dir() {
                dirs.push(entry.path());
            } else if !metadata.is_file() {
                return Err("NSIS test kaynağında normal olmayan dosya var.".to_owned());
            }
        }
    }
    if count == 0 {
        return Err("NSIS test kaynağı boş olamaz.".to_owned());
    }
    Ok(())
}

fn makensis_path(configured: &str) -> Result<PathBuf, String> {
    if !configured.trim().is_empty() {
        return require_regular_file("NSIS derleyici", configured);
    }
    for variable in ["ProgramFiles(x86)", "ProgramFiles"] {
        if let Some(root) = std::env::var_os(variable) {
            let candidate = PathBuf::from(root).join("NSIS").join("makensis.exe");
            if regular_file_exists(&candidate) {
                return Ok(candidate);
            }
        }
    }
    Ok(PathBuf::from("makensis"))
}

pub(super) fn build(
    source: &Path,
    package_root: &Path,
    configured_nsis: &str,
    product_name: &str,
    package_id: &str,
) -> Result<String, String> {
    validate_source(source, package_root)?;
    // Name is shown by NSIS only. It cannot be a script expression.
    let product = super::safe_project_folder_name(product_name);
    let destination = package_root.join("local-test");
    match fs::symlink_metadata(&destination) {
        Ok(_) => return Err("Yerel test klasörü zaten var; üzerine yazılmaz.".to_owned()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(format!("Yerel test klasörü denetlenemedi: {error}")),
    }
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| format!("Sistem saati okunamadı: {error}"))?
        .as_nanos();
    let staging = package_root.join(format!(
        ".nsis-local-test-{}-{nonce}",
        std::process::id()
    ));
    fs::create_dir(&staging)
        .map_err(|error| format!("NSIS test klasörü açılamadı: {error}"))?;
    let _guard = TemporaryDirectoryGuard(staging.clone());
    let script = staging.join("local-test.nsi");
    write_new_file(
        &script,
        include_str!("../../../../packaging/TurkuazLocalTest.nsi").as_bytes(),
    )?;
    let executable = staging.join("LOCAL-TEST-ONLY.exe");
    for value in [
        source.to_string_lossy().as_ref(),
        executable.to_string_lossy().as_ref(),
        &product,
        package_id,
    ] {
        validate_define(value)?;
    }
    let mut command = Command::new(makensis_path(configured_nsis)?);
    command
        .arg(format!("/DSOURCE_DIR={}", source.display()))
        .arg(format!("/DOUTPUT_PATH={}", executable.display()))
        .arg(format!("/DAPP_NAME={product}"))
        .arg(format!("/DPACKAGE_ID={package_id}"))
        .arg(&script);
    run_child(command, "İmzasız NSIS yerel test derlemesi")
        .map_err(|error| format!("{error}. NSIS kurulu ve erişilebilir olmalıdır."))?;
    if !regular_file_exists(&executable) {
        return Err("NSIS yerel test EXE'si oluşturulamadı.".to_owned());
    }
    write_new_file(
        &staging.join("LOCAL-TEST-NOTICE.txt"),
        b"LOCAL TEST ONLY - UNSIGNED NSIS DEMO. NOT A PRODUCTION INSTALLER.\nNo signed manifest verification or rollback.\nDefault target: per-user LocalAppData/TurkuazInstaller/LocalTests.\n",
    )?;
    fs::remove_file(&script)
        .map_err(|error| format!("Geçici NSIS scripti silinemedi: {error}"))?;
    fs::rename(&staging, &destination)
        .map_err(|error| format!("Test çıktısı taşınamadı: {error}"))?;
    Ok(format!(
        "İmzasız yerel TEST EXE hazır: {}. Dağıtım için kullanmayın.",
        destination.join("LOCAL-TEST-ONLY.exe").display(),
    ))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn source_or_output_cannot_overlap() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let dir = std::env::temp_dir().join(format!(
            "turkuaz-local-nsis-test-{}-{nonce}",
            std::process::id()
        ));
        let source = dir.join("source");
        let output = source.join("output");
        fs::create_dir_all(&output).expect("create test fixture");
        fs::write(source.join("game.exe"), b"fixture").expect("create source fixture");
        assert!(validate_source(&source, &output).is_err());
        fs::remove_dir_all(dir).expect("remove fixture");
    }

    #[test]
    fn macro_values_reject_script_expression_characters() {
        for value in ["", "bad!name", "dollar$sign", "line\nline", "bad\"quote"] {
            assert!(validate_define(value).is_err());
        }
        assert!(validate_define("Oyun Türkçe").is_ok());
    }
}
