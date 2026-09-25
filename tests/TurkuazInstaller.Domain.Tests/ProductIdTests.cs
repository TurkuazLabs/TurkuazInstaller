// 📄 Dosya Yolu: /tests/TurkuazInstaller.Domain.Tests/ProductIdTests.cs
// 📌 Amac: ProductId domain degerinin temel validation davranisini test eder
// 📌 Modul - Test CSharp
// Version: 0.1.0
// Aciklama: Bos product id reddini ve gecerli id normalizasyonunu dogrular
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Products;
using Xunit;

namespace TurkuazInstaller.Domain.Tests;

public sealed class ProductIdTests
{
    private const string ValidProductId = "jhoster";

    [Fact]
    public void Parse_WhenValueIsValid_ReturnsNormalizedProductId()
    {
        var result = ProductId.Parse($"  {ValidProductId}  ");

        Assert.Equal(ValidProductId, result.Value);
    }

    [Fact]
    public void Parse_WhenValueIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => ProductId.Parse(string.Empty));
    }
}
