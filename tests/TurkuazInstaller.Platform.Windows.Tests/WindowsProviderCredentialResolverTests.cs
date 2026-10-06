// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsProviderCredentialResolverTests.cs
// 📌 Amac: Windows provider credential resolver target naming, missing secret ve redacted token davranisini test eder
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Gercek Credential Manager kullanmadan target contracti, missing secret, redaction ve invalid authority rejection davranisini dogrular
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsProviderCredentialResolverTests
{
    [Fact]
    public async Task ResolveAsync_GitHubCredential_UsesNormalizedTargetAndReturnsRedactedToken()
    {
        var reader =
            new StubCredentialReader(
                "private-token");

        var resolver =
            new WindowsProviderCredentialResolver(
                reader);

        var token =
            await resolver.ResolveAsync(
                new ProviderCredentialRequest(
                    ProviderCredentialProvider.GitHub,
                    "API.GITHUB.COM"),
                CancellationToken.None);

        Assert.Equal(
            "TurkuazInstaller/provider/github/api.github.com",
            reader.TargetName);

        Assert.NotNull(
            token);

        Assert.Equal(
            "private-token",
            token!.Reveal());

        Assert.Equal(
            "[REDACTED]",
            token.ToString());
    }

    [Fact]
    public async Task ResolveAsync_GiteaCredential_PreservesExplicitPort()
    {
        var reader =
            new StubCredentialReader(
                "gitea-token");

        var resolver =
            new WindowsProviderCredentialResolver(
                reader);

        var token =
            await resolver.ResolveAsync(
                new ProviderCredentialRequest(
                    ProviderCredentialProvider.Gitea,
                    "gitea.example.test:3000"),
                CancellationToken.None);

        Assert.Equal(
            "TurkuazInstaller/provider/gitea/gitea.example.test:3000",
            reader.TargetName);

        Assert.Equal(
            "gitea-token",
            token?.Reveal());
    }

    [Fact]
    public async Task ResolveAsync_MissingCredential_ReturnsNull()
    {
        var resolver =
            new WindowsProviderCredentialResolver(
                new StubCredentialReader(
                    null));

        var token =
            await resolver.ResolveAsync(
                new ProviderCredentialRequest(
                    ProviderCredentialProvider.GitHub,
                    "api.github.com"),
                CancellationToken.None);

        Assert.Null(
            token);
    }


    [Fact]
    public void ProviderCredentialRequest_UserInfoAuthority_Rejects()
    {
        Assert.Throws<ArgumentException>(
            () =>
                new ProviderCredentialRequest(
                    ProviderCredentialProvider.GitHub,
                    "user@api.github.com"));
    }

    private sealed class StubCredentialReader
        : IWindowsCredentialReader
    {
        private readonly string? _secret;

        public StubCredentialReader(
            string? secret)
        {
            _secret = secret;
        }

        public string? TargetName
        {
            get;
            private set;
        }

        public string? ReadGenericSecret(
            string targetName)
        {
            TargetName = targetName;
            return _secret;
        }
    }
}
