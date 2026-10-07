// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/ManifestContentLimits.cs
// 📌 Amac: Manifest ve detached signature byte limitlerini merkezi config sabitlerinde tutar
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Remote ve local providerlar icin parser/verification oncesi bellek tuketimi ust sinirlarini tanimlar
//
// Bagimli Oldugu Katman: Config | Tool

namespace TurkuazInstaller.Infrastructure.Manifests;

public static class ManifestContentLimits
{
    public const int MaximumManifestBytes =
        1024 * 1024;

    public const int MaximumDetachedSignatureBytes =
        256 * 1024;
}
