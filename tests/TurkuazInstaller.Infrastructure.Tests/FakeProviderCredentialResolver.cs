// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/FakeProviderCredentialResolver.cs
// 📌 Amac: Private provider testlerinde credential resolver request ve token davranisini deterministik stub ile izler
// 📌 Modul - Test Tool CSharp
// Version: 1.0.0
// Aciklama: Token degerini dis store kullanmadan providera dondurur ve son typed credential requestini kaydeder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class FakeProviderCredentialResolver
    : IProviderCredentialResolver
{
    private readonly ProviderAccessToken? _accessToken;

    public FakeProviderCredentialResolver(
        string? token)
    {
        _accessToken =
            string.IsNullOrWhiteSpace(
                token)
                ? null
                : new ProviderAccessToken(
                    token);
    }

    public ProviderCredentialRequest? LastRequest
    {
        get;
        private set;
    }

    public int CallCount
    {
        get;
        private set;
    }

    public Task<ProviderAccessToken?> ResolveAsync(
        ProviderCredentialRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LastRequest = request;
        CallCount++;

        return Task.FromResult(
            _accessToken);
    }
}
