// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Prerequisites/PrerequisiteExpression.cs
// 📌 Amac: Numeric build ve runtime surum prerequisite ifadelerini ortak Domain kuralina gore degerlendirir
// 📌 Modul - Domain CSharp
// Version: 1.1.0
// Aciklama: >=, <=, >, < ve = operatorlerini platform detectorlerinden ayirarak tek fail-closed matcher saglar
//
// Bagimli Oldugu Katman: Service | Tool

using System.Globalization;

namespace TurkuazInstaller.Domain.Prerequisites;

public static class PrerequisiteExpression
{
    private const string GreaterThanOrEqual = ">=";
    private const string LessThanOrEqual = "<=";
    private const string GreaterThan = ">";
    private const string LessThan = "<";
    private const string Equal = "=";

    public static bool Matches(
        int current,
        string expression)
    {
        if (
            !TryParse(
                expression,
                out var operation,
                out var valueText) ||
            !int.TryParse(
                valueText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expected))
        {
            return false;
        }

        return Evaluate(
            current.CompareTo(expected),
            operation);
    }

    public static bool Matches(
        Version current,
        string expression)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (
            !TryParse(
                expression,
                out var operation,
                out var valueText) ||
            !Version.TryParse(
                valueText,
                out var expected))
        {
            return false;
        }

        return Evaluate(
            current.CompareTo(expected),
            operation);
    }

    private static bool TryParse(
        string expression,
        out string operation,
        out string value)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            operation = Equal;
            value = string.Empty;
            return false;
        }

        var trimmed =
            expression.Trim();

        foreach (
            var candidate in
            new[]
            {
                GreaterThanOrEqual,
                LessThanOrEqual,
                GreaterThan,
                LessThan,
                Equal
            })
        {
            if (
                trimmed.StartsWith(
                    candidate,
                    StringComparison.Ordinal))
            {
                operation = candidate;
                value =
                    trimmed[candidate.Length..]
                        .Trim();

                return value.Length > 0;
            }
        }

        operation = Equal;
        value = trimmed;
        return value.Length > 0;
    }

    private static bool Evaluate(
        int comparison,
        string operation)
    {
        return operation switch
        {
            GreaterThanOrEqual =>
                comparison >= 0,
            LessThanOrEqual =>
                comparison <= 0,
            GreaterThan =>
                comparison > 0,
            LessThan =>
                comparison < 0,
            Equal =>
                comparison == 0,
            _ =>
                false
        };
    }
}
