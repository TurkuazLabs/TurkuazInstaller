// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/Prerequisite.cs
// 📌 Amac: Installer prerequisite gereksinimini typed model olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Prerequisite kimligi ile surum ifadesini katmanlar arasinda tasir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Prerequisites;

public sealed record Prerequisite
{
    public Prerequisite(string id, string versionExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(versionExpression);
        Id = id.Trim();
        VersionExpression = versionExpression.Trim();
    }

    public string Id { get; }
    public string VersionExpression { get; }
}
