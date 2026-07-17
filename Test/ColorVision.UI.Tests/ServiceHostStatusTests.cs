using ColorVision.ServiceHost;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostStatusTests
    {
        [Fact]
        public void SupportedRunningServiceCanSelfUpdate()
        {
            ServiceHostStatus status = CreateStatus(new Version(1, 4, 10, 5));

            Assert.True(status.NeedsUpdate);
            Assert.True(status.CanSelfUpdate);
        }

        [Fact]
        public void UnknownRunningVersionCannotSelfUpdate()
        {
            ServiceHostStatus status = CreateStatus(null);

            Assert.True(status.NeedsUpdate);
            Assert.False(status.CanSelfUpdate);
        }

        [Fact]
        public void InstallScriptKeepsInheritedPermissionsAndWritesDiagnosticLog()
        {
            string script = ColorVisionServiceHostManager.CreateInstallScript();

            Assert.DoesNotContain("icacls", script, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("inheritance:r", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("install.log", script, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Copy-Item", script, StringComparison.Ordinal);
            Assert.Contains("Service host restarted after failed installation.", script, StringComparison.Ordinal);
        }

        [Fact]
        public void StoppedCurrentServiceNeedsRepair()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Stopped,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                PackageVersion = new Version(1, 4, 10, 7),
                InstalledVersion = new Version(1, 4, 10, 7),
            };

            Assert.False(status.NeedsUpdate);
            Assert.True(status.NeedsRepair);
            Assert.Equal(ServiceHostStartupAction.InstallOrRepair, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        private static ServiceHostStatus CreateStatus(Version? runningVersion) => new()
        {
            State = ServiceHostInstallState.Running,
            PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
            PackageVersion = new Version(1, 4, 10, 7),
            InstalledVersion = runningVersion ?? new Version(1, 4, 10, 4),
            RunningVersion = runningVersion,
        };
    }
}
