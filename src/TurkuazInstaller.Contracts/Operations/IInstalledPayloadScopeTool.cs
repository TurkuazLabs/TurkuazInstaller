// Dosya: /src/TurkuazInstaller.Contracts/Operations/IInstalledPayloadScopeTool.cs
// Amac: Kurulu uygulama dosyalarini degistirmeden current kapsamini okur
// Modul - Tool Port CSharp
// Version: 1.0.0
// Aciklama: Sadece current dizinindeki normal dosyalari listeler, belirsiz reparse veya erisim hatasinda durur
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Operations;

public interface IInstalledPayloadScopeTool
{
    Task<IReadOnlyList<string>> ListCurrentFilesAsync(
        string installRoot,
        CancellationToken cancellationToken);
}
