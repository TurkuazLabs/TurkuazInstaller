// Dosya Yolu: REPO_ROOT/rust/crates/installer-cli/src/setup_design_surface.rs
// Amac: Studio'da gercek Windows kurulumuna benzeyen canli sayfa onizlemesi ve adim inspector'u
// Modul - Rust Presentation
// Version: 0.1.0
// Aciklama: Koyu Studio paneli, acik Windows wizard onizlemesi, dogrulanan EULA ayarlari
// Bagimli Oldugu Katman: View | Application

use eframe::egui;
use turkuaz_installer_package_engine::{SetupThemePreset, WizardConfig, WizardPageId};

use super::SavedProject;

const STUDIO_ACCENT: egui::Color32 = egui::Color32::from_rgb(47, 127, 225);
const STUDIO_CARD: egui::Color32 = egui::Color32::from_rgb(30, 43, 61);
const STUDIO_BORDER: egui::Color32 = egui::Color32::from_rgb(64, 83, 108);
const MUTED_TEXT: egui::Color32 = egui::Color32::from_rgb(162, 178, 196);
const WINDOWS_TEXT: egui::Color32 = egui::Color32::from_rgb(31, 38, 47);
const WINDOWS_BORDER: egui::Color32 = egui::Color32::from_rgb(184, 193, 207);
const WINDOWS_BACKGROUND: egui::Color32 = egui::Color32::from_rgb(248, 249, 251);

fn settings_card(ui: &mut egui::Ui, add_contents: impl FnOnce(&mut egui::Ui)) {
    egui::Frame::new()
        .fill(STUDIO_CARD)
        .stroke(egui::Stroke::new(1.0, STUDIO_BORDER))
        .corner_radius(egui::CornerRadius::same(9))
        .inner_margin(egui::Margin::same(14))
        .show(ui, add_contents);
}

