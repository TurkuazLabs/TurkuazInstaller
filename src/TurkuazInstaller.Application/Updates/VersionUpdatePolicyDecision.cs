// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/VersionUpdatePolicyDecision.cs
// 📌 Amac: Candidate update surumunun version policy tarafindan kabul veya engellenme nedenini typed olarak tanimlar
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Allowed, exact skipped version ve maximum-version pin ceiling kararlarini ayirir
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Application.Updates;

public enum VersionUpdatePolicyDecision
{
    Allowed = 0,
    Skipped = 1,
    Pinned = 2
}
