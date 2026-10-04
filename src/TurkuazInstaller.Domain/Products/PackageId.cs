// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Products/PackageId.cs
// 📌 Amac: Installer paket kimligini contract kurallarina gore typed deger olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Paket kimligi normalizasyonunu ve pattern kontrolunu tek noktada uygular
//
// Bagimli Oldugu Katman: Service

using System.Text.RegularExpressions;

namespace TurkuazInstaller.Domain.Products;

public sealed partial record PackageId
{
    private PackageId(string value) => Value = value;

    public string Value { get; }

    public static PackageId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();

        if (!PackageIdPattern().IsMatch(normalized))
        {
            throw new FormatException("Package id does not match the installer contract.");
        }

        return new PackageId(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9][a-z0-9._-]{1,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex PackageIdPattern();
}
