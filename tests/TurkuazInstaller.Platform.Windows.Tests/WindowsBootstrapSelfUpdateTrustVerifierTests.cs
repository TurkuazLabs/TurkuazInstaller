// 📄 Dosya Yolu: /tests/TurkuazInstaller.Platform.Windows.Tests/WindowsBootstrapSelfUpdateTrustVerifierTests.cs
// 📌 Amac: Bootstrap self-update trust verifier unsigned current executable sinirini Windows unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Unsigned bootstrapta automatic self-update'in devre disi kaldigini ve replacement verification'in fail-closed oldugunu test eder
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Platform.Windows.Tests;

public sealed class WindowsBootstrapSelfUpdateTrustVerifierTests
{
    [Fact]
    public async Task CanSelfUpdateAsync_UnsignedCurrentExecutable_ReturnsFalse()
    {
        var path =
            CreateUnsignedExecutable();

        try
        {
            var result =
                await new WindowsBootstrapSelfUpdateTrustVerifier()
                    .CanSelfUpdateAsync(
                        path,
                        CancellationToken.None);

            Assert.False(
                result);
        }
        finally
        {
            File.Delete(
                path);
        }
    }

    [Fact]
    public async Task VerifyReplacementAsync_UnsignedCurrentExecutable_FailsClosed()
    {
        var currentPath =
            CreateUnsignedExecutable();

        var replacementPath =
            CreateUnsignedExecutable();

        try
        {
            var result =
                await new WindowsBootstrapSelfUpdateTrustVerifier()
                    .VerifyReplacementAsync(
                        currentPath,
                        replacementPath,
                        CancellationToken.None);

            Assert.False(
                result.IsValid);
        }
        finally
        {
            File.Delete(
                currentPath);

            File.Delete(
                replacementPath);
        }
    }

    private static string CreateUnsignedExecutable()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                "TurkuazInstaller.Tests",
                Guid.NewGuid()
                    .ToString("N"));

        Directory.CreateDirectory(
            root);

        var path =
            Path.Combine(
                root,
                "TurkuazInstaller.Bootstrapper.exe");

        File.WriteAllBytes(
            path,
            new byte[]
            {
                0x4d,
                0x5a
            });

        return path;
    }
}
