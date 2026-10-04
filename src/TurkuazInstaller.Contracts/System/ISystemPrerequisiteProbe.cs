// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ISystemPrerequisiteProbe.cs
// 📌 Amac: Sistem prerequisite kontrolu icin platform Tool adapteri portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.3.0
// Aciklama: Runtime, servis veya OS gereksinimlerinin platform bagimsiz sorgulanmasini saglar
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Contracts.System;

public interface ISystemPrerequisiteProbe
{
    Task<bool> IsSatisfiedAsync(Prerequisite prerequisite, CancellationToken cancellationToken);
}
