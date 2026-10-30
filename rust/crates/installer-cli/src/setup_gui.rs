// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_gui.rs
// Amac: Self-extracting Turkuaz setup icin Windows masaustu kurulum sihirbazini sunmak
// Modul - Rust GUI Module
// Version: 0.1.7
// Aciklama: GUI'den install, update, repair, rollback ve onayli uninstall islemlerini yonetir
// Bagimli Oldugu Katman: Presentation | Application

use std::{
    io::{self, Read},
    path::PathBuf,
    process::{Command, Stdio},
    sync::mpsc::{self, Receiver, Sender, TryRecvError},
    thread,
    thread::JoinHandle,
    time::Duration,
};

use eframe::egui;
use turkuaz_installer_domain::package_id::PackageId;

const APP_TITLE: &str = concat!("Turkuaz Installer v", env!("CARGO_PKG_VERSION"));

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum SetupOperation {
    Install,
    Update,
    Repair,
    Rollback,
    Uninstall,
}

impl SetupOperation {
    const ALL: [Self; 5] = [
        Self::Install,
        Self::Update,
        Self::Repair,
        Self::Rollback,
        Self::Uninstall,
    ];

    const fn command_value(self) -> &'static str {
        match self {
            Self::Install => "install",
            Self::Update => "update",
            Self::Repair => "repair",
            Self::Rollback => "rollback",
            Self::Uninstall => "uninstall",
        }
    }

    const fn label(self) -> &'static str {
        match self {
            Self::Install => "Kur",
            Self::Update => "Güncelle",
            Self::Repair => "Onar",
            Self::Rollback => "Önceki sürüme dön",
            Self::Uninstall => "Kaldır",
        }
    }

    const fn description(self) -> &'static str {
        match self {
            Self::Install => "İmzalı bir paketi bu bilgisayara kurun.",
            Self::Update => "Kurulu paketi imzalı daha yeni bir sürüme taşıyın.",
            Self::Repair => "Kurulu sürümü temiz bir dağıtımla yeniden kurun.",
            Self::Rollback => "İmzalı yayın politikası izin veriyorsa önceki sürüme dönün.",
            Self::Uninstall => "Yalnızca bu paketin yönettiği dosyaları kaldırın.",
        }
    }

    const fn action_label(self) -> &'static str {
        match self {
            Self::Install => "Kur ve doğrula",
            Self::Update => "Güncellemeyi başlat",
            Self::Repair => "Onarımı başlat",
            Self::Rollback => "Önceki sürüme dön",
            Self::Uninstall => "Paketi kaldır",
        }
    }
}

struct SetupApp {
    step: usize,
    package_id: String,
    trust_store: String,
    target: String,
    data_root: String,
    operation: SetupOperation,
    result_receiver: Option<Receiver<Result<String, String>>>,
    cancel_sender: Option<Sender<()>>,
    status: Option<(bool, String)>,
    operation_completed: bool,
    cancel_requested: bool,
    recovery_blocked: bool,
    uninstall_confirmed: bool,
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
            step: 0,
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
            operation_completed: false,
            cancel_requested: false,
            recovery_blocked: false,
            uninstall_confirmed: false,
        }
    }
}

pub fn run_with_recovery_summary(recovered_count: usize) -> eframe::Result {
    let message = if recovered_count == 0 {
        "Başlangıçta bekleyen işlem bulunamadı.".to_owned()
    } else {
        format!("Başlangıçta {recovered_count} bekleyen işlem güvenli duruma getirildi.")
    };
    run_with_startup_notice(Some((true, message)))
}

pub fn run_with_recovery_error(message: String) -> eframe::Result {
    run_with_startup_notice(Some((false, message)))
}

