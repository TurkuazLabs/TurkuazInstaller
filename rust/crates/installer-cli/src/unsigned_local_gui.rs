// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/unsigned_local_gui.rs
// Amac: Rust yerel standart kurulumunu cok asamali Windows sihirbazi ile sunmak
// Modul - Rust GUI
// Version: 0.5.3
// Aciklama: Kurulum adimlari ve alt navigasyon paneli geometri guvenligi
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    fs, io,
    path::{Component, Path, PathBuf},
    sync::mpsc::{self, Receiver, TryRecvError},
    thread,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_package_engine::{
    LocalInstallPhase, PackageEngineError, WizardConfig, WizardPageId,
    install_unsigned_local_setup_with_progress, install_unsigned_local_setup_with_replacement,
    read_unsigned_local_wizard_config, unsigned_local_identity,
};

use crate::{
    windows_elevation::{ResumeOptions, parse_resumed_arguments, request_elevated_setup},
    windows_install_scope::{
        InstallScope, current_process_is_elevated, default_install_directory, known_folder,
        validate_install_scope,
    },
    windows_local_registration::register_local_install,
    windows_recycle::validate_recycle_target,
    windows_shortcuts::create_installed_shortcuts,
    wizard_language::WizardLanguage,
};
use windows::Win32::UI::Shell::FOLDERID_ProgramFiles;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum WizardStep {
    Welcome,
    License,
    Destination,
    Shortcuts,
    Review,
    Installing,
    Finished,
}

impl WizardStep {
    fn from_page(page: WizardPageId) -> Self {
        match page {
            WizardPageId::Welcome => Self::Welcome,
            WizardPageId::License => Self::License,
            WizardPageId::Destination => Self::Destination,
            WizardPageId::Shortcuts => Self::Shortcuts,
            WizardPageId::Review => Self::Review,
            WizardPageId::Installing => Self::Installing,
            WizardPageId::Finished => Self::Finished,
        }
    }

    fn page_id(self) -> WizardPageId {
        match self {
            Self::Welcome => WizardPageId::Welcome,
            Self::License => WizardPageId::License,
            Self::Destination => WizardPageId::Destination,
            Self::Shortcuts => WizardPageId::Shortcuts,
            Self::Review => WizardPageId::Review,
            Self::Installing => WizardPageId::Installing,
            Self::Finished => WizardPageId::Finished,
        }
    }

    fn next(self, has_shortcuts: bool) -> Option<Self> {
        match self {
            Self::Welcome => Some(Self::Destination),
            Self::License => Some(Self::Destination),
            Self::Destination if has_shortcuts => Some(Self::Shortcuts),
            Self::Destination => Some(Self::Review),
            Self::Shortcuts => Some(Self::Review),
            Self::Review | Self::Installing | Self::Finished => None,
        }
    }

    fn previous(self, has_shortcuts: bool) -> Option<Self> {
        match self {
            Self::Destination => Some(Self::Welcome),
            Self::License => Some(Self::Welcome),
            Self::Shortcuts => Some(Self::Destination),
            Self::Review if has_shortcuts => Some(Self::Shortcuts),
            Self::Review => Some(Self::Destination),
            Self::Welcome | Self::Installing | Self::Finished => None,
        }
    }

    fn heading(self, language: WizardLanguage) -> &'static str {
        let (tr, en) = match self {
            Self::Welcome => ("Hoş geldiniz", "Welcome"),
            Self::License => ("Lisans sözleşmesi", "License agreement"),
            Self::Destination => ("Kurulum konumu", "Installation folder"),
            Self::Shortcuts => ("Ek seçenekler", "Additional tasks"),
            Self::Review => ("Kuruluma hazır", "Ready to install"),
            Self::Installing => ("Uygulama kuruluyor", "Installing"),
            Self::Finished => ("Kurulum sonucu", "Installation result"),
        };
        language.text(tr, en)
    }

    fn decision_index(self, has_shortcuts: bool) -> Option<(usize, usize)> {
        let count = if has_shortcuts { 4 } else { 3 };
        match self {
            Self::Welcome => Some((1, count)),
            Self::License => None,
            Self::Destination => Some((2, count)),
            Self::Shortcuts if has_shortcuts => Some((3, count)),
            Self::Review => Some((count, count)),
            Self::Shortcuts | Self::Installing | Self::Finished => None,
        }
    }
}

/// @dev-id BRK-LOCAL-WIZARD-001
/// @purpose Let the wizard review existing folders without modifying them.
/// @contract An occupied target is allowed only when the Windows recycle
/// adapter verifies the exact application-owned leaf and safe ancestors.
fn inspect_destination(value: &str, package_id: &str) -> Result<(PathBuf, bool), String> {
    let path = PathBuf::from(value.trim());
    if path.as_os_str().is_empty()
        || !path.is_absolute()
        || path
            .components()
            .any(|part| matches!(part, Component::CurDir | Component::ParentDir))
        || path.parent().is_none()
    {
        return Err("Tam bir Windows klasör yolu girin; '.' ve '..' kullanmayın.".into());
    }
    match fs::symlink_metadata(&path) {
        Ok(_) => {
            validate_recycle_target(&path, package_id)?;
            Ok((path, true))
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok((path, false)),
        Err(error) => Err(format!("Hedef klasör doğrulanamadı: {error}")),
    }
}

/// Work events are delivered by the real package engine, not a fabricated timer.
enum WorkerEvent {
    Phase(LocalInstallPhase),
    CreatingShortcuts,
    RecyclingExistingTarget,
    RegisteringUninstaller,
    Finished(Result<(String, bool), String>),
}

fn phase_label(phase: LocalInstallPhase, language: WizardLanguage) -> &'static str {
    let (tr, en) = match phase {
        LocalInstallPhase::VerifyingPackage => {
            ("Gömülü paket doğrulanıyor…", "Verifying embedded package…")
        }
        LocalInstallPhase::ExtractingPayload => (
            "Uygulama dosyaları çıkartılıyor ve dosya envanteri hazırlanıyor…",
            "Extracting application files and recording the file inventory…",
        ),
        LocalInstallPhase::PublishingInstall => (
            "Doğrulanan dosyalar kurulum klasörüne aktarılıyor…",
            "Publishing verified files into the installation folder…",
        ),
    };
    language.text(tr, en)
}

