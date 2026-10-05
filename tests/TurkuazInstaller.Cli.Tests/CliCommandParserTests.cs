// 📄 Dosya Yolu: /tests/TurkuazInstaller.Cli.Tests/CliCommandParserTests.cs
// 📌 Amac: CLI command-line contractini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Install, silent resume, channel, required option ve duplicate rejection kurallarini kapsar
//
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Cli.Tools;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Releases;
using Xunit;

namespace TurkuazInstaller.Cli.Tests;

public sealed class CliCommandParserTests
{
    [Fact]
    public void Parse_Install_MapsTypedInvocation()
    {
        var invocation =
            new CliCommandParser()
                .Parse(
                    new[]
                    {
                        "install",
                        "--package",
                        "example-app",
                        "--manifest",
                        "https://example.invalid/installer-manifest.yml",
                        "--channel",
                        "beta",
                        "--target",
                        "C:/Apps/Example",
                        "--silent"
                    });

        Assert.Equal(
            InstallerOperationType.Install,
            invocation.Operation);

        Assert.Equal(
            "example-app",
            invocation.PackageId.Value);

        Assert.Equal(
            ReleaseChannel.Beta,
            invocation.Channel);

        Assert.Equal(
            "C:/Apps/Example",
            invocation.TargetPath);

        Assert.True(
            invocation.Silent);

        Assert.False(
            invocation.IsResume);
    }

    [Fact]
    public void Parse_InternalResume_MapsSilentResume()
    {
        var invocation =
            new CliCommandParser()
                .Parse(
                    new[]
                    {
                        "--resume-package",
                        "example-app",
                        "--silent"
                    });

        Assert.True(
            invocation.IsResume);

        Assert.Null(
            invocation.Operation);

        Assert.True(
            invocation.Silent);

        Assert.Equal(
            "example-app",
            invocation.PackageId.Value);
    }

    [Fact]
    public void Parse_RollbackWithoutRollbackManifest_RejectsInvocation()
    {
        Assert.Throws<FormatException>(
            () =>
                new CliCommandParser()
                    .Parse(
                        new[]
                        {
                            "rollback",
                            "--package",
                            "example-app",
                            "--manifest",
                            "https://example.invalid/current.yml"
                        }));
    }

    [Fact]
    public void Parse_DuplicatePackage_RejectsInvocation()
    {
        Assert.Throws<FormatException>(
            () =>
                new CliCommandParser()
                    .Parse(
                        new[]
                        {
                            "uninstall",
                            "--package",
                            "example-app",
                            "--package",
                            "example-app"
                        }));
    }

    [Fact]
    public void Parse_HttpManifest_LeavesTransportRejectionToProviderBoundary()
    {
        var invocation =
            new CliCommandParser()
                .Parse(
                    new[]
                    {
                        "install",
                        "--package",
                        "example-app",
                        "--manifest",
                        "http://example.invalid/installer-manifest.yml"
                    });

        Assert.Equal(
            "http://example.invalid/installer-manifest.yml",
            invocation.ManifestSource);
    }
}
