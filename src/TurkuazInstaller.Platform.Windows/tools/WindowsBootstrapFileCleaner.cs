// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsBootstrapFileCleaner.cs
// 📌 Amac: Self-update resume sonrasinda staged eski bootstrap executable dosyasini best-effort temizler
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: Runtime servisinin dosya sistemi detayini bilmeden cleanup istegi yapmasini saglar
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsBootstrapFileCleaner : IBootstrapFileCleaner
{
    public Task TryDeleteAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return Task.CompletedTask;
    }
}
