// Dosya Yolu: src/TurkuazInstaller.WinUI/services/ThemeSelectionStore.cs
// Amac: Turkuaz Installer temasini ayri ve whitelisted kullanici tercihi olarak saklamak
// Modul - Tool/Service CSharp
// Version: 1.0.0
// Aciklama: System/dark/light disindaki degerleri uygulamaz, UI profilini degistirmez
// Bagimli Oldugu Katman: UI Settings | LocalAppData

using System.IO;

namespace TurkuazInstaller.WinUI.Services;

public static class ThemeSelectionStore
{
    private static readonly string PreferencePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TurkuazInstaller", "config", "theme-selection.txt");

    public static bool IsValid(string? preset) =>
        preset is "system" or "dark" or "light";

    public static string Read()
    {
        try
        {
            if (!File.Exists(PreferencePath))
            {
                return "system";
            }

            var value = File.ReadAllText(PreferencePath).Trim();
            return IsValid(value) ? value : "system";
        }
        catch (IOException)
        {
            return "system";
        }
        catch (UnauthorizedAccessException)
        {
            return "system";
        }
    }

    public static bool TryWrite(string preset)
    {
        if (!IsValid(preset))
        {
            return false;
        }

        try
        {
            var folder = Path.GetDirectoryName(PreferencePath)
                ?? throw new IOException("Theme settings directory unavailable.");
            Directory.CreateDirectory(folder);
            // Do not follow an unexpected symlink/junction to overwrite another file.
            if (File.Exists(PreferencePath) &&
                File.GetAttributes(PreferencePath).HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }

            File.WriteAllText(PreferencePath, preset + Environment.NewLine);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
