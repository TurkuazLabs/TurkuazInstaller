// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.6.13
// Aciklama: IDE karsilama ekrani, proje tipi secimi ve moda ozel modal kurulum proje sihirbazi
// Bagimli Oldugu Katman: View | Application

use std::{
    fs,
    io::{self, Read, Write},
    path::{Component, Path, PathBuf},
    process::{Command, Output},
    sync::mpsc::{self, Receiver, TryRecvError},
    thread,
    time::{Duration, SystemTime, UNIX_EPOCH},
};

use eframe::egui;
use rfd::FileDialog;
use serde::{Deserialize, Serialize};
use turkuaz_installer_domain::{package_id::PackageId, semantic_version::SemanticVersion};
use turkuaz_installer_package_engine::{
    SetupThemeConfig, SetupThemePreset, create_unsigned_local_setup,
};
use url::Url;

#[cfg(windows)]
#[path = "windows_documents.rs"]
mod windows_documents;

#[path = "setup_av_options.rs"]
mod setup_av_options;
use setup_av_options::{AvProvider, AvScanConfig};

const MAXIMUM_PROJECT_FILE_BYTES: u64 = 1024 * 1024;
const PROJECT_SCHEMA_VERSION: u32 = 1;

#[derive(Debug, Default, Clone, Copy, PartialEq, Eq)]
enum AppearancePreference {
    #[default]
    System,
    Light,
    Dark,
}

impl AppearancePreference {
    const ALL: [Self; 3] = [Self::System, Self::Light, Self::Dark];

    const fn label(self) -> &'static str {
        match self {
            Self::System => "Sistem",
            Self::Light => "Açık",
            Self::Dark => "Koyu",
        }
    }

    const fn preference(self) -> egui::ThemePreference {
        match self {
            Self::System => egui::ThemePreference::System,
            Self::Light => egui::ThemePreference::Light,
            Self::Dark => egui::ThemePreference::Dark,
        }
    }
}

// BRK-SETUP-PROJECT-TYPE: persist output mode; old .tzproj files remain offline.
#[derive(Debug, Default, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
enum InstallerKind {
    #[default]
    Offline,
    Web,
}

impl InstallerKind {
    const fn label(self) -> &'static str {
        match self {
            Self::Offline => "Tam Kurulum",
            Self::Web => "Çevrimiçi Kurulum",
        }
    }

    const fn description(self) -> &'static str {
        match self {
            Self::Offline => "Uygulama dosyalarını içeren, internet gerektirmeyen kurulum.",
            Self::Web => {
                "Küçük bir yükleyiciyle uygulama dosyalarını güvenli sunucudan indiren kurulum."
            }
        }
    }

    const fn release_title(self) -> &'static str {
        match self {
            Self::Offline => "Çevrimdışı dağıtım",
            Self::Web => "Çevrimiçi yayın",
        }
    }
}

// BRK-WEB-SOURCE-001: keep provider choice separate from signed release verification.
#[derive(Debug, Default, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
enum WebReleaseSource {
    GithubReleases,
    #[default]
    Https,
}

impl WebReleaseSource {
    const fn label(self) -> &'static str {
        match self {
            Self::GithubReleases => "GitHub Releases",
            Self::Https => "Özel HTTPS sunucusu",
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
    #[serde(default)]
    installer_kind: InstallerKind,
    product_name: String,
    package_id: String,
    version: String,
    channel: String,
    source_directory: String,
    entry_point: String,
    install_target: String,
    output_directory: String,
    theme_preset: String,
    brand_name: String,
    subtitle: String,
    accent_color: String,
    #[serde(default)]
    signed_manifest_path: String,
    #[serde(default)]
    signature_path: String,
    #[serde(default)]
    trust_store_path: String,
    #[serde(default)]
    setup_stub_path: String,
    #[serde(default)]
    makensis_path: String,
    #[serde(default)]
    av_scan: AvScanConfig,
    #[serde(default)]
    web_manifest_url: String,
    #[serde(default)]
    web_signature_url: String,
    #[serde(default)]
    web_release_source: WebReleaseSource,
    #[serde(default)]
    github_repository: String,
    #[serde(default)]
    github_release_tag: String,
    #[serde(default)]
    github_manifest_asset: String,
    #[serde(default)]
    github_signature_asset: String,
}

impl Default for SavedProject {
    fn default() -> Self {
        // BRK-RUST-STUDIO-PATH-001: Default project locations must be
        // per-user and independent from the launcher process working directory.
        // Stored .tzproj paths are preserved when existing projects are opened.
        let local_app_data = std::env::var_os("LOCALAPPDATA")
            .map(PathBuf::from)
            .filter(|path| path.is_absolute())
            .unwrap_or_else(std::env::temp_dir);
        let output_directory = local_app_data.join("TurkuazInstaller").join("Projects");

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            installer_kind: InstallerKind::Offline,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: local_app_data
                .join("Programs")
                .join("Uygulamam")
                .to_string_lossy()
                .into_owned(),
            output_directory: output_directory.to_string_lossy().into_owned(),
            theme_preset: "turkuaz".to_owned(),
            brand_name: "Uygulamam Kurulumu".to_owned(),
            subtitle: "Kurulum sihirbazına hoş geldiniz".to_owned(),
            accent_color: String::new(),
            signed_manifest_path: String::new(),
            signature_path: String::new(),
            trust_store_path: String::new(),
            setup_stub_path: String::new(),
            makensis_path: String::new(),
            av_scan: AvScanConfig::default(),
            web_manifest_url: String::new(),
            web_signature_url: String::new(),
            web_release_source: WebReleaseSource::Https,
            github_repository: String::new(),
            github_release_tag: String::new(),
            github_manifest_asset: "release-manifest.yaml".to_owned(),
            github_signature_asset: "release-manifest.yaml.p7s".to_owned(),
        }
    }
}

// BRK-WEB-RELEASE-CHECK-002: compare request details when async results return.
// Changing projects or source URLs invalidates previous test status.
#[derive(Debug, Clone, PartialEq, Eq)]
struct SourceCheckRequest {
    package_id: String,
    manifest_url: String,
    signature_url: String,
    trust_file: PathBuf,
}

type SourceCheckResponse = (SourceCheckRequest, Result<String, String>);

struct BuilderApp {
    project: SavedProject,
    workspace_open: bool,
    has_project: bool,
    pending_new_kind: Option<InstallerKind>,
    pending_open_requested: bool,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    source_check_receiver: Option<Receiver<SourceCheckResponse>>,
    source_check_result: Option<(SourceCheckRequest, bool, String)>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            workspace_open: false,
            has_project: false,
            pending_new_kind: None,
            pending_open_requested: false,
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
            source_check_receiver: None,
            source_check_result: None,
            project_path: None,
        }
    }
}

// BRK-SETUP-STUDIO-THEME: independent chrome palette, not the generated installer theme.
#[derive(Clone, Copy)]
struct StudioPalette {
    accent: egui::Color32,
    canvas: egui::Color32,
    surface: egui::Color32,
    input: egui::Color32,
    border: egui::Color32,
    hover: egui::Color32,
}

fn studio_palette(dark: bool) -> StudioPalette {
    let rgb = egui::Color32::from_rgb;
    if dark {
        StudioPalette {
            accent: rgb(61, 203, 198),
            canvas: rgb(16, 24, 35),
            surface: rgb(28, 39, 54),
            input: rgb(36, 50, 66),
            border: rgb(60, 77, 91),
            hover: rgb(44, 65, 78),
        }
    } else {
        StudioPalette {
            accent: rgb(10, 126, 135),
            canvas: rgb(242, 246, 249),
            surface: rgb(255, 255, 255),
            input: rgb(233, 241, 246),
            border: rgb(211, 223, 233),
            hover: rgb(222, 239, 242),
        }
    }
}

fn configure_studio_style(context: &egui::Context) {
    // Register named text styles in both themes before the first frame.
    // A missing style panics when Windows or the user switches appearance.
    context.all_styles_mut(|style| {
        style
            .text_styles
            .insert(egui::TextStyle::Small, egui::FontId::proportional(13.0));
        style
            .text_styles
            .insert(egui::TextStyle::Body, egui::FontId::proportional(16.0));
        style
            .text_styles
            .insert(egui::TextStyle::Button, egui::FontId::proportional(15.0));
        style
            .text_styles
            .insert(egui::TextStyle::Heading, egui::FontId::proportional(24.0));
        style.text_styles.insert(
            egui::TextStyle::Name("PageTitle".into()),
            egui::FontId::proportional(29.0),
        );
        style.text_styles.insert(
            egui::TextStyle::Name("ProductName".into()),
            egui::FontId::proportional(27.0),
        );
        style.spacing.item_spacing = egui::vec2(12.0, 9.0);
        style.spacing.button_padding = egui::vec2(14.0, 8.0);
        style.spacing.interact_size = egui::vec2(40.0, 36.0);
        style.spacing.window_margin = egui::Margin::same(20);
        style.spacing.menu_margin = egui::Margin::same(10);
        style.visuals.window_corner_radius = egui::CornerRadius::same(14);
        style.visuals.menu_corner_radius = egui::CornerRadius::same(10);
        style.visuals.widgets.noninteractive.corner_radius = egui::CornerRadius::same(10);
        style.visuals.widgets.inactive.corner_radius = egui::CornerRadius::same(10);
        style.visuals.widgets.hovered.corner_radius = egui::CornerRadius::same(10);
        style.visuals.widgets.active.corner_radius = egui::CornerRadius::same(10);
        style.visuals.widgets.open.corner_radius = egui::CornerRadius::same(10);

        let palette = studio_palette(style.visuals.dark_mode);
        style.visuals.panel_fill = palette.canvas;
        style.visuals.window_fill = palette.surface;
        style.visuals.extreme_bg_color = palette.input;
        style.visuals.faint_bg_color = palette.input;
        style.visuals.window_stroke = egui::Stroke::new(1.0, palette.border);
        style.visuals.widgets.noninteractive.bg_stroke = egui::Stroke::new(1.0, palette.border);
        style.visuals.widgets.inactive.bg_stroke = egui::Stroke::new(1.0, palette.border);
        style.visuals.widgets.hovered.bg_fill = palette.hover;
        style.visuals.widgets.active.bg_fill = palette.hover;
        style.visuals.selection.bg_fill = palette.accent;
    });
}

pub fn run() -> eframe::Result {
    let title = format!("Turkuaz Setup Studio v{}", env!("CARGO_PKG_VERSION"));
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_inner_size([1360.0, 860.0])
            .with_min_inner_size([1024.0, 700.0])
            .with_title(title.clone()),
        ..Default::default()
    };

    eframe::run_native(
        &title,
        options,
        Box::new(|creation_context| {
            let context = &creation_context.egui_ctx;
            context.set_theme(AppearancePreference::System.preference());
            configure_studio_style(context);
            Ok(Box::new(BuilderApp::default()))
        }),
    )
}

impl eframe::App for BuilderApp {
    fn update(&mut self, context: &egui::Context, _frame: &mut eframe::Frame) {
        self.poll_build();
        self.poll_source_check();

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;
        let mut save_as_requested = false;
        let mut web_build_requested = false;

        egui::TopBottomPanel::top("setup-builder-header").show(context, |ui| {
            ui.horizontal(|ui| {
                ui.label(
                    egui::RichText::new("T")
                        .size(32.0)
                        .strong()
                        .color(egui::Color32::from_rgb(18, 121, 132)),
                );
                ui.vertical(|ui| {
                    ui.label(
                        egui::RichText::new("Turkuaz Setup Studio")
                            .text_style(egui::TextStyle::Name("ProductName".into()))
                            .strong(),
                    );
                    ui.label(egui::RichText::new("Windows kurulum paketi geliştirme").weak());
                    ui.small(format!("v{}", env!("CARGO_PKG_VERSION")));
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let previous_appearance = self.appearance;
                    egui::ComboBox::from_id_salt("builder-app-appearance")
                        .selected_text(self.appearance.label())
                        .width(92.0)
                        .show_ui(ui, |ui| {
                            for appearance in AppearancePreference::ALL {
                                ui.selectable_value(
                                    &mut self.appearance,
                                    appearance,
                                    appearance.label(),
                                );
                            }
                        });
                    appearance_changed = previous_appearance != self.appearance;
                    ui.label("Görünüm");
                    ui.separator();
                    if ui
                        .add_enabled(
                            !is_building && self.workspace_open,
                            egui::Button::new("Projeyi kaydet"),
                        )
                        .clicked()
                    {
                        save_requested = true;
                    }
                    if ui
                        .add_enabled(
                            !is_building && self.workspace_open,
                            egui::Button::new("Farklı kaydet…"),
                        )
                        .clicked()
                    {
                        save_as_requested = true;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Başlangıç"))
                        .clicked()
                    {
                        self.workspace_open = false;
                        self.dialog_open = false;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Proje aç"))
                        .clicked()
                    {
                        open_requested = true;
                    }
                });
            });
        });

        if appearance_changed {
            context.set_theme(self.appearance.preference());
            configure_studio_style(context);
        }

        if self.workspace_open {
            let package_ready = prepared_package_is_current(&self.project);
            let local_test_ready = package_ready
                && package_output_directory(&self.project).is_ok_and(|directory| {
                    regular_file_exists(&directory.join("standard/TurkuazSetup.exe"))
                });
            egui::TopBottomPanel::bottom("setup-builder-actions").show(context, |ui| {
                ui.separator();
                ui.horizontal(|ui| {
                    if let Some((succeeded, message)) = &self.status {
                        ui.colored_label(
                            if *succeeded {
                                egui::Color32::from_rgb(35, 135, 75)
                            } else {
                                egui::Color32::from_rgb(180, 45, 45)
                            },
                            message
                                .lines()
                                .next()
                                .unwrap_or("İşlem sonucu ana ekranda."),
                        );
                    } else if is_building {
                        ui.spinner();
                        ui.label("Paket ve kurulum dosyaları hazırlanıyor…");
                    } else if !package_ready {
                        ui.label("Adım 2 için önce güncel uygulama paketini hazırlayın.");
                    } else if local_test_ready {
                        ui.label("Standart Kurulum EXE'si hazır; çıktı klasöründen açabilirsiniz.");
                    } else {
                        ui.label(format!(
                            "{} projesi · Paket hazır; kurulum EXE dosyasını oluşturabilirsiniz.",
                            self.project.installer_kind.label(),
                        ));
                    }

                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        if self.project.installer_kind == InstallerKind::Web {
                            if ui
                                .add_enabled(
                                    !is_building && package_ready,
                                    egui::Button::new("2  Çevrimiçi yükleyici oluştur"),
                                )
                                .clicked()
                            {
                                web_build_requested = true;
                            }
                        } else if ui
                            .add_enabled(
                                !is_building && package_ready && !local_test_ready,
                                egui::Button::new("2  Standart Kurulum oluştur"),
                            )
                            .clicked()
                        {
                            self.start_standard_build();
                        }

                        let build_clicked = ui
                            .add_enabled(
                                !is_building,
                                egui::Button::new("1  Uygulama paketini hazırla")
                                    .fill(egui::Color32::from_rgb(18, 121, 132)),
                            )
                            .clicked();
                        if build_clicked {
                            self.start_build();
                        }
                    });
                });
            });

            egui::SidePanel::left("setup-builder-menu")
                .resizable(false)
                .default_width(220.0)
                .show(context, |ui| {
                    ui.label(egui::RichText::new("Proje adımları").size(19.0).strong());
                    ui.label(
                        egui::RichText::new("Bölüm seçince düzenleme penceresi açılır.")
                            .size(14.0)
                            .weak(),
                    );
                    ui.add_space(12.0);

                    if navigation_item(ui, !self.dialog_open, "Genel bakış").clicked() {
                        self.dialog_open = false;
                        self.status = None;
                    }
                    ui.label(
                        egui::RichText::new(self.project.installer_kind.label())
                            .size(13.0)
                            .strong()
                            .color(studio_palette(ui.visuals().dark_mode).accent),
                    );

                    ui.add_space(8.0);
                    ui.separator();
                    ui.label(
                        egui::RichText::new("PROJE BÖLÜMLERİ")
                            .size(12.0)
                            .strong()
                            .weak(),
                    );

                    let sections: [&str; 4] = [
                        "01   Ürün bilgileri",
                        "02   Uygulama dosyaları",
                        "03   Kurulum tasarımı",
                        match self.project.installer_kind {
                            InstallerKind::Offline => "04   Çevrimdışı dağıtım",
                            InstallerKind::Web => "04   Çevrimiçi yayın",
                        },
                    ];
                    for (index, label) in sections.iter().enumerate() {
                        if navigation_item(ui, self.dialog_open && self.active_step == index, label)
                            .clicked()
                            && !is_building
                        {
                            self.open_editor(index);
                        }
                    }

                    ui.add_space(14.0);
                    ui.separator();
                    ui.label(
                        egui::RichText::new(&self.project.product_name)
                            .size(18.0)
                            .strong(),
                    );
                    ui.label(
                        egui::RichText::new(&self.project.package_id)
                            .size(13.0)
                            .monospace()
                            .weak(),
                    );
                    ui.label(
                        egui::RichText::new(format!("v{}", self.project.version))
                            .size(13.0)
                            .weak(),
                    );
                });

            egui::SidePanel::right("setup-builder-preview")
                .resizable(false)
                .default_width(310.0)
                .show(context, |ui| self.draw_preview(ui));

            egui::CentralPanel::default().show(context, |ui| {
                egui::ScrollArea::vertical().show(ui, |ui| self.draw_workspace(ui));
            });
        } else {
            egui::CentralPanel::default().show(context, |ui| {
                egui::ScrollArea::vertical().show(ui, |ui| self.draw_welcome(ui));
            });
        }

        if self.dialog_open && !is_building {
            self.show_editor_modal(context, false);
        }
        if self.pending_new_kind.is_some() {
            self.confirm_new_project(context);
        }
        if self.pending_open_requested && !is_building {
            self.confirm_open_project(context);
        }

        if open_requested {
            self.request_open_project();
        }
        if save_requested {
            self.save_project();
        }
        if save_as_requested {
            self.save_project_as();
        }
        if web_build_requested {
            self.start_web_build();
        }
        if is_building || self.source_check_receiver.is_some() {
            context.request_repaint_after(Duration::from_millis(120));
        }
    }
}

