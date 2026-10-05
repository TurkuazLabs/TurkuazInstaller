// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/BootstrapCommandParser.cs
// 📌 Amac: Native bootstrap internal command-line protocolunu typed BootstrapInvocation modeline parse eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Self-update begin/complete, cleanup ve normal desktop argumentlarini Controller logic disinda dogrular
//
// Bagimli Oldugu Katman: Tool

using System.Globalization;
using TurkuazInstaller.Contracts.Bootstrap;
using TurkuazInstaller.Platform.Windows.Tools;

namespace TurkuazInstaller.Bootstrapper.Tools;

internal sealed class BootstrapCommandParser
{
    public BootstrapInvocation Parse(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var completeSelfUpdate = false;
        string? source = null;
        string? target = null;
        string? cleanupSource = null;
        string? selfUpdateReplacement = null;
        int? parentProcessId = null;
        var resumeArguments =
            new List<string>();
        var applicationArguments =
            new List<string>();

        for (
            var index = 0;
            index < arguments.Count;
            index++)
        {
            var argument =
                arguments[index];

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.BeginSelfUpdate,
                    StringComparison.Ordinal))
            {
                if (selfUpdateReplacement is not null)
                {
                    throw new FormatException(
                        "Self-update replacement argument cannot be repeated.");
                }

                selfUpdateReplacement =
                    ReadValue(
                        arguments,
                        ref index,
                        argument);

                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.CompleteSelfUpdate,
                    StringComparison.Ordinal))
            {
                completeSelfUpdate = true;
                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.Source,
                    StringComparison.Ordinal))
            {
                source =
                    ReadValue(
                        arguments,
                        ref index,
                        argument);
                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.Target,
                    StringComparison.Ordinal))
            {
                target =
                    ReadValue(
                        arguments,
                        ref index,
                        argument);
                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.ParentProcessId,
                    StringComparison.Ordinal))
            {
                var value =
                    ReadValue(
                        arguments,
                        ref index,
                        argument);

                if (
                    !int.TryParse(
                        value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var parsedProcessId) ||
                    parsedProcessId <= 0)
                {
                    throw new FormatException(
                        "Parent process id is invalid.");
                }

                parentProcessId =
                    parsedProcessId;
                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.ResumeArgument,
                    StringComparison.Ordinal))
            {
                resumeArguments.Add(
                    ReadValue(
                        arguments,
                        ref index,
                        argument));
                continue;
            }

            if (
                string.Equals(
                    argument,
                    BootstrapHandoffArguments.CleanupSource,
                    StringComparison.Ordinal))
            {
                cleanupSource =
                    ReadValue(
                        arguments,
                        ref index,
                        argument);
                continue;
            }

            applicationArguments.Add(
                argument);
        }

        if (completeSelfUpdate)
        {
            if (
                source is null ||
                target is null ||
                parentProcessId is null)
            {
                throw new FormatException(
                    "Self-update completion arguments are incomplete.");
            }

            if (selfUpdateReplacement is not null)
            {
                throw new FormatException(
                    "Self-update begin and complete modes cannot be combined.");
            }

            return new BootstrapInvocation(
                new SelfUpdateCompleteRequest(
                    source,
                    target,
                    parentProcessId.Value,
                    resumeArguments),
                cleanupSource,
                null,
                Array.Empty<string>());
        }

        if (
            source is not null ||
            target is not null ||
            parentProcessId is not null ||
            resumeArguments.Count > 0)
        {
            throw new FormatException(
                "Self-update completion arguments require complete-self-update mode.");
        }

        return new BootstrapInvocation(
            null,
            cleanupSource,
            selfUpdateReplacement,
            applicationArguments);
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string argumentName)
    {
        var valueIndex =
            index + 1;

        if (valueIndex >= arguments.Count)
        {
            throw new FormatException(
                string.Concat(
                    "Missing value for ",
                    argumentName,
                    "."));
        }

        index = valueIndex;

        return arguments[valueIndex];
    }
}
