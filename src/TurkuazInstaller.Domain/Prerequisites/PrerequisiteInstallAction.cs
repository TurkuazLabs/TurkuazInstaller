// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/PrerequisiteInstallAction.cs
// 📌 Amac: Eksik prerequisite icin guvenli auto-install artifact ve process politikasini typed modelde tasir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: Hash ve Authenticode dogrulamasi zorunlu installer artifactini, argument listesini ve elevation politikasini tanimlar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Artifacts;

namespace TurkuazInstaller.Domain.Prerequisites;

public sealed record PrerequisiteInstallAction
{
    public PrerequisiteInstallAction(
        ArtifactDescriptor artifact,
        IReadOnlyList<string> arguments,
        bool requiresElevation)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(arguments);

        if (artifact.Signature is null)
        {
            throw new ArgumentException(
                "Prerequisite auto-install artifact must declare a signature policy.",
                nameof(artifact));
        }

        Artifact = artifact;
        Arguments =
            Array.AsReadOnly(
                arguments
                    .Select(
                        argument =>
                        {
                            ArgumentNullException.ThrowIfNull(
                                argument);

                            return argument;
                        })
                    .ToArray());
        RequiresElevation = requiresElevation;
    }

    public ArtifactDescriptor Artifact { get; }

    public IReadOnlyList<string> Arguments { get; }

    public bool RequiresElevation { get; }
}