impl BuilderApp {
    fn create_new_project(&mut self, kind: InstallerKind) {
        let mut project = SavedProject::default();
        project.installer_kind = kind;
        self.project = project;
        self.source_check_result = None;
        self.project_path = None;
        self.status = None;
        self.has_project = true;
        self.workspace_open = true;
        self.pending_new_kind = None;
        self.pending_open_requested = false;
        self.open_editor(0);
    }

    fn request_new_project(&mut self, kind: InstallerKind) {
        if self.has_project {
            self.pending_new_kind = Some(kind);
        } else {
            self.create_new_project(kind);
        }
    }

    fn confirm_new_project(&mut self, context: &egui::Context) {
        let Some(kind) = self.pending_new_kind else {
            return;
        };
        let mut proceed = false;
        let mut cancel = false;
        egui::Modal::new(egui::Id::new("setup-studio-new-project-confirmation")).show(
            context,
            |ui| {
                ui.set_min_width(460.0);
                ui.heading("Yeni proje oluşturulsun mu?");
                ui.label("Şu an açık proje kapanacak. Kaydedilmemiş değişiklikler kaybolabilir.");
                ui.label(format!("Seçilen proje türü: {}", kind.label()));
                ui.horizontal(|ui| {
                    if ui.button("Vazgeç").clicked() {
                        cancel = true;
                    }
                    if ui.button("Yeni projeyi oluştur").clicked() {
                        proceed = true;
                    }
                });
            },
        );
        if cancel {
            self.pending_new_kind = None;
        } else if proceed {
            self.create_new_project(kind);
        }
    }

    fn draw_welcome(&mut self, ui: &mut egui::Ui) {
        let palette = studio_palette(ui.visuals().dark_mode);
        ui.add_space(30.0);
        ui.colored_label(
            palette.accent,
            egui::RichText::new("TURKUAZ SETUP STUDIO / BAŞLANGIÇ")
                .size(13.0)
                .strong(),
        );
        ui.label(
            egui::RichText::new("Ne oluşturmak istiyorsunuz?")
                .text_style(egui::TextStyle::Name("PageTitle".into()))
                .strong(),
        );
        ui.label(
            "Bir proje şablonu seçin. Ardından dört adımlı kurulum projesi sihirbazı açılacak.",
        );
        ui.add_space(23.0);
        ui.heading("Yeni proje");
        ui.add_space(8.0);
        let mut selected = None;
        ui.columns(2, |columns| {
            for (ui, (kind, number)) in columns
                .iter_mut()
                .zip([(InstallerKind::Offline, "01"), (InstallerKind::Web, "02")])
            {
                studio_card(ui, |ui| {
                    ui.colored_label(
                        palette.accent,
                        egui::RichText::new(number).size(23.0).strong(),
                    );
                    ui.add_space(5.0);
                    ui.label(egui::RichText::new(kind.label()).size(22.0).strong());
                    ui.add_space(4.0);
                    ui.label(kind.description());
                    ui.add_space(15.0);
                    let width = ui.available_width();
                    if ui
                        .add_sized(
                            [width, 44.0],
                            egui::Button::new(format!("{} projesi oluştur", kind.label())),
                        )
                        .clicked()
                    {
                        selected = Some(kind);
                    }
                });
            }
        });
        ui.add_space(22.0);
        ui.separator();
        ui.label(egui::RichText::new("Mevcut projeler").size(19.0).strong());
        ui.label("Daha önce kaydettiğiniz .tzproj dosyalarıyla çalışmaya devam edin.");
        ui.horizontal_wrapped(|ui| {
            if ui.button("Kaydedilmiş proje aç…").clicked() {
                self.request_open_project();
            }
            if self.has_project && ui.button("Açık projeye dön").clicked() {
                self.workspace_open = true;
            }
        });
        if let Some((success, message)) = &self.status {
            ui.colored_label(
                if *success {
                    palette.accent
                } else {
                    egui::Color32::from_rgb(185, 67, 58)
                },
                message,
            );
        }
        if let Some(kind) = selected {
            self.request_new_project(kind);
        }
    }

    fn draw_workspace(&mut self, ui: &mut egui::Ui) {
        let palette = studio_palette(ui.visuals().dark_mode);
        ui.add_space(6.0);
        ui.colored_label(
            palette.accent,
            egui::RichText::new("KURULUM PROJESİ").size(12.0).strong(),
        );
        ui.label(
            egui::RichText::new("Uygulamanız için setup hazırlayın")
                .text_style(egui::TextStyle::Name("PageTitle".into()))
                .strong(),
        );
        ui.label(format!(
            "{} projesi · Dört adımda kurulum projenizi hazırlayın.",
            self.project.installer_kind.label()
        ));
        ui.add_space(14.0);
        self.draw_output_status(ui);
        ui.add_space(16.0);

        ui.label(egui::RichText::new("Proje ayarları").size(21.0).strong());
        ui.label(
            egui::RichText::new("Düzenle düğmesiyle istediğiniz bölüme geçebilirsiniz.")
                .size(14.0)
                .weak(),
        );
        ui.add_space(8.0);

        let theme_label = SetupThemePreset::ALL
            .into_iter()
            .find(|preset| preset.as_str() == self.project.theme_preset)
            .map(SetupThemePreset::label)
            .unwrap_or("Turkuaz");
        let source_summary = if self.project.source_directory.trim().is_empty() {
            "Henüz kaynak klasörü seçilmedi.".to_owned()
        } else {
            self.project.source_directory.clone()
        };
        let signing_summary = match self.project.installer_kind {
            InstallerKind::Offline => {
                if self.project.signed_manifest_path.trim().is_empty() {
                    "İmzalı yayın dosyasını, yayıncı güven dosyasını ve çıktı klasörünü belirleyin."
                        .to_owned()
                } else {
                    "İmzalı yayın dosyası seçildi; diğer dağıtım ayarlarını kontrol edin."
                        .to_owned()
                }
            }
            InstallerKind::Web => match self.project.web_release_source {
                WebReleaseSource::GithubReleases => {
                    if self.project.github_repository.trim().is_empty() {
                        "GitHub deposunu, yayın etiketini ve dosya adlarını belirleyin.".to_owned()
                    } else {
                        format!(
                            "GitHub Releases · {} · {}",
                            self.project.github_repository, self.project.github_release_tag
                        )
                    }
                }
                WebReleaseSource::Https => {
                    if self.project.web_manifest_url.trim().is_empty() {
                        "İmzalı yayın dosyasının ve imzasının HTTPS adreslerini belirleyin."
                            .to_owned()
                    } else {
                        self.project.web_manifest_url.clone()
                    }
                }
            },
        };
        let sections = [
            (
                "Ürün bilgileri",
                format!(
                    "{}  •  v{}  •  {}",
                    self.project.product_name, self.project.version, self.project.channel
                ),
                "Uygulama adı, kimliği ve sürümü",
            ),
            (
                "Uygulama dosyaları",
                source_summary,
                "Uygulama dosyaları ve başlangıç programı",
            ),
            (
                "Kurulum tasarımı",
                format!("{} teması  •  {}", theme_label, self.project.brand_name),
                "Son kullanıcının göreceği kurulum ekranı",
            ),
            (
                self.project.installer_kind.release_title(),
                signing_summary,
                "Çıktı klasörü ve seçilen yükleyiciye ait yayın ayarları",
            ),
        ];
        for (index, (title, summary, description)) in sections.iter().enumerate() {
            self.draw_section_card(ui, index, title, summary, description);
            ui.add_space(7.0);
        }
        ui.add_space(5.0);
        ui.label(
            egui::RichText::new(
                "Paket oluşturmak, kurulum EXE'sini dijital olarak imzalamaz. Güvenli dağıtım için imzalı yayın dosyaları gerekir.",
            )
            .size(13.0)
            .weak(),
        );
        ui.add_space(10.0);
    }

    fn draw_output_status(&mut self, ui: &mut egui::Ui) {
        let output = package_output_directory(&self.project);
        let is_building = self.build_receiver.is_some();
        let recent_status = self.status.clone();
        let mut folder_to_open = None;

        studio_card(ui, |ui| {
            ui.label(
                egui::RichText::new("Oluşturulan dosyalar")
                    .size(20.0)
                    .strong(),
            );
            ui.label(
                egui::RichText::new(
                    "Uygulama dosyalarınızın bulunduğu kaynak klasörü ile çıktı klasörü farklıdır.",
                )
                .size(13.0)
                .weak(),
            );
            ui.add_space(8.0);

            match output {
                Ok(ref package_directory) => {
                    ui.label(egui::RichText::new("Dosyaların kaydedileceği yer:").strong());
                    ui.label(
                        egui::RichText::new(package_directory.display().to_string()).monospace(),
                    );
                    ui.add_space(7.0);

                    let package_present =
                        regular_file_exists(&package_directory.join("application.tzpkg"));
                    let package_ready = prepared_package_is_current(&self.project);
                    let distribution_directory = package_directory.join("distribution");
                    let web_ready = regular_file_exists(
                        &package_directory.join("web").join("TurkuazWebSetup.exe"),
                    );
                    let setup_ready =
                        regular_file_exists(&distribution_directory.join("TurkuazSetup.exe"));
                    let local_test_ready =
                        regular_file_exists(&package_directory.join("standard/TurkuazSetup.exe"));
                    let installer_ready = match self.project.installer_kind {
                        InstallerKind::Offline => setup_ready || local_test_ready,
                        InstallerKind::Web => web_ready,
                    };
                    if installer_ready && package_ready {
                        ui.label("Uygulama paketi hazır: application.tzpkg");
                        if local_test_ready && self.project.installer_kind == InstallerKind::Offline
                        {
                            ui.label("Standart Kurulum hazır: standard/TurkuazSetup.exe");
                            ui.small("İmzasız kurulum; güvenli güncelleme/rollback ve yayıncı doğrulaması içermez.");
                        }
                        if setup_ready && self.project.installer_kind == InstallerKind::Offline {
                            ui.label("İmzalı dağıtım bulundu: distribution/TurkuazSetup.exe");
                            ui.small(
                                "Gerçek yayından önce Authenticode ve Windows kabulü gerekir.",
                            );
                        }
                        if web_ready && self.project.installer_kind == InstallerKind::Web {
                            ui.label("Çevrimiçi Kurulum bulundu: web/TurkuazWebSetup.exe");
                            ui.small(
                                "Gerçek yayından önce Authenticode ve Windows kabulü gerekir.",
                            );
                        }
                    } else if package_ready {
                        ui.label("Uygulama paketi hazır: application.tzpkg");
                        ui.label(match self.project.installer_kind {
                            InstallerKind::Offline => "Standart EXE henüz yok. 2 Standart Kurulum oluştur ile oluşturabilirsiniz.",
                            InstallerKind::Web => "Çevrimiçi EXE henüz yok. 04. adımda yayın ayarlarını tamamlayın.",
                        });
                    } else if is_building {
                        ui.horizontal(|ui| {
                            ui.spinner();
                            ui.label("Paket/kurulum çıktısı hazırlanıyor…");
                        });
                    } else if package_present {
                        ui.colored_label(
                            egui::Color32::from_rgb(170, 92, 22),
                            "Mevcut paket farklı proje ayarlarına ait veya proje kaydı okunamıyor.",
                        );
                        ui.label("Paket kimliğini/sürümünü güncelleyip uygulama paketini yeniden hazırlayın.");
                    } else {
                        ui.label("Bu konumda henüz bir uygulama paketi bulunamadı.");
                        ui.label("Alttaki 1. düğme ile uygulama paketini oluşturun.");
                    }

                    ui.add_space(8.0);
                    let open_directory = if regular_directory_exists(package_directory) {
                        Some(package_directory.clone())
                    } else {
                        resolve_path(&self.project.output_directory)
                            .ok()
                            .filter(|path| regular_directory_exists(path))
                    };
                    if ui
                        .add_enabled(
                            open_directory.is_some() && !is_building,
                            egui::Button::new("Çıktı klasörünü aç"),
                        )
                        .clicked()
                    {
                        folder_to_open = open_directory;
                    }
                }
                Err(error) => {
                    ui.colored_label(egui::Color32::from_rgb(180, 45, 45), error);
                    if ui.button("Çıktı konumunu düzenle").clicked() {
                        self.open_editor(3);
                    }
                }
            }

            if let Some((succeeded, message)) = recent_status {
                ui.add_space(10.0);
                ui.separator();
                ui.colored_label(
                    if succeeded {
                        studio_palette(ui.visuals().dark_mode).accent
                    } else {
                        egui::Color32::from_rgb(180, 45, 45)
                    },
                    message,
                );
            }
        });

        if let Some(path) = folder_to_open {
            if let Err(error) = open_output_directory(&path) {
                self.status = Some((false, error));
            }
        }
    }

