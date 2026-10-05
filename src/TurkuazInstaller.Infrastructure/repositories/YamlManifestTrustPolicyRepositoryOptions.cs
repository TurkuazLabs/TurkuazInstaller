// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/repositories/YamlManifestTrustPolicyRepositoryOptions.cs
// 📌 Amac: Manifest trust store dosya yolunu repository implementasyonundan ayiran typed config tasir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Package publisher trust policy YAML dosyasinin full path degerini normalize eder
//
// Bagimli Oldugu Katman: Repo | Config

namespace TurkuazInstaller.Infrastructure.Repositories;

public sealed record YamlManifestTrustPolicyRepositoryOptions
{
    public YamlManifestTrustPolicyRepositoryOptions(
        string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        FilePath =
            Path.GetFullPath(
                filePath);
    }

    public string FilePath { get; }
}
