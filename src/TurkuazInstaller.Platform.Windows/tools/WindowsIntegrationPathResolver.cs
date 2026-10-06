// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsIntegrationPathResolver.cs
// 📌 Amac: Signed Windows integration executable relative yolunu install root icinde fail-closed resolve eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Root escape, missing executable ve reparse-point zincirlerini reddeder
//
// Bagimli Oldugu Katman: Tool | Service

namespace TurkuazInstaller.Platform.Windows.Tools;

internal static class WindowsIntegrationPathResolver
{
    public static string ResolveExecutable(
        string targetPath,
        string executableRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            executableRelativePath);

        var root =
            Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(
                    targetPath));

        var relative =
            executableRelativePath
                .Replace(
                    '/',
                    Path.DirectorySeparatorChar)
                .Replace(
                    '\\',
                    Path.DirectorySeparatorChar);

        var executablePath =
            Path.GetFullPath(
                Path.Combine(
                    root,
                    relative));

        var rootPrefix =
            string.Concat(
                root,
                Path.DirectorySeparatorChar);

        if (
            !executablePath.StartsWith(
                rootPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Windows integration executable escapes install root.");
        }

        if (!File.Exists(
                executablePath))
        {
            throw new FileNotFoundException(
                "Windows integration executable does not exist.",
                executablePath);
        }

        RejectReparsePoints(
            root,
            relative);

        return executablePath;
    }

    private static void RejectReparsePoints(
        string root,
        string relativePath)
    {
        RejectReparsePoint(
            root);

        var current =
            root;

        foreach (
            var segment in
            relativePath.Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries))
        {
            current =
                Path.Combine(
                    current,
                    segment);

            if (
                Directory.Exists(
                    current) ||
                File.Exists(
                    current))
            {
                RejectReparsePoint(
                    current);
            }
        }
    }

    private static void RejectReparsePoint(
        string path)
    {
        var attributes =
            File.GetAttributes(
                path);

        if (
            (attributes &
                FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                "Windows integration executable path must not traverse reparse points.");
        }
    }
}
