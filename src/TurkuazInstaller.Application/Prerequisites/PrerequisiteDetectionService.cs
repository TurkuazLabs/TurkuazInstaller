// 📄 Dosya Yolu: /src/TurkuazInstaller.Application/Prerequisites/PrerequisiteDetectionService.cs
// 📌 Amac: Manifest prerequisite kimliklerini kayitli detector Tool adapterlarina yonlendiren generic detection motorunu uygular
// 📌 Modul - Service CSharp
// Version: 1.1.1
// Aciklama: Detector registry ile yeni prerequisite turlerini Application akisini degistirmeden eklenebilir yapar ve bilinmeyen kimlikleri fail-closed reddeder
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.System;
using TurkuazInstaller.Domain.Prerequisites;

namespace TurkuazInstaller.Application.Prerequisites;

public sealed class PrerequisiteDetectionService
    : ISystemPrerequisiteProbe
{
    private readonly IReadOnlyDictionary<string, IPrerequisiteDetector>
        _detectors;

    public PrerequisiteDetectionService(
        IEnumerable<IPrerequisiteDetector> detectors)
    {
        ArgumentNullException.ThrowIfNull(detectors);

        var registry =
            new Dictionary<string, IPrerequisiteDetector>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var detector in detectors)
        {
            ArgumentNullException.ThrowIfNull(detector);
            ArgumentException.ThrowIfNullOrWhiteSpace(
                detector.Id);

            if (
                !registry.TryAdd(
                    detector.Id.Trim(),
                    detector))
            {
                throw new InvalidOperationException(
                    string.Concat(
                        "Duplicate prerequisite detector id: ",
                        detector.Id));
            }
        }

        _detectors = registry;
    }

    public bool Supports(
        string prerequisiteId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            prerequisiteId);

        return _detectors.ContainsKey(
            prerequisiteId.Trim());
    }

    public Task<bool> IsSatisfiedAsync(
        Prerequisite prerequisite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prerequisite);
        cancellationToken.ThrowIfCancellationRequested();

        if (
            !_detectors.TryGetValue(
                prerequisite.Id,
                out var detector))
        {
            return Task.FromResult(false);
        }

        return detector.IsSatisfiedAsync(
            prerequisite,
            cancellationToken);
    }
}
