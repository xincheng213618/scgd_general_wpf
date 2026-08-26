using Microsoft.Win32;

namespace ColorVision.UI.Tests;

public sealed class StartupRegistryCheckerTests : IDisposable
{
    private readonly string _registryPath = $@"Software\ColorVision\Tests\StartupRecovery-{Guid.NewGuid():N}";

    [Fact]
    public void FastExitBeforeFirstRenderBecomesARecoveryAttempt()
    {
        const string installationKey = "FastExitInstallation";
        using (RegistryKey root = Registry.CurrentUser.CreateSubKey(_registryPath))
        using (RegistryKey attempt = root.CreateSubKey($@"StartupAttempts\{installationKey}\attempt-1"))
        {
            attempt.SetValue("ProcessId", int.MaxValue, RegistryValueKind.DWord);
            attempt.SetValue("Version", "1.4.13.12", RegistryValueKind.String);
            attempt.SetValue("Stage", "LoadingPlugin", RegistryValueKind.String);
            attempt.SetValue("Component", "camera.plugin", RegistryValueKind.String);
            attempt.SetValue("StartedAt", DateTimeOffset.UtcNow.ToString("O"), RegistryValueKind.String);
        }

        using RegistryKey testRoot = Registry.CurrentUser.OpenSubKey(_registryPath, writable: true)!;
        StartupFailureInfo failure = Assert.Single(
            StartupRegistryChecker.ReadAndRemoveIncompleteAttempts(testRoot, installationKey));

        Assert.Equal("1.4.13.12", failure.Version);
        Assert.Equal("LoadingPlugin", failure.Stage);
        Assert.Equal("camera.plugin", failure.Component);
        Assert.Null(testRoot.OpenSubKey($@"StartupAttempts\{installationKey}\attempt-1"));
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_registryPath, throwOnMissingSubKey: false);
    }
}
