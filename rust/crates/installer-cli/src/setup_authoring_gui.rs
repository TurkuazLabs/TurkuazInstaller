// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

        egui::TopBottomPanel::top("setup-builder-header").show(context, |ui| {
            ui.add_space(5.0);
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.label(
                    egui::RichText::new("T")
                        .size(35.0)
                        .strong()
                        .color(palette.accent),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new("Turkuaz Setup Studio").size(21.0).strong());
                    ui.label(
                        egui::RichText::new(format!(
                            "Kurulum paketi oluşturma  •  v{}",
                            env!("CARGO_PKG_VERSION")
                        ))
                        .size(12.0)
                        .weak(),
                    );
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let previous_appearance = self.appearance;
                    egui::ComboBox::from_id_salt("builder-app-appearance")
                        .selected_text(self.appearance.label())
                        .width(86.0)
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
                        .add_enabled(!is_building, egui::Button::new("Kaydet"))
                        .clicked()
                    {
                        save_requested = true;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Proje aç"))
                        .clicked()
                    {
                        open_requested = true;
                    }
                });
            });
            ui.add_space(5.0);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

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
                    ui.label(egui::RichText::new("Kurulum projeleri için çalışma alanı").weak());
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
                    ui.label("Uygulama görünümü");
                    ui.separator();
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Projeyi kaydet"))
                        .clicked()
                    {
                        save_requested = true;
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

        egui::TopBottomPanel::bottom("setup-builder-actions").show(context, |ui| {
            ui.add_space(4.0);
            ui.separator();
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                if let Some((succeeded, message)) = &self.status {
                    ui.colored_label(
                        if *succeeded {
                            palette.accent
                        } else {
                            egui::Color32::from_rgb(196, 76, 65)
                        },
                        message,
                    );
                } else if is_building {
                    ui.spinner();
                    ui.label("Paket dosyaları hazırlanıyor…");
                } else {
                    ui.label(
                        egui::RichText::new("Önce paketi hazırlayın, ardından imzalı setup oluşturun.")
                            .size(13.0)
                            .weak(),
                    );
                }
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    if ui
                        .add_enabled(!is_building, egui::Button::new("2  İmzalı setup oluştur"))
                        .clicked()
                    {
                        self.start_distribution();
                    }
                    if ui
                        .add_enabled(
                            !is_building,
                            egui::Button::new("1  Uygulama paketini hazırla")
                                .fill(egui::Color32::from_rgb(10, 126, 135)),
                        )
                        .clicked()
                    {
                        self.start_build();
                    }
                });
            });
            ui.add_space(5.0);
        });

        egui::SidePanel::left("setup-builder-menu")
            .resizable(false)
            .default_width(220.0)
            .show(context, |ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.colored_label(
                    palette.accent,
                    egui::RichText::new("PROJE ADIMLARI").size(12.0).strong(),
                );
                ui.add_space(9.0);
                if navigation_item(ui, !self.dialog_open, "Genel bakış").clicked() {
                    self.dialog_open = false;
                    self.status = None;
                }
                ui.add_space(8.0);
                for (index, label) in [
                    "01   Ürün bilgileri",
                    "02   Uygulama dosyaları",
                    "03   Setup tasarımı",
                    "04   Yayın ve çıktı",
                ]
                .iter()
                .enumerate()
                {
                    if navigation_item(ui, self.dialog_open && self.active_step == index, label)
                        .clicked()
                        && !is_building
                    {
                        self.open_editor(index);
                    }
                    ui.add_space(4.0);
                }
                ui.add_space(16.0);
                ui.separator();
                ui.label(egui::RichText::new("ÇALIŞTIĞINIZ PROJE").size(12.0).weak());
                ui.add_space(5.0);
                ui.label(
                    egui::RichText::new(&self.project.product_name)
                        .size(17.0)
                        .strong(),
                );
                ui.small(format!(
                    "v{}  •  {}",
                    self.project.version, self.project.channel
                ));
                if self.project.source_directory.trim().is_empty() {
                    ui.add_space(9.0);
                    ui.colored_label(palette.accent, "Sıradaki: Uygulama dosyalarını seç");
                }
            });

        egui::SidePanel::right("setup-builder-preview")
            .resizable(false)
            .default_width(310.0)
            .show(context, |ui| self.draw_preview(ui));

        egui::CentralPanel::default().show(context, |ui| {
            self.draw_workspace(ui);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

        egui::TopBottomPanel::top("setup-builder-header").show(context, |ui| {
            ui.add_space(5.0);
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.label(
                    egui::RichText::new("T")
                        .size(35.0)
                        .strong()
                        .color(palette.accent),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new("Turkuaz Setup Studio").size(21.0).strong());
                    ui.label(
                        egui::RichText::new(format!(
                            "Kurulum paketi oluşturma  •  v{}",
                            env!("CARGO_PKG_VERSION")
                        ))
                        .size(12.0)
                        .weak(),
                    );
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let previous_appearance = self.appearance;
                    egui::ComboBox::from_id_salt("builder-app-appearance")
                        .selected_text(self.appearance.label())
                        .width(86.0)
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
                        .add_enabled(!is_building, egui::Button::new("Kaydet"))
                        .clicked()
                    {
                        save_requested = true;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Proje aç"))
                        .clicked()
                    {
                        open_requested = true;
                    }
                });
            });
            ui.add_space(5.0);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

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
                    ui.label(egui::RichText::new("Kurulum projeleri için çalışma alanı").weak());
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
                    ui.label("Uygulama görünümü");
                    ui.separator();
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Projeyi kaydet"))
                        .clicked()
                    {
                        save_requested = true;
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
                        message,
                    );
                } else if is_building {
                    ui.spinner();
                    ui.label("Paket ve kurulum dosyaları hazırlanıyor…");
                } else {
                    ui.label("Bir bölümü düzenleyin veya derlemeyi başlatın.");
                }

                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let distribution_clicked = ui
                        .add_enabled(!is_building, egui::Button::new("İmzalı dağıtım çifti üret"))
                        .clicked();
                    if distribution_clicked {
                        self.start_distribution();
                    }

                    let build_clicked = ui
                        .add_enabled(
                            !is_building,
                            egui::Button::new("Uygulama paketini hazırla")
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
            .default_width(232.0)
            .show(context, |ui| {
                ui.label(egui::RichText::new("Çalışma alanı").size(19.0).strong());
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

                ui.add_space(8.0);
                ui.separator();
                ui.label(
                    egui::RichText::new("PROJE BÖLÜMLERİ")
                        .size(12.0)
                        .strong()
                        .weak(),
                );

                const SECTIONS: [&str; 4] = [
                    "Ürün bilgileri",
                    "Uygulama dosyaları",
                    "Setup tasarımı",
                    "Yayın ve çıktı",
                ];
                for (index, label) in SECTIONS.iter().enumerate() {
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
            .default_width(320.0)
            .show(context, |ui| self.draw_preview(ui));

        egui::CentralPanel::default().show(context, |ui| {
            self.draw_workspace(ui);
        });

        if self.dialog_open && !is_building {
            self.show_editor_modal(context, false);
        }

        if open_requested {
            self.open_project();
        }
        if save_requested {
            self.save_project();
        }
        if is_building {
            context.request_repaint_after(Duration::from_millis(120));
        }
    }
}

impl BuilderApp {
    fn draw_workspace(&mut self, ui: &mut egui::Ui) {
        egui::ScrollArea::vertical()
            .auto_shrink([false, false])
            .show(ui, |ui| {
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
                ui.label("Dört adımda bilgileri tamamlayın. Sağdaki ekran, kullanıcıların göreceği kurulumu gösterir.");
                ui.add_space(14.0);

                studio_card(ui, |ui| {
                    ui.label(
                        egui::RichText::new("Nereden başlamalıyım?")
                            .size(20.0)
                            .strong(),
                    );
                    ui.add_space(4.0);
                    ui.label("Önce uygulama adını ve dosyalarını seçin. Ardından setup görünümünü düzenleyin.");
                    ui.add_space(12.0);
                    ui.horizontal_wrapped(|ui| {
                        if ui
                            .add(
                                egui::Button::new("Projeyi oluşturmaya başla")
                                    .fill(egui::Color32::from_rgb(10, 126, 135)),
                            )
                            .clicked()
                        {
                            self.open_editor(0);
                        }
                        if ui.button("Uygulama klasörünü seç").clicked() {
                            self.open_editor(1);
                        }
                    });
                });
                ui.add_space(18.0);
                ui.label(egui::RichText::new("Proje ayarları").size(21.0).strong());
                ui.label(
                    egui::RichText::new("Bir bölümü düzenlemek için sağındaki düğmeye tıklayın.")
                        .size(14.0)
                        .weak(),
                );
                ui.add_space(9.0);

                let theme_name = SetupThemePreset::ALL
                    .into_iter()
                    .find(|preset| preset.as_str() == self.project.theme_preset)
                    .map(SetupThemePreset::label)
                    .unwrap_or("Turkuaz");
                let source_summary = if self.project.source_directory.trim().is_empty() {
                    "Henüz klasör seçilmedi. Kurulacak program dosyalarını belirleyin.".to_owned()
                } else {
                    self.project.source_directory.clone()
                };
                let release_summary = if self.project.signed_manifest_path.trim().is_empty() {
                    "Paket klasörü ve imzalı yayın dosyalarını burada belirleyin.".to_owned()
                } else {
                    "Manifest yolu belirlendi; diğer yayın dosyalarını da kontrol edin.".to_owned()
                };
                let sections = [
                    (
                        "Ürün bilgileri",
                        format!(
                            "{} • v{} • {}",
                            self.project.product_name,
                            self.project.version,
                            self.project.channel
                        ),
                        "Uygulama adı, paket kimliği ve sürüm",
                    ),
                    (
                        "Uygulama dosyaları",
                        source_summary,
                        "Kurulacak klasör ve açılacak EXE",
                    ),
                    (
                        "Setup tasarımı",
                        format!("{} teması • {}", theme_name, self.project.brand_name),
                        "Son kullanıcının göreceği kurulum ekranı",
                    ),
                    (
                        "Yayın ve çıktı",
                        release_summary,
                        "Paket çıkışı, imza ve dağıtım dosyaları",
                    ),
                ];
                for (index, (title, summary, hint)) in sections.iter().enumerate() {
                    if self.draw_section_row(ui, index, title, summary, hint) {
                        self.open_editor(index);
                    }
                    ui.add_space(7.0);
                }
                ui.add_space(7.0);
                ui.label(
                    egui::RichText::new("Paket hazırlama işlemi, kurulum dosyasını imzalamaz.")
                        .size(13.0)
                        .weak(),
                );
                ui.label(
                    egui::RichText::new("Dağıtılabilir EXE için imzalı manifest ve yayıncı güven dosyası gerekir.")
                        .size(13.0)
                        .weak(),
                );
                ui.add_space(12.0);
            });
    }

    fn draw_section_row(
        &self,
        ui: &mut egui::Ui,
        index: usize,
        title: &str,
        summary: &str,
        hint: &str,
    ) -> bool {
        let mut clicked = false;
        studio_card(ui, |ui| {
            let palette = studio_palette(ui.visuals().dark_mode);
            ui.horizontal(|ui| {
                ui.label(
                    egui::RichText::new(format!("{:02}", index + 1))
                        .size(22.0)
                        .strong()
                        .color(palette.accent),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new(title).size(18.0).strong());
                    ui.label(egui::RichText::new(hint).size(13.0).weak());
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    clicked = ui.button("Düzenle").clicked();
                });
            });
            ui.add_space(5.0);
            ui.label(egui::RichText::new(summary).size(14.0));
        });
        clicked
    }

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

        egui::TopBottomPanel::top("setup-builder-header").show(context, |ui| {
            ui.add_space(5.0);
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.label(
                    egui::RichText::new("T")
                        .size(35.0)
                        .strong()
                        .color(palette.accent),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new("Turkuaz Setup Studio").size(21.0).strong());
                    ui.label(
                        egui::RichText::new(format!(
                            "Kurulum paketi oluşturma  •  v{}",
                            env!("CARGO_PKG_VERSION")
                        ))
                        .size(12.0)
                        .weak(),
                    );
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let previous_appearance = self.appearance;
                    egui::ComboBox::from_id_salt("builder-app-appearance")
                        .selected_text(self.appearance.label())
                        .width(86.0)
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
                        .add_enabled(!is_building, egui::Button::new("Kaydet"))
                        .clicked()
                    {
                        save_requested = true;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Proje aç"))
                        .clicked()
                    {
                        open_requested = true;
                    }
                });
            });
            ui.add_space(5.0);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

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
                    ui.label(egui::RichText::new("Kurulum projeleri için çalışma alanı").weak());
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
                    ui.label("Uygulama görünümü");
                    ui.separator();
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Projeyi kaydet"))
                        .clicked()
                    {
                        save_requested = true;
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

        egui::TopBottomPanel::bottom("setup-builder-actions").show(context, |ui| {
            ui.add_space(4.0);
            ui.separator();
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                if let Some((succeeded, message)) = &self.status {
                    ui.colored_label(
                        if *succeeded {
                            palette.accent
                        } else {
                            egui::Color32::from_rgb(196, 76, 65)
                        },
                        message,
                    );
                } else if is_building {
                    ui.spinner();
                    ui.label("Paket dosyaları hazırlanıyor…");
                } else {
                    ui.label(
                        egui::RichText::new("Önce paketi hazırlayın, ardından imzalı setup oluşturun.")
                            .size(13.0)
                            .weak(),
                    );
                }
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    if ui
                        .add_enabled(!is_building, egui::Button::new("2  İmzalı setup oluştur"))
                        .clicked()
                    {
                        self.start_distribution();
                    }
                    if ui
                        .add_enabled(
                            !is_building,
                            egui::Button::new("1  Uygulama paketini hazırla")
                                .fill(egui::Color32::from_rgb(10, 126, 135)),
                        )
                        .clicked()
                    {
                        self.start_build();
                    }
                });
            });
            ui.add_space(5.0);
        });

        egui::SidePanel::left("setup-builder-menu")
            .resizable(false)
            .default_width(220.0)
            .show(context, |ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.colored_label(
                    palette.accent,
                    egui::RichText::new("PROJE ADIMLARI").size(12.0).strong(),
                );
                ui.add_space(9.0);
                if navigation_item(ui, !self.dialog_open, "Genel bakış").clicked() {
                    self.dialog_open = false;
                    self.status = None;
                }
                ui.add_space(8.0);
                for (index, label) in [
                    "01   Ürün bilgileri",
                    "02   Uygulama dosyaları",
                    "03   Setup tasarımı",
                    "04   Yayın ve çıktı",
                ]
                .iter()
                .enumerate()
                {
                    if navigation_item(ui, self.dialog_open && self.active_step == index, label)
                        .clicked()
                        && !is_building
                    {
                        self.open_editor(index);
                    }
                    ui.add_space(4.0);
                }
                ui.add_space(16.0);
                ui.separator();
                ui.label(egui::RichText::new("ÇALIŞTIĞINIZ PROJE").size(12.0).weak());
                ui.add_space(5.0);
                ui.label(
                    egui::RichText::new(&self.project.product_name)
                        .size(17.0)
                        .strong(),
                );
                ui.small(format!(
                    "v{}  •  {}",
                    self.project.version, self.project.channel
                ));
                if self.project.source_directory.trim().is_empty() {
                    ui.add_space(9.0);
                    ui.colored_label(palette.accent, "Sıradaki: Uygulama dosyalarını seç");
                }
            });

        egui::SidePanel::right("setup-builder-preview")
            .resizable(false)
            .default_width(310.0)
            .show(context, |ui| self.draw_preview(ui));

        egui::CentralPanel::default().show(context, |ui| {
            self.draw_workspace(ui);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

        egui::TopBottomPanel::top("setup-builder-header").show(context, |ui| {
            ui.add_space(5.0);
            ui.horizontal(|ui| {
                let palette = studio_palette(ui.visuals().dark_mode);
                ui.label(
                    egui::RichText::new("T")
                        .size(35.0)
                        .strong()
                        .color(palette.accent),
                );
                ui.vertical(|ui| {
                    ui.label(egui::RichText::new("Turkuaz Setup Studio").size(21.0).strong());
                    ui.label(
                        egui::RichText::new(format!(
                            "Kurulum paketi oluşturma  •  v{}",
                            env!("CARGO_PKG_VERSION")
                        ))
                        .size(12.0)
                        .weak(),
                    );
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let previous_appearance = self.appearance;
                    egui::ComboBox::from_id_salt("builder-app-appearance")
                        .selected_text(self.appearance.label())
                        .width(86.0)
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
                        .add_enabled(!is_building, egui::Button::new("Kaydet"))
                        .clicked()
                    {
                        save_requested = true;
                    }
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Proje aç"))
                        .clicked()
                    {
                        open_requested = true;
                    }
                });
            });
            ui.add_space(5.0);
        });

// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_authoring_gui.rs
// Amac: Modal bolum editorleriyle Turkuaz setup projesi hazirlamak
// Modul - Rust GUI Module
// Version: 0.3.0
// Aciklama: Proje paneli, modal editorler ve surekli setup onizlemesi sunar
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
use turkuaz_installer_package_engine::{SetupThemeConfig, SetupThemePreset};
use url::Url;

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

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SavedProject {
    schema_version: u32,
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
}

impl Default for SavedProject {
    fn default() -> Self {
        let program_files = std::env::var_os("ProgramFiles")
            .map(PathBuf::from)
            .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"));
        let output_directory = std::env::current_dir().unwrap_or_else(|_| PathBuf::from("."));

        Self {
            schema_version: PROJECT_SCHEMA_VERSION,
            product_name: "Uygulamam".to_owned(),
            package_id: "ornek.uygulamam".to_owned(),
            version: "1.0.0".to_owned(),
            channel: "stable".to_owned(),
            source_directory: String::new(),
            entry_point: String::new(),
            install_target: program_files
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
        }
    }
}

struct BuilderApp {
    project: SavedProject,
    active_step: usize,
    dialog_open: bool,
    appearance: AppearancePreference,
    preview_dark: bool,
    status: Option<(bool, String)>,
    build_receiver: Option<Receiver<Result<String, String>>>,
    project_path: Option<PathBuf>,
}

impl Default for BuilderApp {
    fn default() -> Self {
        Self {
            project: SavedProject::default(),
            active_step: 0,
            dialog_open: false,
            appearance: AppearancePreference::System,
            preview_dark: false,
            status: None,
            build_receiver: None,
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

        let is_building = self.build_receiver.is_some();
        if is_building && context.input(|input| input.viewport().close_requested()) {
            context.send_viewport_cmd(egui::ViewportCommand::CancelClose);
        }

        let mut appearance_changed = false;
        let mut open_requested = false;
        let mut save_requested = false;

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
                    ui.label(egui::RichText::new("Kurulum projeleri için çalışma alanı").weak());
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
                    ui.label("Uygulama görünümü");
                    ui.separator();
                    if ui
                        .add_enabled(!is_building, egui::Button::new("Projeyi kaydet"))
                        .clicked()
                    {
                        save_requested = true;
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
                        message,
                    );
                } else if is_building {
                    ui.spinner();
                    ui.label("Paket ve kurulum dosyaları hazırlanıyor…");
                } else {
                    ui.label("Bir bölümü düzenleyin veya derlemeyi başlatın.");
                }

                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let distribution_clicked = ui
                        .add_enabled(!is_building, egui::Button::new("İmzalı dağıtım çifti üret"))
                        .clicked();
                    if distribution_clicked {
                        self.start_distribution();
                    }

                    let build_clicked = ui
                        .add_enabled(
                            !is_building,
                            egui::Button::new("Uygulama paketini hazırla")
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
            .default_width(232.0)
            .show(context, |ui| {
                ui.label(egui::RichText::new("Çalışma alanı").size(19.0).strong());
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

                ui.add_space(8.0);
                ui.separator();
                ui.label(
                    egui::RichText::new("PROJE BÖLÜMLERİ")
                        .size(12.0)
                        .strong()
                        .weak(),
                );

                const SECTIONS: [&str; 4] = [
                    "Ürün bilgileri",
                    "Uygulama dosyaları",
                    "Setup tasarımı",
                    "Yayın ve çıktı",
                ];
                for (index, label) in SECTIONS.iter().enumerate() {
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
            .default_width(320.0)
            .show(context, |ui| self.draw_preview(ui));

        egui::CentralPanel::default().show(context, |ui| {
            self.draw_workspace(ui);
        });

        if self.dialog_open && !is_building {
            self.show_editor_modal(context, false);
        }

        if open_requested {
            self.open_project();
        }
        if save_requested {
            self.save_project();
        }
        if is_building {
            context.request_repaint_after(Duration::from_millis(120));
        }
    }
}

impl BuilderApp {
    fn draw_workspace(&mut self, ui: &mut egui::Ui) {
        ui.label(
            egui::RichText::new("Proje çalışma alanı")
                .text_style(egui::TextStyle::Name("PageTitle".into()))
                .strong(),
        );
        ui.label("Proje ayarlarını bölümlere göre düzenleyin. Kurulum önizlemesi sağ tarafta sürekli görünür.");
        ui.add_space(14.0);

        studio_card(ui, |ui| {
            ui.horizontal(|ui| {
                ui.vertical(|ui| {
                    ui.label(
                        egui::RichText::new(&self.project.product_name)
                            .text_style(egui::TextStyle::Name("ProductName".into()))
                            .strong(),
                    );
                    ui.label(format!(
                        "{}  •  v{}  •  {}",
                        self.project.package_id,
                        self.project.version,
                        if self.project.channel == "beta" {
                            "Beta"
                        } else {
                            "Stable"
                        }
                    ));
                });
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let label = if self.project_path.is_some() {
                        "Kayıtlı proje"
                    } else {
                        "Yeni proje"
                    };
                    ui.label(
                        egui::RichText::new(label)
                            .color(egui::Color32::from_rgb(18, 121, 132))
                            .strong(),
                    );
                });
            });
        });
        ui.add_space(12.0);

        let theme_label = SetupThemePreset::ALL
            .into_iter()
            .find(|preset| preset.as_str() == self.project.theme_preset)
            .map(SetupThemePreset::label)
            .unwrap_or("Turkuaz");
        let source_summary = if self.project.source_directory.trim().is_empty() {
            "Kaynak klasörü seçilmedi".to_owned()
        } else {
            self.project.source_directory.clone()
        };
        let signing_summary = if self.project.signed_manifest_path.trim().is_empty() {
            "İmzalı yayın dosyaları henüz seçilmedi".to_owned()
        } else {
            "İmzalı manifest ve yayıncı güven ayarları tanımlandı".to_owned()
        };
        let sections = [
            (
                "Ürün bilgileri",
                format!(
                    "{}  •  {}  •  v{}",
                    self.project.product_name, self.project.package_id, self.project.version
                ),
            ),
            ("Uygulama dosyaları", source_summary),
            (
                "Setup tasarımı",
                format!("{} teması  •  {}", theme_label, self.project.brand_name),
            ),
            ("Yayın ve çıktı", signing_summary),
        ];

        ui.columns(2, |columns| {
            for (index, (title, summary)) in sections.iter().enumerate() {
                self.draw_section_card(&mut columns[index % 2], index, title, summary);
            }
        });

        ui.add_space(8.0);
        studio_card(ui, |ui| {
            ui.label(egui::RichText::new("Sonraki adım").size(19.0).strong());
            ui.label("Önce uygulama paketini oluşturun. İmzalı yayın dosyalarınız hazırsa aynı projeden setup ve NSIS dağıtım çiftini üretebilirsiniz.");
        });
    }

    fn draw_section_card(&mut self, ui: &mut egui::Ui, index: usize, title: &str, summary: &str) {
        studio_card(ui, |ui| {
            ui.set_min_height(142.0);
            ui.label(egui::RichText::new(title).size(19.0).strong());
            ui.add_space(4.0);
            ui.label(egui::RichText::new(summary).size(15.0));
            ui.add_space(8.0);
            if ui
                .add_sized([112.0, 38.0], egui::Button::new("Düzenle"))
                .clicked()
            {
                self.open_editor(index);
            }
        });
        ui.add_space(10.0);
    }

    fn show_editor_modal(&mut self, context: &egui::Context, is_building: bool) {
        const TITLES: [&str; 4] = [
            "Ürün bilgileri",
            "Uygulama dosyaları",
            "Setup tasarımı",
            "Yayın ve çıktı",
        ];
        let title = TITLES[self.active_step];
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
                egui::RichText::new(
                    "Düzenlemeler proje özetine ve setup önizlemesine anında yansır.",
                )
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
                    text_row(ui, "Paket kimliği", &mut self.project.package_id);
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
                ui.small("Paket kimliği publisher.product biçiminde olmalıdır.");
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
                    text_row(ui, "Başlangıç dosyası", &mut self.project.entry_point);
                    if ui
                        .add_enabled(!is_building, egui::Button::new("EXE dosyası seç"))
                        .clicked()
                    {
                        self.pick_entry_point();
                    }
                });
                ui.add_space(8.0);
                ui.small("Başlangıç dosyası, kaynak klasörünün içindeki göreli EXE yoludur. Boş bırakılabilir.");
                ui.small("Kaynakta symlink, özel dosya veya Windows ile çakışan yollar varsa paket derlemesi reddedilir.");
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
                    field_label(ui, "Setup teması");
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
                                ui.selectable_value(
                                    &mut self.project.theme_preset,
                                    preset.as_str().to_owned(),
                                    preset.label(),
                                );
                            }
                        });
                    ui.add_space(12.0);
                    text_row(ui, "Setup marka adı", &mut self.project.brand_name);
                    text_row(ui, "Alt başlık", &mut self.project.subtitle);
                    text_row(ui, "Vurgu rengi", &mut self.project.accent_color);
                });
                ui.add_space(8.0);
                ui.small("Vurgu rengi isteğe bağlıdır; HEX biçimi kullanın, örnek: #127984.");
                ui.small(
                    "Seçilen kurulum teması setup-theme.json olarak paketleme klasörüne yazılır.",
                );
            }
            _ => {
                ui.label("Önce uygulama paketini hazırlayın. İmzalı yayın dosyalarınız hazır olduğunda aynı ekrandan dağıtım çiftini üretebilirsiniz.");
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
                    ui.label("• application.tzpkg — uygulama payload paketi");
                    ui.label("• release-manifest.draft.yaml — imzasız yayın taslağı");
                    ui.label("• setup-theme.json — son kullanıcı setup teması");
                    ui.label("• project.tzproj — daha sonra açılabilir proje");
                });
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    ui.heading("İmzalı kurulum çıktısı");
                    ui.label("İmzalı manifesti, detached imzayı, yayıncı trust store dosyasını ve Windows setup GUI stub dosyasını seçin.");
                    file_row(
                        ui,
                        "İmzalı manifest",
                        &mut self.project.signed_manifest_path,
                        "İmzalı yayın manifestini seçin",
                        "YAML manifest",
                        &["yaml", "yml"],
                    );
                    file_row(
                        ui,
                        "Detached imza",
                        &mut self.project.signature_path,
                        "Manifest imza dosyasını seçin",
                        "İmza",
                        &["p7s", "sig", "der"],
                    );
                    file_row(
                        ui,
                        "Yayıncı trust store",
                        &mut self.project.trust_store_path,
                        "Yayıncı trust store dosyasını seçin",
                        "YAML trust store",
                        &["yaml", "yml"],
                    );
                    file_row(
                        ui,
                        "Setup GUI stub",
                        &mut self.project.setup_stub_path,
                        "Turkuaz setup GUI stub dosyasını seçin",
                        "Windows uygulaması",
                        &["exe"],
                    );
                    file_row(
                        ui,
                        "NSIS yolu (isteğe bağlı)",
                        &mut self.project.makensis_path,
                        "NSIS makensis.exe dosyasını seçin",
                        "Windows uygulaması",
                        &["exe"],
                    );
                    ui.small("NSIS PATH üzerinde değilse makensis.exe yolunu seçin. İki çıktı birlikte hazırlanır: TurkuazSetup.exe ve zorunlu NSIS yedeği.");
                    ui.small("Dağıtım klasöründeki manifest-trust.yaml dosyasını setup dosyalarının yanında tutun.");
                });
                ui.add_space(12.0);

                studio_card(ui, |ui| {
                    ui.colored_label(
                        egui::Color32::from_rgb(170, 92, 22),
                        egui::RichText::new("Yayın imzası ve güven politikası gerekir").strong(),
                    );
                    ui.label(
                        "Önce uygulama paketini hazırlayıp release-manifest.draft.yaml dosyasını yayın koşullarınıza göre imzalayın. Ardından imzalı manifest ve gerçek yayıncı trust store ile “İmzalı setup üret” seçeneğini kullanın.",
                    );
                    ui.label("Üretilen iki EXE de dağıtımdan önce Authenticode ile imzalanmalıdır. İmzalama anahtarı Setup Studio projesine eklenmez.");
                    ui.hyperlink_to(
                        "Yayın imzalama akışını görüntüle",
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
        ui.label(egui::RichText::new("Kurulum ekranı önizlemesi").size(19.0).strong());
        ui.label(
            egui::RichText::new("Bu görünüm, oluşturacağınız setup dosyası içindir.")
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
                                ui.add_sized(
                                    [button_width, 40.0],
                                    egui::Button::new("Kuruluma devam et").fill(accent),
                                );
                            });
                    });
                ui.add_space(10.0);
                ui.label(
                    egui::RichText::new("Tema düzenlemek için soldaki 03 • Setup tasarımı bölümünü açın.")
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

    fn open_project(&mut self) {
        if let Some(path) = FileDialog::new()
            .add_filter("Turkuaz setup project", &["tzproj"])
            .pick_file()
        {
            match read_project(&path) {
                Ok(project) => {
                    self.project = project;
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

        if let Some(mut path) = FileDialog::new()
            .add_filter("Turkuaz setup project", &["tzproj"])
            .set_file_name("new-setup.tzproj")
            .save_file()
        {
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

    fn start_distribution(&mut self) {
        for step in [0, 2, 3] {
            if let Err(error) = validate_project_step(&self.project, step) {
                self.active_step = step;
                self.dialog_open = true;
                self.status = Some((false, error));
                return;
            }
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
                "Dağıtım üretmek için imzalı manifest, imza, trust store ve setup stub seçin."
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
            let picker = FileDialog::new()
                .set_title(picker_title)
                .add_filter(filter_name, extensions);
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
            .map_err(|error| format!("Paket kimliği geçersiz: {error}"))?;
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
        let metadata = fs::symlink_metadata(&output_directory)
            .map_err(|error| format!("Çıktı üst klasörü okunamadı: {error}"))?;
        if metadata.file_type().is_symlink() || !metadata.is_dir() {
            return Err("Çıktı üst klasörü normal ve mevcut bir klasör olmalıdır.".to_owned());
        }
    }

    Ok(())
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
        return Err("Bu Turkuaz setup proje sürümü desteklenmiyor.".to_owned());
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

fn build_distribution(project: SavedProject) -> Result<String, String> {
    for step in [0, 2, 3] {
        validate_project_step(&project, step)?;
    }

    let package_id = PackageId::parse(&project.package_id).map_err(|error| error.to_string())?;
    let output_parent = resolve_path(&project.output_directory)?;
    let package_directory = output_parent.join(format!(
        "{}-{}",
        package_id,
        SemanticVersion::parse(&project.version).map_err(|error| error.to_string())?
    ));
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

    let manifest_path = require_regular_file("İmzalı manifest", &project.signed_manifest_path)?;
    let signature_path = require_regular_file("Detached imza", &project.signature_path)?;
    let trust_store_path = require_regular_file("Yayıncı trust store", &project.trust_store_path)?;
    let setup_stub_path = require_regular_file("Setup GUI stub", &project.setup_stub_path)?;
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
    let result =
        run_child(bundle_command, "İmzalı offline setup bundle oluşturma").and_then(|()| {
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
            run_child(pair_command, "Turkuaz setup ve NSIS yedeği derleme")
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

    Ok(format!(
        "Paket hazırlama tamamlandı.\nÇıktı klasörü: {}\nİçinde application.tzpkg, release-manifest.draft.yaml, setup-theme.json ve project.tzproj bulunur.\nDağıtılabilir setup.exe üretmek için manifesti imzalayıp gerçek yayıncı trust store ile paketlemeye devam edin.",
        final_directory.display()
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