fn run_with_startup_notice(notice: Option<(bool, String)>) -> eframe::Result {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([900.0, 680.0])
            .with_min_inner_size([740.0, 560.0])
            .with_title(APP_TITLE),
        ..Default::default()
    };
    eframe::run_native(
        APP_TITLE,
        options,
        Box::new(move |_creation_context| {
            let mut app = SetupApp::default();
            if let Some((succeeded, message)) = notice {
                app.recovery_blocked = !succeeded;
                app.status = Some((succeeded, message));
            }
            Ok(Box::new(app))
        }),
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
        let mut next_requested = false;
        let mut back_requested = false;
        let mut close_requested = false;

        egui::CentralPanel::default().show(context, |ui| {
            ui.add_space(4.0);
            ui.horizontal(|ui| {
                ui.label(
                    egui::RichText::new("T")
                        .size(28.0)
                        .strong()
                        .color(egui::Color32::from_rgb(18, 121, 132)),
                );
                ui.vertical(|ui| {
                    ui.heading("Turkuaz Installer");
                    ui.label("Güvenli kurulum ve sürüm yönetimi");
                });
                ui.with_layout(
                    egui::Layout::right_to_left(egui::Align::Center),
                    |ui| {
                        ui.label("Yerel kurulum sihirbazı");
                    },
                );
            });

            ui.add_space(6.0);
            ui.separator();
            ui.add_space(8.0);

            const STEPS: [&str; 4] = ["İşlem", "Paket ve güven", "Konumlar", "Son kontrol"];
            ui.horizontal_wrapped(|ui| {
                for (index, label) in STEPS.iter().enumerate() {
                    let marker = if index < self.step {
                        "✓"
                    } else if index == self.step {
                        "●"
                    } else {
                        "○"
                    };
                    let color = if index <= self.step {
                        egui::Color32::from_rgb(18, 121, 132)
                    } else {
                        ui.visuals().weak_text_color()
                    };
                    ui.colored_label(color, format!("{marker} {label}"));
                    if index + 1 < STEPS.len() {
                        ui.label("›");
                    }
                }
            });

            ui.add_space(8.0);
            if self.recovery_blocked {
                ui.group(|ui| {
                    ui.colored_label(
                        egui::Color32::from_rgb(180, 55, 45),
                        egui::RichText::new("Kurtarma gerekiyor").strong(),
                    );
                    ui.label(
                        "Güvenli durum doğrulanana kadar yeni bir işlem başlatılamaz. Günlük ve kurulum dosyalarını silmeyin.",
                    );
                });
                ui.add_space(8.0);
            }

            egui::ScrollArea::vertical()
                .auto_shrink([false, false])
                .show(ui, |ui| {
                    if is_working {
                        ui.group(|ui| {
                            ui.horizontal(|ui| {
                                ui.spinner();
                                ui.vertical(|ui| {
                                    ui.heading(format!(
                                        "{} işlemi sürüyor",
                                        self.operation.label()
                                    ));
                                    ui.label(if self.operation == SetupOperation::Uninstall {
                                        "Paket kimliği, yönetilen dosyalar ve durum kaydı denetleniyor."
                                    } else {
                                        "Paket imzası, dosya bütünlüğü ve kurulum durumu denetleniyor."
                                    });
                                });
                            });
                            ui.add_space(8.0);
                            ui.label(
                                "İşlem iptal edilirse yükleyici çalışan süreci durdurur ve güvenli kurtarmayı dener.",
                            );
                            if self.cancel_requested {
                                ui.label("İptal isteği gönderildi; işlem durumu uzlaştırılıyor.");
                            }
                        });
                    } else if !self.recovery_blocked {
                        match self.step {
                            0 => {
                                ui.heading("Hangi işlemi yapmak istiyorsunuz?");
                                ui.label(
                                    "Devam etmek için yapılacak işlemi seçin. İmzalı yayın politikası yine kontrol edilir.",
                                );
                                ui.add_space(8.0);

                                for operation in SetupOperation::ALL {
                                    ui.group(|ui| {
                                        if ui
                                            .selectable_label(
                                                self.operation == operation,
                                                egui::RichText::new(operation.label()).strong(),
                                            )
                                            .clicked()
                                        {
                                            self.operation = operation;
                                            self.uninstall_confirmed = false;
                                            self.status = None;
                                            self.operation_completed = false;
                                        }
                                        ui.small(operation.description());
                                    });
                                    ui.add_space(4.0);
                                }
                            }
                            1 => {
                                ui.heading(if self.operation == SetupOperation::Uninstall {
                                    "Kaldırılacak paketi belirleyin"
                                } else {
                                    "Paket kimliğini ve yayıncı güvenini belirtin"
                                });
                                ui.label(
                                    "Paket kimliği, yayıncının size verdiği yayın bilgileriyle aynı olmalıdır.",
                                );
                                ui.add_space(8.0);

                                ui.group(|ui| {
                                    ui.horizontal(|ui| {
                                        ui.add_sized(
                                            [150.0, 28.0],
                                            egui::Label::new("Paket kimliği"),
                                        );
                                        let package_id_width = ui.available_width();
                                        if ui
                                            .add_sized(
                                                [package_id_width, 28.0],
                                                egui::TextEdit::singleline(&mut self.package_id),
                                            )
                                            .changed()
                                        {
                                            self.uninstall_confirmed = false;
                                            self.status = None;
                                            self.operation_completed = false;
                                        }
                                    });
                                    ui.small("Örnek biçim: publisher.product");
                                });

                                if self.operation != SetupOperation::Uninstall {
                                    ui.add_space(8.0);
                                    ui.group(|ui| {
                                        ui.heading("Yayıncı güven politikası");
                                        ui.label(
                                            "Yalnızca paketin yayıncısından aldığınız güven dosyasını seçin. Örnek dosyalar güvenilir değildir.",
                                        );
                                        if draw_trust_store_row(
                                            ui,
                                            &mut self.trust_store,
                                            "YAML güven dosyasını seçin",
                                        ) {
                                            self.uninstall_confirmed = false;
                                            self.status = None;
                                            self.operation_completed = false;
                                        }
                                    });
                                }
                            }
                            2 => {
                                ui.heading("Kurulum ve veri konumlarını seçin");
                                ui.label(
                                    "Kurulum klasörü uygulama dosyalarını, veri klasörü ise yükleyici durumunu tutar.",
                                );
                                ui.add_space(8.0);

                                ui.group(|ui| {
                                    if draw_folder_row(
                                        ui,
                                        "Kurulum klasörü",
                                        &mut self.target,
                                        "Kurulum klasörünü seçin",
                                    ) {
                                        self.uninstall_confirmed = false;
                                        self.status = None;
                                        self.operation_completed = false;
                                    }
                                    ui.add_space(8.0);
                                    if draw_folder_row(
                                        ui,
                                        "Veri klasörü",
                                        &mut self.data_root,
                                        "Yükleyici veri klasörünü seçin",
                                    ) {
                                        self.uninstall_confirmed = false;
                                        self.status = None;
                                        self.operation_completed = false;
                                    }
                                });
                            }
                            _ => {
                                ui.heading("İşlemi başlatmadan önce kontrol edin");
                                ui.label(
                                    "Aşağıdaki bilgiler bu işlemde kullanılacak. Başlatmadan önce paket ve yolları doğrulayın.",
                                );
                                ui.add_space(8.0);

                                ui.group(|ui| {
                                    ui.heading("İşlem özeti");
                                    ui.separator();
                                    setup_summary_row(ui, "İşlem", self.operation.label());
                                    setup_summary_row(ui, "Paket kimliği", &self.package_id);
                                    if self.operation != SetupOperation::Uninstall {
                                        setup_summary_row(ui, "Güven politikası", &self.trust_store);
                                    }
                                    setup_summary_row(ui, "Kurulum klasörü", &self.target);
                                    setup_summary_row(ui, "Veri klasörü", &self.data_root);
                                });

                                ui.add_space(8.0);
                                if self.operation == SetupOperation::Uninstall {
                                    ui.group(|ui| {
                                        ui.colored_label(
                                            egui::Color32::from_rgb(170, 92, 22),
                                            egui::RichText::new("Kaldırma onayı").strong(),
                                        );
                                        ui.label(
                                            "Yalnızca bu paketin yönettiği dosyalar ve durum kaydı kaldırılır. Belirtilen hedef dışındaki dosyalar korunur.",
                                        );
                                        if ui
                                            .checkbox(
                                                &mut self.uninstall_confirmed,
                                                "Bu paketi ve gösterilen klasör yollarını kaldırmayı onaylıyorum.",
                                            )
                                            .changed()
                                        {
                                            self.status = None;
                                            self.operation_completed = false;
                                        }
                                    });
                                } else {
                                    ui.group(|ui| {
                                        ui.colored_label(
                                            egui::Color32::from_rgb(18, 121, 132),
                                            egui::RichText::new("Güvenlik denetimi").strong(),
                                        );
                                        ui.label(
                                            "İşlem başlamadan önce imzalı yayın, paket özeti ve mevcut kurulum durumu yükleyici tarafından doğrulanır.",
                                        );
                                    });
                                }
                            }
                        }
                    }

                    if let Some((succeeded, message)) = &self.status {
                        ui.add_space(8.0);
                        ui.group(|ui| {
                            ui.colored_label(
                                if *succeeded {
                                    egui::Color32::from_rgb(35, 135, 75)
                                } else {
                                    egui::Color32::from_rgb(180, 45, 45)
                                },
                                egui::RichText::new(if *succeeded {
                                    "İşlem tamamlandı"
                                } else {
                                    "İşlem tamamlanamadı"
                                })
                                .strong(),
                            );
                            ui.label(message);
                        });
                    }
                });

            ui.add_space(8.0);
            ui.separator();
            ui.add_space(6.0);

            ui.horizontal(|ui| {
                if is_working {
                    ui.label("Kurulum sürüyor");
                    ui.with_layout(
                        egui::Layout::right_to_left(egui::Align::Center),
                        |ui| {
                            if ui.button("İşlemi iptal et").clicked() {
                                self.request_cancel();
                            }
                        },
                    );
                } else {
                    if ui.button("Vazgeç").clicked() {
                        close_requested = true;
                    }
                    ui.with_layout(
                        egui::Layout::right_to_left(egui::Align::Center),
                        |ui| {
                            let completed_successfully = self.operation_completed;
                            let primary_label = if self.step == 0 {
                                "Devam et"
                            } else if self.step < 3 {
                                "İleri"
                            } else if completed_successfully {
                                "Tamamlandı"
                            } else {
                                self.operation.action_label()
                            };
                            let primary_enabled = !self.recovery_blocked
                                && !completed_successfully
                                && (self.step < 3
                                    || self.operation != SetupOperation::Uninstall
                                    || self.uninstall_confirmed);
                            if ui
                                .add_enabled(
                                    primary_enabled,
                                    egui::Button::new(primary_label),
                                )
                                .clicked()
                            {
                                if self.step < 3 {
                                    next_requested = true;
                                } else {
                                    start_requested = true;
                                }
                            }
                            if self.step > 0 && ui.button("Geri").clicked() {
                                back_requested = true;
                            }
                        },
                    );
                }
            });
        });

        if back_requested {
            self.retreat_step();
        }
        if next_requested {
            self.advance_step();
        }
        if start_requested {
            self.start_install();
        }
        if close_requested {
            context.send_viewport_cmd(egui::ViewportCommand::Close);
        }
        if is_working {
            context.request_repaint_after(Duration::from_millis(120));
        }
    }
}

