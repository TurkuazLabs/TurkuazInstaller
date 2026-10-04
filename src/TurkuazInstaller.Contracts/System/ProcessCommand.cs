// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ProcessCommand.cs
// 📌 Amac: Tool katmaninda shell kullanmadan calistirilacak process komutunu typed olarak tanimlar
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Executable, argument listesi ve opsiyonel working directory degerlerini guvenli process kontratinda tasir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public sealed record ProcessCommand
{
    public ProcessCommand(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        FileName = fileName.Trim();
        Arguments = Array.AsReadOnly(arguments.ToArray());
        WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : workingDirectory.Trim();
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string? WorkingDirectory { get; }
}
