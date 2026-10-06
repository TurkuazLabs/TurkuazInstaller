// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsShellLinkShortcutStore.cs
// 📌 Amac: Package-owned Desktop/Start Menu .lnk shortcutlarini Windows ShellLink COM ile guvenli olusturur ve temizler
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Existing dosya yalniz onceki receipt SHA-256 eslesirse overwrite edilir; cleanup yalniz hash eslesirse siler
//
// Bagimli Oldugu Katman: Tool

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsShellLinkShortcutStore
    : IWindowsShortcutStore
{
    private static readonly Guid ShellLinkClassId =
        new(
            "00021401-0000-0000-C000-000000000046");

    private const int NormalShowCommand = 1;

    public Task<WindowsShortcutReceipt> CreateAsync(
        PackageId packageId,
        string targetPath,
        WindowsShortcutIntegration action,
        WindowsShortcutReceipt? existingReceipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentNullException.ThrowIfNull(
            action);

        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Windows shortcut integration is only available on Windows.");
        }

        var executablePath =
            WindowsIntegrationPathResolver
                .ResolveExecutable(
                    targetPath,
                    action.ExecutableRelativePath);

        var shortcutPath =
            ResolveShortcutPath(
                packageId,
                action);

        if (File.Exists(
                shortcutPath))
        {
            if (
                existingReceipt is null ||
                !string.Equals(
                    Path.GetFullPath(
                        existingReceipt.Path),
                    Path.GetFullPath(
                        shortcutPath),
                    StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(
                    ComputeSha256(
                        shortcutPath),
                    existingReceipt.Sha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Windows shortcut path is already occupied by an unowned or modified file.");
            }
        }

        var directory =
            Path.GetDirectoryName(
                shortcutPath)
            ?? throw new InvalidOperationException(
                "Windows shortcut directory could not be resolved.");

        Directory.CreateDirectory(
            directory);

        CreateShellLink(
            shortcutPath,
            executablePath,
            action.Name);

        return Task.FromResult(
            new WindowsShortcutReceipt(
                action.Id,
                Path.GetFullPath(
                    shortcutPath),
                ComputeSha256(
                    shortcutPath)));
    }

    public Task RemoveOwnedAsync(
        WindowsShortcutReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            receipt);

        cancellationToken.ThrowIfCancellationRequested();

        var path =
            Path.GetFullPath(
                receipt.Path);

        if (
            File.Exists(
                path) &&
            string.Equals(
                ComputeSha256(
                    path),
                receipt.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(
                path);
        }

        return Task.CompletedTask;
    }

    private static string ResolveShortcutPath(
        PackageId packageId,
        WindowsShortcutIntegration action)
    {
        var fileName =
            string.Concat(
                action.Name,
                ".lnk");

        if (
            action.Location ==
            WindowsShortcutLocation.Desktop)
        {
            var desktop =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.DesktopDirectory);

            if (string.IsNullOrWhiteSpace(
                    desktop))
            {
                throw new InvalidOperationException(
                    "Windows Desktop directory could not be resolved.");
            }

            return Path.Combine(
                desktop,
                fileName);
        }

        var applicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(
                applicationData))
        {
            throw new InvalidOperationException(
                "Windows Start Menu root could not be resolved.");
        }

        return Path.Combine(
            applicationData,
            "Microsoft",
            "Windows",
            "Start Menu",
            "Programs",
            "TurkuazInstaller",
            packageId.Value,
            fileName);
    }

    private static void CreateShellLink(
        string shortcutPath,
        string executablePath,
        string description)
    {
        var shellLinkType =
            Type.GetTypeFromCLSID(
                ShellLinkClassId,
                throwOnError: true)
            ?? throw new InvalidOperationException(
                "Windows ShellLink COM type could not be resolved.");

        var instance =
            Activator.CreateInstance(
                shellLinkType)
            ?? throw new InvalidOperationException(
                "Windows ShellLink COM instance could not be created.");

        try
        {
            var shellLink =
                (IShellLinkW)instance;

            shellLink.SetPath(
                executablePath);

            shellLink.SetWorkingDirectory(
                Path.GetDirectoryName(
                    executablePath)
                ?? throw new InvalidOperationException(
                    "Windows integration executable directory could not be resolved."));

            shellLink.SetDescription(
                description);

            shellLink.SetArguments(
                string.Empty);

            shellLink.SetShowCmd(
                NormalShowCommand);

            ((IPersistFile)instance)
                .Save(
                    shortcutPath,
                    true);
        }
        finally
        {
            if (Marshal.IsComObject(
                    instance))
            {
                Marshal.FinalReleaseComObject(
                    instance);
            }
        }
    }

    private static string ComputeSha256(
        string path)
    {
        using var stream =
            new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);

        return Convert
            .ToHexString(
                SHA256.HashData(
                    stream))
            .ToLowerInvariant();
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder file,
            int maxPath,
            IntPtr findData,
            uint flags);

        void GetIDList(
            out IntPtr itemIdList);

        void SetIDList(
            IntPtr itemIdList);

        void GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder name,
            int maxName);

        void SetDescription(
            [MarshalAs(UnmanagedType.LPWStr)]
            string name);

        void GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder directory,
            int maxPath);

        void SetWorkingDirectory(
            [MarshalAs(UnmanagedType.LPWStr)]
            string directory);

        void GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder arguments,
            int maxArguments);

        void SetArguments(
            [MarshalAs(UnmanagedType.LPWStr)]
            string arguments);

        void GetHotkey(
            out short hotkey);

        void SetHotkey(
            short hotkey);

        void GetShowCmd(
            out int showCommand);

        void SetShowCmd(
            int showCommand);

        void GetIconLocation(
            [Out, MarshalAs(UnmanagedType.LPWStr)]
            StringBuilder iconPath,
            int iconPathLength,
            out int iconIndex);

        void SetIconLocation(
            [MarshalAs(UnmanagedType.LPWStr)]
            string iconPath,
            int iconIndex);

        void SetRelativePath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string relativePath,
            uint reserved);

        void Resolve(
            IntPtr windowHandle,
            uint flags);

        void SetPath(
            [MarshalAs(UnmanagedType.LPWStr)]
            string path);
    }
}
