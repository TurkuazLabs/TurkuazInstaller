// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/IPrerequisiteInstaller.cs
// 📌 Amac: Dogrulanmis prerequisite installer artifactini shell kullanmadan calistiran Tool portunu tanimlar
// 📌 Modul - Port CSharp
// Version: 1.1.0
// Aciklama: Application katmanini Windows process ve UAC detaylarindan ayirir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Contracts.System;

public interface IPrerequisiteInstaller
{
    Task InstallAsync(
        string verifiedInstallerPath,
        PrerequisiteInstallAction installAction,
        CancellationToken cancellationToken);
}
