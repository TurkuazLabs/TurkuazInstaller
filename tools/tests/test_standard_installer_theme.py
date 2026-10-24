# Dosya Yolu: tools/tests/test_standard_installer_theme.py
# Amac: Builtin Turkuaz varsayilan tema kontratini WinUI kaynaklari uzerinden dogrulamak
# Modul - Python unittest
# Version: 1.0.0
# Aciklama: Pure contract test, GUI render veya Windows runtime testi degildir
# Bagimli Oldugu Katman: Test | View
import re
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / "src/TurkuazInstaller.WinUI/App.xaml"
MAIN = ROOT / "src/TurkuazInstaller.WinUI/views/MainWindow.xaml"
THEME = ROOT / "src/TurkuazInstaller.WinUI/themes/StandardInstallerTheme.xaml"
PALETTES = ROOT / "src/TurkuazInstaller.WinUI/themes"
NAMESPACE = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"


class StandardThemeTests(unittest.TestCase):
    def test_light_dark_and_high_contrast_palettes_share_exact_tokens(self):
        theme = ET.parse(THEME).getroot()
        theme_ns = NAMESPACE + "ResourceDictionary.ThemeDictionaries"
        dictionaries = theme.find(theme_ns)
        self.assertIsNotNone(dictionaries)
        expected = {"Dark": "StandardDark.xaml",
                    "Light": "StandardLight.xaml",
                    "HighContrast": "StandardHighContrast.xaml"}
        self.assertEqual({x.get(X + "Key"): x.get("Source").split("/")[-1]
                          for x in dictionaries}, expected)
        keys = None
        for path in expected.values():
            resources = ET.parse(PALETTES / path).getroot()
            values = [item.get(X + "Key") for item in resources
                      if item.get(X + "Key")]
            self.assertEqual(len(values), len(set(values)))
            self.assertGreaterEqual(len(values), 6)
            keys = set(values) if keys is None else keys
            self.assertEqual(set(values), keys)
        self.assertIn("TurkuazInstallerAccentBrush", keys)
        root = ET.parse(APP).getroot()
        self.assertTrue(any("StandardInstallerTheme.xaml" in (node.get("Source") or "")
                            for node in root.iter(NAMESPACE + "ResourceDictionary")))

    def test_winui_window_uses_builtin_theme_without_changing_controller_contract(self):
        root = ET.parse(MAIN).getroot()
        resources = MAIN.read_text(encoding="utf-8")
        self.assertIn('x:Name="ThemeSelector"', resources)
        self.assertIn('Content="Sistem"', resources)
        self.assertIn('Content="Koyu"', resources)
        self.assertIn('Content="Açık"', resources)
        codebehind = (ROOT / "src/TurkuazInstaller.WinUI/views/MainWindow.xaml.cs").read_text(encoding="utf-8")
        self.assertIn("OnThemeSelectionChanged", codebehind)
        self.assertIn("MainRoot.RequestedTheme", codebehind)
        prefs = (ROOT / "src/TurkuazInstaller.WinUI/services/ThemeSelectionStore.cs").read_text(encoding="utf-8")
        self.assertIn('preset is "system" or "dark" or "light"', prefs)
        self.assertNotIn("LoadXaml", prefs)
        for key in ("TurkuazInstallerHeroBrush",
                    "TurkuazInstallerBackgroundBrush",
                    "TurkuazInstallerAccentBrush"):
            self.assertIn(key, resources)
        # Critical operations remain the same controller handlers.
        for handler in ("Install_Click", "Update_Click", "Repair_Click",
                        "Rollback_Click", "Uninstall_Click", "Cancel_Click"):
            self.assertIn(handler, resources)
        self.assertNotIn("Ragnar", resources)

    def test_standard_flow_documents_no_fake_progress(self):
        guide = (ROOT / "docs/STANDARD_INSTALLER_THEME.md").read_text(encoding="utf-8")
        self.assertIn("Do not fake percentage", guide)
        self.assertIn("not", guide.lower())
        self.assertIn("Ragnar", guide)


if __name__ == "__main__":
    unittest.main()
