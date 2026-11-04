// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/unsigned_local_gui.rs
// Amac: Rust yerel standart kurulumunu cok asamali Windows sihirbazi ile sunmak
// Modul - Rust GUI
// Version: 0.4.1
// Aciklama: Hos geldiniz, hedef, kisayollar, son kontrol, kurulum ve sonuc akisi
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
    LocalInstallPhase, install_unsigned_local_setup_with_progress, unsigned_local_identity,
};

use crate::{
    wizard_language::WizardLanguage,
    windows_install_scope::{
        InstallScope, current_process_is_elevated, default_install_directory, known_folder,
        validate_install_scope,
    },
    windows_shortcuts::create_installed_shortcuts,
};
use windows::Win32::UI::Shell::FOLDERID_ProgramFiles;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum WizardStep {
    Welcome,
    Destination,
    Shortcuts,
    Review,
    Installing,
    Finished,
}

impl WizardStep {
    fn next(self, has_shortcuts: bool) -> Option<Self> {
        match self {
            Self::Welcome => Some(Self::Destination),
            Self::Destination if has_shortcuts => Some(Self::Shortcuts),
            Self::Destination => Some(Self::Review),
            Self::Shortcuts => Some(Self::Review),
            Self::Review | Self::Installing | Self::Finished => None,
        }
    }

    fn previous(self, has_shortcuts: bool) -> Option<Self> {
        match self {
            Self::Destination => Some(Self::Welcome),
            Self::Shortcuts => Some(Self::Destination),
            Self::Review if has_shortcuts => Some(Self::Shortcuts),
            Self::Review => Some(Self::Destination),
            Self::Welcome | Self::Installing | Self::Finished => None,
        }
    }

    fn heading(self, language: WizardLanguage) -> &'static str {
        let (tr, en) = match self {
            Self::Welcome => ("Hoş geldiniz", "Welcome"),
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
            Self::Destination => Some((2, count)),
            Self::Shortcuts if has_shortcuts => Some((3, count)),
            Self::Review => Some((count, count)),
            Self::Shortcuts | Self::Installing | Self::Finished => None,
        }
    }
}

/// @dev-id BRK-LOCAL-WIZARD-001
/// @purpose Reject unsafe or already occupied paths before final confirmation.
/// @contract This is a read-only preflight. The package engine still performs
/// its own authoritative validation immediately before staging and publish.
fn preflight_destination(value: &str) -> Result<PathBuf, String> {
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
        Ok(_) => Err("Hedef klasör zaten var. Mevcut kurulumun üzerine yazılmaz.".into()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => Ok(path),
        Err(error) => Err(format!("Hedef klasör doğrulanamadı: {error}")),
    }
}

/// Work events are delivered by the real package engine, not a fabricated timer.
enum WorkerEvent {
    Phase(LocalInstallPhase),
    CreatingShortcuts,
    Finished(Result<(String, bool), String>),
}

