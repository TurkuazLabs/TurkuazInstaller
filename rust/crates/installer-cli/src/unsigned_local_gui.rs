// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/unsigned_local_gui.rs
// Amac: Rust paket motoruyla imzasiz standart Windows kurulum penceresi
// Modul - Rust GUI
// Version: 0.2.0
// Aciklama: Paket kimligini gosterir, dosyalari yalniz yeni konuma kurar
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    path::PathBuf,
    sync::mpsc::{self, Receiver, TryRecvError},
    thread,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_package_engine::{install_unsigned_local_setup, unsigned_local_identity};

use crate::windows_shortcuts::create_installed_shortcuts;

struct LocalInstaller {
    executable: PathBuf,
    target: String,
    package_label: String,
    entry_point: Option<String>,
    start_menu: bool,
    desktop: bool,
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
        let supports_shortcuts = identity.entry_point.as_ref().is_some_and(|entry| {
            std::path::Path::new(entry)
                .extension()
                .is_some_and(|ext| ext.eq_ignore_ascii_case("exe"))
        });
        Ok(Self {
            executable,
            target,
            package_label: format!("{} · v{}", identity.package_id, identity.version),
            entry_point: identity.entry_point,
            start_menu: supports_shortcuts,
            desktop: false,
            busy: None,
            status: None,
        })
    }

    fn begin_install(&mut self) {
        if self.busy.is_some() {
            return;
        }
        let target = PathBuf::from(self.target.trim());
        let executable = self.executable.clone();
        let start_menu = self.start_menu;
        let desktop = self.desktop;
        let (sender, receiver) = mpsc::channel();
        self.busy = Some(receiver);
        self.status = None;
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
                                            "\\n{} Windows kisayolu olusturuldu.",
                                            report.created.len()
                                        ));
                                    }
                                    if !report.warnings.is_empty() {
                                        warning = true;
                                        message.push_str(&format!(
                                            "\\nKisayol uyarisi: {}",
                                            report.warnings.join("; ")
                                        ));
                                    }
                                }
                                Err(error) => {
                                    warning = true;
                                    message.push_str(&format!(
                                        "\\nDosyalar kuruldu ancak kisayol olusturulamadi: {error}"
                                    ));
                                }
                            },
                            None => {
                                warning = true;
                                message.push_str(
                                    "\\nPakette ana EXE tanimi yok; kisayol olusturulamadi.",
                                );
                            }
                        }
                    }
                    (message, warning)
                });
            let _ = sender.send(result);
        });
    }
}

impl eframe::App for LocalInstaller {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        if let Some(receiver) = &self.busy {
            match receiver.try_recv() {
                Ok(result) => {
                    self.status = Some(result);
                    self.busy = None;
                }
                Err(TryRecvError::Disconnected) => {
                    self.status = Some(Err("Kurulum işi beklenmedik şekilde durdu.".to_owned()));
                    self.busy = None;
                }
                Err(TryRecvError::Empty) => ctx.request_repaint_after(Duration::from_millis(100)),
            }
        }
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.add_space(14.0);
            ui.heading("Turkuaz Standart Kurulum");
            ui.label(&self.package_label);
            ui.add_space(10.0);
            ui.label("İmzasız paket: bu kurulum yalnızca yerel dosya bütünlüğünü kontrol eder.");
            ui.label("Yayıncı kimliği, güvenli güncelleme ve rollback doğrulaması içermez.");
            ui.add_space(14.0);
            ui.label("Kurulacak klasör (mevcut klasörün üzerine yazılmaz)");
            ui.add_enabled_ui(self.busy.is_none(), |ui| {
                ui.add(egui::TextEdit::singleline(&mut self.target).desired_width(f32::INFINITY));
            });
            ui.add_space(8.0);
            if self.entry_point.is_some() {
                ui.add_enabled_ui(self.busy.is_none(), |ui| {
                    ui.checkbox(&mut self.start_menu, "Başlat menüsünde kısayol oluştur");
                    ui.checkbox(&mut self.desktop, "Masaüstünde kısayol oluştur");
                });
            } else {
                ui.small("Kısayol için Studio / 02 Uygulama dosyaları bölümünde ana EXE seçilmelidir.");
            }
            ui.add_space(8.0);
            ui.horizontal(|ui| {
                if ui
                    .add_enabled(self.busy.is_none(), egui::Button::new("Kurulumu başlat"))
                    .clicked()
                {
                    self.begin_install();
                }
                if self.busy.is_some() {
                    ui.spinner();
                    ui.label("Dosyalar güvenli biçimde hazırlanıyor…");
                }
            });
            if let Some(result) = &self.status {
                ui.add_space(16.0);
                match result {
                    Ok((message, warning)) => {
                        ui.colored_label(
                            if *warning {
                                egui::Color32::from_rgb(170, 92, 22)
                            } else {
                                egui::Color32::DARK_GREEN
                            },
                            message,
                        );
                    }
                    Err(message) => {
                        ui.colored_label(egui::Color32::RED, message);
                    }
                }
            }
        });
    }
}

pub fn run() -> Result<(), Box<dyn std::error::Error>> {
    let executable = std::env::current_exe()?;
    let app = LocalInstaller::new(executable)?;
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([660.0, 420.0])
            .with_min_inner_size([600.0, 380.0])
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
