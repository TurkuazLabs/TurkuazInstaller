// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/unsigned_local_gui.rs
// Amac: Rust paket motoruyla imzasiz standart Windows kurulum penceresi
// Modul - Rust GUI
// Version: 0.1.0
// Aciklama: Paket kimligini gosterir, dosyalari yalniz yeni konuma kurar
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    path::PathBuf,
    sync::mpsc::{self, Receiver, TryRecvError},
    thread,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_package_engine::{
    install_unsigned_local_setup, unsigned_local_identity,
};

struct LocalInstaller {
    executable: PathBuf,
    target: String,
    package_label: String,
    busy: Option<Receiver<Result<String, String>>>,
    status: Option<Result<String, String>>,
}

impl LocalInstaller {
    fn new(executable: PathBuf) -> Result<Self, String> {
        let identity = unsigned_local_identity(&executable)
            .map_err(|e| format!("Paket okunamadı: {e}"))?;
        let app_data = std::env::var_os("LOCALAPPDATA")
            .map(PathBuf::from)
            .ok_or("Windows kullanıcı LocalAppData yolu bulunamadı.")?;
        let target = app_data
            .join("Programs")
            .join(&identity.package_id)
            .to_string_lossy()
            .into_owned();
        Ok(Self {
            executable,
            target,
            package_label: format!("{} · v{}", identity.package_id, identity.version),
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
        let (sender, receiver) = mpsc::channel();
        self.busy = Some(receiver);
        self.status = None;
        thread::spawn(move || {
            let result = install_unsigned_local_setup(executable, &target)
                .map(|identity| format!("{} v{} kuruldu: {}", identity.package_id, identity.version, target.display()))
                .map_err(|error| error.to_string());
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
                ui.add(
                    egui::TextEdit::singleline(&mut self.target)
                        .desired_width(f32::INFINITY),
                );
            });
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
                    Ok(message) => { ui.colored_label(egui::Color32::DARK_GREEN, message); }
                    Err(message) => { ui.colored_label(egui::Color32::RED, message); }
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
            .with_inner_size([620.0, 320.0])
            .with_min_inner_size([560.0, 300.0])
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