    fn draw_section_card(
        &mut self,
        ui: &mut egui::Ui,
        index: usize,
        title: &str,
        summary: &str,
        description: &str,
    ) {
        let mut edit = false;
        studio_card(ui, |ui| {
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.colored_label(
                    palette.accent,
                    egui::RichText::new(format!("{:02}", index + 1))
                        .size(22.0)
                        .strong(),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new(title).size(18.0).strong());
                    ui.label(egui::RichText::new(description).size(13.0).weak());
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    edit = ui.button("Düzenle").clicked();
                });
            });
            ui.add_space(5.0);
            ui.label(egui::RichText::new(summary).size(14.0));
        });
        if edit {
            self.open_editor(index);
        }
    }

    fn show_editor_modal(&mut self, context: &egui::Context, is_building: bool) {
        const TITLES: [&str; 3] = ["Ürün bilgileri", "Uygulama dosyaları", "Kurulum tasarımı"];
        let title = if self.active_step == 3 {
            self.project.installer_kind.release_title()
        } else {
            TITLES[self.active_step]
        };
        let mut close_requested = false;
        let mut next_requested = false;
        let mut previous_requested = false;

        egui::Modal::new(egui::Id::new("setup-builder-section-editor")).show(context, |ui| {
            ui.set_min_width(760.0);
            ui.horizontal(|ui| {
                ui.label(egui::RichText::new(title).size(25.0).strong());
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    ui.label(
                        egui::RichText::new(format!("BÖLÜM {} / 4", self.active_step + 1))
                            .size(13.0)
                            .strong()
                            .weak(),
                    );
                });
            });
            ui.label(
                egui::RichText::new(format!(
                    "{} projesi · Düzenlemeler çalışma alanına ve setup önizlemesine yansır.",
                    self.project.installer_kind.label()
                ))
                .size(15.0)
                .weak(),
            );
            ui.add_space(4.0);
            ui.add(
                egui::ProgressBar::new((self.active_step + 1) as f32 / 4.0)
                    .desired_height(6.0)
                    .fill(egui::Color32::from_rgb(18, 121, 132)),
            );
            ui.separator();
            egui::ScrollArea::vertical()
                .max_height(560.0)
                .show(ui, |ui| self.draw_active_step(ui, is_building));
            if let Some((false, message)) = &self.status {
                ui.colored_label(egui::Color32::from_rgb(180, 45, 45), message);
            }
            ui.separator();

            ui.horizontal(|ui| {
                if ui.button("Kapat").clicked() {
                    close_requested = true;
                }
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    if self.active_step < 3 && ui.button("Sonraki bölüm").clicked() {
                        next_requested = true;
                    } else if self.active_step == 3 && ui.button("Tamam").clicked() {
                        close_requested = true;
                    }
                    if self.active_step > 0 && ui.button("Önceki bölüm").clicked() {
                        previous_requested = true;
                    }
                });
            });
        });

        if close_requested {
            self.dialog_open = false;
        } else if previous_requested {
            self.active_step = self.active_step.saturating_sub(1);
            self.status = None;
        } else if next_requested {
            self.advance_step();
        }
    }

    fn open_editor(&mut self, step: usize) {
        self.active_step = step.min(3);
        self.dialog_open = true;
        self.status = None;
    }

    fn draw_active_step(&mut self, ui: &mut egui::Ui, is_building: bool) {
        match self.active_step {
            0 => {
                ui.label("Kurulum projesinin temel kimliğini ve yayın kanalını belirleyin.");
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    text_row(ui, "Uygulama adı", &mut self.project.product_name);
                    text_row(ui, "Uygulama kimliği", &mut self.project.package_id);
                    text_row(ui, "Sürüm", &mut self.project.version);
                    field_label(ui, "Yayın kanalı");
                    egui::ComboBox::from_id_salt("builder-release-channel")
                        .width(ui.available_width())
                        .selected_text(match self.project.channel.as_str() {
                            "beta" => "Beta",
                            _ => "Stable",
                        })
                        .show_ui(ui, |ui| {
                            ui.selectable_value(
                                &mut self.project.channel,
                                "stable".to_owned(),
                                "Stable",
                            );
                            ui.selectable_value(
                                &mut self.project.channel,
                                "beta".to_owned(),
                                "Beta",
                            );
                        });
                    ui.add_space(12.0);
                });
                ui.add_space(8.0);
                ui.small(
                    "Uygulama kimliği yayinci.uygulama biçiminde olmalıdır (örnek: firma.urun).",
                );
            }
            1 => {
                ui.label("Kurulacak uygulamanın yayımlanmış dosyalarının bulunduğu klasörü seçin.");
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    folder_row(
                        ui,
                        "Kaynak klasörü",
                        &mut self.project.source_directory,
                        "Uygulama dosyalarının bulunduğu klasörü seçin",
                    );
                    ui.add_space(10.0);
                    field_label(ui, "Başlangıç dosyası");
                    let mut exe_requested = false;
                    ui.horizontal(|ui| {
                        let button_width = 112.0;
                        let width =
                            (ui.available_width() - button_width - ui.spacing().item_spacing.x)
                                .max(160.0);
                        ui.add_sized(
                            [width, 40.0],
                            egui::TextEdit::singleline(&mut self.project.entry_point)
                                .desired_width(width),
                        );
                        exe_requested = ui
                            .add_enabled_ui(!is_building, |ui| {
                                ui.add_sized([button_width, 40.0], egui::Button::new("EXE seç…"))
                                    .clicked()
                            })
                            .inner;
                    });
                    if exe_requested {
                        self.pick_entry_point();
                    }
                });
                ui.add_space(8.0);
                ui.small("Başlangıç dosyası, kaynak klasörünün içindeki göreli EXE yoludur. Boş bırakılabilir.");
                ui.small("Kaynak klasörde güvenli olmayan bağlantılar veya desteklenmeyen dosyalar varsa paket oluşturulmaz.");
                ui.add_space(12.0);

                // BRK-STUDIO-AV-SELECT-001: AV is a user-selected optional check,
                // separate from signed publisher verification and package SHA-256.
                studio_card(ui, |ui| {
                    ui.heading("Antivirüs taraması (isteğe bağlı)");
                    ui.checkbox(
                        &mut self.project.av_scan.enabled,
                        "Paket oluşturmadan önce antivirüsle tara",
                    );
                    if self.project.av_scan.enabled {
                        field_label(ui, "Antivirüs motoru");
                        egui::ComboBox::from_id_salt("studio-av-provider")
                            .width(ui.available_width())
                            .selected_text(self.project.av_scan.provider.label())
                            .show_ui(ui, |ui| {
                                for provider in AvProvider::ALL {
                                    ui.selectable_value(
                                        &mut self.project.av_scan.provider,
                                        provider,
                                        provider.label(),
                                    );
                                }
                            });
                        file_row(
                            ui,
                            "Motor uygulaması",
                            &mut self.project.av_scan.executable_path,
                            "Seçilen antivirüs programını seçin",
                            "Uygulama",
                            &[],
                        );
                        match self.project.av_scan.provider {
                            AvProvider::ClamAv => {
                                ui.small("ClamAV: clamscan ikili dosyasını seçin. İmza veritabanı kurulu ve güncel olmalıdır. Tehdit, tarama limiti ve hata durumunda paketleme durur.");
                            }
                            AvProvider::TurkuazAv => {
                                ui.small("TurkuazAV: derlenmiş CLI motorunu ve imzalı YARA-X kural dosyalarını seçin.");
                                file_row(
                                    ui,
                                    "YARA-X kuralları",
                                    &mut self.project.av_scan.rules_path,
                                    "YARA-X kural dosyasını seçin",
                                    "YARA-X kuralları",
                                    &["yar", "yara"],
                                );
                                file_row(
                                    ui,
                                    "Kural imzası",
                                    &mut self.project.av_scan.signature_path,
                                    "Kural imzasını seçin",
                                    "İmza",
                                    &["sig"],
                                );
                                file_row(
                                    ui,
                                    "Güvenilir açık anahtar",
                                    &mut self.project.av_scan.public_key_path,
                                    "Yayıncı açık anahtarını seçin",
                                    "PEM anahtarı",
                                    &["pem"],
                                );
                                file_row(
                                    ui,
                                    "YARA-X motoru",
                                    &mut self.project.av_scan.engine_path,
                                    "YARA-X programını seçin",
                                    "Uygulama",
                                    &[],
                                );
                                text_row(
                                    ui,
                                    "TurkuazAV program SHA-256",
                                    &mut self.project.av_scan.expected_cli_sha256,
                                );
                                text_row(
                                    ui,
                                    "Açık anahtar SHA-256",
                                    &mut self.project.av_scan.expected_public_key_sha256,
                                );
                                ui.checkbox(&mut self.project.av_scan.accept_pinned_no_match,
                                    "İmzalı kurallarda eşleşme yoksa devam et (yalnızca seçili kural kapsamı)");
                                ui.small("Eşleşme olmaması dosyanın virüssüz olduğunu kanıtlamaz. Bu izin kapalıysa TurkuazAV bilinmeyen sonucu engeller.");
                            }
                        }
                    } else {
                        ui.small("Kapalı: hiçbir antivirüs programı gerekmiyor. Paket SHA-256 ve mevcut güvenlik kontrolleri devam eder.");
                    }
                });
            }
            2 => {
                ui.label("Son kullanıcı setup penceresinin hedef klasörünü ve görünümünü seçin.");
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    text_row(
                        ui,
                        "Varsayılan kurulum klasörü",
                        &mut self.project.install_target,
                    );
                    ui.add_space(10.0);
                    field_label(ui, "Kurulum teması");
                    egui::ComboBox::from_id_salt("builder-setup-theme")
                        .width(ui.available_width())
                        .selected_text(
                            SetupThemePreset::ALL
                                .into_iter()
                                .find(|preset| preset.as_str() == self.project.theme_preset)
                                .map(SetupThemePreset::label)
                                .unwrap_or("Turkuaz"),
                        )
                        .show_ui(ui, |ui| {
                            for preset in SetupThemePreset::ALL {
                                let mut sample = self.project.clone();
                                sample.theme_preset = preset.as_str().to_owned();
                                let response = ui.selectable_value(
                                    &mut self.project.theme_preset,
                                    preset.as_str().to_owned(),
                                    preset.label(),
                                );
                                response.on_hover_ui(|ui| {
                                    ui.set_min_width(240.0);
                                    ui.label(format!("{} teması", preset.label()));
                                    if let Ok(theme) = theme_config_from_project(&sample) {
                                        for dark in [false, true] {
                                            let palette = theme.palette(dark);
                                            egui::Frame::new()
                                                .inner_margin(egui::Margin::same(12))
                                                .fill(color(palette.background))
                                                .show(ui, |ui| {
                                                    ui.colored_label(
                                                        color(palette.accent),
                                                        theme.brand_name(),
                                                    );
                                                    ui.label(
                                                        egui::RichText::new(theme.subtitle())
                                                            .color(color(palette.text)),
                                                    );
                                                });
                                        }
                                    }
                                });
                            }
                        });
                    ui.add_space(12.0);
                    text_row(
                        ui,
                        "Kurulum ekranında marka adı",
                        &mut self.project.brand_name,
                    );
                    text_row(ui, "Alt başlık", &mut self.project.subtitle);
                    field_label(ui, "Vurgu rengi");
                    let fallback = default_theme_accent(&self.project);
                    let mut rgb = parse_rgb_hex(&self.project.accent_color).unwrap_or(fallback);
                    ui.horizontal(|ui| {
                        let picker_width = 112.0;
                        let field_width =
                            (ui.available_width() - picker_width - ui.spacing().item_spacing.x)
                                .max(160.0);
                        ui.add_sized(
                            [field_width, 40.0],
                            egui::TextEdit::singleline(&mut self.project.accent_color)
                                .hint_text("#127984")
                                .desired_width(field_width),
                        );
                        ui.vertical(|ui| {
                            if ui.color_edit_button_srgb(&mut rgb).changed() {
                                self.project.accent_color =
                                    format!("#{:02X}{:02X}{:02X}", rgb[0], rgb[1], rgb[2]);
                            }
                        });
                    });
                    ui.add_space(12.0);
                });
                ui.add_space(8.0);
                ui.small("Vurgu rengi isteğe bağlıdır; HEX biçimi kullanın, örnek: #127984.");
                ui.small(
                    "Seçilen kurulum teması setup-theme.json olarak paketleme klasörüne yazılır.",
                );
            }
            _ => {
                ui.label(match self.project.installer_kind {
                    InstallerKind::Offline => "Önce uygulama paketini hazırlayın. Ardından NSIS veya sertifika gerektirmeyen Rust Standart Kurulum EXE'sini oluşturun. İmzalı dağıtım gelişmiş seçenektir.",
                    InstallerKind::Web => "Önce uygulama paketini hazırlayın, ardından imzalı yayın dosyalarını ve paketi HTTPS sunucunuza yükleyin. Çevrimiçi yükleyici uygulamayı buradan indirir.",
                });
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    folder_row(
                        ui,
                        "Çıktı üst klasörü",
                        &mut self.project.output_directory,
                        "Çıktıların yazılacağı klasörü seçin",
                    );
                });
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    ui.heading("Uygulama paketi");
                    ui.separator();
                    ui.label("• application.tzpkg — kurulacak uygulama dosyaları");
                    ui.label("• release-manifest.draft.yaml — henüz imzalanmamış yayın bilgileri");
                    ui.label("• setup-theme.json — son kullanıcının göreceği kurulum teması");
                    ui.label("• project.tzproj — tekrar açılabilir proje");
                });
                ui.add_space(12.0);

                if self.project.installer_kind == InstallerKind::Offline {
                    studio_card(ui, |ui| {
                        ui.heading("Standart Kurulum (Rust, imzasız)");
                        ui.label("İkinci adımda standard/TurkuazSetup.exe üretilir.");
                        ui.label("NSIS, başka derleyici veya imza sertifikası gerekmez.");
                        ui.small("İmzasız kurulumda yayıncı doğrulaması, güvenli güncelleme ve rollback yoktur.");
                    });
                    ui.add_space(12.0);
                    let mut signed_requested = false;
                    egui::CollapsingHeader::new("İmzalı dağıtım (gelişmiş)")
                        .default_open(false)
                        .show(ui, |ui| {
                            studio_card(ui, |ui| {
                                ui.label("İmzalı manifest, detached CMS imzası ve güven dosyası gerektirir.");
                        file_row(
                            ui,
                            "İmzalı yayın dosyası",
                            &mut self.project.signed_manifest_path,
                            "İmzalı yayın dosyasını seçin",
                            "YAML yayın dosyası",
                            &["yaml", "yml"],
                        );
                        file_row(
                            ui,
                            "Yayın imza dosyası",
                            &mut self.project.signature_path,
                            "Yayın imza dosyasını seçin",
                            "İmza",
                            &["p7s", "sig", "der"],
                        );
                        file_row(
                            ui,
                            "Yayıncı güven dosyası",
                            &mut self.project.trust_store_path,
                            "Yayıncı güven dosyasını seçin",
                            "YAML güven dosyası",
                            &["yaml", "yml"],
                        );
                        file_row(
                            ui,
                            "Kurulum motoru (EXE)",
                            &mut self.project.setup_stub_path,
                            "Kurulum sihirbazını içeren EXE dosyasını seçin",
                            "Windows uygulaması",
                            &["exe"],
                        );
                        file_row(
                            ui,
                            "İsteğe bağlı NSIS yedek motoru",
                            &mut self.project.makensis_path,
                            "NSIS makensis.exe seçin",
                            "Windows uygulaması",
                            &["exe"],
                        );
                        ui.small("NSIS yalnızca gelişmiş imzalı dağıtımın yedek motoru içindir.");
                                ui.small("Dağıtım klasöründeki manifest-trust.yaml güven dosyasını kurulum EXE dosyalarının yanında tutun.");
                                signed_requested = ui
                                    .add_enabled(
                                        !is_building && prepared_package_is_current(&self.project),
                                        egui::Button::new("İmzalı dağıtım oluştur"),
                                    )
                                    .clicked();
                            });
                        });
                    if signed_requested {
                        self.start_distribution();
                    }
                    ui.add_space(12.0);
                }

                if self.project.installer_kind == InstallerKind::Web {
                    studio_card(ui, |ui| {
                        ui.heading("Çevrimiçi Kurulum (Web Installer)");
                        ui.label("Küçük kurulum dosyası uygulamanın tamamını içermez. Gerekli dosyaları imzalı HTTPS yayınından indirir.");
                        file_row(
                            ui,
                            "Yayıncı güven dosyası",
                            &mut self.project.trust_store_path,
                            "Yayıncı güven dosyasını seçin",
                            "YAML güven politikası",
                            &["yaml", "yml"],
                        );
                        file_row(
                            ui,
                            "Kurulum motoru (EXE)",
                            &mut self.project.setup_stub_path,
                            "Kurulum sihirbazını içeren EXE dosyasını seçin",
                            "Windows uygulaması",
                            &["exe"],
                        );
                        ui.add_space(8.0);
                        field_label(ui, "Yayın kaynağı");
                        egui::ComboBox::from_id_salt("web-release-provider")
                            .width(ui.available_width())
                            .selected_text(self.project.web_release_source.label())
                            .show_ui(ui, |ui| {
                                for source in
                                    [WebReleaseSource::GithubReleases, WebReleaseSource::Https]
                                {
                                    ui.selectable_value(
                                        &mut self.project.web_release_source,
                                        source,
                                        source.label(),
                                    );
                                }
                            });
                        ui.add_space(8.0);
                        match self.project.web_release_source {
                            WebReleaseSource::GithubReleases => {
                                text_row(
                                    ui,
                                    "Herkese açık GitHub deposu (sahip/depo)",
                                    &mut self.project.github_repository,
                                );
                                text_row(
                                    ui,
                                    "Yayın etiketi (örnek: v1.0.0)",
                                    &mut self.project.github_release_tag,
                                );
                                text_row(
                                    ui,
                                    "İmzalı yayın dosyasının adı",
                                    &mut self.project.github_manifest_asset,
                                );
                                text_row(
                                    ui,
                                    "Yayın imza dosyasının adı (.p7s)",
                                    &mut self.project.github_signature_asset,
                                );
                                ui.small(
                                    "Yayın dosyasını, imzasını ve uygulama paketini GitHub Releases'e ayrıca yükleyin. İmzalı YAML içindeki paket adresi de erişilebilir HTTPS bağlantısı olmalıdır. Stable/Beta seçimi etiketi otomatik değiştirmez.",
                                );
                                ui.small(
                                    "Bu ilk sürüm herkese açık depoları destekler; erişim anahtarı kurulum EXE'sine eklenmez.",
                                );
                                if let Ok((manifest, signature)) =
                                    resolve_web_release_urls(&self.project)
                                {
                                    ui.small(format!("Yayın adresi: {manifest}"));
                                    ui.small(format!("İmza adresi: {signature}"));
                                }
                            }
                            WebReleaseSource::Https => {
                                text_row(
                                    ui,
                                    "İmzalı yayın dosyasının HTTPS adresi",
                                    &mut self.project.web_manifest_url,
                                );
                                text_row(
                                    ui,
                                    "Yayın imzasının HTTPS adresi (.p7s)",
                                    &mut self.project.web_signature_url,
                                );
                                ui.small(
                                    "Kendi sunucunuz veya CDN adresini kullanabilirsiniz. Erişim anahtarı içeren bağlantılar desteklenmez.",
                                );
                            }
                        }
                        ui.add_space(9.0);
                        ui.horizontal(|ui| {
                            if ui
                                .add_enabled(
                                    self.source_check_receiver.is_none()
                                        && self.build_receiver.is_none(),
                                    egui::Button::new("Bağlantıyı Test Et"),
                                )
                                .clicked()
                            {
                                self.start_source_check();
                            }
                            if self.source_check_receiver.is_some() {
                                ui.spinner();
                                ui.label("İmzalı yayın doğrulanıyor…");
                            }
                        });
                        if self.source_check_receiver.is_none()
                            && self.source_check_result.is_none()
                        {
                            if let Some((false, message)) = &self.status {
                                ui.colored_label(egui::Color32::from_rgb(180, 45, 45), message);
                            }
                        }
                        if let Some((checked, succeeded, message)) = &self.source_check_result {
                            if source_check_request(&self.project).ok().as_ref() == Some(checked) {
                                ui.colored_label(
                                    if *succeeded {
                                        egui::Color32::from_rgb(35, 135, 75)
                                    } else {
                                        egui::Color32::from_rgb(180, 45, 45)
                                    },
                                    message,
                                );
                            } else {
                                ui.small("Yayın ayarları değişti; bağlantıyı yeniden test edin.");
                            }
                        }
                        ui.small("İmzalı yayın dosyası ve imzası indirilip doğrulanır. Uygulama paketi yalnızca HEAD isteğiyle kontrol edilir; kurulum veya paket indirme yapılmaz.");
                        ui.small("Çevrimiçi yükleyici oluştur düğmesi, yayıncı güven dosyasını ve bu adresleri EXE içine ekler. Yayınlamadan önce EXE'yi dijital olarak imzalayın.");
                    });
                    ui.add_space(12.0);
                }

                studio_card(ui, |ui| {
                    ui.colored_label(
                        egui::Color32::from_rgb(170, 92, 22),
                        egui::RichText::new("Güvenli dağıtım için dijital imza gerekir").strong(),
                    );
                    ui.label(match self.project.installer_kind {
                        InstallerKind::Offline => "Önce release-manifest.draft.yaml yayın taslağını dijital olarak imzalayın. Ardından imzalı yayın dosyası ve yayıncı güven dosyasıyla Tam Kurulum oluşturun.",
                        InstallerKind::Web => "Önce imzalı yayın bilgilerini, imza dosyasını ve uygulama paketini HTTPS sunucusuna yükleyin; çevrimiçi yükleyiciyi yayınlamadan önce dijital olarak imzalayın.",
                    });
                    ui.label("Kurulum EXE dosyaları dağıtımdan önce dijital olarak (Authenticode) imzalanmalıdır. Gizli imzalama anahtarı projeye eklenmez.");
                    ui.hyperlink_to(
                        "Dijital imzalama rehberini aç",
                        "https://github.com/TurkuazLabs/TurkuazInstaller/blob/rewrite/rust-community/rust/RELEASE_SIGNING.md",
                    );
                });
            }
        }

        if is_building {
            ui.add_space(14.0);
            ui.horizontal(|ui| {
                ui.spinner();
                ui.label("Dosyalar hazırlanıyor. İşlem bitene kadar bu pencere açık kalır.");
            });
        }
    }

    fn draw_preview(&mut self, ui: &mut egui::Ui) {
        ui.add_space(7.0);
        ui.label(
            egui::RichText::new("Kurulum ekranı önizlemesi")
                .size(19.0)
                .strong(),
        );
        ui.label(
            egui::RichText::new("Yalnızca görsel önizleme; buradaki düğme kurulum başlatmaz.")
                .size(13.0)
                .weak(),
        );
        ui.add_space(12.0);
        ui.horizontal(|ui| {
            ui.label(egui::RichText::new("Önizleme:").size(13.0));
            ui.selectable_value(&mut self.preview_dark, false, "Açık");
            ui.selectable_value(&mut self.preview_dark, true, "Koyu");
        });
        ui.add_space(10.0);

        match self.theme_config() {
            Ok(theme) => {
                let palette = theme.palette(self.preview_dark);
                let background = color(palette.background);
                let surface = color(palette.surface);
                let text = color(palette.text);
                let accent = color(palette.accent);
                egui::Frame::group(ui.style())
                    .inner_margin(egui::Margin::symmetric(16, 14))
                    .fill(background)
                    .stroke(egui::Stroke::new(1.0, accent))
                    .show(ui, |ui| {
                        ui.set_min_width(240.0);
                        ui.colored_label(
                            accent,
                            egui::RichText::new("TURKUAZ SETUP").size(12.0).strong(),
                        );
                        ui.add_space(10.0);
                        egui::Frame::group(ui.style())
                            .inner_margin(egui::Margin::symmetric(14, 12))
                            .fill(surface)
                            .show(ui, |ui| {
                                ui.label(
                                    egui::RichText::new(theme.brand_name())
                                        .size(21.0)
                                        .strong()
                                        .color(text),
                                );
                                ui.add_space(4.0);
                                ui.label(
                                    egui::RichText::new(theme.subtitle()).size(15.0).color(text),
                                );
                                ui.separator();
                                ui.label(
                                    egui::RichText::new(&self.project.product_name)
                                        .size(16.0)
                                        .strong()
                                        .color(text),
                                );
                                ui.add_space(10.0);
                                let button_width = ui.available_width();
                                ui.add_enabled_ui(false, |ui| {
                                    ui.add_sized(
                                        [button_width, 40.0],
                                        egui::Button::new("Kuruluma devam et").fill(accent),
                                    );
                                });
                            });
                    });
                ui.add_space(10.0);
                ui.label(
                    egui::RichText::new(
                        "Görünümü değiştirmek için soldaki 03 • Kurulum tasarımı bölümünü açın.",
                    )
                    .size(13.0)
                    .weak(),
                );
            }
            Err(error) => {
                ui.colored_label(egui::Color32::from_rgb(180, 45, 45), "Tema ayarı geçersiz.");
                ui.small(error);
            }
        }
    }

    fn theme_config(&self) -> Result<SetupThemeConfig, String> {
        theme_config_from_project(&self.project)
    }

    fn advance_step(&mut self) {
        if let Err(error) = validate_project_step(&self.project, self.active_step) {
            self.status = Some((false, error));
            return;
        }
        if self.active_step < 3 {
            self.active_step += 1;
        }
        self.status = None;
    }

    fn pick_entry_point(&mut self) {
        let mut picker = FileDialog::new().add_filter("Windows application", &["exe"]);
        let source = PathBuf::from(self.project.source_directory.trim());
        if source.is_dir() {
            picker = picker.set_directory(source);
        }
        if let Some(path) = picker.pick_file() {
            match path.strip_prefix(PathBuf::from(self.project.source_directory.trim())) {
                Ok(relative) if !relative.as_os_str().is_empty() => {
                    self.project.entry_point = relative.to_string_lossy().replace('\\', "/");
                    self.status = None;
                }
                _ => {
                    self.status = Some((
                        false,
                        "Başlangıç EXE dosyası seçilen kaynak klasörünün içinde olmalıdır."
                            .to_owned(),
                    ));
                }
            }
        }
    }

    // BRK-STUDIO-PROJECT-OPEN-001: Never replace an active project silently.
    // Canceling the file picker retains every current setting.
    fn request_open_project(&mut self) {
        if self.has_project {
            self.pending_open_requested = true;
        } else {
            self.open_project();
        }
    }

    fn confirm_open_project(&mut self, context: &egui::Context) {
        if !self.pending_open_requested {
            return;
        }
        let mut proceed = false;
        let mut cancel = false;
        egui::Modal::new(egui::Id::new("setup-studio-open-project-confirmation")).show(
            context,
            |ui| {
                ui.set_min_width(460.0);
                ui.heading("Başka bir proje açılsın mı?");
                ui.label(
                    "Mevcut çalışma alanı kapanacak; kaydedilmemiş değişiklikler kaybolabilir.",
                );
                ui.horizontal(|ui| {
                    if ui.button("Vazgeç").clicked() {
                        cancel = true;
                    }
                    if ui.button("Proje seç").clicked() {
                        proceed = true;
                    }
                });
            },
        );
        if cancel {
            self.pending_open_requested = false;
        } else if proceed {
            self.pending_open_requested = false;
            self.open_project();
        }
    }

    fn open_project(&mut self) {
        if let Some(path) = FileDialog::new()
            .add_filter("Turkuaz setup project", &["tzproj"])
            .pick_file()
        {
            match read_project(&path) {
                Ok(project) => {
                    self.project = project;
                    self.source_check_result = None;
                    self.has_project = true;
                    self.workspace_open = true;
                    self.pending_new_kind = None;
                    self.pending_open_requested = false;
                    self.project_path = Some(path.clone());
                    self.active_step = 0;
                    self.dialog_open = false;
                    self.status = Some((true, format!("Proje açıldı: {}", path.display())));
                }
                Err(error) => self.status = Some((false, error)),
            }
        }
    }

    fn save_project(&mut self) {
        if let Some(path) = &self.project_path {
            match write_project(path, &self.project) {
                Ok(()) => {
                    self.status = Some((true, format!("Proje kaydedildi: {}", path.display())));
                }
                Err(error) => self.status = Some((false, error)),
            }
            return;
        }

        let result = default_documents_root()
            .and_then(|documents| save_new_project_in_documents(&self.project, &documents));
        match result {
            Ok(path) => {
                self.project_path = Some(path.clone());
                self.status = Some((true, format!("Proje kaydedildi: {}", path.display())));
            }
            Err(error) => self.status = Some((false, error)),
        }
    }

    fn save_project_as(&mut self) {
        let mut dialog = FileDialog::new()
            .add_filter("Turkuaz setup project", &["tzproj"])
            .set_file_name("project.tzproj");
        if let Ok(directory) = default_documents_root()
            .and_then(|documents| documents_project_directory(&self.project, &documents))
        {
            if directory.is_dir() {
                dialog = dialog.set_directory(directory);
            } else if let Some(parent) = directory.parent() {
                if parent.is_dir() {
                    dialog = dialog.set_directory(parent);
                }
            }
        }
        if let Some(mut path) = dialog.save_file() {
            if path.extension().is_none() {
                path.set_extension("tzproj");
            }
            match write_project(&path, &self.project) {
                Ok(()) => {
                    self.project_path = Some(path.clone());
                    self.status = Some((true, format!("Proje kaydedildi: {}", path.display())));
                }
                Err(error) => self.status = Some((false, error)),
            }
        }
    }

    fn start_build(&mut self) {
        for step in 0..4 {
            if let Err(error) = validate_project_step(&self.project, step) {
                self.active_step = step;
                self.dialog_open = true;
                self.status = Some((false, error));
                return;
            }
        }

        let project = self.project.clone();
        let (sender, receiver) = mpsc::channel();
        self.build_receiver = Some(receiver);
        self.dialog_open = false;
        self.status = None;
        thread::spawn(move || {
            let _ = sender.send(build_project(project));
        });
    }

    fn start_source_check(&mut self) {
        if self.project.installer_kind != InstallerKind::Web
            || self.source_check_receiver.is_some()
            || self.build_receiver.is_some()
        {
            return;
        }
        let request = match source_check_request(&self.project) {
            Ok(request) => request,
            Err(error) => {
                self.source_check_result = None;
                self.status = Some((false, error));
                return;
            }
        };
        let (sender, receiver) = mpsc::channel();
        self.source_check_receiver = Some(receiver);
        self.source_check_result = None;
        self.status = None;
        thread::spawn(move || {
            let result = run_source_check(&request);
            let _ = sender.send((request, result));
        });
    }

    fn poll_source_check(&mut self) {
        let outcome = match self.source_check_receiver.as_ref() {
            Some(receiver) => match receiver.try_recv() {
                Ok(value) => Some(value),
                Err(TryRecvError::Empty) => None,
                Err(TryRecvError::Disconnected) => Some((
                    SourceCheckRequest {
                        package_id: String::new(),
                        manifest_url: String::new(),
                        signature_url: String::new(),
                        trust_file: PathBuf::new(),
                    },
                    Err("Bağlantı testi beklenmedik şekilde sonlandı.".to_owned()),
                )),
            },
            None => None,
        };
        if let Some((request, result)) = outcome {
            self.source_check_receiver = None;
            let still_current =
                source_check_request(&self.project).is_ok_and(|current| current == request);
            if !still_current {
                self.source_check_result = None;
                self.status = Some((
                    false,
                    "Yayın ayarları değişti; bağlantıyı yeniden test edin.".to_owned(),
                ));
                return;
            }
            self.source_check_result = Some(match result {
                Ok(message) => (request, true, message),
                Err(message) => (request, false, message),
            });
        }
    }

    fn start_web_build(&mut self) {
        if self.project.installer_kind != InstallerKind::Web {
            self.status = Some((
                false,
                "Bu proje Tam Kurulum türünde. Yeni bir Çevrimiçi Kurulum projesi oluşturun."
                    .to_owned(),
            ));
            return;
        }
        for step in [0, 2, 3] {
            if let Err(error) = validate_project_step(&self.project, step) {
                self.open_editor(step);
                self.status = Some((false, error));
                return;
            }
        }
        if let Err(error) = ensure_prepared_package_is_current(&self.project) {
            self.status = Some((false, error));
            return;
        }
        if let Err(error) = validate_web_project(&self.project) {
            self.open_editor(3);
            self.status = Some((false, error));
            return;
        }
        let project = self.project.clone();
        let (sender, receiver) = mpsc::channel();
        self.build_receiver = Some(receiver);
        self.dialog_open = false;
        self.status = None;
        thread::spawn(move || {
            let _ = sender.send(build_web_distribution(project));
        });
    }

    // BRK-STUDIO-STANDARD-001: Native unsigned setup never bypasses signed releases.
    fn start_standard_build(&mut self) {
        if self.project.installer_kind != InstallerKind::Offline {
            self.status = Some((
                false,
                "Standart Kurulum yalnızca Tam Kurulum içindir.".to_owned(),
            ));
            return;
        }
        for step in 0..4 {
            if let Err(error) = validate_project_step(&self.project, step) {
                self.open_editor(step);
                self.status = Some((false, error));
                return;
            }
        }
        if let Err(error) = ensure_prepared_package_is_current(&self.project) {
            self.status = Some((false, error));
            return;
        }
        let project = self.project.clone();
        let (sender, receiver) = mpsc::channel();
        self.build_receiver = Some(receiver);
        self.dialog_open = false;
        self.status = None;
        thread::spawn(move || {
            let _ = sender.send(build_native_standard_setup(project));
        });
    }

    fn start_distribution(&mut self) {
        if self.project.installer_kind != InstallerKind::Offline {
            self.status = Some((
                false,
                "Bu proje Çevrimiçi Kurulum türünde. Tam Kurulum için ayrı proje oluşturun."
                    .to_owned(),
            ));
            return;
        }
        for step in [0, 2, 3] {
            if let Err(error) = validate_project_step(&self.project, step) {
                self.active_step = step;
                self.dialog_open = true;
                self.status = Some((false, error));
                return;
            }
        }

        if let Err(error) = ensure_prepared_package_is_current(&self.project) {
            self.status = Some((false, error));
            return;
        }
        if [
            &self.project.signed_manifest_path,
            &self.project.signature_path,
            &self.project.trust_store_path,
            &self.project.setup_stub_path,
        ]
        .iter()
        .any(|path| path.trim().is_empty())
        {
            self.active_step = 3;
            self.dialog_open = true;
            self.status = Some((
                false,
                "Tam Kurulum oluşturmak için imzalı yayın dosyası, imza dosyası, yayıncı güven dosyası ve kurulum motorunu seçin."
                    .to_owned(),
            ));
            return;
        }

        let project = self.project.clone();
        let (sender, receiver) = mpsc::channel();
        self.build_receiver = Some(receiver);
        self.dialog_open = false;
        self.status = None;
        thread::spawn(move || {
            let _ = sender.send(build_distribution(project));
        });
    }

    fn poll_build(&mut self) {
        let result = match self.build_receiver.as_ref() {
            Some(receiver) => match receiver.try_recv() {
                Ok(result) => Some(result),
                Err(TryRecvError::Empty) => None,
                Err(TryRecvError::Disconnected) => Some(Err(
                    "Paket derleme işlemi beklenmedik şekilde sonlandı.".to_owned(),
                )),
            },
            None => None,
        };

        if let Some(result) = result {
            self.build_receiver = None;
            self.status = Some(match result {
                Ok(message) => (true, message),
                Err(message) => (false, message),
            });
        }
    }
}

