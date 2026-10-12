// 📄 Dosya Yolu: /tests/TurkuazInstaller.Presentation.Tests/InstallerInstallScopePolicyTests.cs
// 📌 Amac: Hizli/ozel kurulum scope secimlerinin sistem geneli yetkiyi sessizce acmadigini dogrular
// 📌 Modul - Test CSharp
// Version: 1.1.0
// Aciklama: Varsayilan per-user izin, per-machine reddi ve bilinmeyen enum reddi
//
// Bagimli Oldugu Katman: Service | Config

using TurkuazInstaller.Contracts.Operations;
using TurkuazInstaller.Presentation.Services;
using Xunit;

namespace TurkuazInstaller.Presentation.Tests;

public sealed class InstallerInstallScopePolicyTests
{
    [Fact]
    public void PerUser_IsAllowedByCurrentRuntime()
    {
        InstallerInstallScopePolicy.ValidateCurrentRuntime(InstallerInstallScope.PerUser);
    }

    [Fact]
    public void PerMachine_FailsClosedUntilElevatedMsiIsReady()
    {
        Assert.Throws<NotSupportedException>(
            () => InstallerInstallScopePolicy.ValidateCurrentRuntime(
                InstallerInstallScope.PerMachine));
    }

    [Fact]
    public void CustomPerUserTarget_AllowsAppDataChild()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Turkuaz Office");

        InstallerInstallScopePolicy.ValidatePerUserTarget(folder);
    }

    [Fact]
    public void CustomPerUserTarget_RejectsPathOutsideAppData()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var parent = Directory.GetParent(
            Path.GetFullPath(root))!.FullName;

        Assert.Throws<InvalidOperationException>(
            () => InstallerInstallScopePolicy.ValidatePerUserTarget(
                Path.Combine(parent, "OutsideAppData", "App")));
    }

    [Fact]
    public void CustomPerUserTarget_RejectsTraversalOutsideAppData()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        var traversal = Path.Combine(root, "..", "NotAppData", "App");

        Assert.Throws<InvalidOperationException>(
            () => InstallerInstallScopePolicy.ValidatePerUserTarget(traversal));
    }

    [Fact]
    public void UnknownScope_FailsClosed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InstallerInstallScopePolicy.ValidateCurrentRuntime(
                (InstallerInstallScope)99));
    }
}
