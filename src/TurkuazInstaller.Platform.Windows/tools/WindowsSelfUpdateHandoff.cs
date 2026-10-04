// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsSelfUpdateHandoff.cs
// 📌 Amac: Native bootstrap executable self-update islemini iki-process handoff modeliyle guvenli sekilde tamamlar
// 📌 Modul - Tool CSharp
// Version: 0.6.1
// Aciklama: Replacement process baslatir, parent exit bekler, executable dosyasini retry ile degistirir ve yeni target bootstrap'i resume eder
//
// Bagimli Oldugu Katman: Tool | Config

using System.Diagnostics;
using TurkuazInstaller.Contracts.Bootstrap;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsSelfUpdateHandoff : ISelfUpdateHandoff
{
    private readonly SelfUpdateHandoffOptions _options;

    public WindowsSelfUpdateHandoff(
        SelfUpdateHandoffOptions options)
    {
        _options = options;
    }

    public Task BeginAsync(
        SelfUpdateStartRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var currentPath = ValidateExecutablePath(
            request.CurrentExecutablePath);

        var replacementPath = ValidateExecutablePath(
            request.ReplacementExecutablePath);

        if (!File.Exists(currentPath))
        {
            throw new FileNotFoundException(
                "Current bootstrap executable was not found.",
                currentPath);
        }

        if (!File.Exists(replacementPath))
        {
            throw new FileNotFoundException(
                "Replacement bootstrap executable was not found.",
                replacementPath);
        }

        if (!string.Equals(
                Path.GetFileName(currentPath),
                Path.GetFileName(replacementPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Replacement bootstrap executable name must match the current executable name.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = replacementPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetExecutableDirectory(
                replacementPath)
        };

        startInfo.ArgumentList.Add(
            BootstrapHandoffArguments.CompleteSelfUpdate);

        AddPair(
            startInfo,
            BootstrapHandoffArguments.Source,
            replacementPath);

        AddPair(
            startInfo,
            BootstrapHandoffArguments.Target,
            currentPath);

        AddPair(
            startInfo,
            BootstrapHandoffArguments.ParentProcessId,
            Environment.ProcessId.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

        foreach (var resumeArgument in request.ResumeArguments)
        {
            AddPair(
                startInfo,
                BootstrapHandoffArguments.ResumeArgument,
                resumeArgument);
        }

        using var replacementProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Replacement bootstrap process could not be started.");

        return Task.CompletedTask;
    }

    public async Task CompleteAsync(
        SelfUpdateCompleteRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sourcePath = ValidateExecutablePath(
            request.SourceExecutablePath);

        var targetPath = ValidateExecutablePath(
            request.TargetExecutablePath);

        var runningPath = Environment.ProcessPath;

        if (string.IsNullOrWhiteSpace(runningPath)
            || !PathsEqual(runningPath, sourcePath))
        {
            throw new InvalidOperationException(
                "Self-update source must be the currently running bootstrap executable.");
        }

        if (!string.Equals(
                Path.GetFileName(sourcePath),
                Path.GetFileName(targetPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Self-update target executable name must match the source executable name.");
        }

        await WaitForParentExitAsync(
                request.ParentProcessId,
                cancellationToken)
            .ConfigureAwait(false);

        await ReplaceWithRetryAsync(
                sourcePath,
                targetPath,
                cancellationToken)
            .ConfigureAwait(false);

        var startInfo = new ProcessStartInfo
        {
            FileName = targetPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = GetExecutableDirectory(
                targetPath)
        };

        foreach (var resumeArgument in request.ResumeArguments)
        {
            startInfo.ArgumentList.Add(resumeArgument);
        }

        AddPair(
            startInfo,
            BootstrapHandoffArguments.CleanupSource,
            sourcePath);

        using var resumedProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Updated bootstrap process could not be resumed.");
    }

    private async Task ReplaceWithRetryAsync(
        string sourcePath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        IOException? lastException = null;

        for (var attempt = 1;
             attempt <= _options.ReplacementAttempts;
             attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Copy(
                    sourcePath,
                    targetPath,
                    overwrite: true);

                return;
            }
            catch (IOException exception)
            {
                lastException = exception;

                if (attempt == _options.ReplacementAttempts)
                {
                    break;
                }

                await Task
                    .Delay(
                        _options.RetryDelay,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        throw new IOException(
            "Bootstrap executable could not be replaced after the configured retry attempts.",
            lastException);
    }

    private static async Task WaitForParentExitAsync(
        int parentProcessId,
        CancellationToken cancellationToken)
    {
        try
        {
            using var parentProcess =
                Process.GetProcessById(parentProcessId);

            await parentProcess
                .WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
    }

    private static string ValidateExecutablePath(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);

        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Bootstrap handoff paths must point to executable files.");
        }

        return fullPath;
    }

    private static string GetExecutableDirectory(
        string executablePath)
    {
        return Path.GetDirectoryName(executablePath)
            ?? throw new InvalidOperationException(
                "Bootstrap executable directory could not be resolved.");
    }

    private static void AddPair(
        ProcessStartInfo startInfo,
        string name,
        string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static bool PathsEqual(
        string left,
        string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