// Visible output state is derived from actual files; an earlier status message is
// not treated as proof that a file still exists.
fn package_output_directory(project: &SavedProject) -> Result<PathBuf, String> {
    let package_id = PackageId::parse(&project.package_id)
        .map_err(|error| format!("Uygulama kimliği geçersiz: {error}"))?;
    let version = SemanticVersion::parse(&project.version)
        .map_err(|error| format!("Sürüm geçersiz: {error}"))?;
    let parent = resolve_path(&project.output_directory)?;
    Ok(parent.join(format!("{package_id}-{version}")))
}

// BRK-STUDIO-PACKAGE-STATE-001: A file left by an earlier project is not
// proof that today's source, target and theme have been packaged.
// Signing inputs can change without invalidating the first-stage package.
fn package_build_inputs_match(current: &SavedProject, packaged: &SavedProject) -> bool {
    current.installer_kind == packaged.installer_kind
        && current.product_name == packaged.product_name
        && current.package_id == packaged.package_id
        && current.version == packaged.version
        && current.channel == packaged.channel
        && current.source_directory == packaged.source_directory
        && current.entry_point == packaged.entry_point
        && current.install_target == packaged.install_target
        && current.output_directory == packaged.output_directory
        && current.theme_preset == packaged.theme_preset
        && current.brand_name == packaged.brand_name
        && current.subtitle == packaged.subtitle
        && current.accent_color == packaged.accent_color
        && current.av_scan == packaged.av_scan
}

