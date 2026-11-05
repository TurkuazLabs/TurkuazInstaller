// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/windows_local_uninstall_gui.rs
// Amac: Windows yuklu uygulamalar kaydindan cagirilabilen guvenli kaldirma sihirbazi
// Modul - Rust Windows Uninstall Presentation
// Version: 0.1.1
// Aciklama: Acik onay, envanter on kontrolu ve degistirilmis dosyalari koruma
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    path::{Path, PathBuf},
    sync::mpsc::{self, Receiver, TryRecvError},
    thread,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_package_engine::uninstall_local_install;
use windows::Win32::UI::Shell::FOLDERID_ProgramFiles;

use crate::{
    windows_elevation::request_elevated_uninstall,
    windows_install_scope::{
        InstallScope, current_process_is_elevated, known_folder, validate_install_scope,
    },
    windows_local_registration::{
        LocalRegistration, cleanup_after_uninstall, finalize_uninstaller, read_registration,
    },
};

enum UninstallPhase {
    Confirm,
    Running,
    Result,
}

struct UninstallApp {
    record: LocalRegistration,
    manifest: PathBuf,
    phase: UninstallPhase,
    confirmed: bool,
    worker: Option<Receiver<Result<String, String>>>,
    result: Option<Result<String, String>>,
}

impl UninstallApp {
    fn new(manifest: PathBuf) -> Result<Self, String> {
        let record = read_registration(&manifest)?;
        if record.scope() == InstallScope::AllUsers {
            let program_files = known_folder(&FOLDERID_ProgramFiles)?;
            validate_install_scope(
                InstallScope::AllUsers,
                record.target(),
                &program_files,
                true,
            )?;
        }
        Ok(Self {
            record,
            manifest,
            phase: UninstallPhase::Confirm,
            confirmed: false,
            worker: None,
            result: None,
        })
    }

    fn begin(&mut self, context: &egui::Context) {
        if !self.confirmed || self.worker.is_some() {
            return;
        }
        if self.record.scope() == InstallScope::AllUsers && !current_process_is_elevated() {
            match request_elevated_uninstall(
                &std::env::current_exe().unwrap_or_default(),
                &self.manifest,
            ) {
                Ok(()) => context.send_viewport_cmd(egui::ViewportCommand::Close),
                Err(error) => self.result = Some(Err(error)),
            }
            return;
        }
        let record = self.record.clone();
        let manifest = self.manifest.clone();
        let (sender, receiver) = mpsc::channel();
        self.worker = Some(receiver);
        self.phase = UninstallPhase::Running;
        thread::spawn(move || {
            let result = read_registration(&manifest).and_then(|latest| {
                if latest.package_id() != record.package_id() || latest.target() != record.target()
                {
                    return Err("Kaldırma envanteri işlem sırasında değişti.".into());
                }
                if latest.scope() == InstallScope::AllUsers && !current_process_is_elevated() {
                    return Err("Tüm kullanıcılar kaldırması yönetici yetkisi gerektirir.".into());
                }
                let summary = uninstall_local_install(latest.target(), latest.package_id())
                    .map_err(|e| format!("Dosyalar güvenli biçimde kaldırılamadı: {e}"))?;
                let cleanup = cleanup_after_uninstall(&latest);
                let mut warnings = cleanup.warnings;
                if cleanup.registry_removed {
                    warnings.extend(finalize_uninstaller(&latest, &manifest));
                }
                let mut message = format!(
                    "{}: {} uygulama dosyası kaldırıldı.",
                    latest.package_id(),
                    summary.removed_files
                );
                if summary.retained_unknown_files {
                    message.push_str("\nKullanıcıya ait veya tanınmayan diğer dosyalar korundu.");
                }
                if !warnings.is_empty() {
                    message.push_str("\nWindows bütünleşmesi uyarıları: ");
                    message.push_str(&warnings.join("; "));
                }
                Ok(message)
            });
            let _ = sender.send(result);
        });
    }

    fn poll(&mut self, context: &egui::Context) {
        let Some(receiver) = &self.worker else {
            return;
        };
        match receiver.try_recv() {
            Ok(result) => {
                self.result = Some(result);
                self.worker = None;
                self.phase = UninstallPhase::Result;
            }
            Err(TryRecvError::Disconnected) => {
                self.result = Some(Err("Kaldırma işlemi beklenmedik biçimde durdu.".into()));
                self.worker = None;
                self.phase = UninstallPhase::Result;
            }
            Err(TryRecvError::Empty) => {
                context.request_repaint_after(Duration::from_millis(100));
            }
        }
    }
}

