// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Manifests/BoundedContentReader.cs
// 📌 Amac: Manifest ve detached signature streamlerini sabit maksimum byte siniri ile bellekte okur
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Content-Length olmasa bile stream okuma sirasinda limit asimini fail-closed reddeder
//
// Bagimli Oldugu Katman: Tool | Config

namespace TurkuazInstaller.Infrastructure.Manifests;

internal static class BoundedContentReader
{
    private const int BufferSize = 81920;

    public static async Task<byte[]> ReadAsync(
        Stream source,
        int maximumBytes,
        string contentName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            contentName);

        using var destination =
            new MemoryStream(
                Math.Min(
                    maximumBytes,
                    BufferSize));

        var buffer =
            new byte[
                Math.Min(
                    maximumBytes,
                    BufferSize)];

        var totalBytes = 0;

        while (true)
        {
            var read =
                await source
                    .ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length),
                        cancellationToken)
                    .ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            if (
                totalBytes >
                maximumBytes - read)
            {
                throw new InvalidDataException(
                    string.Concat(
                        contentName,
                        " exceeds the configured maximum size."));
            }

            await destination
                .WriteAsync(
                    buffer.AsMemory(
                        0,
                        read),
                    cancellationToken)
                .ConfigureAwait(false);

            totalBytes += read;
        }

        return destination.ToArray();
    }
}