fn ensure_prepared_package_is_current(project: &SavedProject) -> Result<PathBuf, String> {
    let directory = package_output_directory(project)?;
    if !regular_directory_exists(&directory)
        || !regular_file_exists(&directory.join("application.tzpkg"))
    {
        return Err("Önce 1 Uygulama paketini hazırla işlemini tamamlayın.".to_owned());
    }
    let packaged = read_project(&directory.join("project.tzproj")).map_err(|_| {
        "Paket proje kaydı eksik veya geçersiz; yeni sürümle tekrar paket hazırlayın.".to_owned()
    })?;
    if !package_build_inputs_match(project, &packaged) {
        return Err("Paket, güncel proje ayarlarıyla eşleşmiyor. Yeni sürümle uygulama paketini yeniden hazırlayın.".to_owned());
    }
    Ok(directory)
}

fn prepared_package_is_current(project: &SavedProject) -> bool {
    ensure_prepared_package_is_current(project).is_ok()
}

fn regular_file_exists(path: &Path) -> bool {
    fs::symlink_metadata(path)
        .is_ok_and(|metadata| metadata.is_file() && !metadata.file_type().is_symlink())
}

fn regular_directory_exists(path: &Path) -> bool {
    fs::symlink_metadata(path)
        .is_ok_and(|metadata| metadata.is_dir() && !metadata.file_type().is_symlink())
}

fn open_output_directory(directory: &Path) -> Result<(), String> {
    if !regular_directory_exists(directory) {
        return Err("Çıktı klasörü bulunamadı veya güvenli bir klasör değil.".to_owned());
    }

    let explorer = std::env::var_os("SystemRoot")
        .map(|root| PathBuf::from(root).join("explorer.exe"))
        .unwrap_or_else(|| PathBuf::from("explorer.exe"));
    Command::new(explorer)
        .arg(directory)
        .spawn()
        .map(|_| ())
        .map_err(|error| format!("Windows Dosya Gezgini açılamadı: {error}"))
}

fn navigation_item(ui: &mut egui::Ui, selected: bool, label: &str) -> egui::Response {
    let width = ui.available_width();
    ui.add_sized(
        [width, 44.0],
        egui::Button::selectable(selected, egui::RichText::new(label).size(15.0).strong())
            .frame_when_inactive(selected),
    )
}

fn studio_card(ui: &mut egui::Ui, contents: impl FnOnce(&mut egui::Ui)) {
    let palette = studio_palette(ui.visuals().dark_mode);
    egui::Frame::group(ui.style())
        .inner_margin(egui::Margin::symmetric(18, 16))
        .fill(palette.surface)
        .stroke(egui::Stroke::new(1.0, palette.border))
        .show(ui, contents);
}

fn field_label(ui: &mut egui::Ui, label: &str) {
    ui.label(egui::RichText::new(label).size(14.0).strong());
    ui.add_space(5.0);
}

fn text_row(ui: &mut egui::Ui, label: &str, value: &mut String) {
    field_label(ui, label);
    let width = ui.available_width();
    ui.add_sized(
        [width, 40.0],
        egui::TextEdit::singleline(value).desired_width(width),
    );
    ui.add_space(12.0);
}

fn folder_row(ui: &mut egui::Ui, label: &str, value: &mut String, picker_title: &str) {
    field_label(ui, label);
    ui.horizontal(|ui| {
        let button_width = 112.0;
        let width = (ui.available_width() - button_width - ui.spacing().item_spacing.x).max(160.0);
        ui.add_sized(
            [width, 40.0],
            egui::TextEdit::singleline(value).desired_width(width),
        );
        if ui
            .add_sized([button_width, 40.0], egui::Button::new("Gözat…"))
            .clicked()
        {
            if let Some(path) = FileDialog::new().set_title(picker_title).pick_folder() {
                *value = path.to_string_lossy().into_owned();
            }
        }
    });
    ui.add_space(12.0);
}

fn file_row(
    ui: &mut egui::Ui,
    label: &str,
    value: &mut String,
    picker_title: &str,
    filter_name: &str,
    extensions: &[&str],
) {
    field_label(ui, label);
    ui.horizontal(|ui| {
        let button_width = 112.0;
        let width = (ui.available_width() - button_width - ui.spacing().item_spacing.x).max(160.0);
        ui.add_sized(
            [width, 40.0],
            egui::TextEdit::singleline(value).desired_width(width),
        );
        if ui
            .add_sized([button_width, 40.0], egui::Button::new("Gözat…"))
            .clicked()
        {
            let picker = FileDialog::new().set_title(picker_title);
            // Executables on Linux/macOS usually have no .exe extension.
            let picker = if extensions.is_empty() {
                picker
            } else {
                picker.add_filter(filter_name, extensions)
            };
            if let Some(path) = picker.pick_file() {
                *value = path.to_string_lossy().into_owned();
            }
        }
    });
    ui.add_space(12.0);
}

fn validate_project_step(project: &SavedProject, step: usize) -> Result<(), String> {
    if step == 0 {
        if project.product_name.trim().is_empty()
            || project.product_name.chars().count() > 128
            || project.product_name.chars().any(char::is_control)
        {
            return Err(
                "Uygulama adı boş olamaz, 128 karakteri aşamaz ve kontrol karakteri içeremez."
                    .to_owned(),
            );
        }
        PackageId::parse(&project.package_id)
            .map_err(|error| format!("Uygulama kimliği geçersiz: {error}"))?;
        SemanticVersion::parse(&project.version)
            .map_err(|error| format!("Sürüm geçersiz: {error}"))?;
        if !matches!(project.channel.as_str(), "stable" | "beta") {
            return Err("Yayın kanalı stable veya beta olmalıdır.".to_owned());
        }
    }

    if step == 1 {
        let source = resolve_path(&project.source_directory)?;
        let metadata = fs::symlink_metadata(&source)
            .map_err(|error| format!("Kaynak klasörü okunamadı: {error}"))?;
        if metadata.file_type().is_symlink() || !metadata.is_dir() {
            return Err("Kaynak klasörü normal bir klasör olmalıdır.".to_owned());
        }
        let entry_point = project.entry_point.trim();
        if !entry_point.is_empty() {
            let path = Path::new(entry_point);
            if path.is_absolute()
                || path
                    .components()
                    .any(|component| !matches!(component, Component::Normal(_)))
            {
                return Err(
                    "Başlangıç dosyası kaynak klasörüne göre güvenli bir göreli yol olmalıdır."
                        .to_owned(),
                );
            }
        }
        project.av_scan.validate()?;
    }

    if step == 2 {
        if project.install_target.trim().is_empty()
            || project.install_target.chars().any(char::is_control)
        {
            return Err(
                "Varsayılan kurulum klasörü zorunludur ve kontrol karakteri içeremez.".to_owned(),
            );
        }
        theme_config_from_project(project)?;
    }

    if step == 3 {
        let output_directory = resolve_path(&project.output_directory)?;
        validate_or_create_output_parent(&output_directory, false)?;
    }

    Ok(())
}

