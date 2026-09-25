// 📄 Dosya Yolu: /src/TurkuazInstaller.Domain/Products/ProductId.cs
// 📌 Amac: Urun kimligini type-safe domain degeri olarak temsil eder
// 📌 Modul - Domain CSharp
// Version: 0.1.0
// Aciklama: Bos product id degerlerinin domain icine girmesini engeller
//
// Bagimli Oldugu Katman: Service

namespace TurkuazInstaller.Domain.Products;

public sealed record ProductId
{
    private const string RequiredErrorCode = "product_id_required";

    private ProductId(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static ProductId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(RequiredErrorCode, nameof(value));
        }

        return new ProductId(value.Trim());
    }

    public override string ToString() => Value;
}
