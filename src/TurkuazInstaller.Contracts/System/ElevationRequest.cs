// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/System/ElevationRequest.cs
// 📌 Amac: Yalniz explicit privileged operasyonlarda kullanilacak elevated process istegini typed olarak tasir
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Executable, argument listesi ve executable directory bilgisini UAC Tool adapterina aktarir
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.System;

public sealed record ElevationRequest
{
    public ElevationRequest(
        string fileName,
        IReadOnlyList<string> arguments,
        string executableDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrWhiteSpace(executableDirectory);

        FileName = fileName.Trim();
        Arguments = Array.AsReadOnly(arguments.ToArray());
        ExecutableDirectory = executableDirectory.Trim();
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public string ExecutableDirectory { get; }
}