// BRK-RUST-STUDIO-OUTPUT-001: Validate without side effects in the editor,
// then create missing output ancestors only when building the first package.
// Walk components instead of create_dir_all so existing links/junctions and
// non-directory ancestors are not silently followed.
fn validate_or_create_output_parent(path: &Path, create_missing: bool) -> Result<(), String> {
    if path
        .components()
        .any(|component| matches!(component, Component::ParentDir))
    {
        return Err("Çıktı klasörü yolu '..' bileşeni içeremez.".to_owned());
    }

    let mut current = PathBuf::new();
    for component in path.components() {
        current.push(component.as_os_str());
        if matches!(component, Component::Prefix(_) | Component::RootDir) {
            continue;
        }
        let metadata = match fs::symlink_metadata(&current) {
            Ok(metadata) => metadata,
            Err(error) if error.kind() == io::ErrorKind::NotFound && create_missing => {
                match fs::create_dir(&current) {
                    Ok(()) => {}
                    Err(error) if error.kind() == io::ErrorKind::AlreadyExists => {}
                    Err(error) => {
                        return Err(format!("Çıktı üst klasörü oluşturulamadı: {error}"));
                    }
                }
                fs::symlink_metadata(&current)
                    .map_err(|error| format!("Çıktı üst klasörü doğrulanamadı: {error}"))?
            }
            Err(error) if error.kind() == io::ErrorKind::NotFound => continue,
            Err(error) => return Err(format!("Çıktı üst klasörü okunamadı: {error}")),
        };
        if !metadata.is_dir()
            || metadata.file_type().is_symlink()
            || is_windows_reparse_point(&metadata)
        {
            return Err(
                "Çıktı üst klasörü ve üst dizinleri normal klasörler olmalıdır.".to_owned(),
            );
        }
    }
    Ok(())
}

#[cfg(windows)]
fn is_windows_reparse_point(metadata: &fs::Metadata) -> bool {
    use std::os::windows::fs::MetadataExt;
    // FILE_ATTRIBUTE_REPARSE_POINT covers junctions as well as symlinks.
    metadata.file_attributes() & 0x0400 != 0
}

#[cfg(not(windows))]
fn is_windows_reparse_point(_metadata: &fs::Metadata) -> bool {
    false
}

// BRK-STUDIO-COLOR-001: The picker edits an optional RGB hex field while
// keeping manually entered values backward-compatible with saved .tzproj files.
fn parse_rgb_hex(value: &str) -> Option<[u8; 3]> {
    let hex = value.trim().strip_prefix('#')?;
    if hex.len() != 6 {
        return None;
    }
    let r = u8::from_str_radix(&hex[0..2], 16).ok()?;
    let g = u8::from_str_radix(&hex[2..4], 16).ok()?;
    let b = u8::from_str_radix(&hex[4..6], 16).ok()?;
    Some([r, g, b])
}

fn default_theme_accent(project: &SavedProject) -> [u8; 3] {
    let mut sample = project.clone();
    sample.accent_color.clear();
    theme_config_from_project(&sample)
        .map(|theme| theme.palette(false).accent)
        .unwrap_or([18, 121, 132])
}

fn theme_config_from_project(project: &SavedProject) -> Result<SetupThemeConfig, String> {
    let preset = project
        .theme_preset
        .parse::<SetupThemePreset>()
        .map_err(|error| error.to_string())?;
    let mut value = serde_json::json!({
        "schemaVersion": 1,
        "preset": preset,
    });
    let object = value
        .as_object_mut()
        .ok_or_else(|| "Tema ayarı oluşturulamadı.".to_owned())?;

    if !project.brand_name.trim().is_empty() {
        object.insert(
            "brandName".to_owned(),
            serde_json::Value::String(project.brand_name.trim().to_owned()),
        );
    }
    if !project.subtitle.trim().is_empty() {
        object.insert(
            "subtitle".to_owned(),
            serde_json::Value::String(project.subtitle.trim().to_owned()),
        );
    }
    if !project.accent_color.trim().is_empty() {
        object.insert(
            "accentColor".to_owned(),
            serde_json::Value::String(project.accent_color.trim().to_owned()),
        );
    }

    let bytes = serde_json::to_vec(&value)
        .map_err(|error| format!("Tema ayarı JSON'a dönüştürülemedi: {error}"))?;
    SetupThemeConfig::from_json(&bytes).map_err(|error| error.to_string())
}

// BRK-STUDIO-SAVE-001: New projects default to Documents/TurkuazInstaller/
// <product name>/project.tzproj, independent from the executable's CWD.
// Existing project paths remain intact; no implicit overwrite on first save.
#[cfg(windows)]
fn default_documents_root() -> Result<PathBuf, String> {
    windows_documents::documents_path()
}

#[cfg(not(windows))]
fn default_documents_root() -> Result<PathBuf, String> {
    std::env::var_os("HOME")
        .map(|home| PathBuf::from(home).join("Documents"))
        .ok_or_else(|| "Kullanıcının Belgeler klasörü bulunamadı.".to_owned())
}

fn safe_project_folder_name(name: &str) -> String {
    let cleaned: String = name
        .trim()
        .chars()
        .take(64)
        .map(|ch| {
            if ch.is_alphanumeric() || matches!(ch, '-' | '_' | ' ') {
                ch
            } else {
                '_'
            }
        })
        .collect();
    let cleaned = cleaned.trim_matches([' ', '_', '.']);
    if cleaned.is_empty() {
        "Yeni Proje".to_owned()
    } else {
        // Avoid Windows reserved device names even when the user names the product so.
        let upper = cleaned.to_ascii_uppercase();
        if matches!(
            upper.as_str(),
            "CON" | "PRN" | "AUX" | "NUL" | "COM1" | "LPT1"
        ) {
            format!("Proje_{cleaned}")
        } else {
            cleaned.to_owned()
        }
    }
}

fn documents_project_directory(
    project: &SavedProject,
    documents: &Path,
) -> Result<PathBuf, String> {
    if !documents.is_absolute() {
        return Err("Belgeler dizini mutlak olmalıdır.".to_owned());
    }
    Ok(documents
        .join("TurkuazInstaller")
        .join(safe_project_folder_name(&project.product_name)))
}

fn save_new_project_in_documents(
    project: &SavedProject,
    documents: &Path,
) -> Result<PathBuf, String> {
    let directory = documents_project_directory(project, documents)?;
    validate_or_create_output_parent(&directory, true)?;
    let path = directory.join("project.tzproj");
    let mut saved = project.clone();
    saved.schema_version = PROJECT_SCHEMA_VERSION;
    let bytes = serde_json::to_vec_pretty(&saved)
        .map_err(|error| format!("Proje JSON'a dönüştürülemedi: {error}"))?;
    write_new_file(&path, &bytes)
        .map_err(|error| format!("{error}. Başka bir proje varsa 'Farklı kaydet…' kullanın."))?;
    Ok(path)
}

fn read_project(path: &Path) -> Result<SavedProject, String> {
    let metadata =
        fs::symlink_metadata(path).map_err(|error| format!("Proje dosyası açılamadı: {error}"))?;
    if metadata.file_type().is_symlink()
        || !metadata.is_file()
        || metadata.len() == 0
        || metadata.len() > MAXIMUM_PROJECT_FILE_BYTES
    {
        return Err("Proje, 1 MiB sınırında normal ve boş olmayan bir dosya olmalıdır.".to_owned());
    }

    let mut bytes = Vec::with_capacity(metadata.len() as usize);
    fs::File::open(path)
        .map_err(|error| format!("Proje dosyası açılamadı: {error}"))?
        .take(MAXIMUM_PROJECT_FILE_BYTES + 1)
        .read_to_end(&mut bytes)
        .map_err(|error| format!("Proje dosyası okunamadı: {error}"))?;
    if bytes.len() as u64 != metadata.len() || bytes.len() as u64 > MAXIMUM_PROJECT_FILE_BYTES {
        return Err("Proje dosyası okunurken değişti veya boyut sınırını aştı.".to_owned());
    }

    let project: SavedProject = serde_json::from_slice(&bytes)
        .map_err(|error| format!("Proje dosyası geçersiz: {error}"))?;
    if project.schema_version != PROJECT_SCHEMA_VERSION {
        return Err("Bu kurulum projesinin sürümü desteklenmiyor.".to_owned());
    }
    project
        .theme_preset
        .parse::<SetupThemePreset>()
        .map_err(|error| error.to_string())?;
    Ok(project)
}

fn write_project(path: &Path, project: &SavedProject) -> Result<(), String> {
    let mut project = project.clone();
    project.schema_version = PROJECT_SCHEMA_VERSION;
    let bytes = serde_json::to_vec_pretty(&project)
        .map_err(|error| format!("Proje JSON'a dönüştürülemedi: {error}"))?;
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create(true)
        .truncate(true)
        .open(path)
        .map_err(|error| format!("Proje kaydedilemedi: {error}"))?;
    file.write_all(&bytes)
        .map_err(|error| format!("Proje yazılamadı: {error}"))?;
    file.sync_all()
        .map_err(|error| format!("Proje diske yazılamadı: {error}"))
}

fn resolve_path(value: &str) -> Result<PathBuf, String> {
    let value = value.trim();
    if value.is_empty() {
        return Err("Klasör yolu zorunludur.".to_owned());
    }
    let path = PathBuf::from(value);
    if path.is_absolute() {
        Ok(path)
    } else {
        std::env::current_dir()
            .map(|current| current.join(path))
            .map_err(|error| format!("Çalışma klasörü okunamadı: {error}"))
    }
}

// BRK-WEB-SOURCE-002: signed HTTPS release URLs are resolved without embedding
// tokens, granting access to private repositories or bypassing signature checks.
fn validate_release_component(value: &str, label: &str, allow_plus: bool) -> Result<(), String> {
    if value.is_empty()
        || value.len() > 128
        || value == "."
        || value == ".."
        || !value.bytes().all(|byte| {
            byte.is_ascii_alphanumeric()
                || matches!(byte, b'.' | b'_' | b'-')
                || (allow_plus && byte == b'+')
        })
    {
        return Err(format!(
            "{label} yalnızca harf, rakam, nokta, tire ve alt çizgi içermelidir."
        ));
    }
    Ok(())
}

fn validate_https_release_address(label: &str, value: &str) -> Result<(), String> {
    if value.len() > 2048 {
        return Err(format!("{label} bağlantısı çok uzun."));
    }
    let url = Url::parse(value.trim()).map_err(|_| format!("{label} bağlantısı geçersiz."))?;
    if url.scheme() != "https"
        || url.host_str().is_none()
        || !url.username().is_empty()
        || url.password().is_some()
        || url.fragment().is_some()
        || url.query().is_some()
    {
        return Err(format!(
            "{label} kullanıcı bilgisi veya erişim anahtarı içermeyen HTTPS URL olmalıdır."
        ));
    }
    Ok(())
}

fn resolve_web_release_urls(project: &SavedProject) -> Result<(String, String), String> {
    let (manifest, signature) = match project.web_release_source {
        WebReleaseSource::Https => (
            project.web_manifest_url.trim().to_owned(),
            project.web_signature_url.trim().to_owned(),
        ),
        WebReleaseSource::GithubReleases => {
            let repository = project.github_repository.trim();
            let mut components = repository.split('/');
            let owner = components.next().unwrap_or_default();
            let name = components.next().unwrap_or_default();
            if components.next().is_some() {
                return Err("GitHub deposu sahip/depo biçiminde olmalıdır.".to_owned());
            }
            validate_release_component(owner, "GitHub sahibi", false)?;
            validate_release_component(name, "GitHub depo adı", false)?;
            let tag = project.github_release_tag.trim();
            validate_release_component(tag, "GitHub yayın etiketi", true)?;
            let manifest_asset = project.github_manifest_asset.trim();
            let signature_asset = project.github_signature_asset.trim();
            validate_release_component(manifest_asset, "Yayın dosyası adı", false)?;
            validate_release_component(signature_asset, "İmza dosyası adı", false)?;
            if manifest_asset == signature_asset {
                return Err("Yayın dosyası ve imza dosyası farklı olmalıdır.".to_owned());
            }
            let release_base = format!("https://github.com/{owner}/{name}/releases/download/{tag}");
            (
                format!("{release_base}/{manifest_asset}"),
                format!("{release_base}/{signature_asset}"),
            )
        }
    };
    validate_https_release_address("Yayın dosyası", &manifest)?;
    validate_https_release_address("Yayın imzası", &signature)?;
    if manifest == signature {
        return Err("Yayın dosyası ve imzasının adresleri farklı olmalıdır.".to_owned());
    }
    Ok((manifest, signature))
}

fn source_check_request(project: &SavedProject) -> Result<SourceCheckRequest, String> {
    if project.installer_kind != InstallerKind::Web {
        return Err("Bağlantı testi yalnızca Çevrimiçi Kurulum içindir.".to_owned());
    }
    PackageId::parse(&project.package_id)
        .map_err(|error| format!("Uygulama kimliği geçersiz: {error}"))?;
    let (manifest_url, signature_url) = resolve_web_release_urls(project)?;
    let trust_file = require_regular_file("Yayıncı güven dosyası", &project.trust_store_path)?;
    Ok(SourceCheckRequest {
        package_id: project.package_id.trim().to_owned(),
        manifest_url,
        signature_url,
        trust_file,
    })
}

fn run_source_check(request: &SourceCheckRequest) -> Result<String, String> {
    let executable =
        std::env::current_exe().map_err(|error| format!("Kurulum motoru bulunamadı: {error}"))?;
    let mut command = Command::new(executable);
    command
        .arg("check-release-source")
        .arg("--package-id")
        .arg(&request.package_id)
        .arg("--manifest-url")
        .arg(&request.manifest_url)
        .arg("--signature-url")
        .arg(&request.signature_url)
        .arg("--trust-store")
        .arg(&request.trust_file);
    let output = command
        .output()
        .map_err(|error| format!("Bağlantı testi başlatılamadı: {error}"))?;
    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        let last = stderr
            .lines()
            .rev()
            .find(|line| !line.trim().is_empty())
            .unwrap_or("Yayın veya imza doğrulanamadı.");
        return Err(format!("Bağlantı testi başarısız: {last}"));
    }
    if !String::from_utf8_lossy(&output.stdout).contains("SIGNED_RELEASE_SOURCE_OK") {
        return Err("Bağlantı testi beklenen doğrulama sonucunu üretmedi.".to_owned());
    }
    Ok(
        "İmzalı yayın dosyası ve imzası doğrulandı. Paket bağlantısı HEAD isteğine yanıt verdi; paket indirilmedi."
            .to_owned(),
    )
}

fn validate_web_project(project: &SavedProject) -> Result<(), String> {
    resolve_web_release_urls(project).map(|_| ())
}