struct LocalInstaller {
    executable: PathBuf,
    package_id: String,
    package_label: String,
    target: String,
    program_files_root: PathBuf,
    current_user_root: PathBuf,
    scope: InstallScope,
    language: WizardLanguage,
    wizard: WizardConfig,
    license_accepted: bool,
    recycle_approved: bool,
    entry_point: Option<String>,
    start_menu: bool,
    desktop: bool,
    step: WizardStep,
    path_error: Option<String>,
    busy: Option<Receiver<WorkerEvent>>,
    progress_label: &'static str,
    status: Option<Result<(String, bool), String>>,
}

impl LocalInstaller {
    fn new(executable: PathBuf) -> Result<Self, String> {
        let identity =
            unsigned_local_identity(&executable).map_err(|e| format!("Paket okunamadı: {e}"))?;
        let wizard = read_unsigned_local_wizard_config(&executable)
            .map_err(|e| format!("Kurulum sayfaları doğrulanamadı: {e}"))?;
        let current_user_path =
            default_install_directory(InstallScope::CurrentUser, &identity.package_id)?;
        let current_user_root = current_user_path
            .parent()
            .ok_or("Windows kullanıcı program klasörü geçersiz.")?
            .to_path_buf();
        let target = current_user_path.to_string_lossy().into_owned();
        let program_files_root = known_folder(&FOLDERID_ProgramFiles)?;

        let entry_point = identity.entry_point.filter(|entry| {
            let path = Path::new(entry);
            path.components()
                .all(|part| matches!(part, Component::Normal(_)))
                && path
                    .extension()
                    .is_some_and(|ext| ext.eq_ignore_ascii_case("exe"))
        });
        let supports_shortcuts = entry_point.is_some();
        let first_page = wizard
            .visible_pages(supports_shortcuts)
            .into_iter()
            .next()
            .ok_or("Kurulumda gösterilecek sayfa bulunamadı.")?;
        let shortcut_default = supports_shortcuts && wizard.is_enabled(WizardPageId::Shortcuts);

        Ok(Self {
            executable,
            package_id: identity.package_id.clone(),
            package_label: format!("{} · v{}", identity.package_id, identity.version),
            target,
            program_files_root,
            current_user_root,
            scope: InstallScope::CurrentUser,
            language: WizardLanguage::default(),
            wizard,
            license_accepted: false,
            recycle_approved: false,
            entry_point,
            start_menu: shortcut_default,
            desktop: false,
            step: WizardStep::from_page(first_page),
            path_error: None,
            busy: None,
            progress_label: "Kurulum hazırlanıyor…",
            status: None,
        })
    }

    /// @dev-id BRK-UAC-RECYCLE-CONSENT-001
    /// @purpose Apply a validated UAC restart without inheriting any consent.
    /// @contract The caller first verifies the embedded identity, install scope
    /// and elevated token. No filesystem changes occur here.
    fn apply_privileged_resume(&mut self, args: &ResumeOptions, path: &Path) {
        self.scope = InstallScope::AllUsers;
        self.target = path.to_string_lossy().into_owned();
        self.language = args.language;
        self.start_menu = self.entry_point.is_some() && args.start_menu;
        self.desktop = self.entry_point.is_some() && args.desktop;
        self.step = if self.wizard.is_enabled(WizardPageId::License) {
            WizardStep::License
        } else {
            WizardStep::Review
        };
        self.license_accepted = false;
        self.recycle_approved = false;
    }

    fn flow(&self) -> Vec<WizardStep> {
        self.wizard
            .visible_pages(self.entry_point.is_some())
            .into_iter()
            .map(WizardStep::from_page)
            .collect()
    }

    fn next_step(&self) -> Option<WizardStep> {
        let flow = self.flow();
        let index = flow.iter().position(|step| *step == self.step)?;
        flow.get(index + 1)
            .copied()
            .filter(|step| *step != WizardStep::Installing)
    }

    fn previous_step(&self) -> Option<WizardStep> {
        let flow = self.flow();
        let index = flow.iter().position(|step| *step == self.step)?;
        index.checked_sub(1).and_then(|idx| flow.get(idx).copied())
    }

    fn step_counter(&self) -> Option<(usize, usize)> {
        if matches!(self.step, WizardStep::Installing | WizardStep::Finished) {
            return None;
        }
        let flow = self.flow();
        let end = flow.iter().position(|step| *step == WizardStep::Review)? + 1;
        let index = flow[..end].iter().position(|step| *step == self.step)?;
        Some((index + 1, end))
    }

    /// @dev-id BRK-WIN-SCOPE-WIZARD-001
    /// @purpose Changing scope switches the default path immediately.
    /// @contract Scope switches never create folders or silently keep an unsafe custom path.
    fn select_scope(&mut self, scope: InstallScope) {
        if self.scope != scope && self.busy.is_none() {
            self.scope = scope;
            self.target = match scope {
                InstallScope::CurrentUser => self
                    .current_user_root
                    .join(&self.package_id)
                    .to_string_lossy()
                    .into_owned(),
                InstallScope::AllUsers => self
                    .program_files_root
                    .join(&self.package_id)
                    .to_string_lossy()
                    .into_owned(),
            };
            self.path_error = None;
            self.recycle_approved = false;
        }
    }

    fn validated_target(&self, elevated: bool) -> Result<(PathBuf, bool), String> {
        let (path, occupied) = inspect_destination(&self.target, &self.package_id)?;
        if occupied && !self.recycle_approved {
            return Err(self.language.text(
                "Klasör zaten var. Temiz kurulum için gözden geçirme ekranında Geri Dönüşüm Kutusu onayını verin.",
                "Folder already exists. Approve moving it to the Recycle Bin on the review page.",
            ).to_owned());
        }
        validate_install_scope(self.scope, &path, &self.program_files_root, elevated)?;
        Ok((path, occupied))
    }

    fn advance(&mut self) {
        if self.busy.is_some() {
            return;
        }
        if self.step == WizardStep::License && !self.license_accepted {
            return;
        }
        if self.step == WizardStep::Destination {
            // Let the user choose shortcuts before the UAC prompt; we still
            // validate the protected root now, and the token at installation.
            let choice =
                inspect_destination(&self.target, &self.package_id).and_then(|(path, _)| {
                    validate_install_scope(self.scope, &path, &self.program_files_root, true)
                        .map(|_| path)
                });
            match choice {
                Ok(_) => self.path_error = None,
                Err(error) => {
                    self.path_error = Some(error);
                    return;
                }
            }
        }
        if let Some(next) = self.next_step() {
            self.step = next;
        }
    }

    fn back(&mut self) {
        if self.busy.is_none() {
            if let Some(previous) = self.previous_step() {
                self.step = previous;
                self.path_error = None;
            }
        }
    }