/// @dev-id BRK-STUDIO-DESIGNER-001
/// @purpose Keep page ordering, EULA text, and active step properties in a
/// persistent full-height inspector instead of a small modal edit form.
/// @contract Mandatory steps stay locked; actual order and enable flags are
/// persisted in WizardConfig. License text is never generated automatically.
pub(super) fn draw_step_inspector(
    ui: &mut egui::Ui,
    config: &mut WizardConfig,
    selected: &mut WizardPageId,
    english: &mut bool,
) {
    ui.add_space(11.0);
    ui.label(
        egui::RichText::new("Kurulum Adımları")
            .size(22.0)
            .strong(),
    );
    ui.label(
        egui::RichText::new("Görünen sayfaları açın, gizleyin ve sıralayın.")
            .color(MUTED_TEXT)
            .size(13.0),
    );
    ui.add_space(12.0);

    settings_card(ui, |ui| {
        ui.horizontal(|ui| {
            ui.strong("Sihirbaz sayfaları");
            ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                ui.label(
                    egui::RichText::new(format!(
                        "{} / {} etkin",
                        config.pages.iter().filter(|p| p.enabled).count(),
                        config.pages.len()
                    ))
                    .color(MUTED_TEXT)
                    .size(12.0),
                );
            });
        });
        ui.add_space(8.0);

        let mut picked = *selected;
        for (index, page) in config.pages.iter_mut().enumerate() {
            let is_selected = picked == page.page;
            egui::Frame::new()
                .fill(if is_selected {
                    egui::Color32::from_rgb(36, 87, 151)
                } else {
                    STUDIO_CARD
                })
                .corner_radius(egui::CornerRadius::same(5))
                .inner_margin(egui::Margin::symmetric(7, 2))
                .show(ui, |ui| {
                    ui.horizontal(|ui| {
                        let label = format!("{:02}  {}", index + 1, page.page.label());
                        if ui
                            .add_sized(
                                [ui.available_width().max(120.0) - 80.0, 31.0],
                                egui::Button::selectable(
                                    is_selected,
                                    egui::RichText::new(label).size(13.0),
                                )
                                .frame(false),
                            )
                            .clicked()
                        {
                            picked = page.page;
                        }
                        if page.page.required() {
                            ui.label(egui::RichText::new("Kilitli").color(MUTED_TEXT).size(11.0));
                        } else {
                            ui.checkbox(&mut page.enabled, "");
                        }
                    });
                });
            ui.add_space(2.0);
        }
        *selected = picked;
        ui.add_space(10.0);

        let movable = selected.movable();
        let enabled = config.is_enabled(*selected);
        let mut toggle_to = None;
        let mut move_delta = 0;
        ui.horizontal_wrapped(|ui| {
            if ui
                .add_enabled(!selected.required() && !enabled, egui::Button::new("+ Ekle"))
                .clicked()
            {
                toggle_to = Some(true);
            }
            if ui
                .add_enabled(!selected.required() && enabled, egui::Button::new("− Kaldır"))
                .clicked()
            {
                toggle_to = Some(false);
            }
            if ui.add_enabled(movable, egui::Button::new("↑ Yukarı")).clicked() {
                move_delta = -1;
            }
            if ui.add_enabled(movable, egui::Button::new("↓ Aşağı")).clicked() {
                move_delta = 1;
            }
        });
        if let Some(value) = toggle_to {
            if let Some(page) = config.page_mut(*selected) {
                page.enabled = value;
            }
        }
        if move_delta != 0 {
            let mut proposed = config.clone();
            if proposed.move_optional(*selected, move_delta).is_ok() {
                *config = proposed;
            }
        }
    });

    ui.add_space(14.0);
    settings_card(ui, |ui| {
        ui.label(
            egui::RichText::new(format!("Sayfa Özellikleri — {}", selected.label()))
                .size(17.0)
                .strong(),
        );
        ui.add_space(8.0);

        let required = selected.required();
        let mut enabled = config.is_enabled(*selected);
        ui.add_enabled_ui(!required, |ui| {
            ui.checkbox(&mut enabled, "Bu sayfayı göster");
        });
        if required {
            ui.label(
                egui::RichText::new("Kurulum güvenliği için zorunlu aşama.")
                    .color(MUTED_TEXT)
                    .size(12.0),
            );
        } else if let Some(page) = config.page_mut(*selected) {
            page.enabled = enabled;
        }
        ui.add_space(8.0);

        if let Some(page) = config.page_mut(*selected) {
            ui.label("Başlık (Türkçe)");
            ui.add(
                egui::TextEdit::singleline(&mut page.title_tr)
                    .desired_width(f32::INFINITY),
            );
            ui.label("Açıklama (Türkçe)");
            ui.add(
                egui::TextEdit::singleline(&mut page.description_tr)
                    .desired_width(f32::INFINITY),
            );
            ui.collapsing("English başlık ve açıklama", |ui| {
                ui.label("Title");
                ui.add(
                    egui::TextEdit::singleline(&mut page.title_en)
                        .desired_width(f32::INFINITY),
                );
                ui.label("Description");
                ui.add(
                    egui::TextEdit::singleline(&mut page.description_en)
                        .desired_width(f32::INFINITY),
                );
            });
        }

        if *selected == WizardPageId::License {
            ui.add_space(11.0);
            ui.separator();
            ui.label(
                egui::RichText::new("Son Kullanıcı Lisans Sözleşmesi")
                    .size(15.0)
                    .strong(),
            );
            ui.horizontal(|ui| {
                ui.selectable_value(english, false, "Türkçe");
                ui.selectable_value(english, true, "English");
            });
            let license = if *english {
                &mut config.license_text_en
            } else {
                &mut config.license_text_tr
            };
            ui.add(
                egui::TextEdit::multiline(license)
                    .desired_width(f32::INFINITY)
                    .desired_rows(7)
                    .hint_text("Gerçek lisans sözleşmesi metnini girin."),
            );
            ui.label(
                egui::RichText::new(
                    "EULA etkinse her iki dilde gerçek sözleşme metni gereklidir.",
                )
                .color(MUTED_TEXT)
                .size(12.0),
            );
        }
    });
    if let Err(error) = config.validate() {
        ui.add_space(10.0);
        ui.colored_label(
            egui::Color32::from_rgb(255, 186, 107),
            format!("Doğrulama: {error}"),
        );
    }
}

