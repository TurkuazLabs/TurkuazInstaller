// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FakeProcessRunner.cs
// 📌 Amac: Package Engine testlerinde gercek executable calistirmadan process komutlarini yakalar
// 📌 Modul - Test Tool CSharp
// Version: 0.6.0
// Aciklama: Son ProcessCommand degerini saklar, test callback'i calistirir ve ayarlanabilir ProcessResult dondurur
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Contracts.System;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class FakeProcessRunner : IProcessRunner
{
    public ProcessCommand? LastCommand { get; private set; }

    public ProcessResult Result { get; set; } = new(
        0,
        string.Empty,
        string.Empty);

    public Action<ProcessCommand>? BeforeReturn { get; set; }

    public Task<ProcessResult> RunAsync(
        ProcessCommand command,
        CancellationToken cancellationToken)
    {
        LastCommand = command;
        BeforeReturn?.Invoke(command);
        return Task.FromResult(Result);
    }
}
