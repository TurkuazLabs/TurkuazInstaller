// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/Prerequisite.cs
// 📌 Amac: Installer prerequisite gereksinimini typed model olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Prerequisite kimligi, surum ifadesi ve optional guvenli auto-install politikasini katmanlar arasinda tasir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Prerequisites;

public sealed record Prerequisite
{
    public Prerequisite(
        string id,
        string versionExpression,
        PrerequisiteInstallAction? installAction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionExpression);

        Id = id.Trim();
        VersionExpression = versionExpression.Trim();
        InstallAction = installAction;
    }

    public string Id { get; }

    public string VersionExpression { get; }

    public PrerequisiteInstallAction? InstallAction { get; }
}