    /// @dev-id BRK-LOCAL-WIZARD-002
    /// @purpose Install only after explicit review-page confirmation.
    /// @contract Existing safe Rust package extraction and shortcut policy
    /// remain the only side-effecting implementation, on a worker thread.
    fn begin_install(&mut self) -> bool {
        if self.busy.is_some() || self.step != WizardStep::Review {
            return false;
        }
        if self.wizard.is_enabled(WizardPageId::License) && !self.license_accepted {
            self.step = WizardStep::License;
            self.path_error = Some(
                self.language
                    .text(
                        "Lisans sözleşmesini kabul etmeden kurulum başlatılamaz.",
                        "You must accept the license agreement before installing.",
                    )
                    .to_owned(),
            );
            return false;
        }
        // On a UAC restart consent is intentionally not inherited; the
        // elevated wizard displays Review and asks again.
        let elevated = current_process_is_elevated();
        let (target, replacing_existing) =
            match self.validated_target(elevated || self.scope == InstallScope::AllUsers) {
                Ok(value) => value,
                Err(error) => {
                    self.path_error = Some(error);
                    return false;
                }
            };
        if self.scope == InstallScope::AllUsers && !elevated {
            match request_elevated_setup(
                &self.executable,
                &self.package_id,
                &target,
                self.start_menu,
                self.desktop,
                self.language,
            ) {
                Ok(()) => return true,
                Err(error) => {
                    self.path_error = Some(error);
                    return false;
                }
            }
        }
        let executable = self.executable.clone();
        let source_uninstaller = executable.clone();
        let start_menu = self.start_menu;
        let desktop = self.desktop;
        let scope = self.scope;
        let language = self.language;
        let program_files_root = self.program_files_root.clone();
        let package_id = self.package_id.clone();
        let (sender, receiver) = mpsc::channel();
        self.busy = Some(receiver);
        self.status = None;
        self.path_error = None;
        self.progress_label = self
            .language
            .text("Kurulum hazırlanıyor…", "Preparing installation…");
        self.step = WizardStep::Installing;

        thread::spawn(move || {
            let phase_sender = sender.clone();
            let mut on_phase = move |phase| {
                let _ = phase_sender.send(WorkerEvent::Phase(phase));
            };
            let mut recycled = false;
            let result = validate_install_scope(
                scope,
                &target,
                &program_files_root,
                current_process_is_elevated(),
            )
            .and_then(|_| {
                if replacing_existing {
                    install_unsigned_local_setup_with_replacement(
                        executable,
                        &target,
                        &mut on_phase,
                        &mut |path| {
                            let _ = sender.send(WorkerEvent::RecyclingExistingTarget);
                            crate::windows_recycle::recycle_existing_target(path, &package_id)
                                .map_err(|error| PackageEngineError::Io(io::Error::other(error)))?;
                            recycled = true;
                            Ok(())
                        },
                    )
                } else {
                    install_unsigned_local_setup_with_progress(executable, &target, &mut on_phase)
                }
                .map_err(|error| error.to_string())
            })
            .map(|identity| {
                let mut message = format!(
                    "{} v{} {}: {}",
                    identity.package_id,
                    identity.version,
                    language.text("kuruldu", "installed"),
                    target.display()
                );
                let mut warning = false;
                if recycled {
                    message.push_str(language.text(
                        "\nÖnceki klasör Windows Geri Dönüşüm Kutusu'na taşındı.",
                        "\nPrevious folder moved to the Windows Recycle Bin.",
                    ));
                }
                let mut owned_links = Vec::new();
                if start_menu || desktop {
                    let _ = sender.send(WorkerEvent::CreatingShortcuts);
                    match identity.entry_point.as_deref() {
                        Some(entry) => match create_installed_shortcuts(
                            &target,
                            entry,
                            &identity.package_id,
                            start_menu,
                            desktop,
                            scope,
                        ) {
                            Ok(report) => {
                                owned_links = report.created;
                                if !owned_links.is_empty() {
                                    message.push_str(&format!(
                                        "\n{} {}",
                                        owned_links.len(),
                                        language.text(
                                            "Windows kısayolu oluşturuldu.",
                                            "Windows shortcut(s) created."
                                        )
                                    ));
                                }
                                if !report.warnings.is_empty() {
                                    warning = true;
                                    message.push_str(&format!(
                                        "\n{}: {}",
                                        language.text("Kısayol uyarısı", "Shortcut warning"),
                                        report.warnings.join("; ")
                                    ));
                                }
                            }
                            Err(error) => {
                                warning = true;
                                message.push_str(&format!(
                                    "{}: {error}",
                                    language.text(
                                        "\nDosyalar kuruldu ancak kısayol oluşturulamadı",
                                        "\nFiles installed, but shortcuts could not be created"
                                    )
                                ));
                            }
                        },
                        None => {
                            warning = true;
                            message
                                .push_str(language.text(
                                    "\nPakette ana EXE tanımı yok; kısayol oluşturulamadı.",
                                    "\nNo main EXE is defined in the package; shortcuts were not created."
                                ));
                        }
                    }
                }
                let _ = sender.send(WorkerEvent::RegisteringUninstaller);
                match register_local_install(
                    &source_uninstaller,
                    &identity.package_id,
                    &identity.version,
                    &target,
                    scope,
                    &owned_links,
                ) {
                    Ok(warnings) => {
                        message.push_str(language.text(
                            "\nWindows Yüklü Uygulamalar kaydı oluşturuldu.",
                            "\nRegistered in Windows Installed Apps.",
                        ));
                        if !warnings.is_empty() {
                            warning = true;
                            message.push_str("\n");
                            message.push_str(&warnings.join("; "));
                        }
                    }
                    Err(error) => {
                        warning = true;
                        message.push_str(&format!(
                            "{}: {error}",
                            language.text(
                                "\nWindows kaldırma kaydı oluşturulamadı",
                                "\nWindows uninstall registration failed"
                            )
                        ));
                    }
                }
                (message, warning)
            });
            let _ = sender.send(WorkerEvent::Finished(result));
        });
        false
    }

