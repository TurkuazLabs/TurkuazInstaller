// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstallerUiCultureService.cs
// 📌 Amac: UI profile culture degerini masaustu process culture ayarlarina uygulamak
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: Typed Language katalogundaki optional culture adini dogrulanmis CultureInfo olarak current ve default thread culture'a uygular
//
// Bagimli Oldugu Katman: Service | Language | Config

using System.Globalization;
using TurkuazInstaller.Presentation.Language;

namespace TurkuazInstaller.Presentation.Services;

public sealed class InstallerUiCultureService
{
    public CultureInfo? Resolve(
        InstallerUiCatalog uiCatalog)
    {
        ArgumentNullException.ThrowIfNull(
            uiCatalog);

        if (string.IsNullOrWhiteSpace(
                uiCatalog.Culture))
        {
            return null;
        }

        return CultureInfo.GetCultureInfo(
            uiCatalog.Culture);
    }

    public void Apply(
        InstallerUiCatalog uiCatalog)
    {
        var culture =
            Resolve(
                uiCatalog);

        if (culture is null)
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentCulture =
            culture;

        CultureInfo.DefaultThreadCurrentUICulture =
            culture;

        CultureInfo.CurrentCulture =
            culture;

        CultureInfo.CurrentUICulture =
            culture;
    }
}