fn build_web_distribution(project: SavedProject) -> Result<String, String> {
    let (manifest_url, signature_url) = resolve_web_release_urls(&project)?;
    let target_dir = ensure_prepared_package_is_current(&project)?;
    let theme = target_dir.join("setup-theme.json");
    let _ = require_regular_file("Kurulum teması ayarı", &theme.to_string_lossy())?;
    let trust = require_regular_file("Yayıncı güven dosyası", &project.trust_store_path)?;
    let stub = require_regular_file("Kurulum motoru (EXE)", &project.setup_stub_path)?;
    let web_dir = target_dir.join("web");
    fs::create_dir(&web_dir)
        .map_err(|error| format!("WebSetup çıktı klasörü oluşturulamadı: {error}"))?;
    let output = web_dir.join("TurkuazWebSetup.exe");
    let result = (|| {
        let executable = std::env::current_exe()
            .map_err(|error| format!("Setup Studio uygulaması bulunamadı: {error}"))?;
        let mut command = Command::new(executable);
        command
            .arg("build-web-setup")
            .arg("--stub")
            .arg(stub)
            .arg("--package-id")
            .arg(&project.package_id)
            .arg("--manifest-url")
            .arg(&manifest_url)
            .arg("--signature-url")
            .arg(&signature_url)
            .arg("--trust-store")
            .arg(trust)
            .arg("--target")
            .arg(&project.install_target)
            .arg("--theme-config")
            .arg(theme)
            .arg("--output")
            .arg(&output);
        run_child(command, "Çevrimiçi yükleyici oluşturma")
    })();
    if let Err(error) = result {
        let _ = fs::remove_dir_all(&web_dir);
        return Err(error);
    }
    Ok(format!(
        "Çevrimiçi yükleyici hazır: {}. Dağıtmadan önce EXE dosyasını dijital olarak (Authenticode) imzalayın.",
        output.display()
    ))
}

fn build_distribution(project: SavedProject) -> Result<String, String> {
    for step in [0, 2, 3] {
        validate_project_step(&project, step)?;
    }

    let package_id = PackageId::parse(&project.package_id).map_err(|error| error.to_string())?;
    let package_directory = ensure_prepared_package_is_current(&project)?;
    let directory_metadata = fs::symlink_metadata(&package_directory)
        .map_err(|error| format!("Paket çıktı klasörü açılamadı: {error}"))?;
    if directory_metadata.file_type().is_symlink() || !directory_metadata.is_dir() {
        return Err("Paket çıktı yolu normal bir klasör olmalıdır.".to_owned());
    }
    let package_path = package_directory.join("application.tzpkg");
    let package_metadata = fs::symlink_metadata(&package_path)
        .map_err(|error| format!("Önce uygulama paketini hazırlayın: {error}"))?;
    if package_metadata.file_type().is_symlink() || !package_metadata.is_file() {
        return Err("Uygulama paketi normal bir dosya olmalıdır.".to_owned());
    }

    let distribution_directory = package_directory.join("distribution");
    if fs::symlink_metadata(&distribution_directory).is_ok() {
        return Err(format!(
            "Dağıtım klasörü zaten var: {}",
            distribution_directory.display()
        ));
    }

    let manifest_path =
        require_regular_file("İmzalı yayın dosyası", &project.signed_manifest_path)?;
    let signature_path = require_regular_file("Yayın imza dosyası", &project.signature_path)?;
    let trust_store_path =
        require_regular_file("Yayıncı güven dosyası", &project.trust_store_path)?;
    let setup_stub_path = require_regular_file("Kurulum motoru (EXE)", &project.setup_stub_path)?;
    let makensis_path = if project.makensis_path.trim().is_empty() {
        None
    } else {
        Some(require_regular_file(
            "NSIS makensis",
            &project.makensis_path,
        )?)
    };

    let saved_project_path = package_directory.join("project.tzproj");
    let saved_project_metadata = fs::symlink_metadata(&saved_project_path)
        .map_err(|error| format!("Proje dosyası açılamadı: {error}"))?;
    if saved_project_metadata.file_type().is_symlink() || !saved_project_metadata.is_file() {
        return Err("Kaydedilmiş proje dosyası normal bir dosya olmalıdır.".to_owned());
    }
    write_project(&saved_project_path, &project)?;

    let executable = std::env::current_exe()
        .map_err(|error| format!("Setup Studio uygulama yolu okunamadı: {error}"))?;
    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| format!("Sistem saati okunamadı: {error}"))?
        .as_nanos();
    let temporary_directory = package_directory.join(format!(
        ".turkuaz-release-build-{}-{nonce}",
        std::process::id()
    ));
    fs::create_dir(&temporary_directory)
        .map_err(|error| format!("Geçici yayın klasörü oluşturulamadı: {error}"))?;
    let _temporary_guard = TemporaryDirectoryGuard(temporary_directory.clone());

    let bundle_path = temporary_directory.join("release.tzsetup");
    let theme_path = package_directory.join("setup-theme.json");
    let mut bundle_command = Command::new(&executable);
    bundle_command
        .arg("build-setup-bundle")
        .arg("--package-id")
        .arg(package_id.to_string())
        .arg("--manifest")
        .arg(&manifest_path)
        .arg("--signature")
        .arg(&signature_path)
        .arg("--trust-store")
        .arg(&trust_store_path)
        .arg("--package")
        .arg(&package_path)
        .arg("--output")
        .arg(&bundle_path)
        .arg("--theme-config")
        .arg(&theme_path);
    let result = run_child(bundle_command, "İmzalı tam kurulum paketi hazırlama").and_then(|()| {
        let mut pair_command = Command::new(&executable);
        pair_command
            .arg("build-distribution-pair")
            .arg("--package-id")
            .arg(package_id.to_string())
            .arg("--stub")
            .arg(&setup_stub_path)
            .arg("--bundle")
            .arg(&bundle_path)
            .arg("--trust-store")
            .arg(&trust_store_path)
            .arg("--output-dir")
            .arg(&distribution_directory);
        if let Some(path) = &makensis_path {
            pair_command.arg("--makensis").arg(path);
        }
        run_child(pair_command, "Tam Kurulum ve yedek yükleyici oluşturma")
    });
    result?;

    Ok(format!(
        "İmzalı dağıtım çifti hazırlandı.\nTurkuaz setup: {}\nNSIS yedeği: {}\nTrust policy: {}\nHer iki EXE dosyasını dağıtımdan önce Authenticode ile imzalayın.",
        distribution_directory.join("TurkuazSetup.exe").display(),
        distribution_directory
            .join("TurkuazInstallerBackup.exe")
            .display(),
        distribution_directory.join("manifest-trust.yaml").display()
    ))
}

struct TemporaryDirectoryGuard(PathBuf);

impl Drop for TemporaryDirectoryGuard {
    fn drop(&mut self) {
        let _ = fs::remove_dir_all(&self.0);
    }
}

fn require_regular_file(label: &str, value: &str) -> Result<PathBuf, String> {
    let path = resolve_path(value)?;
    let metadata = fs::symlink_metadata(&path)
        .map_err(|error| format!("{label} dosyası okunamadı: {error}"))?;
    if metadata.file_type().is_symlink() || !metadata.is_file() {
        return Err(format!("{label}, normal bir dosya olmalıdır."));
    }
    Ok(path)
}

// BRK-STUDIO-STANDARD-002: Create-new native EXE without external build tools.
fn build_native_standard_setup(project: SavedProject) -> Result<String, String> {
    if project.installer_kind != InstallerKind::Offline {
        return Err("Standart Kurulum yalnızca Tam Kurulum içindir.".to_owned());
    }
    let root = ensure_prepared_package_is_current(&project)?;
    let directory = root.join("standard");
    match fs::symlink_metadata(&directory) {
        Ok(_) => return Err("Standart Kurulum klasörü zaten var; üzerine yazılmadı.".to_owned()),
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(format!("Çıktı klasörü doğrulanamadı: {error}")),
    }
    fs::create_dir(&directory)
        .map_err(|error| format!("Standart Kurulum klasörü oluşturulamadı: {error}"))?;
    let output = directory.join("TurkuazSetup.exe");
    let stub = std::env::current_exe()
        .map_err(|error| format!("Rust kurulum motoru yolu okunamadı: {error}"))?;
    if let Err(error) = create_unsigned_local_setup(&stub, root.join("application.tzpkg"), &output)
    {
        let _ = fs::remove_dir(&directory);
        return Err(format!("Standart Kurulum EXE oluşturulamadı: {error}"));
    }
    Ok(format!(
        "Standart Kurulum hazır: {}. İmzasızdır; yayıncı doğrulaması, güvenli güncelleme ve rollback içermez.",
        output.display()
    ))
}

fn build_project(project: SavedProject) -> Result<String, String> {
    for step in 0..4 {
        validate_project_step(&project, step)?;
    }

    let package_id = PackageId::parse(&project.package_id).map_err(|error| error.to_string())?;
    let version = SemanticVersion::parse(&project.version).map_err(|error| error.to_string())?;
    let source_directory = resolve_path(&project.source_directory)?;
    let output_parent = resolve_path(&project.output_directory)?;
    let executable = std::env::current_exe()
        .map_err(|error| format!("Setup Builder executable path could not be read: {error}"))?;
    validate_or_create_output_parent(&output_parent, true)?;

    let final_directory = output_parent.join(format!("{package_id}-{version}"));
    match fs::symlink_metadata(&final_directory) {
        Ok(_) => {
            return Err(format!(
                "Çıktı klasörü zaten var: {}",
                final_directory.display()
            ));
        }
        Err(error) if error.kind() == io::ErrorKind::NotFound => {}
        Err(error) => return Err(format!("Çıktı klasörü denetlenemedi: {error}")),
    }

    let nonce = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map_err(|error| format!("Sistem saati okunamadı: {error}"))?
        .as_nanos();
    let staging_directory = output_parent.join(format!(
        ".turkuaz-setup-build-{}-{nonce}",
        std::process::id()
    ));
    fs::create_dir(&staging_directory)
        .map_err(|error| format!("Geçici çıktı klasörü oluşturulamadı: {error}"))?;

    let result = build_into_staging(
        &project,
        &package_id,
        &version,
        &source_directory,
        &executable,
        &staging_directory,
        &final_directory.join("application.tzpkg"),
    );
    if let Err(error) = result {
        let _ = fs::remove_dir_all(&staging_directory);
        return Err(error);
    }

    if let Err(error) = fs::rename(&staging_directory, &final_directory) {
        let _ = fs::remove_dir_all(&staging_directory);
        return Err(format!("Hazır proje çıktı klasörüne taşınamadı: {error}"));
    }

    let scan_status = if project.av_scan.enabled {
        format!(
            "\nAntivirüs taraması: {} ile tamamlandı.",
            project.av_scan.provider.label()
        )
    } else {
        "\nAntivirüs taraması: kullanıcı tarafından kapatıldı.".to_owned()
    };
    Ok(format!(
        "Uygulama paketi hazır.\nÇıktı klasörü: {}\nDosyalar: application.tzpkg, release-manifest.draft.yaml, setup-theme.json ve project.tzproj.{}\nStandart Kurulum için 2 Standart Kurulum oluştur seçeneğini kullanın. Güvenilir yayın için 04 İmzalı dağıtım (gelişmiş) bölümündeki imza ve trust dosyaları gereklidir.",
        final_directory.display(),
        scan_status
    ))
}

fn build_into_staging(
    project: &SavedProject,
    package_id: &PackageId,
    version: &SemanticVersion,
    source_directory: &Path,
    executable: &Path,
    staging_directory: &Path,
    final_package_path: &Path,
) -> Result<(), String> {
    let package_path = staging_directory.join("application.tzpkg");
    let mut package_command = Command::new(executable);
    package_command
        .arg("build-package")
        .arg("--package-id")
        .arg(package_id.to_string())
        .arg("--version")
        .arg(version.to_string())
        .arg("--source")
        .arg(source_directory)
        .arg("--output")
        .arg(&package_path);
    if !project.entry_point.trim().is_empty() {
        package_command
            .arg("--executable")
            .arg(project.entry_point.trim());
    }
    run_child(package_command, "Paket derlemesi")?;

    // Scan the package's extracted bytes, not the mutable source folder.
    // Enabled policy never falls back silently to another AV provider.
    if project.av_scan.enabled {
        project.av_scan.scan_package(
            &package_path,
            staging_directory,
            &package_id.to_string(),
            &version.to_string(),
        )?;
    }

    let package_uri = Url::from_file_path(final_package_path)
        .map_err(|_| "Paket dosyası için yerel URI üretilemedi.".to_owned())?;
    let manifest_path = staging_directory.join("release-manifest.draft.yaml");
    let mut manifest_command = Command::new(executable);
    manifest_command
        .arg("describe-package")
        .arg("--package-id")
        .arg(package_id.to_string())
        .arg("--version")
        .arg(version.to_string())
        .arg("--channel")
        .arg(project.channel.trim())
        .arg("--package")
        .arg(&package_path)
        .arg("--uri")
        .arg(package_uri.as_str())
        .arg("--target")
        .arg(project.install_target.trim())
        .arg("--output")
        .arg(&manifest_path);
    run_child(manifest_command, "Yayın taslağı oluşturma")?;

    let theme = theme_config_from_project(project)?;
    let theme_path = staging_directory.join("setup-theme.json");
    write_new_file(
        &theme_path,
        &theme.to_json().map_err(|error| error.to_string())?,
    )?;

    let project_path = staging_directory.join("project.tzproj");
    let mut saved_project = project.clone();
    saved_project.schema_version = PROJECT_SCHEMA_VERSION;
    let project_bytes = serde_json::to_vec_pretty(&saved_project)
        .map_err(|error| format!("Proje JSON'a dönüştürülemedi: {error}"))?;
    write_new_file(&project_path, &project_bytes)?;
    Ok(())
}

fn run_child(mut command: Command, operation: &str) -> Result<(), String> {
    let output: Output = command
        .output()
        .map_err(|error| format!("{operation} başlatılamadı: {error}"))?;
    if output.status.success() {
        return Ok(());
    }
    let stderr = String::from_utf8_lossy(&output.stderr);
    let stdout = String::from_utf8_lossy(&output.stdout);
    let detail = stderr
        .lines()
        .rev()
        .find(|line| !line.trim().is_empty())
        .or_else(|| stdout.lines().rev().find(|line| !line.trim().is_empty()))
        .unwrap_or("İşlem hata ayrıntısı üretmedi.");
    Err(format!("{operation} başarısız oldu: {detail}"))
}

fn write_new_file(path: &Path, bytes: &[u8]) -> Result<(), String> {
    let mut file = fs::OpenOptions::new()
        .write(true)
        .create_new(true)
        .open(path)
        .map_err(|error| format!("{} oluşturulamadı: {error}", path.display()))?;
    file.write_all(bytes)
        .map_err(|error| format!("{} yazılamadı: {error}", path.display()))?;
    file.sync_all()
        .map_err(|error| format!("{} diske yazılamadı: {error}", path.display()))
}

