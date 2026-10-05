// 📄 Dosya Yolu: /src/TurkuazInstaller.Infrastructure/Operations/FileInstallerOperationLock.cs
// 📌 Amac: Package bazli cross-process installer operation lockunu exclusive file handle ile uygular
// 📌 Modul - Tool CSharp
// Version: 1.1.0
// Aciklama: FileShare.None kullanarak ayni package icin ikinci install/update/repair/rollback/uninstall islemini fail-fast reddeder
//
// Bagimli Oldugu Katman: Tool | Service

using System.Globalization;
using System.Text;
using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Infrastructure.Operations;

public sealed class FileInstallerOperationLock
    : IInstallerOperationLock
{
    private const string LockExtension = ".lock";

    private readonly FileInstallerOperationLockOptions _options;

    public FileInstallerOperationLock(
        FileInstallerOperationLockOptions options)
    {
        _options = options;
    }

    public Task<IAsyncDisposable> AcquireAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        Directory.CreateDirectory(
            _options.RootDirectory);

        var path =
            Path.Combine(
                _options.RootDirectory,
                string.Concat(
                    packageId.Value,
                    LockExtension));

        FileStream stream;

        try
        {
            stream =
                new FileStream(
                    path,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous);
        }
        catch (IOException)
        {
            throw new PackageOperationLockedException(
                packageId);
        }

        try
        {
            stream.SetLength(0);

            var metadata =
                string.Concat(
                    "pid=",
                    Environment.ProcessId
                        .ToString(
                            CultureInfo.InvariantCulture),
                    Environment.NewLine,
                    "utc=",
                    DateTimeOffset.UtcNow
                        .ToString(
                            "O",
                            CultureInfo.InvariantCulture),
                    Environment.NewLine);

            var bytes =
                Encoding.UTF8.GetBytes(
                    metadata);

            stream.Write(
                bytes,
                0,
                bytes.Length);

            stream.Flush(
                flushToDisk: true);

            return Task.FromResult<IAsyncDisposable>(
                new Lease(
                    path,
                    stream));
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private sealed class Lease
        : IAsyncDisposable
    {
        private readonly string _path;
        private FileStream? _stream;

        public Lease(
            string path,
            FileStream stream)
        {
            _path = path;
            _stream = stream;
        }

        public ValueTask DisposeAsync()
        {
            var stream =
                Interlocked.Exchange(
                    ref _stream,
                    null);

            if (stream is null)
            {
                return ValueTask.CompletedTask;
            }

            stream.Dispose();

            try
            {
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return ValueTask.CompletedTask;
        }
    }
}
