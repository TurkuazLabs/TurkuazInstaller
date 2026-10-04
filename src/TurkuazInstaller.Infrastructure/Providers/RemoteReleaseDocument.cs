// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Providers/RemoteReleaseDocument.cs
// 📌 Amac: GitHub ve Gitea release JSON alanlarini ortak internal modele map eder
// 📌 Modul - Tool CSharp
// Version: 0.4.0
// Aciklama: Draft, prerelease ve asset alanlarini provider locator kodu icin tasir
//
// Bagimli Oldugu Katman: Tool

using System.Text.Json.Serialization;

namespace TurkuazInstaller.Infrastructure.Providers;

internal sealed class RemoteReleaseDocument
{
    [JsonPropertyName("draft")]
    public bool Draft { get; init; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; init; }

    [JsonPropertyName("assets")]
    public IReadOnlyList<RemoteReleaseAsset> Assets { get; init; } = Array.Empty<RemoteReleaseAsset>();
}

internal sealed class RemoteReleaseAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; init; } = string.Empty;
}
