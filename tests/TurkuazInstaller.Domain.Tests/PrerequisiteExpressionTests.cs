// 📄 Dosya Yolu: /tests/TurkuazInstaller.Domain.Tests/PrerequisiteExpressionTests.cs
// 📌 Amac: Prerequisite numeric ve Version expression matcher davranisini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Karsilastirma operatorleri ile gecersiz expression degerlerinin fail-closed sonucunu kapsar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Domain.Prerequisites;
using Xunit;

namespace TurkuazInstaller.Domain.Tests;

public sealed class PrerequisiteExpressionTests
{
    [Theory]
    [InlineData(19045, ">=17763", true)]
    [InlineData(17762, ">=17763", false)]
    [InlineData(17763, "=17763", true)]
    [InlineData(19045, "<19045", false)]
    [InlineData(19045, "invalid", false)]
    public void Matches_IntegerExpression_ReturnsExpected(
        int current,
        string expression,
        bool expected)
    {
        Assert.Equal(
            expected,
            PrerequisiteExpression.Matches(
                current,
                expression));
    }

    [Theory]
    [InlineData("10.0.5", ">=10.0.0", true)]
    [InlineData("9.0.9", ">=10.0.0", false)]
    [InlineData("10.0.0", "=10.0.0", true)]
    [InlineData("10.0.0", ">10.0.0", false)]
    [InlineData("10.0.0", "not-a-version", false)]
    public void Matches_VersionExpression_ReturnsExpected(
        string current,
        string expression,
        bool expected)
    {
        Assert.Equal(
            expected,
            PrerequisiteExpression.Matches(
                Version.Parse(current),
                expression));
    }
}
