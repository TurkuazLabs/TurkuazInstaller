// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/BootstrapInvocation.cs
// 📌 Amac: Native bootstrap startup, self-update ve app argumentlarini runtime servisine typed invocation olarak tasir
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Normal launch, self-update begin/complete ve staged cleanup verilerini parserdan Service katmanina aktarir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Bootstrapper.Tools;

internal sealed record BootstrapInvocation
{
    public BootstrapInvocation(
        SelfUpdateCompleteRequest? selfUpdateRequest,
        string? cleanupSourcePath,
        string? selfUpdateReplacementPath,
        IReadOnlyList<string> applicationArguments)
    {
        ArgumentNullException.ThrowIfNull(
            applicationArguments);

        SelfUpdateRequest = selfUpdateRequest;
        CleanupSourcePath =
            string.IsNullOrWhiteSpace(
                cleanupSourcePath)
                ? null
                : cleanupSourcePath.Trim();

        SelfUpdateReplacementPath =
            string.IsNullOrWhiteSpace(
                selfUpdateReplacementPath)
                ? null
                : selfUpdateReplacementPath.Trim();

        ApplicationArguments =
            Array.AsReadOnly(
                applicationArguments.ToArray());
    }

    public SelfUpdateCompleteRequest? SelfUpdateRequest { get; }

    public string? CleanupSourcePath { get; }

    public string? SelfUpdateReplacementPath { get; }

    public IReadOnlyList<string> ApplicationArguments { get; }

    public bool IsSelfUpdateCompletion =>
        SelfUpdateRequest is not null;

    public bool IsSelfUpdateStart =>
        SelfUpdateReplacementPath is not null;
}
