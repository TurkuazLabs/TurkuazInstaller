// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonInstallerResumeRequestRepositoryOptions.cs
// 📌 Amac: Installer reboot resume request repository root yolunu typed config ile tasir
// 📌 Modul - Config CSharp
// Version: 1.0.0
// Aciklama: Resume storage path degerini repository implementasyonundaki inline configten ayirir
//
// Bagimli Oldugu Katman: Repo | Config

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed record JsonInstallerResumeRequestRepositoryOptions(
    string RootDirectory)
{
    public string RootDirectory { get; } =
        Path.GetFullPath(
            ArgumentException.ThrowIfNullOrWhiteSpace(
                RootDirectory));
}