    fn poll(&mut self, ctx: &egui::Context) {
        let mut finished = None;
        if let Some(receiver) = &self.busy {
            loop {
                match receiver.try_recv() {
                    Ok(WorkerEvent::Phase(phase)) => {
                        self.progress_label = phase_label(phase, self.language);
                    }
                    Ok(WorkerEvent::RecyclingExistingTarget) => {
                        self.progress_label = self.language.text(
                            "Onaylanan eski klasör Geri Dönüşüm Kutusu'na taşınıyor…",
                            "Moving the approved previous folder to the Recycle Bin…",
                        );
                    }
                    Ok(WorkerEvent::CreatingShortcuts) => {
                        self.progress_label = self.language.text(
                            "Windows kısayolları oluşturuluyor…",
                            "Creating Windows shortcuts…",
                        );
                    }
                    Ok(WorkerEvent::RegisteringUninstaller) => {
                        self.progress_label = self.language.text(
                            "Windows kaldırma kaydı hazırlanıyor…",
                            "Registering the Windows uninstaller…",
                        );
                    }
                    Ok(WorkerEvent::Finished(result)) => {
                        finished = Some(result);
                        break;
                    }
                    Err(TryRecvError::Disconnected) => {
                        finished = Some(Err("Kurulum işi beklenmedik şekilde durdu.".to_owned()));
                        break;
                    }
                    Err(TryRecvError::Empty) => {
                        ctx.request_repaint_after(Duration::from_millis(100));
                        break;
                    }
                }
            }
        }
        if let Some(result) = finished {
            self.status = Some(result);
            self.busy = None;
            self.step = WizardStep::Finished;
        }
    }

