// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Releases/SemanticVersion.cs
// 📌 Amac: SemVer 2.0 surumlerini parse eder ve precedence kurallarina gore karsilastirir
// 📌 Modul - Domain CSharp
// Version: 0.3.0
// Aciklama: Update kararlarinda System.Version yerine contract uyumlu semantic version kullanir
//
// Bagimli Oldugu Katman: Service

using System.Globalization;
using System.Text.RegularExpressions;

namespace TurkuazInstaller.Domain.Releases;

public sealed partial class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private SemanticVersion(int major, int minor, int patch, string? preRelease, string? buildMetadata)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        BuildMetadata = buildMetadata;
    }

    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }
    public string? PreRelease { get; }
    public string? BuildMetadata { get; }

    public static SemanticVersion Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var match = SemanticVersionPattern().Match(value.Trim());

        if (!match.Success)
        {
            throw new FormatException("Version is not valid SemVer 2.0.");
        }

        return new SemanticVersion(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            EmptyToNull(match.Groups["pre"].Value),
            EmptyToNull(match.Groups["build"].Value));
    }

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null) return 1;

        var comparison = Major.CompareTo(other.Major);
        if (comparison != 0) return comparison;

        comparison = Minor.CompareTo(other.Minor);
        if (comparison != 0) return comparison;

        comparison = Patch.CompareTo(other.Patch);
        if (comparison != 0) return comparison;

        return ComparePreRelease(PreRelease, other.PreRelease);
    }

    public bool Equals(SemanticVersion? other)
    {
        return other is not null
            && Major == other.Major
            && Minor == other.Minor
            && Patch == other.Patch
            && string.Equals(PreRelease, other.PreRelease, StringComparison.Ordinal)
            && string.Equals(BuildMetadata, other.BuildMetadata, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj) => Equals(obj as SemanticVersion);
    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease, BuildMetadata);

    public override string ToString()
    {
        var value = $"{Major}.{Minor}.{Patch}";
        if (PreRelease is not null) value = string.Concat(value, "-", PreRelease);
        if (BuildMetadata is not null) value = string.Concat(value, "+", BuildMetadata);
        return value;
    }

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    private static int ComparePreRelease(string? left, string? right)
    {
        if (left is null && right is null) return 0;
        if (left is null) return 1;
        if (right is null) return -1;

        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var count = Math.Max(leftParts.Length, rightParts.Length);

        for (var index = 0; index < count; index++)
        {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;

            var comparison = CompareIdentifier(leftParts[index], rightParts[index]);
            if (comparison != 0) return comparison;
        }

        return 0;
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftNumeric = int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
        var rightNumeric = int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);

        if (leftNumeric && rightNumeric) return leftNumber.CompareTo(rightNumber);
        if (leftNumeric) return -1;
        if (rightNumeric) return 1;
        return string.CompareOrdinal(left, right);
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    [GeneratedRegex(
        "^(?<major>0|[1-9]\\d*)\\.(?<minor>0|[1-9]\\d*)\\.(?<patch>0|[1-9]\\d*)(?:-(?<pre>(?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\\+(?<build>[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*))?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();
}
