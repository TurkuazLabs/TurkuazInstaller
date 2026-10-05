// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/tools/ManifestReleaseProviderFactory.cs
// 📌 Amac: Kullanici tarafindan girilen manifest URL veya dosya yolunu signed release provider adapterina map eder
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: HTTPS, file URI ve Windows local path kaynaklarini ayirir ve detached manifest signature verifierini tum providerlara zorunlu enjekte eder
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Contracts.Manifests;
using TurkuazInstaller.Contracts.Releases;
using TurkuazInstaller.Infrastructure.Manifests;
using TurkuazInstaller.Infrastructure.Providers.File;
using TurkuazInstaller.Infrastructure.Providers.Http;

namespace TurkuazInstaller.WinUI.Tools;

internal sealed class ManifestReleaseProviderFactory
{
    private const string UnsupportedSourceMessage =
        "Manifest source only supports HTTPS or local file paths.";

    private readonly HttpClient _httpClient;
    private readonly InstallerManifestReader _manifestReader;
    private readonly IManifestSignatureVerifier
        _manifestSignatureVerifier;

    public ManifestReleaseProviderFactory(
        HttpClient httpClient,
        InstallerManifestReader manifestReader,
        IManifestSignatureVerifier manifestSignatureVerifier)
    {
        _httpClient =
            httpClient;

        _manifestReader =
            manifestReader;

        _manifestSignatureVerifier =
            manifestSignatureVerifier;
    }

    public IReleaseProvider Create(
        string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            source);

        if (Path.IsPathFullyQualified(source))
        {
            return CreateFileProvider(
                Path.GetFullPath(
                    source));
        }

        if (
            Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var uri))
        {
            if (
                uri.Scheme ==
                Uri.UriSchemeHttps)
            {
                return new HttpReleaseProvider(
                    _httpClient,
                    _manifestReader,
                    _manifestSignatureVerifier,
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
            Path.GetFullPath(
                source));
    }

    private IReleaseProvider CreateFileProvider(
        string path)
    {
        return new FileReleaseProvider(
            _manifestReader,
            _manifestSignatureVerifier,
            new FileReleaseProviderOptions(
                path,
                path));
    }
}
