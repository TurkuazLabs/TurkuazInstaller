// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_gui.rs
// Amac: Self-extracting Turkuaz setup icin Windows masaustu kurulum sihirbazini sunmak
// Modul - Rust GUI Module
// Version: 0.1.0
// Aciklama: Kullanici girdilerini alir, mevcut guvenli CLI kurulum motorunu arka planda calistirir
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    path::PathBuf,
    process::{Command, Stdio},
    sync::mpsc::{self, Receiver, Sender, TryRecvError},
    thread,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_domain::package_id::PackageId;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum SetupOperation {
    Install,
    Update,
    Repair,
    Rollback,
}

impl SetupOperation {
    const ALL: [Self; 4] = [Self::Install, Self::Update, Self::Repair, Self::Rollback];

    const fn command_value(self) -> &'static str {
        match self {
            Self::Install => "install",
            Self::Update => "update",
            Self::Repair => "repair",
            Self::Rollback => "rollback",
        }
    }

    const fn label(self) -> &'static str {
        match self {
            Self::Install => "Kur",
            Self::Update => "Guncelle",
            Self::Repair => "Onar",
            Self::Rollback => "Onceki surume don",
        }
    }
}

struct SetupApp {
    package_id: String,
    trust_store: String,
    target: String,
    data_root: String,
    operation: SetupOperation,
    result_receiver: Option<Receiver<Result<String, String>>>,
    cancel_sender: Option<Sender<()>>,
    status: Option<(bool, String)>,
    cancel_requested: bool,
}

impl Default for SetupApp {
    fn default() -> Self {
        let local_app_data = std::env::var_os("LOCALAPPDATA")
            .map(PathBuf::from)
            .unwrap_or_else(|| std::env::current_dir().unwrap_or_else(|_| PathBuf::from(".")));
        let executable = std::env::current_exe().ok();
        let trust_store = executable
            .as_ref()
            .and_then(|path| path.parent())
            .map(|parent| parent.join("manifest-trust.yaml"))
            .unwrap_or_else(|| PathBuf::from("manifest-trust.yaml"));

        Self {
            package_id: String::new(),
            trust_store: trust_store.to_string_lossy().into_owned(),
            target: local_app_data
                .join("Programs")
                .join("TurkuazLabs")
                .join("TurkuazInstaller")
                .to_string_lossy()
                .into_owned(),
            data_root: local_app_data
                .join("TurkuazInstaller")
                .to_string_lossy()
                .into_owned(),
            operation: SetupOperation::Install,
            result_receiver: None,
            cancel_sender: None,
            status: None,
            cancel_requested: false,
        }
    }
}

pub fn run() -> eframe::Result {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([680.0, 560.0])
            .with_min_inner_size([620.0, 500.0])
            .with_title("Turkuaz Installer"),
        ..Default::default()
    };
    eframe::run_native(
        "Turkuaz Installer",
        options,
        Box::new(|_creation_context| Ok(Box::<SetupApp>::default())),
    )
}

