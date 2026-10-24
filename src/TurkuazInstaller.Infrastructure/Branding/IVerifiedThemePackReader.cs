// Dosya Yolu: src/TurkuazInstaller.Infrastructure/Branding/IVerifiedThemePackReader.cs
// Amac: Imzali ve sifreli tema okuyuculari icin tek sonuc kontrati
// Modul - CSharp Contract
// Version: 0.1.0-qa
// Aciklama: Hangi tasiyici kullanilirsa kullanilsin sadece tam dogrulanmis asset doner
// Bagimli Oldugu Katman: Infrastructure | View

using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Branding;

public interface IVerifiedThemePackReader
{
    Task<VerifiedThemePack> ReadAsync(
        Stream source,
        PackageId expectedPackageId,
        CancellationToken cancellationToken = default);
}