    fn scope_label(&self) -> &'static str {
        match self.scope {
            InstallScope::CurrentUser => self
                .language
                .text("Yalnızca bu kullanıcı", "Only for this user"),
            InstallScope::AllUsers => self.language.text(
                "Bu bilgisayardaki tüm kullanıcılar",
                "For all users of this computer",
            ),
        }
    }

    fn page(&mut self, ui: &mut egui::Ui) {
        let language = self.language;
        if let Some(settings) = self.wizard.page(self.step.page_id()) {
            ui.label(settings.description(self.language == WizardLanguage::English));
            ui.add_space(12.0);
        }
        match self.step {
            WizardStep::License => {
                let english = self.language == WizardLanguage::English;
                egui::Frame::group(ui.style()).show(ui, |ui| {
                    egui::ScrollArea::vertical()
                        .id_salt("local-eula-scroll")
                        .max_height(275.0)
                        .show(ui, |ui| {
                            ui.add(egui::Label::new(self.wizard.license_text(english)).wrap());
                        });
                });
                ui.add_space(14.0);
                ui.checkbox(
                    &mut self.license_accepted,
                    self.language.text(
                        "Lisans sözleşmesinin koşullarını kabul ediyorum.",
                        "I accept the terms in the License Agreement.",
                    ),
                );
                ui.add_space(10.0);
                if !self.license_accepted {
                    ui.colored_label(
                        egui::Color32::from_rgb(173, 101, 37),
                        self.language.text(
                            "Devam etmek için lisans koşullarını kabul etmelisiniz.",
                            "Accept the license terms to enable Next.",
                        ),
                    );
                }
            }
            WizardStep::Welcome => {
                ui.heading(language.text("Turkuaz Standart Kurulum", "Turkuaz Standard Setup"));
                ui.add_space(12.0);
                ui.label(format!(
                    "{} {}",
                    language.text("Kurulacak paket:", "Package to install:"),
                    self.package_label
                ));
                ui.add_space(8.0);
                ui.label(
                    language.text("Bu sihirbaz kurulum klasörünü ve Windows kısayol tercihlerini seçmenize yardımcı olur.", "This wizard lets you choose the destination and Windows shortcut options."),
                );
                ui.add_space(12.0);
                ui.colored_label(
                    egui::Color32::from_rgb(170, 95, 32),
                    language.text("Bu imzasız yerel paket yalnızca dosya bütünlüğünü denetler; yayıncı kimliğini doğrulamaz.", "This unsigned local package checks file integrity only; it cannot verify the publisher."),
                );
                ui.small(language.text(
                    "Güvenli güncelleme ve geri alma için imzalı dağıtım akışı ayrı tutulur.",
                    "Signed distribution is required for trusted updates and rollback.",
                ));
            }
            WizardStep::Destination => {
                ui.label(language.text(
                    "Kurulum klasörünü seçin. Varsa eski klasör gözden geçirme ekranında ayrıca onaylanır.",
                    "Choose the destination. An existing folder requires separate approval on Review.",
                ));
                ui.add_space(14.0);
                ui.strong(language.text("Kurulum kapsamı", "Installation scope"));
                ui.add_space(6.0);
                let mut selected = self.scope;
                ui.horizontal(|ui| {
                    ui.radio_value(
                        &mut selected,
                        InstallScope::CurrentUser,
                        language.text("Yalnızca bu kullanıcı", "Only for this user"),
                    );
                    ui.add_space(18.0);
                    ui.radio_value(
                        &mut selected,
                        InstallScope::AllUsers,
                        language.text(
                            "Bu bilgisayardaki tüm kullanıcılar",
                            "For all users of this computer",
                        ),
                    );
                });
                self.select_scope(selected);
                ui.add_space(8.0);
                if self.scope == InstallScope::AllUsers {
                    ui.colored_label(
                        egui::Color32::from_rgb(170, 95, 32),
                        language.text("Tüm kullanıcılar kurulumu başlatılırken Windows yönetici onayı isteyecektir.", "Windows will request administrator consent before an all-users installation begins."),
                    );
                } else {
                    ui.label(language.text(
                        "Kurulum yalnızca oturum açmış kullanıcıya ait olacaktır.",
                        "Installation will be available only for the current user.",
                    ));
                }
                ui.add_space(16.0);
                ui.horizontal(|ui| {
                    if ui
                        .add(
                            egui::TextEdit::singleline(&mut self.target)
                                .desired_width((ui.available_width() - 94.0).max(240.0)),
                        )
                        .changed()
                    {
                        self.recycle_approved = false;
                        self.path_error = None;
                    }
                    if ui.button(language.text("Gözat…", "Browse…")).clicked() {
                        if let Some(parent) = rfd::FileDialog::new().pick_folder() {
                            self.target =
                                parent.join(&self.package_id).to_string_lossy().into_owned();
                            self.recycle_approved = false;
                            self.path_error = None;
                        }
                    }
                });
                ui.small(language.text(
                    "Gözat seçilen klasörün içine uygulama kimliğiyle yeni bir klasör ekler.",
                    "Browse adds a new folder named after the application identifier.",
                ));
                ui.small(language.text(
                    "Var olan klasör, yalnızca açık onayınızla Geri Dönüşüm Kutusu'na taşınabilir; üzerine yazılmaz.",
                    "An existing folder can be moved to the Recycle Bin only with your explicit approval; no overwrite.",
                ));
                if let Some(error) = &self.path_error {
                    ui.add_space(10.0);
                    ui.colored_label(egui::Color32::RED, error);
                }
            }
            WizardStep::Shortcuts => {
                if self.entry_point.is_some() {
                    ui.label(language.text(
                        "Kurulumdan sonra oluşturulacak Windows kısayollarını seçin.",
                        "Choose Windows shortcuts to create after installation.",
                    ));
                    ui.add_space(12.0);
                    ui.checkbox(
                        &mut self.start_menu,
                        language.text(
                            "Başlat menüsünde kısayol oluştur",
                            "Create Start menu shortcut",
                        ),
                    );
                    ui.checkbox(
                        &mut self.desktop,
                        language.text("Masaüstünde kısayol oluştur", "Create Desktop shortcut"),
                    );
                    ui.add_space(12.0);
                    ui.small(language.text(
                        "Kısayollar yalnızca pakette tanımlı uygulama EXE'sine bağlanır.",
                        "Shortcuts are created only for the EXE declared in the package.",
                    ));
                    ui.small(language.text(
                        "Var olan başka uygulama kısayollarının üzerine yazılmaz.",
                        "Existing shortcuts are never overwritten.",
                    ));
                } else {
                    ui.label(
                        language.text("Bu pakette ana EXE tanımlı olmadığı için uygulama kısayolu oluşturulamaz.", "No main EXE is defined in the package, so application shortcuts are unavailable."),
                    );
                    ui.add_space(10.0);
                    ui.small(language.text("Yeni paket oluştururken Studio / 02 Uygulama dosyaları bölümünden ana EXE seçebilirsiniz.", "Choose the main EXE in Studio / 02 Application files when building a new package."));
                }
            }
            WizardStep::Review => {
                ui.label(language.text(
                    "Dosyalar henüz kurulmadı. Seçimlerinizi kontrol edin.",
                    "No files have been installed. Review your choices.",
                ));
                ui.add_space(12.0);
                egui::Grid::new("local_setup_review")
                    .num_columns(2)
                    .spacing([14.0, 12.0])
                    .show(ui, |ui| {
                        ui.strong(language.text("Uygulama", "Application"));
                        ui.label(&self.package_label);
                        ui.end_row();

                        ui.strong(language.text("Kurulum kapsamı", "Installation scope"));
                        ui.label(self.scope_label());
                        ui.end_row();

                        ui.strong(language.text("Kurulum klasörü", "Installation folder"));
                        ui.add(egui::Label::new(&self.target).wrap());
                        ui.end_row();

                        ui.strong(language.text("Başlat menüsü", "Start menu"));
                        ui.label(if self.entry_point.is_some() && self.start_menu {
                            language.text("Kısayol oluşturulacak", "Shortcut will be created")
                        } else {
                            language.text("Oluşturulmayacak", "No shortcut")
                        });
                        ui.end_row();

                        ui.strong(language.text("Masaüstü", "Desktop"));
                        ui.label(if self.entry_point.is_some() && self.desktop {
                            language.text("Kısayol oluşturulacak", "Shortcut will be created")
                        } else {
                            language.text("Oluşturulmayacak", "No shortcut")
                        });
                        ui.end_row();
                    });
                ui.add_space(16.0);
                ui.small(language.text(
                    "Kurulumu başlat düğmesiyle onay verdiğinizde dosyalar hazırlanır.",
                    "Files will be prepared after you confirm with Install.",
                ));
                match inspect_destination(&self.target, &self.package_id) {
                    Ok((path, true)) => {
                        ui.add_space(8.0);
                        ui.colored_label(
                            egui::Color32::from_rgb(190, 112, 39),
                            language.text(
                                "DİKKAT: Bu klasör zaten var. İçindeki tüm dosyalar ve kullanıcı verileri birlikte Geri Dönüşüm Kutusu'na taşınacak.",
                                "WARNING: This folder exists. All its files, including user data, will be moved together to the Recycle Bin.",
                            ),
                        );
                        ui.add(egui::Label::new(path.display().to_string()).wrap());
                        ui.checkbox(
                            &mut self.recycle_approved,
                            language.text(
                                "Onaylıyorum: Eski klasörü Geri Dönüşüm Kutusu'na taşı, ardından temiz kurulum yap.",
                                "I approve moving the old folder to the Recycle Bin, then performing a clean install.",
                            ),
                        );
                        ui.small(language.text(
                            "Kurulum başarısız olursa eski klasörü Geri Dönüşüm Kutusu'ndan kendiniz geri yükleyebilirsiniz. Otomatik geri alma yapılmaz.",
                            "If installation fails, you may restore the previous folder from the Recycle Bin manually. There is no automatic restore.",
                        ));
                    }
                    Ok((_, false)) => {
                        self.recycle_approved = false;
                        ui.small(language.text(
                            "Hedef klasör yeni; eski dosyalar değiştirilmez.",
                            "The target is new; existing files are not changed.",
                        ));
                    }
                    Err(error) => {
                        self.recycle_approved = false;
                        ui.colored_label(egui::Color32::RED, error);
                    }
                }
                if let Some(error) = &self.path_error {
                    ui.add_space(10.0);
                    ui.colored_label(egui::Color32::RED, error);
                }
            }
            WizardStep::Installing => {
                ui.horizontal(|ui| {
                    ui.spinner();
                    ui.strong(self.progress_label);
                });
                ui.add_space(12.0);
                ui.label(language.text("Kurulum aşamaları Rust paket motorundan alınan gerçek olaylara dayanır.", "Installation stages reflect actual operations performed by the Rust package engine."));
                ui.small(language.text("Dosya bazlı yüzde ölçülmediği için yanıltıcı bir ilerleme yüzdesi gösterilmez.", "An estimated completion percentage is not shown because exact byte-level progress is unavailable."));
                ui.small(
                    language.text("Kurulum sırasında güvenli iptal henüz desteklenmiyor. İşlem tamamlanana kadar pencere açık kalır.", "Safe cancellation during installation is not available yet. Keep the window open until completion."),
                );
            }
            WizardStep::Finished => {
                if let Some(result) = &self.status {
                    match result {
                        Ok((message, warning)) => {
                            ui.heading(if *warning {
                                language.text(
                                    "Dosyalar kuruldu; ek işlem uyarısı var",
                                    "Files installed with additional warnings",
                                )
                            } else {
                                language.text("Kurulum tamamlandı", "Installation complete")
                            });
                            ui.add_space(12.0);
                            ui.colored_label(
                                if *warning {
                                    egui::Color32::from_rgb(170, 95, 32)
                                } else {
                                    egui::Color32::DARK_GREEN
                                },
                                message,
                            );
                            ui.add_space(12.0);
                            ui.small(
                                language.text("Bu imzasız paket için güvenli güncelleme/rollback desteği yoktur.", "Trusted updates and rollback are not available for this unsigned package."),
                            );
                        }
                        Err(message) => {
                            ui.heading(
                                language
                                    .text("Kurulum tamamlanamadı", "Installation could not finish"),
                            );
                            ui.add_space(12.0);
                            ui.colored_label(egui::Color32::RED, message);
                            ui.add_space(12.0);
                            ui.label(language.text(
                                "Kurulum konumunu değiştirip yeniden deneyebilirsiniz.",
                                "Change the destination folder and try again.",
                            ));
                        }
                    }
                }
            }
        }
    }
}