pub(super) fn draw_appearance_editor(ui: &mut egui::Ui, project: &mut SavedProject) {
    ui.add_space(12.0);
    egui::CollapsingHeader::new("Görünüm ve kurulum konumu")
        .default_open(false)
        .show(ui, |ui| {
            settings_card(ui, |ui| {
                ui.label("Varsayılan kurulum yolu");
                ui.add(
                    egui::TextEdit::singleline(&mut project.install_target)
                        .desired_width(f32::INFINITY),
                );
                ui.add_space(6.0);
                ui.label("Kurulum teması");
                egui::ComboBox::from_id_salt("studio-designer-theme")
                    .selected_text(
                        SetupThemePreset::ALL
                            .into_iter()
                            .find(|preset| preset.as_str() == project.theme_preset)
                            .map(SetupThemePreset::label)
                            .unwrap_or("Turkuaz"),
                    )
                    .show_ui(ui, |ui| {
                        for preset in SetupThemePreset::ALL {
                            ui.selectable_value(
                                &mut project.theme_preset,
                                preset.as_str().to_owned(),
                                preset.label(),
                            );
                        }
                    });
                ui.label("Marka adı");
                ui.add(
                    egui::TextEdit::singleline(&mut project.brand_name)
                        .desired_width(f32::INFINITY),
                );
                ui.label("Alt başlık");
                ui.add(
                    egui::TextEdit::singleline(&mut project.subtitle)
                        .desired_width(f32::INFINITY),
                );
                ui.label("Vurgu rengi (HEX)");
                ui.add(
                    egui::TextEdit::singleline(&mut project.accent_color)
                        .hint_text("#127984")
                        .desired_width(f32::INFINITY),
                );
            });
        });
}

/// Preview navigation is sandboxed to the GUI selection and can never invoke
/// the package install, build, registry or filesystem operations.
fn preview_neighbor(
    config: &WizardConfig,
    current: WizardPageId,
    offset: isize,
) -> Option<WizardPageId> {
    let visible = config.visible_pages(true);
    let index = visible.iter().position(|page| *page == current)?;
    let target = index.checked_add_signed(offset)?;
    visible.get(target).copied()
}

fn preview_page_body(
    ui: &mut egui::Ui,
    project: &SavedProject,
    selected: WizardPageId,
    english: bool,
    accepted: &mut bool,
) {
    match selected {
        WizardPageId::License => {
            egui::Frame::new()
                .fill(egui::Color32::WHITE)
                .stroke(egui::Stroke::new(1.0, WINDOWS_BORDER))
                .inner_margin(egui::Margin::symmetric(13, 10))
                .show(ui, |ui| {
                    egui::ScrollArea::vertical()
                        .id_salt("designer-license-preview-scroll")
                        .max_height(245.0)
                        .show(ui, |ui| {
                            let text = project.wizard.license_text(english);
                            if text.trim().is_empty() {
                                ui.add(
                                    egui::Label::new(
                                        egui::RichText::new(
                                            "Lisans metni henüz girilmedi. Sağdaki özellikler panelinden düzenleyin.",
                                        )
                                        .color(egui::Color32::from_rgb(120, 130, 144)),
                                    )
                                    .wrap(),
                                );
                            } else {
                                ui.add(
                                    egui::Label::new(
                                        egui::RichText::new(text)
                                            .monospace()
                                            .size(12.0)
                                            .color(WINDOWS_TEXT),
                                    )
                                    .wrap(),
                                );
                            }
                            ui.add_space(185.0);
                        });
                });
            ui.add_space(13.0);
            ui.checkbox(
                accepted,
                if english {
                    "I accept the terms of the License Agreement"
                } else {
                    "Lisans sözleşmesinin koşullarını kabul ediyorum"
                },
            );
        }
        WizardPageId::Destination => {
            ui.label("Kurulum dizini");
            ui.add_space(9.0);
            ui.horizontal(|ui| {
                ui.add(
                    egui::TextEdit::singleline(&mut project.install_target.clone())
                        .desired_width((ui.available_width() - 80.0).max(130.0))
                        .interactive(false),
                );
                ui.add_enabled(false, egui::Button::new("Gözat…"));
            });
            ui.add_space(12.0);
            ui.label("Kurulum yalnızca önizleniyor. Klasör oluşturulmaz.");
        }
        WizardPageId::Shortcuts => {
            let mut start_menu = true;
            let mut desktop = false;
            ui.checkbox(&mut start_menu, "Başlat menüsüne kısayol oluştur");
            ui.checkbox(&mut desktop, "Masaüstüne kısayol oluştur");
            ui.add_space(12.0);
            ui.label("Bu seçimler örnektir; gerçek kurulumda kullanıcı karar verir.");
        }
        WizardPageId::Welcome => {
            ui.label(
                egui::RichText::new(&project.brand_name)
                    .strong()
                    .size(20.0)
                    .color(WINDOWS_TEXT),
            );
            ui.add_space(10.0);
            ui.label(&project.subtitle);
            ui.add_space(12.0);
            ui.label(format!("{} v{}", project.product_name, project.version));
        }
        WizardPageId::Review => {
            ui.label(format!("Uygulama: {} v{}", project.product_name, project.version));
            ui.add_space(10.0);
            ui.label(format!("Hedef: {}", project.install_target));
            ui.add_space(10.0);
            ui.label("Bu örnek ekranda dosya yazılmaz.");
        }
        WizardPageId::Installing => {
            ui.horizontal(|ui| {
                ui.spinner();
                ui.label("Kurulum ilerleme görünümü (yalnızca önizleme)");
            });
            ui.add_space(12.0);
            ui.add(egui::ProgressBar::new(0.48).desired_width(f32::INFINITY));
        }
        WizardPageId::Finished => {
            ui.label(
                egui::RichText::new("✓ Kurulum tamamlandı")
                    .size(20.0)
                    .strong()
                    .color(egui::Color32::from_rgb(25, 130, 83)),
            );
            ui.add_space(12.0);
            ui.label("Bu görünüm kurulum yapmadan sonucu örnekler.");
        }
    }
}

