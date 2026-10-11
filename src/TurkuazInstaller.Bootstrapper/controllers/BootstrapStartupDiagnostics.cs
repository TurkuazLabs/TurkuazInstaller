// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/controllers/BootstrapStartupDiagnostics.cs
// 📌 Amac: WinExe bootstrap exceptionlarini ekrana cikmadan kapandiginda local loga fail-safe yazar
// 📌 Modul - Tool CSharp
// Version: 0.1.0
// Aciklama: Diagnostic exit code, exception tipi ve HRESULT; argv, token veya signing key loglamaz
//
// Bagimli Oldugu Katman: Tool | Controller

using System.Runtime.InteropServices;
using System.Text;

namespace TurkuazInstaller.Bootstrapper.Controllers;

internal static class BootstrapStartupDiagnostics
{
    private const int MaxLogBytes = 131072;
    private const string ProductDirectory = "TurkuazInstaller";
    private const string LogDirectory = "logs";
    private const string LogFileName = "bootstrap-startup.log";

    public static void RecordExit(int exitCode)
    {
        Write(
            exitCode == 0 ? "HANDOFF_RETURNED" : "BOOTSTRAP_EXIT",
            exitCode,
            null);
    }

    public static void RecordException(Exception exception, int exitCode)
    {
        Write("BOOTSTRAP_EXCEPTION", exitCode, exception);
    }

    private static void Write(
        string eventName,
        int exitCode,
        Exception? exception)
    {
        // Diagnostic logs must never turn a usable Bootstrapper into
        // a failing executable (e.g. locked profile, disk full).
        try
        {
            var root = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            var directory = Path.Combine(
                root,
                ProductDirectory,
                LogDirectory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, LogFileName);

            var row = new StringBuilder();
            row.Append(DateTimeOffset.UtcNow.ToString("O"));
            row.Append(" event=");
            row.Append(eventName);
            row.Append(" exit=");
            row.Append(exitCode);
            row.Append(" osBuild=");
            row.Append(Environment.OSVersion.Version.Build);
            row.Append(" arch=");
            row.Append(RuntimeInformation.ProcessArchitecture);

            if (exception is not null)
            {
                row.Append(" type=");
                row.Append(exception.GetType().Name);
                row.Append(" hresult=0x");
                row.Append(exception.HResult.ToString("X8"));
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
            // Logging may be unavailable; never change the exit code.
        }
    }
}