/// @dev-id BRK-LOCAL-WIZARD-FOOTER-001
/// @purpose Keep navigation actions at the bottom without swallowing the
/// welcome/destination/review content in the native setup wizard.
const LOCAL_WIZARD_FOOTER_HEIGHT: f32 = 92.0;
const LOCAL_WIZARD_ACTION_ROW_HEIGHT: f32 = 38.0;

fn show_local_wizard_action_row<R>(
    ui: &mut egui::Ui,
    add_actions: impl FnOnce(&mut egui::Ui) -> R,
) -> egui::InnerResponse<R> {
    // Ui::with_layout consumes all remaining space in a bottom panel.
    // A bounded row preserves the CentralPanel at Windows minimum sizes.
    ui.allocate_ui_with_layout(
        egui::vec2(ui.available_width(), LOCAL_WIZARD_ACTION_ROW_HEIGHT),
        egui::Layout::right_to_left(egui::Align::Center),
        add_actions,
    )
}

impl eframe::App for LocalInstaller {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        let language = self.language;
        self.poll(ctx);
        if self.busy.is_some() && ctx.input(|input| input.viewport().close_requested()) {
            ctx.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        egui::TopBottomPanel::top("local_setup_header").show(ctx, |ui| {
            ui.add_space(12.0);
            let title = self
                .wizard
                .page(self.step.page_id())
                .map(|settings| settings.title(self.language == WizardLanguage::English))
                .unwrap_or(self.step.heading(self.language));
            ui.heading(egui::RichText::new(title).size(25.0).strong());
            ui.label(egui::RichText::new(&self.package_label).size(15.0));
            let before = self.language;
            ui.horizontal(|ui| {
                ui.label("Dil / Language");
                egui::ComboBox::from_id_salt("native-wizard-language")
                    .selected_text(self.language.label())
                    .show_ui(ui, |ui| {
                        ui.selectable_value(
                            &mut self.language,
                            WizardLanguage::Turkish,
                            WizardLanguage::Turkish.label(),
                        );
                        ui.selectable_value(
                            &mut self.language,
                            WizardLanguage::English,
                            WizardLanguage::English.label(),
                        );
                    });
            });
            if self.language != before {
                self.license_accepted = false;
            }
            if let Some((index, total)) = self.step_counter() {
                ui.small(format!(
                    "{} {index}/{total}",
                    self.language.text("Hazırlık adımı", "Preparation step")
                ));
            }
            ui.add_space(10.0);
            ui.separator();
        });

        egui::TopBottomPanel::bottom("local_setup_footer")
            .exact_height(LOCAL_WIZARD_FOOTER_HEIGHT)
            .show(ctx, |ui| {
            ui.separator();
            ui.add_space(10.0);
            // In right-to-left layout the primary action is anchored to
            // the bottom-right edge; back remains to its left.
            show_local_wizard_action_row(ui, |ui| {
                if self.busy.is_some() {
                    ui.label(
                        self.language
                            .text("Kurulum devam ediyor…", "Installation in progress…"),
                    );
                    return;
                }
                let primary = egui::Color32::from_rgb(6, 123, 134);
                match self.step {
                    WizardStep::Welcome
                    | WizardStep::License
                    | WizardStep::Destination
                    | WizardStep::Shortcuts => {
                        if ui
                            .add_enabled(
                                self.step != WizardStep::License || self.license_accepted,
                                egui::Button::new(
                                    egui::RichText::new(language.text("İleri  ›", "Next  ›"))
                                        .color(egui::Color32::WHITE),
                                )
                                .fill(primary)
                                .min_size(egui::vec2(110.0, 38.0)),
                            )
                            .clicked()
                        {
                            self.advance();
                        }
                        if ui
                            .add_sized(
                                [95.0, 38.0],
                                egui::Button::new(language.text("İptal", "Cancel")),
                            )
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if self.previous_step().is_some()
                            && ui
                                .add_sized(
                                    [95.0, 38.0],
                                    egui::Button::new(language.text("‹  Geri", "‹  Back")),
                                )
                                .clicked()
                        {
                            self.back();
                        }
                    }
                    WizardStep::Review => {
                        if ui
                            .add_sized(
                                [175.0, 38.0],
                                egui::Button::new(
                                    egui::RichText::new(
                                        language.text("Kurulumu başlat", "Install"),
                                    )
                                    .color(egui::Color32::WHITE),
                                )
                                .fill(primary),
                            )
                            .clicked()
                        {
                            if self.begin_install() {
                                ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                            }
                        }
                        if ui
                            .add_sized(
                                [95.0, 38.0],
                                egui::Button::new(language.text("İptal", "Cancel")),
                            )
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if ui
                            .add_sized(
                                [95.0, 38.0],
                                egui::Button::new(language.text("‹  Geri", "‹  Back")),
                            )
                            .clicked()
                        {
                            self.back();
                        }
                    }
                    WizardStep::Installing => {}
                    WizardStep::Finished => {
                        if ui
                            .add_sized(
                                [105.0, 38.0],
                                egui::Button::new(language.text("Kapat", "Close")),
                            )
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if self.status.as_ref().is_some_and(Result::is_err)
                            && ui
                                .add_sized(
                                    [165.0, 38.0],
                                    egui::Button::new(
                                        language.text("Seçimleri düzenle", "Change settings"),
                                    ),
                                )
                                .clicked()
                        {
                            self.step = WizardStep::Destination;
                            self.status = None;
                            self.path_error = None;
                        }
                    }
                }
            });
            ui.add_space(10.0);
        });

        // egui CentralPanel must come after all top/bottom panels.
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.add_space(22.0);
            self.page(ui);
        });
    }
}

