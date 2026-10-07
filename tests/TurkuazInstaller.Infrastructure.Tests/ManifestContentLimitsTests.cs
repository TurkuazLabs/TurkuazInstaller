// 📄 Dosya Yolu: /tests/TurkuazInstaller.Infrastructure.Tests/ManifestContentLimitsTests.cs
// 📌 Amac: Runtime manifest/signature byte limitlerinin public contract degerleriyle drift etmesini engeller
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Manifest 1 MiB ve detached signature 256 KiB limitlerini explicit regression testiyle sabitler
//
// Bagimli Oldugu Katman: Config | Tool

using TurkuazInstaller.Infrastructure.Manifests;
using Xunit;

namespace TurkuazInstaller.Infrastructure.Tests;

public sealed class ManifestContentLimitsTests
{
    private const int ExpectedMaximumManifestBytes =
        1_048_576;

    private const int ExpectedMaximumDetachedSignatureBytes =
        262_144;

    [Fact]
    public void RuntimeLimits_MatchStableContract()
    {
        Assert.Equal(
            ExpectedMaximumManifestBytes,
            ManifestContentLimits.MaximumManifestBytes);

        Assert.Equal(
            ExpectedMaximumDetachedSignatureBytes,
            ManifestContentLimits.MaximumDetachedSignatureBytes);
    }
}
