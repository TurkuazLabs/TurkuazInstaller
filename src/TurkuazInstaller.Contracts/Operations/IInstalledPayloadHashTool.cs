// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Operations/IInstalledPayloadHashTool.cs
// 📌 Amac: Kurulu dosyalarin SHA-256 olcumunu salt-okunur Tool adapterine delege eder
// 📌 Modul - Contract CSharp
// Version: 1.0.0
// Aciklama: Eksik dosyayi null dondurur, erisim ve guvenlik hatalarini gizlemez
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstalledPayloadHashTool
{
    Task<string?> ComputeSha256Async(
        string installRoot,
        string relativePath,
        CancellationToken cancellationToken);
}