fn draw_folder_row(ui: &mut egui::Ui, label: &str, value: &mut String, title: &str) -> bool {
    let available_width = ui.available_width();
    let mut changed = false;
    ui.horizontal(|ui| {
        ui.add_sized([138.0, 28.0], egui::Label::new(label));
        let edit_width = (available_width - 232.0).max(150.0);
        changed |= ui
            .add_sized([edit_width, 28.0], egui::TextEdit::singleline(value))
            .changed();

        if ui
            .add_sized([82.0, 28.0], egui::Button::new("Gözat"))
            .clicked()
        {
            let mut picker = rfd::FileDialog::new().set_title(title);
            let current = PathBuf::from(value.as_str());
            if current.is_dir() {
                picker = picker.set_directory(current);
            }
            if let Some(path) = picker.pick_folder() {
                *value = path.to_string_lossy().into_owned();
                changed = true;
            }
        }
    });
    changed
}

fn draw_trust_store_row(ui: &mut egui::Ui, value: &mut String, title: &str) -> bool {
    let available_width = ui.available_width();
    let mut changed = false;
    ui.horizontal(|ui| {
        ui.add_sized([138.0, 28.0], egui::Label::new("Dosya yolu"));
        let edit_width = (available_width - 232.0).max(150.0);
        changed |= ui
            .add_sized([edit_width, 28.0], egui::TextEdit::singleline(value))
            .changed();

        if ui
            .add_sized([82.0, 28.0], egui::Button::new("Dosya seç"))
            .clicked()
        {
            let mut picker = rfd::FileDialog::new()
                .add_filter("YAML", &["yaml", "yml"])
                .set_title(title);
            let current = PathBuf::from(value.as_str());
            if let Some(parent) = current.parent().filter(|parent| parent.is_dir()) {
                picker = picker.set_directory(parent);
            }
            if let Some(path) = picker.pick_file() {
                *value = path.to_string_lossy().into_owned();
                changed = true;
            }
        }
    });
    changed
}

