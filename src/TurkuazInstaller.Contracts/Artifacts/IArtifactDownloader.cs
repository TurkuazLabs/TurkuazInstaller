// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Artifacts/IArtifactDownloader.cs
// 📌 Amac: Artifact indirme islemi icin Application ile Tool adapteri arasindaki portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.3.0
// Aciklama: Download implementasyonunu Core is akislarindan ayirir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Artifacts;

namespace TurkuazInstaller.Contracts.Artifacts;

public interface IArtifactDownloader
{
    Task<string> DownloadAsync(ArtifactDescriptor artifact, string stagingDirectory, CancellationToken cancellationToken);
}
