// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ISystemPrerequisiteProbe.cs
// 📌 Amac: Sistem prerequisite kontrolu icin platform Tool adapteri portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Prerequisite id destek bilgisini ve requirement satisfaction sonucunu platform bagimsiz sorgular
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Contracts.System;

public interface ISystemPrerequisiteProbe
{
    bool Supports(
        string prerequisiteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            prerequisiteId);

        return true;
    }

    Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken);
}