impl eframe::App for SetupApp {
    fn update(&mut self, context: &egui::Context, _frame: &mut eframe::Frame) {
        self.poll_worker();
        let is_working = self.result_receiver.is_some();

        if is_working && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut start_requested = false;
        egui::CentralPanel::default().show(context, |ui| {
            ui.heading("Turkuaz Installer");
            ui.label("Kurulum paketi imza ve butunluk kontrollerinden gecirilir.");
            ui.add_space(12.0);

            ui.add_enabled_ui(!is_working, |ui| {
                ui.horizontal(|ui| {
                    ui.label("Paket kimligi");
                    ui.text_edit_singleline(&mut self.package_id);
                });
                ui.label("Paket kimligini yayincidan aldiginiz release bilgisiyle eslestirin.");
                ui.add_space(10.0);

                ui.horizontal(|ui| {
                    ui.label("Yayinci guven politikasi");
                    ui.text_edit_singleline(&mut self.trust_store);
                    if ui.button("Dosya sec").clicked() {
                        if let Some(path) = rfd::FileDialog::new()
                            .add_filter("YAML", &["yaml", "yml"])
                            .set_title("Yayinci guven politikasini secin")
                            .pick_file()
                        {
                            self.trust_store = path.to_string_lossy().into_owned();
                        }
                    }
                });
                ui.label("Yayincinin sagladigi gercek trust dosyasini kullanin; ornek dosya guvenilir degildir.");
                ui.add_space(10.0);

                ui.horizontal(|ui| {
                    ui.label("Kurulum klasoru");
                    ui.text_edit_singleline(&mut self.target);
                    if ui.button("Klasor sec").clicked() {
                        let mut picker = rfd::FileDialog::new().set_title("Kurulum klasorunu secin");
                        if PathBuf::from(&self.target).is_dir() {
                            picker = picker.set_directory(&self.target);
                        }
                        if let Some(path) = picker.pick_folder() {
                            self.target = path.to_string_lossy().into_owned();
                        }
                    }
                });
                ui.add_space(10.0);

                ui.horizontal(|ui| {
                    ui.label("Veri klasoru");
                    ui.text_edit_singleline(&mut self.data_root);
                    if ui.button("Klasor sec").clicked() {
                        let mut picker = rfd::FileDialog::new().set_title("Installer veri klasorunu secin");
                        if PathBuf::from(&self.data_root).is_dir() {
                            picker = picker.set_directory(&self.data_root);
                        }
                        if let Some(path) = picker.pick_folder() {
                            self.data_root = path.to_string_lossy().into_owned();
                        }
                    }
                });
                ui.add_space(10.0);

                egui::ComboBox::from_label("Islem")
                    .selected_text(self.operation.label())
                    .show_ui(ui, |ui| {
                        for operation in SetupOperation::ALL {
                            ui.selectable_value(
                                &mut self.operation,
                                operation,
                                operation.label(),
                            );
                        }
                    });
                ui.add_space(14.0);

                if ui.button(format!("{} ve dogrula", self.operation.label())).clicked() {
                    start_requested = true;
                }
            });

            ui.add_space(12.0);
            if is_working {
                ui.horizontal(|ui| {
                    ui.spinner();
                    ui.label("Kurulum suruyor; release imzasi ve paket butunlugu denetleniyor.");
                });
                ui.label("Iptal edilirse backend durdurulur ve recovery otomatik denenir; durum belirsiz kalirsa manuel recovery gerekir.");
                if ui.button("Kurulumu iptal et").clicked() {
                    self.request_cancel();
                }
                if self.cancel_requested {
                    ui.label("Iptal istegi gonderildi; kurulum islemi sonlandiriliyor.");
                }
                context.request_repaint_after(Duration::from_millis(100));
            }

            if let Some((succeeded, message)) = &self.status {
                ui.add_space(8.0);
                ui.separator();
                ui.colored_label(
                    if *succeeded {
                        egui::Color32::from_rgb(30, 130, 70)
                    } else {
                        egui::Color32::from_rgb(180, 45, 45)
                    },
                    message,
                );
            }
        });

        if start_requested {
            self.start_install();
        }
    }
}

impl SetupApp {
    fn start_install(&mut self) {
        if let Err(error) = PackageId::parse(self.package_id.trim()) {
            self.status = Some((false, format!("Paket kimligi gecersiz: {error}")));
            return;
        }
        if self.trust_store.trim().is_empty()
            || self.target.trim().is_empty()
            || self.data_root.trim().is_empty()
        {
            self.status = Some((
                false,
                "Guven politikasi ve klasor alanlari zorunludur.".to_owned(),
            ));
            return;
        }

        let package_id = self.package_id.trim().to_owned();
        let trust_store = self.trust_store.trim().to_owned();
        let target = self.target.trim().to_owned();
        let data_root = self.data_root.trim().to_owned();
        let operation = self.operation.command_value().to_owned();
        let (result_sender, result_receiver) = mpsc::channel();
        let (cancel_sender, cancel_receiver) = mpsc::channel();
        self.result_receiver = Some(result_receiver);
        self.cancel_sender = Some(cancel_sender);
        self.cancel_requested = false;
        self.status = None;

        thread::spawn(move || {
            run_installer_worker(
                package_id,
                trust_store,
                target,
                data_root,
                operation,
                result_sender,
                cancel_receiver,
            );
        });
    }

    fn request_cancel(&mut self) {
        if let Some(sender) = &self.cancel_sender {
            let _ = sender.send(());
            self.cancel_requested = true;
        }
    }

    fn poll_worker(&mut self) {
        let result = match self.result_receiver.as_ref() {
            Some(receiver) => match receiver.try_recv() {
                Ok(result) => Some(result),
                Err(TryRecvError::Empty) => None,
                Err(TryRecvError::Disconnected) => Some(Err(
                    "Kurulum islemi beklenmedik sekilde sonlandi.".to_owned(),
                )),
            },
            None => None,
        };
        if let Some(result) = result {
            self.result_receiver = None;
            self.cancel_sender = None;
            self.cancel_requested = false;
            self.status = Some(match result {
                Ok(message) => (true, message),
                Err(message) => (false, message),
            });
        }
    }
}