fn setup_summary_row(ui: &mut egui::Ui, label: &str, value: &str) {
    ui.horizontal_wrapped(|ui| {
        ui.add_sized(
            [145.0, 22.0],
            egui::Label::new(egui::RichText::new(label).strong()),
        );
        ui.label(value);
    });
}

impl SetupApp {
    fn advance_step(&mut self) {
        match self.step {
            0 => {
                self.status = None;
                self.operation_completed = false;
                self.step = 1;
            }
            1 => {
                if let Err(error) = PackageId::parse(self.package_id.trim()) {
                    self.status = Some((false, format!("Paket kimliği geçersiz: {error}")));
                    return;
                }
                if self.operation != SetupOperation::Uninstall {
                    if self.trust_store.trim().is_empty() {
                        self.status = Some((
                            false,
                            "Yayıncı güven politikası dosyası zorunludur.".to_owned(),
                        ));
                        return;
                    }
                    let trust_path = PathBuf::from(self.trust_store.trim());
                    if !std::fs::symlink_metadata(trust_path)
                        .is_ok_and(|metadata| metadata.file_type().is_file())
                    {
                        self.status = Some((
                            false,
                            "Güven politikası yolu normal bir dosyayı göstermelidir.".to_owned(),
                        ));
                        return;
                    }
                }
                self.status = None;
                self.operation_completed = false;
                self.step = 2;
            }
            2 => {
                if self.target.trim().is_empty() || self.data_root.trim().is_empty() {
                    self.status =
                        Some((false, "Kurulum ve veri klasörleri zorunludur.".to_owned()));
                    return;
                }
                self.status = None;
                self.operation_completed = false;
                self.step = 3;
            }
            _ => {}
        }
    }

