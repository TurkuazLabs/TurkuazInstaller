// 📄 Dosya Yolu: /tests/TurkuazInstaller.Bootstrapper.Tests/BootstrapCommandParserTests.cs
// 📌 Amac: Bootstrap internal CLI protocol parserinin normal launch ve self-update modlarini unit test eder
// 📌 Modul - Test CSharp
// Version: 1.0.0
// Aciklama: App argument forwarding, begin/complete self-update ve gecersiz invocation davranislarini dogrular
//
// Bagimli Oldugu Katman: Tool

using TurkuazInstaller.Bootstrapper.Tools;
using TurkuazInstaller.Platform.Windows.Tools;
using Xunit;

namespace TurkuazInstaller.Bootstrapper.Tests;

public sealed class BootstrapCommandParserTests
{
    [Fact]
    public void Parse_NormalArguments_ForwardsToApplication()
    {
        var parser =
            new BootstrapCommandParser();

        var invocation =
            parser.Parse(
                new[]
                {
                    "--product",
                    "example-app"
                });

        Assert.False(
            invocation.IsSelfUpdateStart);

        Assert.False(
            invocation.IsSelfUpdateCompletion);

        Assert.Equal(
            new[]
            {
                "--product",
                "example-app"
            },
            invocation.ApplicationArguments);
    }

    [Fact]
    public void Parse_SelfUpdateReplacement_CreatesBeginInvocation()
    {
        var parser =
            new BootstrapCommandParser();

        const string replacement =
            @"C:\Stage\TurkuazInstaller.Bootstrapper.exe";

        var invocation =
            parser.Parse(
                new[]
                {
                    BootstrapHandoffArguments.BeginSelfUpdate,
                    replacement,
                    "--product",
                    "example-app"
                });

        Assert.True(
            invocation.IsSelfUpdateStart);

        Assert.Equal(
            replacement,
            invocation.SelfUpdateReplacementPath);

        Assert.Equal(
            new[]
            {
                "--product",
                "example-app"
            },
            invocation.ApplicationArguments);
    }

    [Fact]
    public void Parse_SelfUpdateCompletion_MapsInternalProtocol()
    {
        var parser =
            new BootstrapCommandParser();

        var invocation =
            parser.Parse(
                new[]
                {
                    BootstrapHandoffArguments.CompleteSelfUpdate,
                    BootstrapHandoffArguments.Source,
                    @"C:\Stage\TurkuazInstaller.Bootstrapper.exe",
                    BootstrapHandoffArguments.Target,
                    @"C:\Apps\TurkuazInstaller.Bootstrapper.exe",
                    BootstrapHandoffArguments.ParentProcessId,
                    "42",
                    BootstrapHandoffArguments.ResumeArgument,
                    "--product",
                    BootstrapHandoffArguments.ResumeArgument,
                    "example-app"
                });

        Assert.True(
            invocation.IsSelfUpdateCompletion);

        Assert.NotNull(
            invocation.SelfUpdateRequest);

        Assert.Equal(
            42,
            invocation.SelfUpdateRequest.ParentProcessId);

        Assert.Equal(
            new[]
            {
                "--product",
                "example-app"
            },
            invocation.SelfUpdateRequest.ResumeArguments);
    }

    [Fact]
    public void Parse_CompletionFieldsWithoutMode_Throws()
    {
        var parser =
            new BootstrapCommandParser();

        Assert.Throws<FormatException>(
            () =>
                parser.Parse(
                    new[]
                    {
                        BootstrapHandoffArguments.Source,
                        @"C:\Stage\TurkuazInstaller.Bootstrapper.exe"
                    }));
    }
}
