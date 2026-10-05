// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IPrerequisiteInstaller.cs
// 📌 Amac: Dogrulanmis prerequisite installer artifactini shell kullanmadan calistiran Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.2.0
// Aciklama: Application katmanini Windows process/UAC detaylarindan ayirir ve reboot sonucunu typed olarak dondurur
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Contracts.System;

public interface IPrerequisiteInstaller
{
    Task<PrerequisiteInstallResult> InstallAsync(
        string verifiedInstallerPath,
        PrerequisiteInstallAction installAction,
        CancellationToken cancellationToken);
}
