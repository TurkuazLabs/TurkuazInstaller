// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/StubHttpMessageHandler.cs
// 📌 Amac: Provider HTTP contract testlerinde dis ağa cikmadan deterministik response uretir
// 📌 Modul - Test Tool CSharp
// Version: 0.4.0
// Aciklama: URI bazli test response map'i ile HttpClient adapterlarini izole eder
//
// Bagimli Oldugu Katman: Tool

using System.Net;
using System.Text;

namespace TurkuazInstaller.Infrastructure.Tests;

internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly IReadOnlyDictionary<string, string> _responses;

    public StubHttpMessageHandler(IReadOnlyDictionary<string, string> responses)
    {
        _responses = responses;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var key = request.RequestUri?.AbsoluteUri ?? string.Empty;

        if (!_responses.TryGetValue(key, out var content))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8)
        });
    }
}
