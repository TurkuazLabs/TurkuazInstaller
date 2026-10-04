// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Packages/PackageEngineException.cs
// 📌 Amac: Package Engine operasyon hatalarini UI ve Application katmanina typed olarak tasir
// 📌 Modul - Port CSharp
// Version: 0.5.0
// Aciklama: Operasyon turu ve varsa process exit code bilgisini guvenli exception kontratinda tutar
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Packages;

public sealed class PackageEngineException : Exception
{
    public PackageEngineException(
        PackageEngineOperation operation,
        string message,
        int? exitCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Operation = operation;
        ExitCode = exitCode;
    }

    public PackageEngineOperation Operation { get; }

    public int? ExitCode { get; }
}
