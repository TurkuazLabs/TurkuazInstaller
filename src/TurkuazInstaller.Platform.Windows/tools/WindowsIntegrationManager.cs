// 📄 Dosya Yolu: /src/TurkuazInstaller.Platform.Windows/tools/WindowsIntegrationManager.cs
// 📌 Amac: Signed Windows integration policy'yi package-scoped receipt ownership ile reconcile eder
// 📌 Modul - Tool CSharp
// Version: 1.0.0
// Aciklama: Yeni shortcut/protocol aksiyonlarini uygular, receipt'i adim adim persist eder ve stale owned aksiyonlari guvenli temizler
//
// Bagimli Oldugu Katman: Tool | Repo | Service

using TurkuazInstaller.Contracts.Integrations;
using TurkuazInstaller.Domain.Integrations;
using TurkuazInstaller.Domain.Products;

namespace TurkuazInstaller.Platform.Windows.Tools;

public sealed class WindowsIntegrationManager
    : IWindowsIntegrationManager
{
    private readonly WindowsIntegrationReceiptStore
        _receiptStore;

    private readonly IWindowsShortcutStore
        _shortcutStore;

    private readonly IWindowsProtocolRegistrationStore
        _protocolStore;

    public WindowsIntegrationManager(
        WindowsIntegrationReceiptStore receiptStore,
        IWindowsShortcutStore shortcutStore,
        IWindowsProtocolRegistrationStore protocolStore)
    {
        ArgumentNullException.ThrowIfNull(
            receiptStore);
        ArgumentNullException.ThrowIfNull(
            shortcutStore);
        ArgumentNullException.ThrowIfNull(
            protocolStore);

        _receiptStore =
            receiptStore;

        _shortcutStore =
            shortcutStore;

        _protocolStore =
            protocolStore;
    }

    public async Task ApplyAsync(
        PackageId packageId,
        string targetPath,
        WindowsIntegrationPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetPath);
        ArgumentNullException.ThrowIfNull(
            policy);

        var existing =
            await _receiptStore
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        var trackedShortcuts =
            existing?.Shortcuts.ToList()
            ?? new List<WindowsShortcutReceipt>();

        var trackedProtocols =
            existing?.ProtocolSchemes
                .ToList()
            ?? new List<string>();

        var desiredShortcuts =
            new List<WindowsShortcutReceipt>(
                policy.Shortcuts.Count);

        foreach (var action in policy.Shortcuts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var priorReceipt =
                existing?.Shortcuts
                    .LastOrDefault(
                        receipt =>
                            string.Equals(
                                receipt.ActionId,
                                action.Id,
                                StringComparison.OrdinalIgnoreCase));

            var receipt =
                await _shortcutStore
                    .CreateAsync(
                        packageId,
                        targetPath,
                        action,
                        priorReceipt,
                        cancellationToken)
                    .ConfigureAwait(false);

            desiredShortcuts.Add(
                receipt);

            if (
                !trackedShortcuts.Any(
                    item =>
                        ReceiptEquals(
                            item,
                            receipt)))
            {
                trackedShortcuts.Add(
                    receipt);
            }

            await SaveWorkingReceiptAsync(
                    packageId,
                    trackedShortcuts,
                    trackedProtocols,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var desiredProtocols =
            new List<string>(
                policy.Protocols.Count);

        foreach (var action in policy.Protocols)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await _protocolStore
                .ApplyAsync(
                    packageId,
                    targetPath,
                    action,
                    cancellationToken)
                .ConfigureAwait(false);

            desiredProtocols.Add(
                action.Scheme);

            if (
                !trackedProtocols.Contains(
                    action.Scheme,
                    StringComparer.OrdinalIgnoreCase))
            {
                trackedProtocols.Add(
                    action.Scheme);
            }

            await SaveWorkingReceiptAsync(
                    packageId,
                    trackedShortcuts,
                    trackedProtocols,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (
            var staleShortcut in
            trackedShortcuts.Where(
                tracked =>
                    !desiredShortcuts.Any(
                        desired =>
                            ReceiptEquals(
                                tracked,
                                desired)))
                .ToArray())
        {
            await _shortcutStore
                .RemoveOwnedAsync(
                    staleShortcut,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (
            var staleScheme in
            trackedProtocols.Where(
                tracked =>
                    !desiredProtocols.Contains(
                        tracked,
                        StringComparer.OrdinalIgnoreCase))
                .ToArray())
        {
            await _protocolStore
                .RemoveOwnedAsync(
                    packageId,
                    staleScheme,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (
            desiredShortcuts.Count == 0 &&
            desiredProtocols.Count == 0)
        {
            await _receiptStore
                .DeleteAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

            return;
        }

        await _receiptStore
            .SaveAsync(
                new WindowsIntegrationReceipt(
                    packageId,
                    desiredShortcuts,
                    desiredProtocols),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        PackageId packageId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            packageId);

        var receipt =
            await _receiptStore
                .GetAsync(
                    packageId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (receipt is null)
        {
            return;
        }

        var errors =
            new List<Exception>();

        foreach (var shortcut in receipt.Shortcuts)
        {
            try
            {
                await _shortcutStore
                    .RemoveOwnedAsync(
                        shortcut,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(
                    exception);
            }
        }

        foreach (var scheme in receipt.ProtocolSchemes)
        {
            try
            {
                await _protocolStore
                    .RemoveOwnedAsync(
                        packageId,
                        scheme,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                errors.Add(
                    exception);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException(
                "One or more owned Windows integrations could not be removed.",
                errors);
        }

        await _receiptStore
            .DeleteAsync(
                packageId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private Task SaveWorkingReceiptAsync(
        PackageId packageId,
        IReadOnlyList<WindowsShortcutReceipt> shortcuts,
        IReadOnlyList<string> protocols,
        CancellationToken cancellationToken)
    {
        return _receiptStore
            .SaveAsync(
                new WindowsIntegrationReceipt(
                    packageId,
                    shortcuts.ToArray(),
                    protocols.ToArray()),
                cancellationToken);
    }

    private static bool ReceiptEquals(
        WindowsShortcutReceipt left,
        WindowsShortcutReceipt right)
    {
        return
            string.Equals(
                left.ActionId,
                right.ActionId,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFullPath(
                    left.Path),
                Path.GetFullPath(
                    right.Path),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                left.Sha256,
                right.Sha256,
                StringComparison.OrdinalIgnoreCase);
    }
}
