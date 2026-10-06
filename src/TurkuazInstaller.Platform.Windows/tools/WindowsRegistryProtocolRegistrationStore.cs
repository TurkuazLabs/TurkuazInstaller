// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsRegistryProtocolRegistrationStore.cs
// 📌 Amac: Package-owned URL protocol kayitlarini HKCU Software Classes altinda ownership marker ile guvenli yonetir
// 📌 Modul - Tool CSharp
// Version: 1.1.1
// Aciklama: Existing third-party scheme overwrite edilmez; remove yalniz owner marker package id ile eslesirse calisir
//
// Bagimli Oldugu Katman: Tool

using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsRegistryProtocolRegistrationStore
    : IWindowsProtocolRegistrationStore
{
    private const string ClassesRoot =
        "Software\\Classes";

    private const string OwnerValueName =
        "TurkuazInstallerOwner";

    public Task ApplyAsync(
        PackageId packageId,
        string targetPath,
        WindowsProtocolIntegration action,
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
                "Windows protocol integration is only available on Windows.");
        }

        var executablePath =
            WindowsIntegrationPathResolver
                .ResolveExecutable(
                    targetPath,
                    action.ExecutableRelativePath);

        using var mutex =
            CreateProtocolMutex(
                action.Scheme);

        AcquireMutex(
            mutex,
            cancellationToken);

        try
        {
            var keyPath =
                string.Concat(
                    ClassesRoot,
                    "\\",
                    action.Scheme);

            using var existing =
                Registry.CurrentUser
                    .OpenSubKey(
                        keyPath,
                        writable: true);

            if (existing is not null)
            {
                var owner =
                    existing.GetValue(
                        OwnerValueName)
                    as string;

                if (
                    !string.Equals(
                        owner,
                        packageId.Value,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "Windows URL protocol is already owned by another application.");
                }
            }

            using var protocolKey =
                Registry.CurrentUser
                    .CreateSubKey(
                        keyPath,
                        writable: true)
                ?? throw new IOException(
                    "Windows URL protocol registry key could not be created.");

            protocolKey.SetValue(
                string.Empty,
                string.Concat(
                    "URL:",
                    action.Scheme,
                    " Protocol"),
                RegistryValueKind.String);

            protocolKey.SetValue(
                "URL Protocol",
                string.Empty,
                RegistryValueKind.String);

            protocolKey.SetValue(
                OwnerValueName,
                packageId.Value,
                RegistryValueKind.String);

            using var commandKey =
                protocolKey.CreateSubKey(
                    "shell\\open\\command",
                    writable: true)
                ?? throw new IOException(
                    "Windows URL protocol command key could not be created.");

            commandKey.SetValue(
                string.Empty,
                string.Concat(
                    "\"",
                    executablePath,
                    "\" \"%1\""),
                RegistryValueKind.String);
        }
        finally
        {
            ReleaseMutex(
                mutex);
        }

        return Task.CompletedTask;
    }

    public Task RemoveOwnedAsync(
        PackageId packageId,
        string scheme,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            scheme);

        cancellationToken.ThrowIfCancellationRequested();

        var normalizedScheme =
            new WindowsProtocolIntegration(
                scheme,
                "TurkuazInstallerReceiptTarget.exe")
                .Scheme;

        using var mutex =
            CreateProtocolMutex(
                normalizedScheme);

        AcquireMutex(
            mutex,
            cancellationToken);

        try
        {
            var keyPath =
                string.Concat(
                    ClassesRoot,
                    "\\",
                    normalizedScheme);

            using var existing =
                Registry.CurrentUser
                    .OpenSubKey(
                        keyPath,
                        writable: false);

            if (existing is null)
            {
                return Task.CompletedTask;
            }

            var owner =
                existing.GetValue(
                    OwnerValueName)
                as string;

            if (
                string.Equals(
                    owner,
                    packageId.Value,
                    StringComparison.Ordinal))
            {
                existing.Close();

                Registry.CurrentUser
                    .DeleteSubKeyTree(
                        keyPath,
                        throwOnMissingSubKey: false);
            }
        }
        finally
        {
            ReleaseMutex(
                mutex);
        }

        return Task.CompletedTask;
    }

    private static Mutex CreateProtocolMutex(
        string scheme)
    {
        var hash =
            Convert
                .ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            scheme.Trim()
                                .ToLowerInvariant())))
                .Substring(
                    0,
                    24);

        return new Mutex(
            false,
            string.Concat(
                "Local\\TurkuazInstaller.Protocol.",
                hash));
    }

    private static void AcquireMutex(
        Mutex mutex,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (mutex.WaitOne(
                        TimeSpan.FromMilliseconds(
                            250)))
                {
                    return;
                }
            }
            catch (AbandonedMutexException)
            {
                return;
            }
        }
    }

    private static void ReleaseMutex(
        Mutex mutex)
    {
        try
        {
            mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }
    }
}