    fn retreat_step(&mut self) {
        if self.step > 0 {
            self.step -= 1;
            self.status = None;
            self.operation_completed = false;
        }
    }

    fn start_install(&mut self) {
        if self.recovery_blocked {
            return;
        }
        if let Err(error) = PackageId::parse(self.package_id.trim()) {
            self.status = Some((false, format!("Paket kimliği geçersiz: {error}")));
            return;
        }
        if (self.operation != SetupOperation::Uninstall && self.trust_store.trim().is_empty())
            || self.target.trim().is_empty()
            || self.data_root.trim().is_empty()
        {
            let message = if self.operation == SetupOperation::Uninstall {
                "Kurulum ve veri klasörleri zorunludur."
            } else {
                "Güven politikası ve klasör alanları zorunludur."
            };
            self.status = Some((false, message.to_owned()));
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
        self.operation_completed = false;
        self.uninstall_confirmed = false;

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
                    "Yükleyici işlemi beklenmedik şekilde sonlandı.".to_owned(),
                )),
            },
            None => None,
        };
        if let Some(result) = result {
            self.result_receiver = None;
            self.cancel_sender = None;
            self.cancel_requested = false;
            self.operation_completed = result.is_ok();
            self.status = Some(match result {
                Ok(message) => (
                    true,
                    format!("{} işlemi tamamlandı.\n{message}", self.operation.label()),
                ),
                Err(message) => (false, message),
            });
        }
    }
}

const MAX_CAPTURED_BACKEND_OUTPUT_BYTES: usize = 64 * 1024;

fn start_output_reader<R>(mut reader: R) -> JoinHandle<io::Result<Vec<u8>>>
where
    R: Read + Send + 'static,
{
    thread::spawn(move || {
        let mut captured = Vec::with_capacity(MAX_CAPTURED_BACKEND_OUTPUT_BYTES);
        let mut buffer = [0_u8; 8192];
        loop {
            let read = reader.read(&mut buffer)?;
            if read == 0 {
                break;
            }

            if read >= MAX_CAPTURED_BACKEND_OUTPUT_BYTES {
                captured.clear();
                captured.extend_from_slice(&buffer[read - MAX_CAPTURED_BACKEND_OUTPUT_BYTES..read]);
                continue;
            }

            let overflow = captured
                .len()
                .saturating_add(read)
                .saturating_sub(MAX_CAPTURED_BACKEND_OUTPUT_BYTES);
            if overflow > 0 {
                captured.drain(..overflow);
            }
            captured.extend_from_slice(&buffer[..read]);
        }
        Ok(captured)
    })
}