/// @dev-id BRK-LOCAL-WIZARD-STYLE-001
/// @purpose Use Windows' installed Segoe UI font when available; otherwise
/// egui's bundled proportional font remains a safe Unicode fallback.
fn configure_local_style(context: &egui::Context) {
    if let Some(windows_dir) = std::env::var_os("WINDIR") {
        let font_path = PathBuf::from(windows_dir).join("Fonts").join("segoeui.ttf");
        if let Ok(font_bytes) = fs::read(&font_path) {
            let mut fonts = egui::FontDefinitions::default();
            fonts.font_data.insert(
                "WindowsSegoeUI".to_owned(),
                egui::FontData::from_owned(font_bytes).into(),
            );
            if let Some(family) = fonts.families.get_mut(&egui::FontFamily::Proportional) {
                family.insert(0, "WindowsSegoeUI".to_owned());
            }
            context.set_fonts(fonts);
        }
    }
    context.all_styles_mut(|style| {
        style
            .text_styles
            .insert(egui::TextStyle::Small, egui::FontId::proportional(14.0));
        style
            .text_styles
            .insert(egui::TextStyle::Body, egui::FontId::proportional(17.0));
        style
            .text_styles
            .insert(egui::TextStyle::Button, egui::FontId::proportional(16.0));
        style
            .text_styles
            .insert(egui::TextStyle::Heading, egui::FontId::proportional(26.0));
        style.spacing.item_spacing = egui::vec2(12.0, 10.0);
        style.spacing.button_padding = egui::vec2(14.0, 8.0);
        style.spacing.interact_size = egui::vec2(40.0, 38.0);
        style.spacing.window_margin = egui::Margin::same(18);
        style.visuals.selection.bg_fill = egui::Color32::from_rgb(6, 123, 134);
    });
}

/// @dev-id BRK-UAC-002
/// @purpose Resume exactly the chosen all-users setup after the OS UAC dialog.
/// @contract The elevated process checks its embedded identity, path and token;
/// the review page requires explicit confirmation before any file is installed.
pub fn run_resumed() -> Result<(), Box<dyn std::error::Error>> {
    let args = parse_resumed_arguments()?;
    let executable = std::env::current_exe()?;
    let mut app = LocalInstaller::new(executable)?;
    if args.package_id != app.package_id {
        return Err("UAC paket kimliği doğrulanamadı.".into());
    }
    let (path, _) = inspect_destination(&args.target, &app.package_id)?;
    validate_install_scope(
        InstallScope::AllUsers,
        &path,
        &app.program_files_root,
        current_process_is_elevated(),
    )?;
    app.apply_privileged_resume(&args, &path);
    launch(app)
}

pub fn run() -> Result<(), Box<dyn std::error::Error>> {
    let executable = std::env::current_exe()?;
    let app = LocalInstaller::new(executable)?;
    launch(app)
}

