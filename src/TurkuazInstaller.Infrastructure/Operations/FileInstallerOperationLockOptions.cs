// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/FileInstallerOperationLockOptions.cs
// 📌 Amac: Cross-process installer lock dosyalarinin root dizinini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Lock storage yolunu operation lock implementationindan ayirir
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed record FileInstallerOperationLockOptions
{
    public FileInstallerOperationLockOptions(
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
