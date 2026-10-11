// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/tools/WinUiStartupDiagnostics.cs
// 📌 Amac: WinUI XAML olusumu ve aktivasyonunda yerel kullanici icin gizli olmayan startup asamalarini kaydeder
// 📌 Modul - Tool CSharp
// Version: 0.1.2
// Aciklama: Application crash/stowed exception asamasini token veya arguman kaydetmeden tespit eder
//
// Bagimli Oldugu Katman: Tool | View | Controller

using System.Text;

namespace TurkuazInstaller.WinUI.Tools;

internal static class WinUiStartupDiagnostics
{
    private const int MaxLogBytes = 131072;

    public static void Record(string stage, Exception? error = null)
    {
        try
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var directory = Path.Combine(root, "TurkuazInstaller", "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "winui-startup.log");

            var row = new StringBuilder();
            row.Append(DateTimeOffset.UtcNow.ToString("O"));
            row.Append(" stage=");
            row.Append(stage);
            row.Append(" osBuild=");
            row.Append(Environment.OSVersion.Version.Build);
            row.Append(" pid=");
            row.Append(Environment.ProcessId);
            if (error is not null)
            {
                row.Append(" errorType=");
                row.Append(error.GetType().Name);
                row.Append(" hresult=0x");
                row.Append(error.HResult.ToString("X8"));
                // Escape line breaks and cap the message: XamlParseException
                // often identifies a missing runtime resource in the text.
                // No command-line parameters or release credentials are read.
                var safeMessage = error.Message
                    .Replace("\r", " ", StringComparison.Ordinal)
                    .Replace("\n", " ", StringComparison.Ordinal);
                if (safeMessage.Length > 300)
                {
                    safeMessage = safeMessage[..300];
                }

                row.Append(" detail=");
                row.Append(safeMessage);
            }

            row.AppendLine();
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
            {
                File.Delete(path);
            }

            File.AppendAllText(path, row.ToString(), Encoding.UTF8);
        }
        catch
        {
            // A read-only profile must not cause a startup failure.
        }
    }
}
