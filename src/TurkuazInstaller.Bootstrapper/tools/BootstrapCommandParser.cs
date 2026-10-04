// 📄 Dosya Yolu: /src/TurkuazInstaller.Bootstrapper/tools/BootstrapCommandParser.cs
// 📌 Amac: Native bootstrap internal command-line protocolunu typed BootstrapInvocation modeline parse eder
// 📌 Modul - Tool CSharp
// Version: 0.6.0
// Aciklama: Self-update handoff ve cleanup argumentlerini controller logic disinda dogrular
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
        int? parentProcessId = null;
        var resumeArguments = new List<string>();

        for (var index = 0;
             index < arguments.Count;
             index++)
        {
            var argument = arguments[index];

            if (string.Equals(
                    argument,
                    BootstrapHandoffArguments.CompleteSelfUpdate,
                    StringComparison.Ordinal))
            {
                completeSelfUpdate = true;
                continue;
            }

            if (string.Equals(
                    argument,
                    BootstrapHandoffArguments.Source,
                    StringComparison.Ordinal))
            {
                source = ReadValue(
                    arguments,
                    ref index,
                    argument);

                continue;
            }

            if (string.Equals(
                    argument,
                    BootstrapHandoffArguments.Target,
                    StringComparison.Ordinal))
            {
                target = ReadValue(
                    arguments,
                    ref index,
                    argument);

                continue;
            }

            if (string.Equals(
                    argument,
                    BootstrapHandoffArguments.ParentProcessId,
                    StringComparison.Ordinal))
            {
                var value = ReadValue(
                    arguments,
                    ref index,
                    argument);

                if (!int.TryParse(
                        value,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out var parsedProcessId)
                    || parsedProcessId <= 0)
                {
                    throw new FormatException(
                        "Parent process id is invalid.");
                }

                parentProcessId = parsedProcessId;
                continue;
            }

            if (string.Equals(
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

            if (string.Equals(
                    argument,
                    BootstrapHandoffArguments.CleanupSource,
                    StringComparison.Ordinal))
            {
                cleanupSource = ReadValue(
                    arguments,
                    ref index,
                    argument);

                continue;
            }

            resumeArguments.Add(argument);
        }

        if (!completeSelfUpdate)
        {
            return new BootstrapInvocation(
                null,
                cleanupSource);
        }

        if (source is null
            || target is null
            || parentProcessId is null)
        {
            throw new FormatException(
                "Self-update completion arguments are incomplete.");
        }

        return new BootstrapInvocation(
            new SelfUpdateCompleteRequest(
                source,
                target,
                parentProcessId.Value,
                resumeArguments),
            cleanupSource);
    }

    private static string ReadValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string argumentName)
    {
        var valueIndex = index + 1;

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
