// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/BootstrapInvocation.cs
// 📌 Amac: Native bootstrap command-line requestini runtime servisine typed invocation olarak tasir
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: Normal startup, self-update completion ve staged cleanup verilerini parserdan service katmanina aktarir
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Bootstrapper.Tools;

internal sealed record BootstrapInvocation(
    SelfUpdateCompleteRequest? SelfUpdateRequest,
    string? CleanupSourcePath)
{
    public bool IsSelfUpdateCompletion =>
        SelfUpdateRequest is not null;
}
