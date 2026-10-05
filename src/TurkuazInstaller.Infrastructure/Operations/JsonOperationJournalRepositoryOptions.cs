// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/JsonOperationJournalRepositoryOptions.cs
// 📌 Amac: Operation journal repository root dizinini typed config olarak tasir
// 📌 Modul - Config CSharp
// Version: 1.1.0
// Aciklama: Journal storage yolunu JSON repository implementasyonundan ayirir
//
// Bagimli Oldugu Katman: Repo | Config

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed record JsonOperationJournalRepositoryOptions
{
    public JsonOperationJournalRepositoryOptions(
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
