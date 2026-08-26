using ColorVision.ServiceHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class ServiceHostStatusTests
    {
        private const string InstalledPath = @"C:\ProgramData\ColorVision\ServiceHost\ColorVisionServiceHost.exe";

        [Fact]
        public void SupportedRunningServiceCanSelfUpdate()
        {
            ServiceHostStatus status = CreateStatus(new Version(1, 4, 10, 5));

            Assert.True(status.NeedsUpdate);
            Assert.True(status.CanSelfUpdate);
        }

        [Fact]
        public void SameVersionDifferentPackageCanSelfUpdateWithoutElevation()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Running,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                InstalledExecutablePath = InstalledPath,
                PackageVersion = new Version(1, 4, 12, 37),
                InstalledVersion = new Version(1, 4, 12, 37),
                RunningVersion = new Version(1, 4, 12, 37),
                RunningProcessPath = InstalledPath,
                PackageSha256 = new string('a', 64),
                InstalledSha256 = new string('b', 64),
            };

            Assert.True(status.HasPackageContentMismatch);
            Assert.True(status.NeedsUpdate);
            Assert.False(status.HasCurrentOrNewerInstalledVersion);
            Assert.True(status.CanSelfUpdate);
            Assert.Equal(ServiceHostStartupAction.SelfUpdate, ServiceHostStartupUpdateChecker.ResolveAction(status));
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
        public void StoppedCurrentServiceStartsBeforeRepair()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Stopped,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                InstalledExecutablePath = InstalledPath,
                PackageVersion = new Version(1, 4, 10, 7),
                InstalledVersion = new Version(1, 4, 10, 7),
            };

            Assert.False(status.NeedsUpdate);
            Assert.True(status.NeedsRepair);
            Assert.Equal(ServiceHostStartupAction.Start, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void RunningServiceIsReadyOnlyWithPipeVersionAndInstalledPath()
        {
            ServiceHostStatus ready = CreateStatus(new Version(1, 4, 10, 7));
            ServiceHostStatus missingVersion = CopyWith(ready, null, ready.RunningProcessPath);
            ServiceHostStatus wrongPath = CopyWith(ready, ready.RunningVersion, @"C:\Temp\ColorVisionServiceHost.exe");

            Assert.True(ready.IsReady);
            Assert.True(ColorVisionServiceHostManager.IsReadyForPackagedVersion(ready));
            Assert.Equal(ServiceHostStartupAction.None, ServiceHostStartupUpdateChecker.ResolveAction(ready));
            Assert.False(missingVersion.IsReady);
            Assert.Equal(ServiceHostStartupAction.InstallOrRepair, ServiceHostStartupUpdateChecker.ResolveAction(missingVersion));
            Assert.False(wrongPath.IsReady);
            Assert.True(wrongPath.NeedsRepair);
            Assert.Equal(ServiceHostStartupAction.InstallOrRepair, ServiceHostStartupUpdateChecker.ResolveAction(wrongPath));
        }

        [Fact]
        public void ReadyPathComparisonNormalizesQuotesSegmentsAndCase()
        {
            ServiceHostStatus status = CopyWith(
                CreateStatus(new Version(1, 4, 10, 7)),
                new Version(1, 4, 10, 7),
                @"""c:\PROGRAMDATA\ColorVision\ServiceHost\..\ServiceHost\ColorVisionServiceHost.exe""");

            Assert.True(status.HasExpectedRunningPath);
            Assert.True(status.IsReady);
        }

        [Fact]
        public void NewerStoppedInstallIsStartedWithoutPackageDowngrade()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Stopped,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                InstalledExecutablePath = InstalledPath,
                PackageVersion = new Version(1, 4, 10, 7),
                InstalledVersion = new Version(1, 4, 10, 8),
            };

            Assert.True(status.HasCurrentOrNewerInstalledVersion);
            Assert.True(status.WouldInstallDowngrade);
            Assert.Equal(ServiceHostStartupAction.Start, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void WrongPathNewerRunningServiceIsNotRepairedFromOlderPackage()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Running,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                InstalledExecutablePath = InstalledPath,
                PackageVersion = new Version(1, 4, 10, 7),
                InstalledVersion = new Version(1, 4, 10, 8),
                RunningVersion = new Version(1, 4, 10, 8),
                RunningProcessPath = @"D:\Other\ColorVisionServiceHost.exe",
            };

            Assert.False(status.IsReady);
            Assert.True(status.WouldInstallDowngrade);
            Assert.Equal(ServiceHostStartupAction.None, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void OlderRunningProcessDoesNotSelfUpdateOverNewerInstalledExecutable()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Running,
                PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
                InstalledExecutablePath = InstalledPath,
                PackageVersion = new Version(1, 4, 10, 7),
                InstalledVersion = new Version(1, 4, 10, 8),
                RunningVersion = new Version(1, 4, 10, 5),
                RunningProcessPath = InstalledPath,
            };

            Assert.True(status.NeedsUpdate);
            Assert.True(status.WouldInstallDowngrade);
            Assert.Equal(ServiceHostStartupAction.None, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void OperationalOldServiceStillRequiresPackagedVersionUpdate()
        {
            ServiceHostStatus status = CreateStatus(new Version(1, 4, 10, 5));

            Assert.True(status.IsReady);
            Assert.False(ColorVisionServiceHostManager.IsReadyForPackagedVersion(status));
            Assert.Equal(ServiceHostStartupAction.SelfUpdate, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void HealthyInstalledServiceDoesNotRequirePackageToRemainReady()
        {
            ServiceHostStatus status = new()
            {
                State = ServiceHostInstallState.Running,
                PackageExecutablePath = @"Z:\missing\ColorVisionServiceHost.exe",
                InstalledExecutablePath = InstalledPath,
                InstalledVersion = new Version(1, 4, 10, 8),
                RunningVersion = new Version(1, 4, 10, 8),
                RunningProcessPath = InstalledPath,
            };

            Assert.False(status.IsPackageAvailable);
            Assert.True(status.IsReady);
            Assert.True(ColorVisionServiceHostManager.IsReadyForPackagedVersion(status));
            Assert.Equal(ServiceHostStartupAction.None, ServiceHostStartupUpdateChecker.ResolveAction(status));
        }

        [Fact]
        public void MissingInstalledRuntimeFileRequiresRepair()
        {
            string root = Path.Combine(Path.GetTempPath(), $"ColorVision-ServiceHostIntegrity-{Guid.NewGuid():N}");
            string packageDirectory = Path.Combine(root, "package");
            string installedDirectory = Path.Combine(root, "installed");
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(installedDirectory);
            try
            {
                WriteRuntimeFixture(packageDirectory);
                CopyDirectory(packageDirectory, installedDirectory);
                File.Delete(Path.Combine(installedDirectory, "System.Management.dll"));

                ServiceHostRuntimeIntegrity integrity = ServiceHostRuntimeIntegrityChecker.Inspect(packageDirectory, installedDirectory);
                ServiceHostStatus status = new()
                {
                    State = ServiceHostInstallState.Running,
                    PackageExecutablePath = Path.Combine(packageDirectory, "ColorVisionServiceHost.exe"),
                    InstalledExecutablePath = Path.Combine(installedDirectory, "ColorVisionServiceHost.exe"),
                    PackageVersion = new Version(1, 4, 13, 13),
                    InstalledVersion = new Version(1, 4, 13, 13),
                    RunningVersion = new Version(1, 4, 13, 13),
                    RunningProcessPath = Path.Combine(installedDirectory, "ColorVisionServiceHost.exe"),
                    RuntimeIntegrity = integrity,
                };

                Assert.True(integrity.IsPackageComplete);
                Assert.False(integrity.IsInstalledComplete);
                Assert.Contains("System.Management.dll", integrity.MissingInstalledFiles);
                Assert.True(status.HasIncompleteInstalledRuntime);
                Assert.True(status.NeedsRepair);
                Assert.False(status.IsReady);
                Assert.Equal(ServiceHostStartupAction.SelfUpdate, ServiceHostStartupUpdateChecker.ResolveAction(status));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void MissingPackageDependencyIsReportedBeforeInstall()
        {
            string root = Path.Combine(Path.GetTempPath(), $"ColorVision-ServiceHostPackage-{Guid.NewGuid():N}");
            string packageDirectory = Path.Combine(root, "package");
            string installedDirectory = Path.Combine(root, "installed");
            Directory.CreateDirectory(packageDirectory);
            Directory.CreateDirectory(installedDirectory);
            try
            {
                WriteRuntimeFixture(packageDirectory);
                File.Delete(Path.Combine(packageDirectory, "System.Management.dll"));

                ServiceHostRuntimeIntegrity integrity = ServiceHostRuntimeIntegrityChecker.Inspect(packageDirectory, installedDirectory);

                Assert.False(integrity.IsPackageComplete);
                Assert.Contains("System.Management.dll", integrity.MissingPackageFiles);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static ServiceHostStatus CreateStatus(Version? runningVersion) => new()
        {
            State = ServiceHostInstallState.Running,
            PackageExecutablePath = Environment.ProcessPath ?? typeof(ServiceHostStatusTests).Assembly.Location,
            InstalledExecutablePath = InstalledPath,
            PackageVersion = new Version(1, 4, 10, 7),
            InstalledVersion = runningVersion ?? new Version(1, 4, 10, 4),
            RunningVersion = runningVersion,
            RunningProcessPath = InstalledPath,
        };

        private static ServiceHostStatus CopyWith(
            ServiceHostStatus source,
            Version? runningVersion,
            string runningProcessPath)
        {
            return new ServiceHostStatus
            {
                State = source.State,
                RawOutput = source.RawOutput,
                PackageExecutablePath = source.PackageExecutablePath,
                InstalledExecutablePath = source.InstalledExecutablePath,
                PackageVersion = source.PackageVersion,
                InstalledVersion = source.InstalledVersion,
                RunningVersion = runningVersion,
                RunningProcessPath = runningProcessPath,
                PackageSha256 = source.PackageSha256,
                InstalledSha256 = source.InstalledSha256,
                RuntimeIntegrity = source.RuntimeIntegrity,
            };
        }

        private static void WriteRuntimeFixture(string directory)
        {
            File.WriteAllText(Path.Combine(directory, "ColorVisionServiceHost.exe"), "host-exe");
            File.WriteAllText(Path.Combine(directory, "ColorVisionServiceHost.dll"), "host-dll");
            File.WriteAllText(Path.Combine(directory, "ColorVisionServiceHost.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(directory, "System.Management.dll"), "management");
            File.WriteAllText(
                Path.Combine(directory, "ColorVisionServiceHost.deps.json"),
                JsonSerializer.Serialize(new
                {
                    targets = new Dictionary<string, object>
                    {
                        [".NETCoreApp,Version=v10.0"] = new Dictionary<string, object>
                        {
                            ["ColorVisionServiceHost/1.0.0"] = new
                            {
                                runtime = new Dictionary<string, object>
                                {
                                    ["ColorVisionServiceHost.dll"] = new { },
                                },
                            },
                            ["System.Management/10.0.0"] = new
                            {
                                runtime = new Dictionary<string, object>
                                {
                                    ["lib/net10.0/System.Management.dll"] = new { },
                                },
                            },
                        },
                    },
                }));
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            foreach (string sourceFile in Directory.EnumerateFiles(sourceDirectory))
                File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)));
        }
    }
}
