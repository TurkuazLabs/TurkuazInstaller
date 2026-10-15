// Dosya: /src/TurkuazInstaller.Infrastructure/tools/InstalledPayloadScopeTool.cs
// Amac: Velopack current altindaki dosya yollarini salt-okunur olarak raporlar
// Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Symlink/reparse ve fazla buyuk agac reddi; root disina cikmaz, UserData veya root disinda arama yapmaz
// Bagimli Oldugu Katman: Tool | Service

using TurkuazInstaller.Contracts.Operations;

namespace TurkuazInstaller.Infrastructure.Tools;

public sealed class InstalledPayloadScopeTool : IInstalledPayloadScopeTool
{
    private const int MaximumEntries = 20000;
    private const int MaximumFiles = 4096;

    public Task<IReadOnlyList<string>> ListCurrentFilesAsync(
        string installRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(installRoot);
        // Check every parent, not just current; a reparse ancestor can
        // redirect the observation to somebody else's installation.
        for (var ancestor = new DirectoryInfo(root);
             ancestor is not null;
             ancestor = ancestor.Parent)
        {
            EnsureRealDirectory(ancestor);
        }

        var current = new DirectoryInfo(Path.Combine(root, "current"));
        EnsureRealDirectory(current);

        var result = new List<string>();
        var stack = new Stack<(DirectoryInfo Directory, string RelativePath)>();
        stack.Push((current, "current"));
        var count = 0;

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (directory, relativePath) = stack.Pop();
            // Recheck before each read; this does not make the
            // assessment an atomic snapshot.
            EnsureRealDirectory(directory);

            foreach (var child in directory.EnumerateFileSystemInfos())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > MaximumEntries)
                {
                    throw new InvalidDataException(
                        "Installed current scope exceeds maximum entries.");
                }
                var attributes = child.Attributes;
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException(
                        "Installed current scope contains a reparse point.");
                }
                var relative = relativePath + "/" + child.Name;
                // Invalid names or alternate paths are evidence of an
                // ambiguous scope, never grounds for deleting anything.
                ValidateFileName(child.Name);
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    stack.Push((new DirectoryInfo(child.FullName), relative));
                }
                else
                {
                    if (result.Count >= MaximumFiles)
                    {
                        throw new InvalidDataException(
                            "Installed current scope exceeds maximum files.");
                    }
                    result.Add(relative);
                }
            }
        }

        result.Sort(StringComparer.Ordinal);
        return Task.FromResult<IReadOnlyList<string>>(result.AsReadOnly());
    }

    private static void EnsureRealDirectory(DirectoryInfo directory)
    {
        var attributes = File.GetAttributes(directory.FullName);
        if ((attributes & FileAttributes.ReparsePoint) != 0 ||
            (attributes & FileAttributes.Directory) == 0)
        {
            throw new IOException(
                "Installed current scope is not a real directory.");
        }
    }

    private static void ValidateFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name is "." or ".." ||
            name.EndsWith('.') || name.EndsWith(' ') ||
            name.Contains(':') ||
            name.Any(ch => ch < 0x20 || ch is '<' or '>' or '"' or '|' or '?' or '*' or '/' or '\\'))
        {
            throw new InvalidDataException(
                "Installed current scope contains an unsafe path.");
        }
    }
}