fn launch(app: LocalInstaller) -> Result<(), Box<dyn std::error::Error>> {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([860.0, 550.0])
            .with_min_inner_size([760.0, 490.0])
            .with_title("Turkuaz Standart Kurulum"),
        ..Default::default()
    };
    eframe::run_native(
        "Turkuaz Standart Kurulum",
        options,
        Box::new(move |creation_context| {
            configure_local_style(&creation_context.egui_ctx);
            Ok(Box::new(app))
        }),
    )?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    /// @dev-id BRK-LOCAL-WIZARD-FOOTER-TEST-001
    /// @purpose Regression for an empty wizard page caused by the action row
    /// taking all remaining height in the bottom panel.
    #[test]
    fn wizard_footer_preserves_page_content_at_minimum_and_default_sizes() {
        for (width, height) in [(760.0, 490.0), (860.0, 550.0)] {
            let ctx = egui::Context::default();
            let input = egui::RawInput {
                screen_rect: Some(egui::Rect::from_min_size(
                    egui::Pos2::ZERO,
                    egui::vec2(width, height),
                )),
                ..Default::default()
            };
            let _ = ctx.run(input, |ctx| {
                egui::TopBottomPanel::top("wizard_test_header")
                    .exact_height(140.0)
                    .show(ctx, |ui| {
                        ui.label("Welcome and language");
                    });
                let footer = egui::TopBottomPanel::bottom("wizard_test_footer")
                    .exact_height(LOCAL_WIZARD_FOOTER_HEIGHT)
                    .show(ctx, |ui| {
                        ui.separator();
                        ui.add_space(10.0);
                        show_local_wizard_action_row(ui, |ui| {
                            ui.add_sized([110.0, 38.0], egui::Button::new("Next"));
                        });
                        ui.add_space(10.0);
                    });
                let central = egui::CentralPanel::default().show(ctx, |ui| {
                    ui.heading("Turkuaz Standard Setup");
                    ui.label("Package details must remain visible.");
                });
                assert!(
                    footer.response.rect.height() <= LOCAL_WIZARD_FOOTER_HEIGHT + 2.0,
                    "footer expanded beyond its navigation row"
                );
                assert!(
                    central.response.rect.height() >= 180.0,
                    "wizard content was hidden by the footer: {}x{}",
                    width,
                    height
                );
                assert!(
                    central.response.rect.bottom() <= footer.response.rect.top() + 2.0,
                    "wizard body overlaps footer"
                );
            });
        }
    }


    #[test]
    fn wizard_requires_explicit_install_after_review() {
        let mut step = WizardStep::Welcome;
        assert_eq!(step.next(true), Some(WizardStep::Destination));
        step = step.next(true).unwrap();
        assert_eq!(step.previous(true), Some(WizardStep::Welcome));
        step = step.next(true).unwrap();
        assert_eq!(step, WizardStep::Shortcuts);
        step = step.next(true).unwrap();
        assert_eq!(step, WizardStep::Review);
        assert_eq!(step.next(true), None, "next must not trigger installation");
        assert_eq!(WizardStep::Installing.previous(true), None);
        assert_eq!(WizardStep::Finished.next(true), None);
    }

    #[test]
    fn wizard_skips_shortcut_page_when_package_has_no_exe() {
        assert_eq!(
            WizardStep::Welcome.next(false),
            Some(WizardStep::Destination)
        );
        assert_eq!(
            WizardStep::Destination.next(false),
            Some(WizardStep::Review)
        );
        assert_eq!(
            WizardStep::Review.previous(false),
            Some(WizardStep::Destination)
        );
        assert_eq!(WizardStep::Review.decision_index(false), Some((3, 3)));
        assert_eq!(WizardStep::Shortcuts.decision_index(false), None);
    }

    #[test]
    fn configured_eula_blocks_progress_until_explicit_acceptance() {
        let mut wizard = WizardConfig::default();
        wizard.page_mut(WizardPageId::License).unwrap().enabled = true;
        wizard.license_text_tr = "Lisans koşulları".into();
        wizard.license_text_en = "License terms".into();
        let flow = wizard.visible_pages(true);
        assert_eq!(flow[0], WizardPageId::Welcome);
        assert_eq!(flow[1], WizardPageId::License);
        assert_eq!(flow[2], WizardPageId::Destination);
        assert_eq!(flow.last(), Some(&WizardPageId::Finished));
    }

    #[test]
    fn worker_stage_labels_are_real_operation_names() {
        assert!(
            phase_label(LocalInstallPhase::VerifyingPackage, WizardLanguage::Turkish)
                .contains("doğrulanıyor")
        );
        assert!(
            phase_label(
                LocalInstallPhase::ExtractingPayload,
                WizardLanguage::Turkish
            )
            .contains("envanteri")
        );
        assert!(
            phase_label(
                LocalInstallPhase::PublishingInstall,
                WizardLanguage::Turkish
            )
            .contains("aktarılıyor")
        );
    }

    #[test]
    fn changing_scope_recalculates_destination_without_writing_files() {
        let user = PathBuf::from(r"C:\Users\Test\AppData\Local\Programs");
        let machine = PathBuf::from(r"C:\Program Files");
        let mut app = LocalInstaller {
            executable: PathBuf::from("fixture.exe"),
            package_id: "qa.scope".into(),
            package_label: "qa.scope · 1.0.0".into(),
            target: user.join("qa.scope").to_string_lossy().into_owned(),
            program_files_root: machine.clone(),
            current_user_root: user.clone(),
            scope: InstallScope::CurrentUser,
            language: WizardLanguage::Turkish,
            wizard: WizardConfig::default(),
            license_accepted: false,
            recycle_approved: true,
            entry_point: None,
            start_menu: false,
            desktop: false,
            step: WizardStep::Destination,
            path_error: Some("previous error".into()),
            busy: None,
            progress_label: "",
            status: None,
        };
        app.select_scope(InstallScope::AllUsers);
        assert_eq!(app.scope, InstallScope::AllUsers);
        assert_eq!(PathBuf::from(&app.target), machine.join("qa.scope"));
        assert!(app.path_error.is_none());
        assert!(!app.recycle_approved);
        app.select_scope(InstallScope::CurrentUser);
        assert_eq!(PathBuf::from(&app.target), user.join("qa.scope"));
        assert_eq!(app.step, WizardStep::Destination);
    }

    /// @dev-id BRK-UAC-RECYCLE-TEST-001
    /// @purpose Ensure UAC elevation drops both EULA and old-folder approval
    /// regardless of the initial wizard selection.
    #[test]
    fn elevated_resume_requires_fresh_license_and_recycle_approval() {
        let user = PathBuf::from(r"C:\Users\Test\AppData\Local\Programs");
        let machine = PathBuf::from(r"C:\Program Files");
        let target = machine.join("qa.consent");
        let mut wizard = WizardConfig::default();
        wizard.page_mut(WizardPageId::License).unwrap().enabled = true;
        let mut app = LocalInstaller {
            executable: PathBuf::from("fixture.exe"),
            package_id: "qa.consent".into(),
            package_label: "qa.consent · 1.0.0".into(),
            target: user.join("qa.consent").to_string_lossy().into_owned(),
            program_files_root: machine,
            current_user_root: user,
            scope: InstallScope::CurrentUser,
            language: WizardLanguage::Turkish,
            wizard,
            license_accepted: true,
            recycle_approved: true,
            entry_point: None,
            start_menu: false,
            desktop: false,
            step: WizardStep::Review,
            path_error: None,
            busy: None,
            progress_label: "",
            status: None,
        };
        let args = ResumeOptions {
            package_id: "qa.consent".into(),
            target: target.to_string_lossy().into_owned(),
            start_menu: true,
            desktop: true,
            language: WizardLanguage::English,
        };
        app.apply_privileged_resume(&args, &target);
        assert_eq!(app.scope, InstallScope::AllUsers);
        assert_eq!(PathBuf::from(&app.target), target);
        assert_eq!(app.language, WizardLanguage::English);
        assert!(!app.license_accepted);
        assert!(!app.recycle_approved);
        assert_eq!(app.step, WizardStep::License);
        assert!(!app.start_menu);
        assert!(!app.desktop);

        app.wizard.page_mut(WizardPageId::License).unwrap().enabled = false;
        app.license_accepted = true;
        app.recycle_approved = true;
        app.apply_privileged_resume(&args, &target);
        assert_eq!(app.step, WizardStep::Review);
        assert!(!app.license_accepted);
        assert!(!app.recycle_approved);
    }

    #[test]
    fn preflight_rejects_unsafe_targets_but_reports_existing_without_writes() {
        let temp = std::env::temp_dir();
        assert!(inspect_destination(&temp.to_string_lossy(), "qa.recycle").is_err());
        let unsafe_path = temp.join("..").join("should-not-install");
        assert!(inspect_destination(&unsafe_path.to_string_lossy(), "qa.recycle").is_err());
        assert!(inspect_destination("relative-path", "qa.recycle").is_err());
        let root = temp.join(format!("turkuaz-wizard-recycle-{}", std::process::id()));
        let old = root.join("qa.recycle");
        fs::create_dir_all(&old).expect("old fixture");
        fs::write(old.join("user.txt"), b"user-owned").expect("fixture content");
        let (selected, occupied) =
            inspect_destination(&old.to_string_lossy(), "qa.recycle").expect("inspect");
        assert_eq!(selected, old);
        assert!(occupied);
        assert_eq!(fs::read(old.join("user.txt")).unwrap(), b"user-owned");
        assert!(inspect_destination(&old.to_string_lossy(), "wrong.package").is_err());
        let fresh = root.join("new.qa");
        assert!(
            !inspect_destination(&fresh.to_string_lossy(), "qa.recycle")
                .expect("new path")
                .1
        );
        assert!(!fresh.exists(), "preflight must be read-only");
        fs::remove_dir_all(root).expect("test cleanup");
    }
}