fn color(rgb: [u8; 3]) -> egui::Color32 {
    egui::Color32::from_rgb(rgb[0], rgb[1], rgb[2])
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn new_project_uses_os_data_root_instead_of_process_cwd() {
        let project = SavedProject::default();
        let output = Path::new(&project.output_directory);
        assert!(output.is_absolute(), "new project output must be absolute");
        assert!(
            output.ends_with(Path::new("TurkuazInstaller").join("Projects")),
            "new project output must stay in the installer project root"
        );
        let target = Path::new(&project.install_target);
        assert!(
            target.is_absolute(),
            "default install path must be absolute"
        );
        assert!(
            target.ends_with(Path::new("Programs").join("Uygulamam")),
            "default install must be per-user and avoid Program Files elevation"
        );
    }

    #[test]
    fn missing_output_parent_is_created_only_when_build_starts() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let temporary = std::env::temp_dir().join(format!(
            "turkuaz-studio-output-{}-{nonce}",
            std::process::id()
        ));
        let parent = temporary.join("Türkçe klasör").join("Projects");
        let mut project = SavedProject::default();
        project.output_directory = parent.to_string_lossy().into_owned();

        validate_project_step(&project, 3).expect("missing output path is valid");
        assert!(!temporary.exists(), "validation must not write to the disk");

        validate_or_create_output_parent(&parent, true).expect("create nested output path");
        assert!(regular_directory_exists(&parent));
        validate_or_create_output_parent(&parent, true).expect("idempotent output preparation");
        fs::remove_dir_all(&temporary).expect("clean test fixture");
    }

    #[test]
    fn output_parent_rejects_existing_file_and_traversal() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let temporary = std::env::temp_dir().join(format!(
            "turkuaz-studio-output-file-{}-{nonce}",
            std::process::id()
        ));
        fs::create_dir(&temporary).expect("create fixture");
        let file = temporary.join("not-a-directory");
        fs::write(&file, b"fixture").expect("create blocking file");
        let blocked = file.join("Projects");
        assert!(validate_or_create_output_parent(&blocked, false).is_err());
        assert!(validate_or_create_output_parent(&blocked, true).is_err());
        assert!(!blocked.exists());

        let traversal = temporary.join("nested").join("..").join("Projects");
        assert!(validate_or_create_output_parent(&traversal, true).is_err());
        fs::remove_dir_all(&temporary).expect("clean fixture");
    }

    #[cfg(unix)]
    #[test]
    fn output_parent_rejects_symlink_ancestor() {
        use std::os::unix::fs::symlink;

        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let temporary = std::env::temp_dir().join(format!(
            "turkuaz-studio-output-symlink-{}-{nonce}",
            std::process::id()
        ));
        fs::create_dir(&temporary).expect("create fixture");
        let target = temporary.join("target");
        fs::create_dir(&target).expect("create target");
        let link = temporary.join("linked");
        symlink(&target, &link).expect("create symlink");

        assert!(validate_or_create_output_parent(&link.join("Projects"), true).is_err());
        assert!(!target.join("Projects").exists());
        fs::remove_dir_all(&temporary).expect("clean fixture");
    }

    #[test]
    fn optional_av_preferences_preserve_legacy_projects_and_refresh_package_state() {
        let mut original = SavedProject::default();
        let mut json = serde_json::to_value(&original).expect("to JSON");
        json.as_object_mut().expect("object").remove("avScan");
        let restored: SavedProject = serde_json::from_value(json).expect("legacy project");
        assert!(!restored.av_scan.enabled);
        assert!(restored.av_scan.validate().is_ok());

        original.av_scan.enabled = true;
        original.av_scan.provider = AvProvider::ClamAv;
        assert!(!package_build_inputs_match(&original, &restored));

        original.av_scan.enabled = false;
        assert!(!package_build_inputs_match(&original, &restored));
        original.av_scan = AvScanConfig::default();
        assert!(package_build_inputs_match(&original, &restored));
    }

    #[test]
    fn saved_project_kind_preserves_legacy_offline_and_web_projects() {
        let legacy = SavedProject::default();
        assert_eq!(InstallerKind::Offline.label(), "Tam Kurulum");
        assert_eq!(InstallerKind::Web.label(), "Çevrimiçi Kurulum");
        let mut value = serde_json::to_value(&legacy).expect("serialize project");
        value
            .as_object_mut()
            .expect("project object")
            .remove("installerKind");
        let restored: SavedProject =
            serde_json::from_value(value).expect("legacy project must remain supported");
        assert_eq!(restored.installer_kind, InstallerKind::Offline);

        let mut web = SavedProject::default();
        web.installer_kind = InstallerKind::Web;
        web.web_manifest_url = "https://updates.example.test/stable.yaml".to_owned();
        let bytes = serde_json::to_vec(&web).expect("serialize web project");
        let saved: SavedProject = serde_json::from_slice(&bytes).expect("load web project");
        assert_eq!(saved.installer_kind, InstallerKind::Web);
        assert_eq!(saved.web_manifest_url, web.web_manifest_url);
        assert_eq!(saved.schema_version, PROJECT_SCHEMA_VERSION);
    }

    #[test]
    fn web_provider_preserves_legacy_https_projects_and_roundtrips_github_settings() {
        let mut legacy = SavedProject::default();
        legacy.installer_kind = InstallerKind::Web;
        legacy.web_manifest_url = "https://updates.example.test/stable.yaml".to_owned();
        legacy.web_signature_url = "https://updates.example.test/stable.yaml.p7s".to_owned();

        let mut serialized = serde_json::to_value(&legacy).expect("serialize legacy");
        let object = serialized.as_object_mut().expect("project object");
        for field in [
            "webReleaseSource",
            "githubRepository",
            "githubReleaseTag",
            "githubManifestAsset",
            "githubSignatureAsset",
        ] {
            object.remove(field);
        }
        let restored: SavedProject =
            serde_json::from_value(serialized).expect("deserialize legacy");
        assert_eq!(restored.web_release_source, WebReleaseSource::Https);
        assert_eq!(
            resolve_web_release_urls(&restored).expect("legacy urls"),
            (legacy.web_manifest_url, legacy.web_signature_url)
        );

        let mut github = SavedProject::default();
        github.installer_kind = InstallerKind::Web;
        github.web_release_source = WebReleaseSource::GithubReleases;
        github.github_repository = "example-org/desktop-app".to_owned();
        github.github_release_tag = "v1.2.3-beta.1".to_owned();
        let expected = (
            "https://github.com/example-org/desktop-app/releases/download/v1.2.3-beta.1/release-manifest.yaml".to_owned(),
            "https://github.com/example-org/desktop-app/releases/download/v1.2.3-beta.1/release-manifest.yaml.p7s".to_owned(),
        );
        assert_eq!(
            resolve_web_release_urls(&github).expect("github URLs"),
            expected
        );
        let encoded = serde_json::to_vec(&github).expect("encode github project");
        let decoded: SavedProject = serde_json::from_slice(&encoded).expect("decode github");
        assert_eq!(decoded.web_release_source, WebReleaseSource::GithubReleases);
        assert_eq!(
            resolve_web_release_urls(&decoded).expect("restored urls"),
            expected
        );
    }

    #[test]
    fn web_provider_rejects_secret_links_invalid_segments_and_ambiguous_release_assets() {
        let mut project = SavedProject::default();
        project.web_release_source = WebReleaseSource::GithubReleases;
        project.github_repository = "publisher/product".to_owned();
        project.github_release_tag = "v1.0.0".to_owned();
        assert!(resolve_web_release_urls(&project).is_ok());

        for invalid in [
            "publisher/product/extra",
            "publisher/../product",
            "publisher/product?token=secret",
            "publisher%2fother/product",
            "",
        ] {
            project.github_repository = invalid.to_owned();
            assert!(resolve_web_release_urls(&project).is_err());
        }
        project.github_repository = "publisher/product".to_owned();
        for invalid in ["", "../latest", "v1.0.0?token=x", "v1/2", "#tag"] {
            project.github_release_tag = invalid.to_owned();
            assert!(resolve_web_release_urls(&project).is_err());
        }
        project.github_release_tag = "v1.0.0".to_owned();
        project.github_signature_asset = project.github_manifest_asset.clone();
        assert!(resolve_web_release_urls(&project).is_err());

        project.web_release_source = WebReleaseSource::Https;
        project.web_manifest_url =
            "https://user:secret@updates.example.test/stable.yaml".to_owned();
        project.web_signature_url = "https://updates.example.test/stable.yaml.p7s".to_owned();
        assert!(resolve_web_release_urls(&project).is_err());
        project.web_manifest_url =
            "https://updates.example.test/stable.yaml?token=secret".to_owned();
        assert!(resolve_web_release_urls(&project).is_err());
        project.web_manifest_url = "https://updates.example.test/stable.yaml".to_owned();
        assert!(resolve_web_release_urls(&project).is_ok());
    }

    #[test]
    fn source_check_uses_selected_release_and_requires_trust_file() {
        let mut project = SavedProject::default();
        assert!(source_check_request(&project).is_err());
        project.installer_kind = InstallerKind::Web;
        project.web_release_source = WebReleaseSource::GithubReleases;
        project.github_repository = "public-owner/desktop-client".to_owned();
        project.github_release_tag = "v1.2.3".to_owned();
        assert!(source_check_request(&project).is_err());

        let stamp = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let file = std::env::temp_dir().join(format!(
            "turkuaz-check-trust-{}-{stamp}.yaml",
            std::process::id()
        ));
        fs::write(&file, b"schemaVersion: 1\n").expect("write test trust");
        project.trust_store_path = file.to_string_lossy().into_owned();
        let request = source_check_request(&project).expect("check request");
        assert_eq!(
            request.manifest_url,
            "https://github.com/public-owner/desktop-client/releases/download/v1.2.3/release-manifest.yaml"
        );
        assert_eq!(request.package_id, "ornek.uygulamam");
        assert_eq!(request.trust_file, file);
        fs::remove_file(&file).expect("clean test trust");
    }

    #[test]
    fn welcome_selection_opens_mode_specific_modal_without_replacing_old_project_silently() {
        let mut app = BuilderApp::default();
        assert!(!app.workspace_open);
        assert!(!app.has_project);
        app.request_new_project(InstallerKind::Web);
        assert!(app.workspace_open);
        assert!(app.has_project);
        assert!(app.dialog_open);
        assert_eq!(app.active_step, 0);
        assert_eq!(app.project.installer_kind, InstallerKind::Web);

        app.request_new_project(InstallerKind::Offline);
        assert_eq!(app.pending_new_kind, Some(InstallerKind::Offline));
        assert_eq!(app.project.installer_kind, InstallerKind::Web);
    }

    #[test]
    fn output_directory_is_derived_from_package_identity_and_version() {
        let project = SavedProject::default();
        let actual = package_output_directory(&project).expect("valid default project");
        assert!(actual.ends_with("ornek.uygulamam-1.0.0"));

        let mut invalid = project;
        invalid.version = "wrong version".to_owned();
        assert!(package_output_directory(&invalid).is_err());
    }

    #[test]
    fn existing_project_is_not_replaced_without_open_confirmation() {
        let mut app = BuilderApp::default();
        app.create_new_project(InstallerKind::Offline);
        app.project.product_name = "Kaydedilmemiş değişiklik".to_owned();
        app.request_open_project();
        assert!(app.pending_open_requested);
        assert_eq!(app.project.product_name, "Kaydedilmemiş değişiklik");
    }

    #[test]
    fn package_snapshot_detects_stale_fields_but_not_signing_settings() {
        let mut saved = SavedProject::default();
        let mut current = saved.clone();
        assert!(package_build_inputs_match(&current, &saved));

        current.signed_manifest_path = "signed-manifest.yaml".to_owned();
        current.signature_path = "manifest.p7s".to_owned();
        current.trust_store_path = "publisher-trust.yaml".to_owned();
        current.setup_stub_path = "setup.exe".to_owned();
        assert!(package_build_inputs_match(&current, &saved));

        current.source_directory = "C:/build/changed-source".to_owned();
        assert!(!package_build_inputs_match(&current, &saved));
        current.source_directory = saved.source_directory.clone();
        current.brand_name = "Farklı tema".to_owned();
        assert!(!package_build_inputs_match(&current, &saved));
        saved.brand_name = current.brand_name.clone();
        assert!(package_build_inputs_match(&current, &saved));
    }

    #[test]
    fn prepared_package_must_match_saved_project_snapshot() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let temporary = std::env::temp_dir().join(format!(
            "turkuaz-studio-package-state-{}-{nonce}",
            std::process::id()
        ));
        let mut project = SavedProject::default();
        project.output_directory = temporary.to_string_lossy().into_owned();
        assert!(!prepared_package_is_current(&project));
        let dir = package_output_directory(&project).expect("package dir");
        fs::create_dir_all(&dir).expect("test output root");
        fs::write(dir.join("application.tzpkg"), b"fixture").expect("test package");
        assert!(!prepared_package_is_current(&project));
        write_project(&dir.join("project.tzproj"), &project).expect("save snapshot");
        assert!(prepared_package_is_current(&project));

        project.subtitle = "Yeni alt başlık".to_owned();
        assert!(!prepared_package_is_current(&project));
        assert!(ensure_prepared_package_is_current(&project).is_err());
        fs::remove_dir_all(&temporary).expect("clean fixture");
    }

    #[test]
    fn accent_picker_hex_parsing_is_strict_and_roundtrips() {
        assert_eq!(parse_rgb_hex("#127984"), Some([18, 121, 132]));
        assert_eq!(parse_rgb_hex("#FF7a00"), Some([255, 122, 0]));
        for invalid in ["", "red", "#fff", "#gg1122", "#12345678"] {
            assert_eq!(parse_rgb_hex(invalid), None);
        }
        let project = SavedProject::default();
        assert_eq!(default_theme_accent(&project).len(), 3);
    }

    #[test]
    fn documents_project_folder_sanitizes_names_without_changing_project_data() {
        let project = SavedProject {
            product_name: "Oyun: Türkçe / Test?".to_owned(),
            ..SavedProject::default()
        };
        let docs = std::env::temp_dir().join("Documents");
        let folder = documents_project_directory(&project, &docs).expect("documents path");
        assert!(folder.ends_with(Path::new("TurkuazInstaller").join("Oyun_ Türkçe _ Test")));
        assert_eq!(project.product_name, "Oyun: Türkçe / Test?");
        assert_eq!(safe_project_folder_name("CON"), "Proje_CON");
    }

    #[test]
    fn documents_default_save_creates_named_directory_and_refuses_overwrite() {
        let nonce = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .expect("system time")
            .as_nanos();
        let root = std::env::temp_dir().join(format!(
            "turkuaz-documents-test-{}-{nonce}",
            std::process::id()
        ));
        let documents = root.join("Documents");
        let project = SavedProject::default();
        let first = save_new_project_in_documents(&project, &documents)
            .expect("create default project under Documents");
        assert!(
            first.ends_with(
                Path::new("TurkuazInstaller")
                    .join("Uygulamam")
                    .join("project.tzproj")
            )
        );
        let restored = read_project(&first).expect("read saved project");
        assert_eq!(restored.product_name, project.product_name);
        assert!(save_new_project_in_documents(&project, &documents).is_err());
        assert_eq!(
            read_project(&first)
                .expect("unchanged project")
                .product_name,
            project.product_name
        );
        fs::remove_dir_all(&root).expect("clean test project");
    }

    #[test]
    fn studio_palettes_keep_separate_light_and_dark_surfaces() {
        let light = studio_palette(false);
        let dark = studio_palette(true);
        assert_ne!(light.canvas, dark.canvas);
        assert_ne!(light.surface, dark.surface);
        assert_ne!(light.accent, dark.accent);
    }

    #[test]
    fn studio_typography_is_available_for_both_appearances() {
        let context = egui::Context::default();
        configure_studio_style(&context);

        for theme in [egui::Theme::Light, egui::Theme::Dark] {
            let style = context.style_of(theme);
            let title = egui::TextStyle::Name("PageTitle".into()).resolve(&style);
            let product = egui::TextStyle::Name("ProductName".into()).resolve(&style);
            assert_eq!(title.size, 29.0);
            assert_eq!(product.size, 27.0);
        }
    }
}
