// 📄 Dosya Yolu: /src/TurkuazInstaller.Contracts/Credentials/ProviderAccessToken.cs
// 📌 Amac: Private release provider access tokenini accidental string rendering'e karsi redacted typed modelde tasir
// 📌 Modul - Port Model CSharp
// Version: 1.0.0
// Aciklama: Token yalniz request authorization adapteri tarafindan Reveal ile okunur; ToString secret degeri asla dondurmez
//
// Bagimli Oldugu Katman: Service | Tool

namespace TurkuazInstaller.Contracts.Credentials;

public sealed class ProviderAccessToken
{
    private readonly string _value;

    public ProviderAccessToken(
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            value);

        _value =
            value.Trim();
    }

    public string Reveal()
    {
        return _value;
    }

    public override string ToString()
    {
        return "[REDACTED]";
    }
}
