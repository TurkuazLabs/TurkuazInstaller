// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/File/FileReleaseProviderOptions.cs
// 📌 Amac: Local file provider manifest pathlerini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.4.0
// Aciklama: Stable ve beta manifest dosya yollarini adapter kaynak kodundan ayirir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Infrastructure.Providers.File;

public sealed record FileReleaseProviderOptions
{
    public FileReleaseProviderOptions(string stableManifestPath, string betaManifestPath)
    {
        StableManifestPath = ProviderValidation.Required(stableManifestPath, nameof(stableManifestPath));
        BetaManifestPath = ProviderValidation.Required(betaManifestPath, nameof(betaManifestPath));
    }

    public string StableManifestPath { get; }
    public string BetaManifestPath { get; }
}
