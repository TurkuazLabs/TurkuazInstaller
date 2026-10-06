// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/tools/CliManifestReleaseProviderFactory.cs
// 📌 Amac: CLI manifest source degerini signed HTTPS veya local file release providerina map eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Detached manifest signature verifierini tum CLI provider yollarina zorunlu enjekte eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using TurkuazInstaller.Infrastructure.Providers.Http;

namespace TurkuazInstaller.Cli.Tools;

internal sealed class CliManifestReleaseProviderFactory
{
    private const string UnsupportedSourceMessage =
        "Manifest source only supports HTTPS or local file paths.";

    private readonly HttpClient _httpClient;
    private readonly InstallerManifestReader _manifestReader;
    private readonly IManifestSignatureVerifier _signatureVerifier;

    public CliManifestReleaseProviderFactory(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier signatureVerifier)
    {
        _httpClient = httpClient;
        _manifestReader = manifestReader;
        _signatureVerifier = signatureVerifier;
    }

    public IReleaseProvider Create(
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        if (Path.IsPathFullyQualified(source))
        {
            return CreateFileProvider(
                Path.GetFullPath(source));
        }

        if (
            Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return new HttpReleaseProvider(
                    _httpClient,
                    _manifestReader,
                    _signatureVerifier,
                    new HttpReleaseProviderOptions(
                        uri,
                        uri));
            }

            if (uri.IsFile)
            {
                return CreateFileProvider(
                    uri.LocalPath);
            }

            throw new InvalidOperationException(
                UnsupportedSourceMessage);
        }

        return CreateFileProvider(
            Path.GetFullPath(source));
    }

    private IReleaseProvider CreateFileProvider(
        string path)
    {
        return new FileReleaseProvider(
            _manifestReader,
            _signatureVerifier,
            new FileReleaseProviderOptions(
                path,
                path));
    }
}
