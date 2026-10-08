// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Updates/InstallerStagedUpdatePolicyException.cs
// 📌 Amac: Rollout yetkisi engelini typed exception ile yukari tasimak
// 📌 Modul - Service CSharp
// Version: 2.5.0
// Aciklama: Deferred, Denied ve RollbackRequested update islemlerini baslatmaz
// Bagimli Oldugu Katman: Service | Model

using TurkuazInstaller.Contracts.Updates;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Application.Updates;

public sealed class InstallerStagedUpdatePolicyException : InvalidOperationException
{
    public InstallerStagedUpdatePolicyException(
        PackageRelease release,
        StagedUpdatePolicyDecision decision)
        : base($"Staged update is not authorized: {decision}.")
    {
        ArgumentNullException.ThrowIfNull(release);
        PackageId = release.PackageId.ToString();
        Version = release.Version.ToString();
        Decision = decision;
    }

    public string PackageId { get; }

    public string Version { get; }

    public StagedUpdatePolicyDecision Decision { get; }
}
