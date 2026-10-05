// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerResumeLaunchParserTests.cs
// 📌 Amac: Internal reboot resume startup arguman parserini unit test ile dogrular
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: Resume package parse, tekrar ve eksik deger fail-closed davranislarini kapsar
//
// Bagimli Oldugu Katman: Service | Tool

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Presentation.Services;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class InstallerResumeLaunchParserTests
{
    [Fact]
    public void Parse_ResumePackage_ReturnsTypedPackageId()
    {
        var parser =
            new InstallerResumeLaunchParser();

        var result =
            parser.Parse(
                new[]
                {
                    InstallerResumeLaunchArguments.PackageOption,
                    "Example-App"
                });

        Assert.NotNull(
            result);

        Assert.Equal(
            "example-app",
            result!.Value);
    }

    [Fact]
    public void Parse_RepeatedResumePackage_FailsClosed()
    {
        var parser =
            new InstallerResumeLaunchParser();

        Assert.Throws<FormatException>(
            () =>
                parser.Parse(
                    new[]
                    {
                        InstallerResumeLaunchArguments.PackageOption,
                        "example-app",
                        InstallerResumeLaunchArguments.PackageOption,
                        "example-app"
                    }));
    }

    [Fact]
    public void Parse_MissingResumePackageValue_FailsClosed()
    {
        var parser =
            new InstallerResumeLaunchParser();

        Assert.Throws<FormatException>(
            () =>
                parser.Parse(
                    new[]
                    {
                        InstallerResumeLaunchArguments.PackageOption
                    }));
    }
}
