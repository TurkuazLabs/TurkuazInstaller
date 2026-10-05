// 📄 Dosya Yolu: /src/TurkuazInstaller.Cli/tools/CliCommandParser.cs
// 📌 Amac: Raw command-line argumanlarini dogrulanmis CliInvocation modeline parse eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Explicit operation/options, duplicate rejection, stable/beta channel ve internal resume protokolunu fail-closed parse eder
//
// Bagimli Oldugu Katman: Tool | Service | Config

using TurkuazInstaller.Cli.Config;
using TurkuazInstaller.Cli.Services;
using TurkuazInstaller.Domain.Operations;
using TurkuazInstaller.Domain.Products;
using TurkuazInstaller.Domain.Releases;

namespace TurkuazInstaller.Cli.Tools;

public sealed class CliCommandParser
{
    public CliInvocation Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0)
        {
            throw new FormatException(
                "CLI operation is required.");
        }

        if (arguments.Contains(
                CliArguments.InternalResumePackageOption,
                StringComparer.Ordinal))
        {
            return ParseResume(arguments);
        }

        var operation =
            ParseOperation(
                arguments[0]);

        string? package = null;
        string? manifest = null;
        string? rollbackManifest = null;
        string? target = null;
        var channel = ReleaseChannel.Stable;
        var channelSeen = false;
        var silent = false;

        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            switch (argument)
            {
                case CliArguments.PackageOption:
                    package = ReadSingleValue(
                        arguments,
                        ref index,
                        argument,
                        package);
                    break;

                case CliArguments.ManifestOption:
                    manifest = ReadSingleValue(
                        arguments,
                        ref index,
                        argument,
                        manifest);
                    break;

                case CliArguments.RollbackManifestOption:
                    rollbackManifest = ReadSingleValue(
                        arguments,
                        ref index,
                        argument,
                        rollbackManifest);
                    break;

                case CliArguments.TargetOption:
                    target = ReadSingleValue(
                        arguments,
                        ref index,
                        argument,
                        target);
                    break;

                case CliArguments.ChannelOption:
                    if (channelSeen)
                    {
                        throw new FormatException(
                            "Channel option cannot be repeated.");
                    }

                    channel =
                        ParseChannel(
                            ReadValue(
                                arguments,
                                ref index,
                                argument));

                    channelSeen = true;
                    break;

                case CliArguments.SilentOption:
                    if (silent)
                    {
                        throw new FormatException(
                            "Silent option cannot be repeated.");
                    }

                    silent = true;
                    break;

                default:
                    throw new FormatException(
                        string.Concat(
                            "Unsupported CLI argument: ",
                            argument));
            }
        }

        if (string.IsNullOrWhiteSpace(package))
        {
            throw new FormatException(
                "Package option is required.");
        }

        if (
            operation != InstallerOperationType.Uninstall &&
            string.IsNullOrWhiteSpace(manifest))
        {
            throw new FormatException(
                "Manifest option is required for this operation.");
        }

        if (
            operation == InstallerOperationType.Rollback &&
            string.IsNullOrWhiteSpace(rollbackManifest))
        {
            throw new FormatException(
                "Rollback manifest option is required for rollback.");
        }

        return new CliInvocation(
            operation,
            PackageId.Parse(package),
            channel,
            manifest,
            rollbackManifest,
            target,
            silent,
            false);
    }

    private static CliInvocation ParseResume(
        IReadOnlyList<string> arguments)
    {
        string? package = null;
        var silent = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            switch (argument)
            {
                case CliArguments.InternalResumePackageOption:
                    package = ReadSingleValue(
                        arguments,
                        ref index,
                        argument,
                        package);
                    break;

                case CliArguments.SilentOption:
                    if (silent)
                    {
                        throw new FormatException(
                            "Silent option cannot be repeated.");
                    }

                    silent = true;
                    break;

                default:
                    throw new FormatException(
                        "Internal reboot resume invocation contains unsupported arguments.");
            }
        }

        if (string.IsNullOrWhiteSpace(package))
        {
            throw new FormatException(
                "Resume package value is required.");
        }

        return new CliInvocation(
            null,
            PackageId.Parse(package),
            ReleaseChannel.Stable,
            null,
            null,
            null,
            silent,
            true);
    }

    private static InstallerOperationType ParseOperation(
        string value)
    {
        return value switch
        {
            CliArguments.InstallCommand =>
                InstallerOperationType.Install,
            CliArguments.UpdateCommand =>
                InstallerOperationType.Update,
            CliArguments.RepairCommand =>
                InstallerOperationType.Repair,
            CliArguments.RollbackCommand =>
                InstallerOperationType.Rollback,
            CliArguments.UninstallCommand =>
                InstallerOperationType.Uninstall,
            _ =>
                throw new FormatException(
                    string.Concat(
                        "Unsupported CLI operation: ",
                        value))
        };
    }

    private static ReleaseChannel ParseChannel(
        string value)
    {
        return value switch
        {
            CliArguments.StableChannel =>
                ReleaseChannel.Stable,
            CliArguments.BetaChannel =>
                ReleaseChannel.Beta,
            _ =>
                throw new FormatException(
                    "Channel must be stable or beta.")
        };
    }

    private static string ReadSingleValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option,
        string? currentValue)
    {
        if (currentValue is not null)
        {
            throw new FormatException(
                string.Concat(
                    option,
                    " cannot be repeated."));
        }

        return ReadValue(
            arguments,
            ref index,
            option);
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        var valueIndex = index + 1;

        if (valueIndex >= arguments.Count)
        {
            throw new FormatException(
                string.Concat(
                    "Missing value for ",
                    option,
                    "."));
        }

        var value = arguments[valueIndex];

        if (
            string.IsNullOrWhiteSpace(value) ||
            value.StartsWith(
                "--",
                StringComparison.Ordinal))
        {
            throw new FormatException(
                string.Concat(
                    "Invalid value for ",
                    option,
                    "."));
        }

        index = valueIndex;
        return value.Trim();
    }
}
