// 📄 Dosya Yolu: /src/TurkuazInstaller.WinUI/tools/ManifestReleaseProviderFactory.cs
// 📌 Amac: Kullanici tarafindan girilen manifest URL veya dosya yolunu mevcut release provider adapterina map eder
// 📌 Modul - Tool CSharp
// Version: 0.7.1
// Aciklama: HTTPS, file URI ve Windows local path kaynaklarini provider factory sinirinda ayirir
//
// Bagimli Oldugu Katman: Tool

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

    public ManifestReleaseProviderFactory(
        HttpClient httpClient,
        InstallerManifestReader manifestReader)
    {
        _httpClient = httpClient;
        _manifestReader = manifestReader;
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

        if (Uri.TryCreate(
                source,
                UriKind.Absolute,
                out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return new HttpReleaseProvider(
                    _httpClient,
                    _manifestReader,
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
            new FileReleaseProviderOptions(
                path,
                path));
    }
}
