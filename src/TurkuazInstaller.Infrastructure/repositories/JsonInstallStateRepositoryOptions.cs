// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/repositories/JsonInstallStateRepositoryOptions.cs
// 📌 Amac: JSON install state repository root dizinini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 0.7.0
// Aciklama: State storage yolunu repository implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Repo | Config

namespace TurkuazInstaller.Infrastructure.Repositories;

public sealed record JsonInstallStateRepositoryOptions
{
    public JsonInstallStateRepositoryOptions(
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }
}
