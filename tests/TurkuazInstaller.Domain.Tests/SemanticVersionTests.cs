// 📄 Dosya Yolu: /tests/TurkuazInstaller.Domain.Tests/SemanticVersionTests.cs
// 📌 Amac: SemanticVersion parse ve precedence davranisini SemVer kurallariyla dogrular
// 📌 Modul - Test CSharp
// Version: 0.3.0
// Aciklama: Stable, prerelease ve build metadata senaryolarini unit test ile kapsar
//
// Bagimli Oldugu Katman: Service

using TurkuazInstaller.Domain.Releases;
using Xunit;

namespace TurkuazInstaller.Domain.Tests;

public sealed class SemanticVersionTests
{
    [Fact]
    public void Parse_PreservesSemanticVersion()
    {
        var version = SemanticVersion.Parse("1.2.3-beta.2+build.7");
        Assert.Equal("1.2.3-beta.2+build.7", version.ToString());
    }

    [Fact]
    public void Compare_StableIsNewerThanPreRelease()
    {
        Assert.True(SemanticVersion.Parse("1.2.3") > SemanticVersion.Parse("1.2.3-beta.9"));
    }

    [Fact]
    public void Compare_NumericPreReleaseUsesNumericOrdering()
    {
        Assert.True(SemanticVersion.Parse("1.2.3-beta.10") > SemanticVersion.Parse("1.2.3-beta.2"));
    }
}
