// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/SelfUpdateHandoffOptions.cs
// 📌 Amac: Windows self-update file replacement retry politikasini inline config disina tasir
// 📌 Modul - Config CSharp
// Version: 0.6.0
// Aciklama: Replacement deneme sayisi ve retry gecikmesini typed Tool konfigurasyonu olarak tanimlar
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed record SelfUpdateHandoffOptions
{
    public SelfUpdateHandoffOptions(
        int replacementAttempts,
        TimeSpan retryDelay)
    {
        if (replacementAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(replacementAttempts));
        }

        if (retryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDelay));
        }

        ReplacementAttempts = replacementAttempts;
        RetryDelay = retryDelay;
    }

    public int ReplacementAttempts { get; }

    public TimeSpan RetryDelay { get; }
}
