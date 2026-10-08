// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Updates/StagedUpdatePolicyDecision.cs
// 📌 Amac: Optional commercial staged update policy kararlarini typed olarak tasimak
// 📌 Modul - Model CSharp
// Version: 2.5.0
// Aciklama: Only Eligible allows update; all other states block without automatic rollback
// Bagimli Oldugu Katman: Model | Service

namespace TurkuazInstaller.Contracts.Updates;

public enum StagedUpdatePolicyDecision
{
    Eligible = 0,
    Deferred = 1,
    Denied = 2,
    RollbackRequested = 3
}
