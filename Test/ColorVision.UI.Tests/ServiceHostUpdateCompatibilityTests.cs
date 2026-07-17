using ColorVision.ServiceHost;

namespace ColorVision.UI.Tests;

public sealed class ServiceHostUpdateCompatibilityTests
{
    private const string InstalledPath = @"C:\ProgramData\ColorVision\ServiceHost\ColorVisionServiceHost.exe";

    [Fact]
    public void LegacyRunningVersionRequiresElevatedInstall()
    {
        ServiceHostStatus status = CreateStatus(new Version(1, 4, 10, 4));

        Assert.True(status.NeedsUpdate);
        Assert.False(status.CanSelfUpdate);
    }

    [Fact]
    public void SupportedRunningVersionCanUseSilentSelfUpdate()
    {
        ServiceHostStatus status = CreateStatus(new Version(1, 4, 10, 5));

        Assert.True(status.NeedsUpdate);
        Assert.True(status.CanSelfUpdate);
    }

    [Fact]
    public void UnknownRunningVersionRequiresElevatedInstall()
    {
        ServiceHostStatus status = CreateStatus(null);

        Assert.True(status.NeedsUpdate);
        Assert.False(status.CanSelfUpdate);
    }

    private static ServiceHostStatus CreateStatus(Version? runningVersion) => new()
    {
        State = ServiceHostInstallState.Running,
        PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostUpdateCompatibilityTests).Assembly.Location,
        InstalledExecutablePath = InstalledPath,
        PackageVersion = new Version(1, 4, 10, 7),
        InstalledVersion = runningVersion ?? new Version(1, 4, 10, 4),
        RunningVersion = runningVersion,
        RunningProcessPath = InstalledPath,
    };
}