/// @dev-id BRK-STUDIO-DESIGNER-002
/// @purpose Show a full-size light Windows wizard inside the dark Studio,
/// matching the selected page and localized strings without side effects.
pub(super) fn draw_install_preview(
    ui: &mut egui::Ui,
    project: &SavedProject,
    selected: &mut WizardPageId,
    english: &mut bool,
    accepted: &mut bool,
) {
    ui.add_space(12.0);
    ui.horizontal(|ui| {
        ui.label(
            egui::RichText::new("Kurulum Önizlemesi")
                .size(23.0)
                .strong(),
        );
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            egui::ComboBox::from_id_salt("designer-preview-language")
                .selected_text(if *english { "English" } else { "Türkçe" })
                .show_ui(ui, |ui| {
                    if ui.selectable_value(english, false, "Türkçe").changed() {
                        *accepted = false;
                    }
                    if ui.selectable_value(english, true, "English").changed() {
                        *accepted = false;
                    }
                });
        });
    });
    ui.label(
        egui::RichText::new("Windows kurulum sihirbazının görsel simülasyonu · dosya kurulmaz")
            .color(MUTED_TEXT)
            .size(13.0),
    );
    ui.add_space(16.0);

    egui::Frame::new()
        .fill(egui::Color32::from_rgb(20, 30, 45))
        .stroke(egui::Stroke::new(1.0, STUDIO_BORDER))
        .corner_radius(egui::CornerRadius::same(10))
        .inner_margin(egui::Margin::symmetric(13, 18))
        .show(ui, |ui| {
            ui.add_space(8.0);
            egui::Frame::new()
                .fill(WINDOWS_BACKGROUND)
                .stroke(egui::Stroke::new(1.0, WINDOWS_BORDER))
                .corner_radius(egui::CornerRadius::same(7))
                .show(ui, |ui| {
                    ui.scope(|ui| {
                        // Inherit layout, but render this simulated window with
                        // Win32-like light controls even when Studio is dark.
                        ui.visuals_mut().override_text_color = Some(WINDOWS_TEXT);
                        ui.visuals_mut().widgets.inactive.bg_fill =
                            egui::Color32::from_rgb(230, 234, 240);
                        ui.visuals_mut().widgets.inactive.fg_stroke.color = WINDOWS_TEXT;

                        egui::Frame::new()
                            .fill(egui::Color32::from_rgb(250, 251, 253))
                            .inner_margin(egui::Margin::symmetric(16, 11))
                            .show(ui, |ui| {
                                ui.horizontal(|ui| {
                                    ui.label(
                                        egui::RichText::new("▣")
                                            .color(STUDIO_ACCENT)
                                            .size(17.0),
                                    );
                                    ui.label(
                                        egui::RichText::new(format!(
                                            "{} Setup",
                                            project.product_name
                                        ))
                                        .strong()
                                        .size(15.0),
                                    );
                                    ui.with_layout(
                                        egui::Layout::right_to_left(egui::Align::Center),
                                        |ui| {
                                            ui.label("─   □   ×");
                                        },
                                    );
                                });
                            });
                        ui.separator();
                        egui::Frame::new()
                            .fill(egui::Color32::WHITE)
                            .inner_margin(egui::Margin::symmetric(24, 19))
                            .show(ui, |ui| {
                                let page = project.wizard.page(*selected);
                                let title = page
                                    .map(|page| page.title(*english))
                                    .unwrap_or_else(|| selected.label());
                                let description = page
                                    .map(|page| page.description(*english))
                                    .unwrap_or("");
                                ui.horizontal(|ui| {
                                    ui.vertical(|ui| {
                                        ui.label(
                                            egui::RichText::new(title)
                                                .size(22.0)
                                                .strong()
                                                .color(WINDOWS_TEXT),
                                        );
                                        ui.label(
                                            egui::RichText::new(description)
                                                .size(14.0)
                                                .color(WINDOWS_TEXT),
                                        );
                                    });
                                    ui.with_layout(
                                        egui::Layout::right_to_left(egui::Align::Center),
                                        |ui| {
                                            ui.label(
                                                egui::RichText::new("⬇")
                                                    .color(STUDIO_ACCENT)
                                                    .size(36.0),
                                            );
                                        },
                                    );
                                });
                            });
                        ui.separator();
                        egui::Frame::new()
                            .fill(WINDOWS_BACKGROUND)
                            .inner_margin(egui::Margin::symmetric(24, 20))
                            .show(ui, |ui| {
                                ui.set_min_height(340.0);
                                preview_page_body(ui, project, *selected, *english, accepted);
                            });
                        ui.separator();
                        egui::Frame::new()
                            .fill(egui::Color32::from_rgb(246, 247, 249))
                            .inner_margin(egui::Margin::symmetric(17, 12))
                            .show(ui, |ui| {
                                let previous = preview_neighbor(
                                    &project.wizard,
                                    *selected,
                                    -1,
                                );
                                let next = preview_neighbor(
                                    &project.wizard,
                                    *selected,
                                    1,
                                );
                                let next_enabled = next.is_some()
                                    && (*selected != WizardPageId::License || *accepted);
                                let mut selection = None;
                                ui.with_layout(
                                    egui::Layout::right_to_left(egui::Align::Center),
                                    |ui| {
                                        if ui
                                            .add_sized(
                                                [92.0, 34.0],
                                                egui::Button::new("İptal"),
                                            )
                                            .on_hover_text("Önizlemeyi ilk sayfaya döndürür.")
                                            .clicked()
                                        {
                                            selection = project
                                                .wizard
                                                .visible_pages(true)
                                                .first()
                                                .copied();
                                        }
                                        if ui
                                            .add_enabled(
                                                next_enabled,
                                                egui::Button::new("İleri  ›")
                                                    .min_size(egui::vec2(108.0, 34.0)),
                                            )
                                            .clicked()
                                        {
                                            selection = next;
                                        }
                                        if ui
                                            .add_enabled(
                                                previous.is_some(),
                                                egui::Button::new("‹  Geri")
                                                    .min_size(egui::vec2(108.0, 34.0)),
                                            )
                                            .clicked()
                                        {
                                            selection = previous;
                                        }
                                    },
                                );
                                if let Some(next_page) = selection {
                                    if *selected != next_page {
                                        *accepted = false;
                                    }
                                    *selected = next_page;
                                }
                            });
                    });
                });
            ui.add_space(8.0);
        });
    ui.add_space(13.0);
    if !project.wizard.is_enabled(*selected) {
        ui.label(
            egui::RichText::new(
                "Bu sayfa şu anda kapalı: yalnızca tasarım önizlemesinde gösteriliyor.",
            )
            .color(egui::Color32::from_rgb(255, 180, 109)),
        );
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn preview_navigation_skips_disabled_eula_and_keeps_required_finish() {
        let mut config = WizardConfig::default();
        assert_eq!(
            preview_neighbor(&config, WizardPageId::Welcome, 1),
            Some(WizardPageId::Destination)
        );
        config.page_mut(WizardPageId::License).unwrap().enabled = true;
        assert_eq!(
            preview_neighbor(&config, WizardPageId::Welcome, 1),
            Some(WizardPageId::License)
        );
        assert_eq!(
            preview_neighbor(&config, WizardPageId::Finished, 1),
            None
        );
    }
}
