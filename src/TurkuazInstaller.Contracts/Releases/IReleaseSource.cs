// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Releases/IReleaseSource.cs
// 📌 Amac: Release kaynagi providerlari icin Core portunu tanimlar
// 📌 Modul - Contract CSharp
// Version: 0.1.0
// Aciklama: GitHub, Gitea ve HTTP adapterlarinin ortak davranis kontratidir
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Contracts.Releases;

public interface IReleaseSource
{
    Task<ReleaseDescriptor?> GetLatestAsync(
        ProductId productId,
        ReleaseChannel channel,
        CpuArchitecture architecture,
        CancellationToken cancellationToken);
}
