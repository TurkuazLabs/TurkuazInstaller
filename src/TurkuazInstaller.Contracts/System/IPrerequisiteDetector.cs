// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IPrerequisiteDetector.cs
// 📌 Amac: Tek bir prerequisite kimligi icin detection Tool adapteri portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Prerequisite detection motorunun hard-coded switch yerine kayitli detector adapterlari ile genislemesini saglar
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Contracts.System;

public interface IPrerequisiteDetector
{
    string Id { get; }

    Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken);
}