fn run_installer_worker(
    package_id: String,
    trust_store: String,
    target: String,
    data_root: String,
    operation: String,
    result_sender: Sender<Result<String, String>>,
    cancel_receiver: Receiver<()>,
) {
    let executable = match std::env::current_exe() {
        Ok(path) => path,
        Err(error) => {
            let _ = result_sender.send(Err(format!(
                "Setup executable path could not be read: {error}"
            )));
            return;
        }
    };
    let recovery_executable = executable.clone();
    let recovery_package_id = package_id.clone();
    let recovery_target = target.clone();
    let recovery_data_root = data_root.clone();
    let mut child = match Command::new(executable)
        .arg("install-self")
        .arg("--package-id")
        .arg(&package_id)
        .arg("--trust-store")
        .arg(&trust_store)
        .arg("--target")
        .arg(&target)
        .arg("--data-root")
        .arg(&data_root)
        .arg("--operation")
        .arg(&operation)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
    {
        Ok(child) => child,
        Err(error) => {
            let _ = result_sender.send(Err(format!("Setup backend could not start: {error}")));
            return;
        }
    };

    loop {
        match child.try_wait() {
            Ok(Some(_)) => break,
            Ok(None) => {}
            Err(error) => {
                let _ = child.kill();
                let _ = child.wait_with_output();
                let _ = result_sender.send(Err(format!("Kurulum durumu okunamadi: {error}")));
                return;
            }
        }

        match cancel_receiver.try_recv() {
            Ok(()) | Err(TryRecvError::Disconnected) => {
                match child.try_wait() {
                    Ok(Some(_)) => break,
                    Ok(None) => {}
                    Err(error) => {
                        let _ = child.kill();
                        let _ = child.wait_with_output();
                        let _ = result_sender.send(Err(format!(
                            "Iptal sirasinda kurulum durumu okunamadi: {error}"
                        )));
                        return;
                    }
                }

                let _ = child.kill();
                let _ = child.wait_with_output();
                let recovery = Command::new(recovery_executable)
                    .arg("recover")
                    .arg("--package-id")
                    .arg(&recovery_package_id)
                    .arg("--target")
                    .arg(&recovery_target)
                    .arg("--data-root")
                    .arg(&recovery_data_root)
                    .stdout(Stdio::piped())
                    .stderr(Stdio::piped())
                    .output();
                let message = match recovery {
                    Ok(output) if output.status.success() => {
                        let detail = String::from_utf8_lossy(&output.stdout)
                            .lines()
                            .last()
                            .filter(|line| !line.trim().is_empty())
                            .unwrap_or("Kurulum durumu uzlastirildi.")
                            .to_owned();
                        format!("Kurulum iptal edildi. {detail}")
                    }
                    Ok(output) => {
                        let detail = String::from_utf8_lossy(&output.stderr)
                            .lines()
                            .last()
                            .filter(|line| !line.trim().is_empty())
                            .unwrap_or("Otomatik recovery tamamlanamadi.")
                            .to_owned();
                        format!(
                            "Kurulum iptal edildi; durum belirsiz. Manuel recovery gerekli: {detail}"
                        )
                    }
                    Err(error) => format!("Kurulum iptal edildi; recovery baslatilamadi: {error}"),
                };
                let _ = result_sender.send(Err(message));
                return;
            }
            Err(TryRecvError::Empty) => {}
        }

        thread::sleep(Duration::from_millis(150));
    }

    match child.wait_with_output() {
        Ok(output) if output.status.success() => {
            let message = String::from_utf8_lossy(&output.stdout)
                .lines()
                .last()
                .filter(|line| !line.trim().is_empty())
                .unwrap_or("Kurulum tamamlandi.")
                .to_owned();
            let _ = result_sender.send(Ok(message));
        }
        Ok(output) => {
            let stderr = String::from_utf8_lossy(&output.stderr);
            let stdout = String::from_utf8_lossy(&output.stdout);
            let detail = stderr
                .lines()
                .last()
                .filter(|line| !line.trim().is_empty())
                .or_else(|| stdout.lines().last())
                .unwrap_or("Kurulum basarisiz oldu.");
            let _ = result_sender.send(Err(detail.to_owned()));
        }
        Err(error) => {
            let _ = result_sender.send(Err(format!("Kurulum sonucu okunamadi: {error}")));
        }
    }
}
