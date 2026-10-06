// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/VersionUpdatePolicyEvaluation.cs
// 📌 Amac: Version policy kararini resolved policy contextiyle birlikte Application sonucunda tasir
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Mutation/discovery katmanlarinin ayni karar semantigini kullanmasini saglar
//
// Bagimli Oldugu Katman: Service | Repo

using TurkuazInstaller.Contracts.Updates;

namespace TurkuazInstaller.Application.Updates;

public sealed record VersionUpdatePolicyEvaluation(
    VersionUpdatePolicyDecision Decision,
    VersionUpdatePolicy? Policy);
