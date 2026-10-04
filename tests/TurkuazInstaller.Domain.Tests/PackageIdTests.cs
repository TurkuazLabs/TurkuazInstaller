// 📄 Dosya Yolu: /tests/TurkuazInstaller.Domain.Tests/PackageIdTests.cs
// 📌 Amac: PackageId normalizasyon ve pattern kurallarini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 0.3.0
// Aciklama: Gecerli kimlik normalizasyonunu ve gecersiz uzunluk reddini test eder
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;
using Xunit;

namespace TurkuazInstaller.Domain.Tests;

public sealed class PackageIdTests
{
    [Fact]
    public void Parse_NormalizesValidValue()
    {
        var packageId = PackageId.Parse("  Example-App  ");
        Assert.Equal("example-app", packageId.Value);
    }

    [Fact]
    public void Parse_RejectsSingleCharacterValue()
    {
        Assert.Throws<FormatException>(() => PackageId.Parse("a"));
    }
}