fn finish_output_reader(reader: JoinHandle<io::Result<Vec<u8>>>) -> Vec<u8> {
    reader.join().ok().and_then(Result::ok).unwrap_or_default()
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
    let is_uninstall = operation == "uninstall";
    let mut backend = Command::new(executable);
    backend
        .arg(if is_uninstall {
            "uninstall"
        } else {
            "install-self"
        })
        .arg("--package-id")
        .arg(&package_id)
        .arg("--target")
        .arg(&target)
        .arg("--data-root")
        .arg(&data_root);
    if is_uninstall {
        backend.arg("--confirm-uninstall").arg("YES");
    } else {
        backend
            .arg("--trust-store")
            .arg(&trust_store)
            .arg("--operation")
            .arg(&operation);
    }
    let mut child = match backend
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

    let (stdout, stderr) = match (child.stdout.take(), child.stderr.take()) {
        (Some(stdout), Some(stderr)) => (stdout, stderr),
        _ => {
            let _ = child.kill();
            let _ = child.wait();
            let _ = result_sender.send(Err(
                "Setup backend output streams could not be captured.".to_owned()
            ));
            return;
        }
    };
    let stdout_reader = start_output_reader(stdout);
    let stderr_reader = start_output_reader(stderr);

    loop {
        match child.try_wait() {
            Ok(Some(_)) => break,
            Ok(None) => {}
            Err(error) => {
                let _ = child.kill();
                let _ = child.wait();
                let _ = finish_output_reader(stdout_reader);
                let _ = finish_output_reader(stderr_reader);
                let _ = result_sender.send(Err(format!("İşlem durumu okunamadı: {error}")));
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
                        let _ = child.wait();
                        let _ = finish_output_reader(stdout_reader);
                        let _ = finish_output_reader(stderr_reader);
                        let _ = result_sender.send(Err(format!(
                            "İptal sırasında işlem durumu okunamadı: {error}"
                        )));
                        return;
                    }
                }

                let _ = child.kill();
                let _ = child.wait();
                let _ = finish_output_reader(stdout_reader);
                let _ = finish_output_reader(stderr_reader);
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
                            .unwrap_or("Kurulum durumu uzlaştırıldı.")
                            .to_owned();
                        format!("İşlem iptal edildi. {detail}")
                    }
                    Ok(output) => {
                        let detail = String::from_utf8_lossy(&output.stderr)
                            .lines()
                            .last()
                            .filter(|line| !line.trim().is_empty())
                            .unwrap_or("Otomatik kurtarma tamamlanamadı.")
                            .to_owned();
                        format!(
                            "İşlem iptal edildi; durum belirsiz. Elle kurtarma gerekli: {detail}"
                        )
                    }
                    Err(error) => format!("İşlem iptal edildi; kurtarma başlatılamadı: {error}"),
                };
                let _ = result_sender.send(Err(message));
                return;
            }
            Err(TryRecvError::Empty) => {}
        }

        thread::sleep(Duration::from_millis(150));
    }

    let status = match child.wait() {
        Ok(status) => status,
        Err(error) => {
            let _ = finish_output_reader(stdout_reader);
            let _ = finish_output_reader(stderr_reader);
            let _ = result_sender.send(Err(format!("Kurulum sonucu okunamadı: {error}")));
            return;
        }
    };
    let stdout = finish_output_reader(stdout_reader);
    let stderr = finish_output_reader(stderr_reader);

    if status.success() {
        let message = String::from_utf8_lossy(&stdout)
            .lines()
            .last()
            .filter(|line| !line.trim().is_empty())
            .unwrap_or("İşlem tamamlandı.")
            .to_owned();
        let _ = result_sender.send(Ok(message));
    } else {
        let stderr = String::from_utf8_lossy(&stderr);
        let stdout = String::from_utf8_lossy(&stdout);
        let detail = stderr
            .lines()
            .last()
            .filter(|line| !line.trim().is_empty())
            .or_else(|| stdout.lines().last())
            .unwrap_or("İşlem başarısız oldu.");
        let _ = result_sender.send(Err(detail.to_owned()));
    }
}
