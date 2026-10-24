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
NAMESPACE = "{http://schemas.microsoft.com/winfx/2006/xaml/presentation}"
X = "{http://schemas.microsoft.com/winfx/2006/xaml}"


class StandardThemeTests(unittest.TestCase):
    def test_theme_resources_are_unique_and_loaded(self):
        theme = ET.parse(THEME).getroot()
        self.assertEqual(theme.tag, NAMESPACE + "ResourceDictionary")
        keys = [node.get(X + "Key") for node in theme if node.get(X + "Key")]
        self.assertEqual(len(keys), len(set(keys)))
        self.assertGreaterEqual(len(keys), 7)
        self.assertIn("TurkuazInstallerPrimaryButtonStyle", keys)
        root = ET.parse(APP).getroot()
        merged = list(root.iter(NAMESPACE + "ResourceDictionary"))
        self.assertTrue(any(
            "StandardInstallerTheme.xaml" in (node.get("Source") or "")
            for node in merged
        ))

    def test_winui_window_uses_builtin_theme_without_changing_controller_contract(self):
        root = ET.parse(MAIN).getroot()
        resources = MAIN.read_text(encoding="utf-8")
        self.assertIn('RequestedTheme="Dark"', resources)
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
