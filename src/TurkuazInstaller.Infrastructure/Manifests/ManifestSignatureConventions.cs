// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/ManifestSignatureConventions.cs
// 📌 Amac: Detached manifest signature dosya ve URI adlandirma kontratini tek noktada sabitler
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Manifest kaynaginin yaninda ayni ada .p7s suffix'i ekleyerek CMS detached signature konumunu uretir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Manifests;

public static class ManifestSignatureConventions
{
    public const string DetachedSignatureSuffix =
        ".p7s";

    public static string GetDetachedSignaturePath(
        string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            manifestPath);

        return string.Concat(
            manifestPath,
            DetachedSignatureSuffix);
    }

    public static Uri GetDetachedSignatureUri(
        Uri manifestUri)
    {
        ArgumentNullException.ThrowIfNull(
            manifestUri);

        var builder =
            new UriBuilder(
                manifestUri)
            {
                Path =
                    string.Concat(
                        manifestUri.AbsolutePath,
                        DetachedSignatureSuffix)
            };

        return builder.Uri;
    }
}
