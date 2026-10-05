// 📄 Dosya Yolu: /src/TurkuazInstaller.Presentation/services/InstallerResumeLaunchParser.cs
// 📌 Amac: Desktop startup argumanlarindan internal reboot resume package requestini typed olarak parse eder
// 📌 Modul - Service CSharp
// Version: 1.0.0
// Aciklama: --resume-package protokolunu tekrar, eksik deger ve package id validation kurallariyla dogrular
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Presentation.Services;

public sealed class InstallerResumeLaunchParser
{
    public PackageId? Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments);

        PackageId? packageId = null;

        for (
            var index = 0;
            index < arguments.Count;
            index++)
        {
            if (
                !string.Equals(
                    arguments[index],
                    InstallerResumeLaunchArguments.PackageOption,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (packageId is not null)
            {
                throw new FormatException(
                    "Reboot resume package argument cannot be repeated.");
            }

            var valueIndex =
                index + 1;

            if (valueIndex >= arguments.Count)
            {
                throw new FormatException(
                    "Reboot resume package argument value is missing.");
            }

            packageId =
                PackageId.Parse(
                    arguments[valueIndex]);

            index = valueIndex;
        }

        return packageId;
    }
}
