// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Bootstrap/ISelfUpdateHandoff.cs
// 📌 Amac: Bootstrap self-update iki-process handoff davranisini Application ve runtime servislerinden ayiran portu tanimlar
// 📌 Modul - Port CSharp
// Version: 0.6.0
// Aciklama: Begin ve complete asamalarini Windows Tool adapterina delege eder
//
// Bagimli Oldugu Katman: Tool

namespace TurkuazInstaller.Contracts.Bootstrap;

public interface ISelfUpdateHandoff
{
    Task BeginAsync(
        SelfUpdateStartRequest request,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        SelfUpdateCompleteRequest request,
        CancellationToken cancellationToken);
}
