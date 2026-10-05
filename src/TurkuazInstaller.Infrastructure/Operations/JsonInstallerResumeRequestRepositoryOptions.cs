// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonInstallerResumeRequestRepositoryOptions.cs
// 📌 Amac: Installer reboot resume request repository root yolunu typed config ile tasir
// 📌 Modul - Config CSharp
// Version: 1.0.1
// Aciklama: Resume storage path degerini repository implementasyonundaki inline configten ayirir
//
// Bagimli Oldugu Katman: Repo | Config

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed record JsonInstallerResumeRequestRepositoryOptions
{
    public JsonInstallerResumeRequestRepositoryOptions(
        string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            rootDirectory);

        RootDirectory =
            Path.GetFullPath(
                rootDirectory);
    }

    public string RootDirectory { get; }
}