fn phase_label(phase: LocalInstallPhase, language: WizardLanguage) -> &'static str {
    let (tr, en) = match phase {
        LocalInstallPhase::VerifyingPackage => (
            "Gömülü paket doğrulanıyor…",
            "Verifying embedded package…",
        ),
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

        Ok(Self {
            executable,
            package_id: identity.package_id.clone(),
            package_label: format!("{} · v{}", identity.package_id, identity.version),
            target,
            program_files_root,
            current_user_root,
            scope: InstallScope::CurrentUser,
            language: WizardLanguage::default(),
            entry_point,
            start_menu: supports_shortcuts,
            desktop: false,
            step: WizardStep::Welcome,
            path_error: None,
            busy: None,
            progress_label: "Kurulum hazırlanıyor…",
            status: None,
        })
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
        }
    }

    fn validated_target(&self) -> Result<PathBuf, String> {
        let path = preflight_destination(&self.target)?;
        validate_install_scope(
            self.scope,
            &path,
            &self.program_files_root,
            current_process_is_elevated(),
        )?;
        Ok(path)
    }

    fn advance(&mut self) {
        if self.busy.is_some() {
            return;
        }
        if self.step == WizardStep::Destination {
            match self.validated_target() {
                Ok(_) => self.path_error = None,
                Err(error) => {
                    self.path_error = Some(error);
                    return;
                }
            }
        }
        if let Some(next) = self.step.next(self.entry_point.is_some()) {
            self.step = next;
        }
    }

    fn back(&mut self) {
        if self.busy.is_none() {
            if let Some(previous) = self.step.previous(self.entry_point.is_some()) {
                self.step = previous;
                self.path_error = None;
            }
        }
    }

    /// @dev-id BRK-LOCAL-WIZARD-002
    /// @purpose Install only after explicit review-page confirmation.
    /// @contract Existing safe Rust package extraction and shortcut policy
    /// remain the only side-effecting implementation, on a worker thread.
    fn begin_install(&mut self) {
        if self.busy.is_some() || self.step != WizardStep::Review {
            return;
        }
        let target = match self.validated_target() {
            Ok(target) => target,
            Err(error) => {
                self.path_error = Some(error);
                self.step = WizardStep::Destination;
                return;
            }
        };
        let executable = self.executable.clone();
        let start_menu = self.start_menu;
        let desktop = self.desktop;
        let scope = self.scope;
        let language = self.language;
        let program_files_root = self.program_files_root.clone();
        let (sender, receiver) = mpsc::channel();
        self.busy = Some(receiver);
        self.status = None;
        self.path_error = None;
        self.progress_label = self.language.text(
            "Kurulum hazırlanıyor…",
            "Preparing installation…",
        );
        self.step = WizardStep::Installing;

        thread::spawn(move || {
            let phase_sender = sender.clone();
            let mut on_phase = move |phase| {
                let _ = phase_sender.send(WorkerEvent::Phase(phase));
            };
            let result = validate_install_scope(
                scope,
                &target,
                &program_files_root,
                current_process_is_elevated(),
            )
            .and_then(|_| {
                install_unsigned_local_setup_with_progress(executable, &target, &mut on_phase)
                    .map_err(|error| error.to_string())
            })
            .map(|identity| {
                let mut message = format!(
                    "{} v{} kuruldu: {}",
                    identity.package_id,
                    identity.version,
                    target.display()
                );
                let mut warning = false;
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
                                if !report.created.is_empty() {
                                    message.push_str(&format!(
                                        "\n{} Windows kısayolu oluşturuldu.",
                                        report.created.len()
                                    ));
                                }
                                if !report.warnings.is_empty() {
                                    warning = true;
                                    message.push_str(&format!(
                                        "\nKısayol uyarısı: {}",
                                        report.warnings.join("; ")
                                    ));
                                }
                            }
                            Err(error) => {
                                warning = true;
                                message.push_str(&format!(
                                    "\nDosyalar kuruldu ancak kısayol oluşturulamadı: {error}"
                                ));
                            }
                        },
                        None => {
                            warning = true;
                            message
                                .push_str("\nPakette ana EXE tanımı yok; kısayol oluşturulamadı.");
                        }
                    }
                }
                (message, warning)
            });
            let _ = sender.send(WorkerEvent::Finished(result));
        });
    }

    fn poll(&mut self, ctx: &egui::Context) {
        let mut finished = None;
        if let Some(receiver) = &self.busy {
            loop {
                match receiver.try_recv() {
                    Ok(WorkerEvent::Phase(phase)) => {
                        self.progress_label = phase_label(phase, self.language);
                    }
                    Ok(WorkerEvent::CreatingShortcuts) => {
                        self.progress_label = self.language.text(
                            "Windows kısayolları oluşturuluyor…",
                            "Creating Windows shortcuts…",
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

    fn page(&mut self, ui: &mut egui::Ui) {
        let language = self.language;
        match self.step {
            WizardStep::Welcome => {
                ui.horizontal(|ui| {
                    ui.label("Dil / Language");
                    egui::ComboBox::from_id_salt("setup-language")
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
                ui.add_space(18.0);
                ui.heading(language.text("Turkuaz Standart Kurulum", "Turkuaz Standard Setup"));
                ui.add_space(12.0);
                ui.label(format!(language.text("Kurulacak paket: {}", "Package to install: {}"), self.package_label));
                ui.add_space(8.0);
                ui.label(
                    language.text("Bu sihirbaz kurulum klasörünü ve Windows kısayol tercihlerini seçmenize yardımcı olur.", "This wizard lets you choose the destination and Windows shortcut options."),
                );
                ui.add_space(12.0);
                ui.colored_label(
                    egui::Color32::from_rgb(170, 95, 32),
                    language.text("Bu imzasız yerel paket yalnızca dosya bütünlüğünü denetler; yayıncı kimliğini doğrulamaz.", "This unsigned local package checks file integrity only; it cannot verify the publisher."),
                );
                ui.small(language.text("Güvenli güncelleme ve geri alma için imzalı dağıtım akışı ayrı tutulur.", "Signed distribution is required for trusted updates and rollback."));
            }
            WizardStep::Destination => {
                ui.label(language.text("Uygulamanın kurulacağı yeni klasörü seçin.", "Choose a new folder for this application."));
                ui.add_space(14.0);
                ui.strong(language.text("Kurulum kapsamı", "Installation scope"));
                ui.add_space(6.0);
                let mut selected = self.scope;
                ui.horizontal(|ui| {
                    ui.radio_value(
                        &mut selected,
                        InstallScope::CurrentUser,
                        InstallScope::CurrentUser.label(),
                    );
                    ui.add_space(18.0);
                    ui.radio_value(
                        &mut selected,
                        InstallScope::AllUsers,
                        InstallScope::AllUsers.label(),
                    );
                });
                self.select_scope(selected);
                ui.add_space(8.0);
                if self.scope == InstallScope::AllUsers {
                    ui.colored_label(
                        egui::Color32::from_rgb(170, 95, 32),
                        language.text("Tüm kullanıcılar için yönetici yetkisi gerekir; otomatik UAC yükseltmesi henüz eklenmedi.", "Installing for all users requires administrator privileges; automatic UAC elevation is not yet available."),
                    );
                } else {
                    ui.label(language.text("Kurulum yalnızca oturum açmış kullanıcıya ait olacaktır.", "Installation will be available only for the current user."));
                }
                ui.add_space(16.0);
                ui.horizontal(|ui| {
                    ui.add(
                        egui::TextEdit::singleline(&mut self.target)
                            .desired_width((ui.available_width() - 94.0).max(240.0)),
                    );
                    if ui.button(language.text("Gözat…", "Browse…")).clicked() {
                        if let Some(parent) = rfd::FileDialog::new().pick_folder() {
                            self.target =
                                parent.join(&self.package_id).to_string_lossy().into_owned();
                        }
                    }
                });
                ui.small(language.text("Gözat seçilen klasörün içine uygulama kimliğiyle yeni bir klasör ekler.", "Browse adds a new folder named after the application identifier."));
                ui.small(language.text("Mevcut bir klasörün üzerine yazılmaz; eski kurulum korunur.", "Existing folders are not overwritten; previous installations remain intact."));
                if let Some(error) = &self.path_error {
                    ui.add_space(10.0);
                    ui.colored_label(egui::Color32::RED, error);
                }
            }
            WizardStep::Shortcuts => {
                if self.entry_point.is_some() {
                    ui.label(language.text("Kurulumdan sonra oluşturulacak Windows kısayollarını seçin.", "Choose Windows shortcuts to create after installation."));
                    ui.add_space(12.0);
                    ui.checkbox(&mut self.start_menu, language.text("Başlat menüsünde kısayol oluştur", "Create Start menu shortcut"));
                    ui.checkbox(&mut self.desktop, language.text("Masaüstünde kısayol oluştur", "Create Desktop shortcut"));
                    ui.add_space(12.0);
                    ui.small(language.text("Kısayollar yalnızca pakette tanımlı uygulama EXE'sine bağlanır.", "Shortcuts are created only for the EXE declared in the package."));
                    ui.small(language.text("Var olan başka uygulama kısayollarının üzerine yazılmaz.", "Existing shortcuts are never overwritten."));
                } else {
                    ui.label(
                        language.text("Bu pakette ana EXE tanımlı olmadığı için uygulama kısayolu oluşturulamaz.", "No main EXE is defined in the package, so application shortcuts are unavailable."),
                    );
                    ui.add_space(10.0);
                    ui.small(language.text("Yeni paket oluştururken Studio / 02 Uygulama dosyaları bölümünden ana EXE seçebilirsiniz.", "Choose the main EXE in Studio / 02 Application files when building a new package."));
                }
            }
            WizardStep::Review => {
                ui.label(language.text("Dosyalar henüz kurulmadı. Seçimlerinizi kontrol edin.", "No files have been installed. Review your choices."));
                ui.add_space(12.0);
                egui::Grid::new("local_setup_review")
                    .num_columns(2)
                    .spacing([14.0, 12.0])
                    .show(ui, |ui| {
                        ui.strong(language.text("Uygulama", "Application"));
                        ui.label(&self.package_label);
                        ui.end_row();

                        ui.strong(language.text("Kurulum kapsamı", "Installation scope"));
                        ui.label(self.scope.label());
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
                ui.small(language.text("Kurulumu başlat düğmesiyle onay verdiğinizde dosyalar hazırlanır.", "Files will be prepared after you confirm with Install."));
                ui.small(language.text("Mevcut klasörler değiştirilmez; yeni klasör gereklidir.", "A new destination folder is required; existing folders are not changed."));
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
                                language.text("Dosyalar kuruldu; ek işlem uyarısı var", "Files installed with additional warnings")
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
                            ui.heading(language.text("Kurulum tamamlanamadı", "Installation could not finish"));
                            ui.add_space(12.0);
                            ui.colored_label(egui::Color32::RED, message);
                            ui.add_space(12.0);
                            ui.label(language.text("Kurulum konumunu değiştirip yeniden deneyebilirsiniz.", "Change the destination folder and try again."));
                        }
                    }
                }
            }
        }
    }
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
            ui.heading(egui::RichText::new(self.step.heading(self.language)).size(25.0).strong());
            ui.label(egui::RichText::new(&self.package_label).size(15.0));
            if let Some((index, total)) = self.step.decision_index(self.entry_point.is_some()) {
                ui.small(format!("{} {index}/{total}",
                    self.language.text("Hazırlık adımı", "Preparation step")
                ));
            }
            ui.add_space(10.0);
            ui.separator();
        });

        egui::TopBottomPanel::bottom("local_setup_footer").show(ctx, |ui| {
            ui.separator();
            ui.add_space(10.0);
            // In right-to-left layout the primary action is anchored to
            // the bottom-right edge; back remains to its left.
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                if self.busy.is_some() {
                    ui.label(self.language.text("Kurulum devam ediyor…", "Installation in progress…"));
                    return;
                }
                let primary = egui::Color32::from_rgb(6, 123, 134);
                match self.step {
                    WizardStep::Welcome | WizardStep::Destination | WizardStep::Shortcuts => {
                        if ui
                            .add_sized(
                                [110.0, 38.0],
                                egui::Button::new(
                                    egui::RichText::new(language.text("İleri  ›", "Next  ›")).color(egui::Color32::WHITE),
                                )
                                .fill(primary),
                            )
                            .clicked()
                        {
                            self.advance();
                        }
                        if ui
                            .add_sized([95.0, 38.0], egui::Button::new(language.text("İptal", "Cancel")))
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if self.step.previous(self.entry_point.is_some()).is_some()
                            && ui
                                .add_sized([95.0, 38.0], egui::Button::new(language.text("‹  Geri", "‹  Back")))
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
                                    egui::RichText::new(language.text("Kurulumu başlat", "Install"))
                                        .color(egui::Color32::WHITE),
                                )
                                .fill(primary),
                            )
                            .clicked()
                        {
                            self.begin_install();
                        }
                        if ui
                            .add_sized([95.0, 38.0], egui::Button::new(language.text("İptal", "Cancel")))
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if ui
                            .add_sized([95.0, 38.0], egui::Button::new(language.text("‹  Geri", "‹  Back")))
                            .clicked()
                        {
                            self.back();
                        }
                    }
                    WizardStep::Installing => {}
                    WizardStep::Finished => {
                        if ui
                            .add_sized([105.0, 38.0], egui::Button::new(language.text("Kapat", "Close")))
                            .clicked()
                        {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        if self.status.as_ref().is_some_and(Result::is_err)
                            && ui
                                .add_sized([165.0, 38.0], egui::Button::new(language.text("Seçimleri düzenle", "Change settings")))
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

pub fn run() -> Result<(), Box<dyn std::error::Error>> {
    let executable = std::env::current_exe()?;
    let app = LocalInstaller::new(executable)?;
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
    fn worker_stage_labels_are_real_operation_names() {
        assert!(phase_label(LocalInstallPhase::VerifyingPackage, WizardLanguage::Turkish).contains("doğrulanıyor"));
        assert!(phase_label(LocalInstallPhase::ExtractingPayload, WizardLanguage::Turkish).contains("envanteri"));
        assert!(phase_label(LocalInstallPhase::PublishingInstall, WizardLanguage::Turkish).contains("aktarılıyor"));
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
        app.select_scope(InstallScope::CurrentUser);
        assert_eq!(PathBuf::from(&app.target), user.join("qa.scope"));
        assert_eq!(app.step, WizardStep::Destination);
    }

    #[test]
    fn preflight_rejects_existing_or_traversal_targets_without_writes() {
        let temp = std::env::temp_dir();
        assert!(preflight_destination(&temp.to_string_lossy()).is_err());
        let unsafe_path = temp.join("..").join("should-not-install");
        assert!(preflight_destination(&unsafe_path.to_string_lossy()).is_err());
        assert!(preflight_destination("relative-path").is_err());
        let new_path = temp.join(format!("turkuaz-wizard-missing-{}", std::process::id()));
        if !new_path.exists() {
            assert!(preflight_destination(&new_path.to_string_lossy()).is_ok());
            assert!(!new_path.exists(), "preflight must be read-only");
        }
    }
}
