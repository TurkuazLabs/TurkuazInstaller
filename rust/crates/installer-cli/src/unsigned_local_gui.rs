// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/unsigned_local_gui.rs
// Amac: Rust yerel standart kurulumunu cok asamali Windows sihirbazi ile sunmak
// Modul - Rust GUI
// Version: 0.3.0
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
use turkuaz_installer_package_engine::{install_unsigned_local_setup, unsigned_local_identity};

use crate::windows_shortcuts::create_installed_shortcuts;

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
    fn next(self) -> Option<Self> {
        match self {
            Self::Welcome => Some(Self::Destination),
            Self::Destination => Some(Self::Shortcuts),
            Self::Shortcuts => Some(Self::Review),
            Self::Review | Self::Installing | Self::Finished => None,
        }
    }

    fn previous(self) -> Option<Self> {
        match self {
            Self::Destination => Some(Self::Welcome),
            Self::Shortcuts => Some(Self::Destination),
            Self::Review => Some(Self::Shortcuts),
            Self::Welcome | Self::Installing | Self::Finished => None,
        }
    }

    fn heading(self) -> &'static str {
        match self {
            Self::Welcome => "Hoş geldiniz",
            Self::Destination => "Kurulum konumu",
            Self::Shortcuts => "Ek seçenekler",
            Self::Review => "Kuruluma hazır",
            Self::Installing => "Uygulama kuruluyor",
            Self::Finished => "Kurulum sonucu",
        }
    }

    fn decision_index(self) -> Option<usize> {
        match self {
            Self::Welcome => Some(1),
            Self::Destination => Some(2),
            Self::Shortcuts => Some(3),
            Self::Review => Some(4),
            Self::Installing | Self::Finished => None,
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

struct LocalInstaller {
    executable: PathBuf,
    package_id: String,
    package_label: String,
    target: String,
    entry_point: Option<String>,
    start_menu: bool,
    desktop: bool,
    step: WizardStep,
    path_error: Option<String>,
    busy: Option<Receiver<Result<(String, bool), String>>>,
    status: Option<Result<(String, bool), String>>,
}

impl LocalInstaller {
    fn new(executable: PathBuf) -> Result<Self, String> {
        let identity =
            unsigned_local_identity(&executable).map_err(|e| format!("Paket okunamadı: {e}"))?;
        let app_data = std::env::var_os("LOCALAPPDATA")
            .map(PathBuf::from)
            .ok_or("Windows kullanıcı LocalAppData yolu bulunamadı.")?;
        let target = app_data
            .join("Programs")
            .join(&identity.package_id)
            .to_string_lossy()
            .into_owned();

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
            entry_point,
            start_menu: supports_shortcuts,
            desktop: false,
            step: WizardStep::Welcome,
            path_error: None,
            busy: None,
            status: None,
        })
    }

    fn advance(&mut self) {
        if self.busy.is_some() {
            return;
        }
        if self.step == WizardStep::Destination {
            match preflight_destination(&self.target) {
                Ok(_) => self.path_error = None,
                Err(error) => {
                    self.path_error = Some(error);
                    return;
                }
            }
        }
        if let Some(next) = self.step.next() {
            self.step = next;
        }
    }

    fn back(&mut self) {
        if self.busy.is_none() {
            if let Some(previous) = self.step.previous() {
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
        let target = match preflight_destination(&self.target) {
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
        let (sender, receiver) = mpsc::channel();
        self.busy = Some(receiver);
        self.status = None;
        self.path_error = None;
        self.step = WizardStep::Installing;

        thread::spawn(move || {
            let result = install_unsigned_local_setup(executable, &target)
                .map_err(|error| error.to_string())
                .map(|identity| {
                    let mut message = format!(
                        "{} v{} kuruldu: {}",
                        identity.package_id,
                        identity.version,
                        target.display()
                    );
                    let mut warning = false;
                    if start_menu || desktop {
                        match identity.entry_point.as_deref() {
                            Some(entry) => match create_installed_shortcuts(
                                &target,
                                entry,
                                &identity.package_id,
                                start_menu,
                                desktop,
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
                                message.push_str(
                                    "\nPakette ana EXE tanımı yok; kısayol oluşturulamadı.",
                                );
                            }
                        }
                    }
                    (message, warning)
                });
            let _ = sender.send(result);
        });
    }

    fn poll(&mut self, ctx: &egui::Context) {
        if let Some(receiver) = &self.busy {
            match receiver.try_recv() {
                Ok(result) => {
                    self.status = Some(result);
                    self.busy = None;
                    self.step = WizardStep::Finished;
                }
                Err(TryRecvError::Disconnected) => {
                    self.status = Some(Err("Kurulum işi beklenmedik şekilde durdu.".to_owned()));
                    self.busy = None;
                    self.step = WizardStep::Finished;
                }
                Err(TryRecvError::Empty) => ctx.request_repaint_after(Duration::from_millis(100)),
            }
        }
    }

    fn page(&mut self, ui: &mut egui::Ui) {
        match self.step {
            WizardStep::Welcome => {
                ui.heading("Turkuaz Standart Kurulum");
                ui.add_space(12.0);
                ui.label(format!("Kurulacak paket: {}", self.package_label));
                ui.add_space(8.0);
                ui.label(
                    "Bu sihirbaz kurulum klasörünü ve Windows kısayol tercihlerini seçmenize yardımcı olur.",
                );
                ui.add_space(12.0);
                ui.colored_label(
                    egui::Color32::from_rgb(170, 95, 32),
                    "Bu imzasız yerel paket yalnızca dosya bütünlüğünü denetler; yayıncı kimliğini doğrulamaz.",
                );
                ui.small("Güvenli güncelleme ve geri alma için imzalı dağıtım akışı ayrı tutulur.");
            }
            WizardStep::Destination => {
                ui.label("Uygulamanın kurulacağı yeni klasörü seçin.");
                ui.add_space(12.0);
                ui.horizontal(|ui| {
                    ui.add(
                        egui::TextEdit::singleline(&mut self.target)
                            .desired_width((ui.available_width() - 94.0).max(240.0)),
                    );
                    if ui.button("Gözat…").clicked() {
                        if let Some(parent) = rfd::FileDialog::new().pick_folder() {
                            self.target =
                                parent.join(&self.package_id).to_string_lossy().into_owned();
                        }
                    }
                });
                ui.small("Gözat seçilen klasörün içine uygulama kimliğiyle yeni bir klasör ekler.");
                ui.small("Mevcut bir klasörün üzerine yazılmaz; eski kurulum korunur.");
                if let Some(error) = &self.path_error {
                    ui.add_space(10.0);
                    ui.colored_label(egui::Color32::RED, error);
                }
            }
            WizardStep::Shortcuts => {
                if self.entry_point.is_some() {
                    ui.label("Kurulumdan sonra oluşturulacak Windows kısayollarını seçin.");
                    ui.add_space(12.0);
                    ui.checkbox(&mut self.start_menu, "Başlat menüsünde kısayol oluştur");
                    ui.checkbox(&mut self.desktop, "Masaüstünde kısayol oluştur");
                    ui.add_space(12.0);
                    ui.small("Kısayollar yalnızca pakette tanımlı uygulama EXE'sine bağlanır.");
                    ui.small("Var olan başka uygulama kısayollarının üzerine yazılmaz.");
                } else {
                    ui.label(
                        "Bu pakette ana EXE tanımlı olmadığı için uygulama kısayolu oluşturulamaz.",
                    );
                    ui.add_space(10.0);
                    ui.small("Yeni paket oluştururken Studio / 02 Uygulama dosyaları bölümünden ana EXE seçebilirsiniz.");
                }
            }
            WizardStep::Review => {
                ui.label("Dosyalar henüz kurulmadı. Seçimlerinizi kontrol edin.");
                ui.add_space(12.0);
                egui::Grid::new("local_setup_review")
                    .num_columns(2)
                    .spacing([14.0, 12.0])
                    .show(ui, |ui| {
                        ui.strong("Uygulama");
                        ui.label(&self.package_label);
                        ui.end_row();

                        ui.strong("Kurulum klasörü");
                        ui.add(egui::Label::new(&self.target).wrap());
                        ui.end_row();

                        ui.strong("Başlat menüsü");
                        ui.label(if self.entry_point.is_some() && self.start_menu {
                            "Kısayol oluşturulacak"
                        } else {
                            "Oluşturulmayacak"
                        });
                        ui.end_row();

                        ui.strong("Masaüstü");
                        ui.label(if self.entry_point.is_some() && self.desktop {
                            "Kısayol oluşturulacak"
                        } else {
                            "Oluşturulmayacak"
                        });
                        ui.end_row();
                    });
                ui.add_space(16.0);
                ui.small("Kurulumu başlat düğmesiyle onay verdiğinizde dosyalar hazırlanır.");
                ui.small("Mevcut klasörler değiştirilmez; yeni klasör gereklidir.");
            }
            WizardStep::Installing => {
                ui.horizontal(|ui| {
                    ui.spinner();
                    ui.strong("Paket doğrulanıyor ve uygulama dosyaları kuruluyor…");
                });
                ui.add_space(12.0);
                ui.label("İşlem, mevcut Rust kurulum motoruyla arka planda yürütülüyor.");
                ui.small(
                    "Motor dosya bazlı ilerleme yayımlamadığı için yanıltıcı bir yüzde gösterilmez.",
                );
                ui.small(
                    "Kurulum sırasında güvenli iptal henüz desteklenmiyor. İşlem tamamlanana kadar pencere açık kalır.",
                );
            }
            WizardStep::Finished => {
                if let Some(result) = &self.status {
                    match result {
                        Ok((message, warning)) => {
                            ui.heading(if *warning {
                                "Dosyalar kuruldu; ek işlem uyarısı var"
                            } else {
                                "Kurulum tamamlandı"
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
                                "Bu imzasız paket için güvenli güncelleme/rollback desteği yoktur.",
                            );
                        }
                        Err(message) => {
                            ui.heading("Kurulum tamamlanamadı");
                            ui.add_space(12.0);
                            ui.colored_label(egui::Color32::RED, message);
                            ui.add_space(12.0);
                            ui.label("Kurulum konumunu değiştirip yeniden deneyebilirsiniz.");
                        }
                    }
                }
            }
        }
    }
}

impl eframe::App for LocalInstaller {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.poll(ctx);
        if self.busy.is_some() && ctx.input(|input| input.viewport().close_requested()) {
            ctx.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        egui::TopBottomPanel::top("local_setup_header").show(ctx, |ui| {
            ui.add_space(12.0);
            ui.heading(self.step.heading());
            ui.label(&self.package_label);
            if let Some(index) = self.step.decision_index() {
                ui.small(format!("Hazırlık adımı {index}/4"));
            }
            ui.add_space(10.0);
            ui.separator();
        });

        egui::CentralPanel::default().show(ctx, |ui| {
            ui.add_space(22.0);
            self.page(ui);
        });

        egui::TopBottomPanel::bottom("local_setup_footer").show(ctx, |ui| {
            ui.separator();
            ui.add_space(10.0);
            ui.horizontal(|ui| {
                if self.busy.is_some() {
                    ui.label("Kurulum devam ediyor…");
                    return;
                }

                match self.step {
                    WizardStep::Welcome | WizardStep::Destination | WizardStep::Shortcuts => {
                        if self.step.previous().is_some() && ui.button("Geri").clicked() {
                            self.back();
                        }
                        if ui.button("İptal").clicked() {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        ui.add_space(14.0);
                        if ui.button("İleri").clicked() {
                            self.advance();
                        }
                    }
                    WizardStep::Review => {
                        if ui.button("Geri").clicked() {
                            self.back();
                        }
                        if ui.button("İptal").clicked() {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                        ui.add_space(14.0);
                        if ui.button("Kurulumu başlat").clicked() {
                            self.begin_install();
                        }
                    }
                    WizardStep::Installing => {}
                    WizardStep::Finished => {
                        if self.status.as_ref().is_some_and(Result::is_err)
                            && ui.button("Seçimleri düzenle").clicked()
                        {
                            self.step = WizardStep::Destination;
                            self.status = None;
                            self.path_error = None;
                        }
                        if ui.button("Kapat").clicked() {
                            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                    }
                }
            });
            ui.add_space(10.0);
        });
    }
}

pub fn run() -> Result<(), Box<dyn std::error::Error>> {
    let executable = std::env::current_exe()?;
    let app = LocalInstaller::new(executable)?;
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([740.0, 480.0])
            .with_min_inner_size([640.0, 400.0])
            .with_title("Turkuaz Standart Kurulum"),
        ..Default::default()
    };
    eframe::run_native(
        "Turkuaz Standart Kurulum",
        options,
        Box::new(move |_ctx| Ok(Box::new(app))),
    )?;
    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn wizard_requires_explicit_install_after_review() {
        let mut step = WizardStep::Welcome;
        assert_eq!(step.next(), Some(WizardStep::Destination));
        step = step.next().unwrap();
        assert_eq!(step.previous(), Some(WizardStep::Welcome));
        step = step.next().unwrap();
        assert_eq!(step, WizardStep::Shortcuts);
        step = step.next().unwrap();
        assert_eq!(step, WizardStep::Review);
        assert_eq!(step.next(), None, "next must not trigger installation");
        assert_eq!(WizardStep::Installing.previous(), None);
        assert_eq!(WizardStep::Finished.next(), None);
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
