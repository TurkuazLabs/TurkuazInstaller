// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/StubHttpMessageHandler.cs
// 📌 Amac: Provider HTTP contract testlerinde dis aga cikmadan deterministik response ve request kaydi uretir
// 📌 Modul - Test Tool CSharp
// Version: 0.5.0
// Aciklama: URI bazli response map'ine ek olarak Authorization scheme/parameter degerlerini request aninda snapshot eder
//
// Bagimli Oldugu Katman: Tool

using System.Net;
using System.Text;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class StubHttpMessageHandler
    : HttpMessageHandler
{
    private readonly IReadOnlyDictionary<string, string>
        _responses;

    private readonly List<StubHttpRequestRecord>
        _requests = new();

    public StubHttpMessageHandler(
        IReadOnlyDictionary<string, string> responses)
    {
        _responses = responses;
    }

    public IReadOnlyList<StubHttpRequestRecord> Requests =>
        _requests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var key =
            request.RequestUri?.AbsoluteUri
            ?? string.Empty;

        _requests.Add(
            new StubHttpRequestRecord(
                key,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter));

        if (
            !_responses.TryGetValue(
                key,
                out var content))
        {
            return Task.FromResult(
                new HttpResponseMessage(
                    HttpStatusCode.NotFound));
        }

        return Task.FromResult(
            new HttpResponseMessage(
                HttpStatusCode.OK)
            {
                Content =
                    new StringContent(
                        content,
                        Encoding.UTF8)
            });
    }
}

internal sealed record StubHttpRequestRecord(
    string Uri,
    string? AuthorizationScheme,
    string? AuthorizationParameter);