impl eframe::App for UninstallApp {
    fn update(&mut self, context: &egui::Context, _frame: &mut eframe::Frame) {
        self.poll(context);
        if self.worker.is_some() && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        egui::TopBottomPanel::top("uninstall_header").show(context, |ui| {
            ui.add_space(12.0);
            ui.heading(egui::RichText::new("Turkuaz Uygulama Kaldırma").size(24.0));
            ui.label(format!(
                "{} · {}",
                self.record.package_id(),
                self.record.display_version()
            ));
            ui.add_space(8.0);
            ui.separator();
        });
        egui::TopBottomPanel::bottom("uninstall_actions").show(context, |ui| {
            ui.separator();
            ui.add_space(9.0);
            ui.with_layout(
                egui::Layout::right_to_left(egui::Align::Center),
                |ui| match self.phase {
                    UninstallPhase::Confirm => {
                        if ui
                            .add_enabled(
                                self.confirmed,
                                egui::Button::new("Uygulamayı kaldır")
                                    .min_size(egui::vec2(175.0, 40.0)),
                            )
                            .clicked()
                        {
                            self.begin(context);
                        }
                        if ui
                            .add_sized([95.0, 40.0], egui::Button::new("Vazgeç"))
                            .clicked()
                        {
                            context.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                    }
                    UninstallPhase::Running => {
                        ui.label("Güvenli kaldırma sürüyor…");
                    }
                    UninstallPhase::Result => {
                        if ui
                            .add_sized([110.0, 40.0], egui::Button::new("Kapat"))
                            .clicked()
                        {
                            context.send_viewport_cmd(egui::ViewportCommand::Close);
                        }
                    }
                },
            );
        });
        egui::CentralPanel::default().show(context, |ui| {
            ui.add_space(16.0);
            match self.phase {
                UninstallPhase::Confirm => {
                    ui.strong("Kaldırılacak uygulama:");
                    ui.label(self.record.package_id());
                    ui.add_space(12.0);
                    ui.strong("Kurulum konumu:");
                    ui.label(self.record.target().display().to_string());
                    ui.add_space(12.0);
                    ui.label("Yalnızca kurulum envanterinde kayıtlı ve değiştirilmemiş uygulama dosyaları kaldırılır.");
                    ui.label("Kullanıcı dosyaları, değiştirilmiş dosyalar ve yabancı kısayollar korunur.");
                    if self.record.scope() == InstallScope::AllUsers {
                        ui.colored_label(
                            egui::Color32::from_rgb(180, 105, 30),
                            "Tüm kullanıcılar kurulumu Windows yönetici onayı gerektirir.",
                        );
                    }
                    ui.add_space(16.0);
                    ui.checkbox(&mut self.confirmed, "Bu uygulamayı kaldırmayı onaylıyorum.");
                    if let Some(Err(error)) = &self.result {
                        ui.colored_label(egui::Color32::RED, error);
                    }
                }
                UninstallPhase::Running => {
                    ui.horizontal(|ui| {
                        ui.spinner();
                        ui.label("Dosyalar ve kısayol sahipliği doğrulanıyor, uygulama kaldırılıyor…");
                    });
                }
                UninstallPhase::Result => {
                    match &self.result {
                        Some(Ok(message)) => {
                            ui.heading("Kaldırma tamamlandı");
                            ui.add_space(12.0);
                            ui.label(message);
                        }
                        Some(Err(message)) => {
                            ui.heading("Kaldırma tamamlanamadı");
                            ui.add_space(12.0);
                            ui.colored_label(egui::Color32::RED, message);
                            ui.label("Değişmiş uygulama dosyaları ve mevcut veriler korunmuştur.");
                        }
                        None => {}
                    }
                }
            }
        });
    }
}

/// @dev-id BRK-WIN-UNINSTALL-GUI-001
/// @purpose Dedicated removal UI invoked from registered Windows Apps
/// entry, never from the signed installation lifecycle.
pub(super) fn run() -> Result<(), Box<dyn std::error::Error>> {
    let args = std::env::args_os().collect::<Vec<_>>();
    if args.len() != 4 || args[1] != "uninstall-local-gui" || args[2] != "--registration" {
        return Err("Kaldırıcı parametreleri eksik veya geçersiz.".into());
    }
    let manifest = Path::new(&args[3]).to_path_buf();
    let app = UninstallApp::new(manifest)?;
    eframe::run_native(
        "Turkuaz Uygulama Kaldırma",
        eframe::NativeOptions {
            viewport: egui::ViewportBuilder::default()
                .with_inner_size([760.0, 490.0])
                .with_min_inner_size([680.0, 430.0]),
            ..Default::default()
        },
        Box::new(move |_| Ok(Box::new(app))),
    )?;
    Ok(())
}
