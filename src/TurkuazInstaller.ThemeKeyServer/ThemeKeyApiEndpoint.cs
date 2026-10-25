// Dosya Yolu: src/TurkuazInstaller.ThemeKeyServer/ThemeKeyApiEndpoint.cs
// Amac: JWT ile korunan anahtar POST protokolu ve yalnizca yetkili key response
// Modul - CSharp API Endpoint
// Version: 0.1.0-qa
// Aciklama: JSON strict, 1024-byte cap, no-store, no secret logging, denial-first
// Bagimli Oldugu Katman: ASP.NET Core | ThemeKeyIssueService

using System.Text.Json;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Infrastructure.Branding;

namespace TurkuazInstaller.ThemeKeyServer;

internal static class ThemeKeyApiEndpoint
{
    private const int MaxBodyBytes = 1024;

    internal static async Task<IResult> HandleAsync(
        HttpContext context, ThemeKeyIssueService service,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (context.Request.ContentLength > MaxBodyBytes)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        using var payload = new MemoryStream();
        var buffer = new byte[256];
        int n;
        while ((n = await context.Request.Body.ReadAsync(
            buffer, cancellationToken)) > 0)
        {
            if (payload.Length + n > MaxBodyBytes)
            {
                return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }
            payload.Write(buffer, 0, n);
        }

        try
        {
            using var parsed = JsonDocument.Parse(
                payload.GetBuffer().AsMemory(0, (int)payload.Length),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 3
                });
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                root.EnumerateObject().Count() != 2)
            {
                return Results.BadRequest();
            }

            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var member in root.EnumerateObject())
            {
                if (!names.Add(member.Name) ||
                    member.Name is not ("package_id" or "key_id") ||
                    member.Value.ValueKind != JsonValueKind.String)
                {
                    return Results.BadRequest();
                }
            }

            if (!root.TryGetProperty("package_id", out var product) ||
                !root.TryGetProperty("key_id", out var key))
            {
                return Results.BadRequest();
            }

            var rawProduct = product.GetString();
            var rawKey = key.GetString();
            if (rawProduct is null || rawKey is null)
            {
                return Results.BadRequest();
            }

            PackageId packageId;
            try
            {
                packageId = PackageId.Parse(rawProduct);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest();
            }
            catch (FormatException)
            {
                return Results.BadRequest();
            }

            using var issued = await service.IssueAsync(
                context.User, packageId, rawKey, cancellationToken);
            if (issued is null)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            // Convert only after FULL server-side authorization. No key,
            // token or serialized body is written to logs.
            return Results.Json(new
            {
                package_id = issued.PackageId,
                key_id = issued.KeyId,
                key_base64 = Convert.ToBase64String(issued.Key)
            });
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }
    }
}
