// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsProviderCredentialResolver.cs
// 📌 Amac: GitHub/Gitea provider access tokenlarini Windows Credential Manager target adindan resolve eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Secret manifest/config yerine generic credential password alaninda kalir; target provider+authority bazli deterministic uretilir
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Credentials;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsProviderCredentialResolver
    : IProviderCredentialResolver
{
    private const string TargetPrefix =
        "TurkuazInstaller/provider";

    private readonly IWindowsCredentialReader
        _credentialReader;

    public WindowsProviderCredentialResolver()
        : this(
            new WindowsCredentialManagerReader())
    {
    }

    public WindowsProviderCredentialResolver(
        IWindowsCredentialReader credentialReader)
    {
        ArgumentNullException.ThrowIfNull(
            credentialReader);

        _credentialReader =
            credentialReader;
    }

    public Task<ProviderAccessToken?> ResolveAsync(
        ProviderCredentialRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        cancellationToken.ThrowIfCancellationRequested();

        var secret =
            _credentialReader
                .ReadGenericSecret(
                    BuildTargetName(
                        request));

        return Task.FromResult(
            secret is null
                ? null
                : new ProviderAccessToken(
                    secret));
    }

    public static string BuildTargetName(
        ProviderCredentialRequest request)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        return string.Concat(
            TargetPrefix,
            "/",
            GetProviderSegment(
                request.Provider),
            "/",
            request.Authority);
    }

    private static string GetProviderSegment(
        ProviderCredentialProvider provider)
    {
        return provider switch
        {
            ProviderCredentialProvider.GitHub =>
                "github",
            ProviderCredentialProvider.Gitea =>
                "gitea",
            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(provider))
        };
    }
}
