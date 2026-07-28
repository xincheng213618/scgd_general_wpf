using ColorVision.Update;
using Newtonsoft.Json;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using Resources = ColorVision.Properties.Resources;

namespace ColorVision.UI.Tests
{
    public sealed class AutoUpdatePlanTests
    {
        [Fact]
        public void LegacySkippedVersionConfigIsIgnored()
        {
            AutoUpdateConfig? config = JsonConvert.DeserializeObject<AutoUpdateConfig>("""{"IsAutoUpdate":false,"SkippedVersion":"1.4.10.85"}""");

            Assert.NotNull(config);
            Assert.False(config.IsAutoUpdate);
            Assert.Null(typeof(AutoUpdateConfig).GetProperty("SkippedVersion"));
        }

        [Fact]
        public void UpdateSnapshotOptionDefaultsToDisabled()
        {
            ApplicationSnapshotConfig config = new();

            Assert.False(config.CreateSnapshotBeforeUpdate);
        }

        [Fact]
        public void UpdateNetworkOptionDefaultsToDirectConnection()
        {
            UpdateNetworkConfig config = new();

            Assert.True(config.DisableSystemProxyForUpdates);
        }

        [Fact]
        public void LatestVersionChecksReuseOnlyTheSameInFlightRequest()
        {
            LatestVersionCheckRequestCache requests = new();
            TaskCompletionSource<LatestVersionCheckResult> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            LatestVersionCheckResult result = new(new Version(1, 4, 10, 130), UpdateServerCheckStatus.Success);
            int requestCount = 0;
            Func<Task<LatestVersionCheckResult>> requestFactory = () =>
            {
                requestCount++;
                return requestCount == 1 ? completion.Task : Task.FromResult(result);
            };

            Task<LatestVersionCheckResult> first = requests.GetOrCreate(
                "https://updates.example.test/api/app/latest-version",
                requestFactory,
                out bool firstReused);
            Task<LatestVersionCheckResult> second = requests.GetOrCreate(
                "https://updates.example.test/api/app/latest-version",
                requestFactory,
                out bool secondReused);

            Assert.False(firstReused);
            Assert.True(secondReused);
            Assert.Same(first, second);
            Assert.Equal(1, requestCount);

            completion.SetResult(result);
            Task<LatestVersionCheckResult> third = requests.GetOrCreate(
                "https://updates.example.test/api/app/latest-version",
                requestFactory,
                out bool thirdReused);

            Assert.False(thirdReused);
            Assert.NotSame(first, third);
            Assert.Equal(2, requestCount);
        }

        [Fact]
        public void OfflineInstallerCommandDownloadsTheLatestFullInstallerToTheDesktop()
        {
            string command = AutoUpdater.BuildOfflineInstallerDownloadPowerShellCommand(
                "https://updates.example.test/",
                "user:password");

            Assert.Contains("https://updates.example.test/api/app/latest-version", command);
            Assert.Contains("$downloadUrl = 'https://updates.example.test/api/app/releases/{0}/download' -f $latest", command);
            Assert.Contains("Invoke-WebRequest -Uri $downloadUrl", command);
            Assert.DoesNotContain("-Uri 'https://updates.example.test/api/app/releases/$latest/download'", command);
            Assert.Contains("[Environment]::GetFolderPath('Desktop')", command);
            Assert.Contains("ColorVision-{0}.exe", command);
            Assert.Contains("Basic dXNlcjpwYXNzd29yZA==", command);
            Assert.DoesNotContain(".cvx", command, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OfflineInstallerCommandFileBypassesScriptPolicyAndPreservesTheCommand()
        {
            const string command = "$value = 'ColorVision'; Write-Host $value";

            string commandFile = AutoUpdater.BuildOfflineInstallerDownloadCommandFileContent(command);
            string encodedCommand = commandFile
                .Split("-EncodedCommand ", StringSplitOptions.None)[1]
                .Split('\r', '\n')[0]
                .Trim();

            Assert.Contains("-ExecutionPolicy Bypass", commandFile);
            Assert.Equal(command, System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(encodedCommand)));
        }

        [Fact]
        public void PreviewStateTransitionPreservesUpdateOptionsMadeWhileChecking()
        {
            UpdatePreviewDialogContext checkingContext = new()
            {
                CreateSnapshotBeforeUpdate = true,
                DisableSystemProxyForUpdates = true,
            };
            UpdatePreviewDialogContext loadedContext = new()
            {
                CreateSnapshotBeforeUpdate = false,
                DisableSystemProxyForUpdates = false,
            };

            checkingContext.CopyFrom(loadedContext);

            Assert.True(checkingContext.CreateSnapshotBeforeUpdate);
            Assert.True(checkingContext.DisableSystemProxyForUpdates);
        }

        [Fact]
        public void SameSeriesBuildJumpUsesOrderedIncrementalPackages()
        {
            AutoUpdatePlan? plan = AutoUpdater.BuildUpdatePlan(new Version(1, 4, 7, 11), new Version(1, 4, 9, 14));

            Assert.NotNull(plan);
            Assert.True(plan.IsIncremental);
            Assert.Equal(
                new[] { new Version(1, 4, 8, 1), new Version(1, 4, 9, 1), new Version(1, 4, 9, 14) },
                plan.VersionsToApply);
        }

        [Fact]
        public void VeryOldSameSeriesVersionUsesEveryRequiredCheckpoint()
        {
            AutoUpdatePlan? plan = AutoUpdater.BuildUpdatePlan(new Version(1, 4, 1, 1), new Version(1, 4, 10, 82));

            Assert.NotNull(plan);
            Assert.True(plan.IsIncremental);
            Assert.Equal(
                new[]
                {
                    new Version(1, 4, 2, 1),
                    new Version(1, 4, 3, 1),
                    new Version(1, 4, 4, 1),
                    new Version(1, 4, 5, 1),
                    new Version(1, 4, 6, 1),
                    new Version(1, 4, 7, 1),
                    new Version(1, 4, 8, 1),
                    new Version(1, 4, 9, 1),
                    new Version(1, 4, 10, 1),
                    new Version(1, 4, 10, 82),
                },
                plan.VersionsToApply);
        }

        [Fact]
        public void DifferentMinorUsesFullPackage()
        {
            AutoUpdatePlan? plan = AutoUpdater.BuildUpdatePlan(new Version(1, 4, 7, 11), new Version(1, 5, 4, 5));

            Assert.NotNull(plan);
            Assert.False(plan.IsIncremental);
            Assert.Equal(new[] { new Version(1, 5, 4, 5) }, plan.VersionsToApply);
        }

        [Fact]
        public void DifferentMajorWithSameMinorUsesFullPackage()
        {
            AutoUpdatePlan? plan = AutoUpdater.BuildUpdatePlan(new Version(1, 4, 7, 11), new Version(2, 4, 1, 1));

            Assert.NotNull(plan);
            Assert.False(plan.IsIncremental);
            Assert.Equal(new[] { new Version(2, 4, 1, 1) }, plan.VersionsToApply);
        }

        [Fact]
        public void RevisionOnlyUpdateUsesTargetIncrementalPackage()
        {
            AutoUpdatePlan? plan = AutoUpdater.BuildUpdatePlan(new Version(1, 4, 9, 1), new Version(1, 4, 9, 14));

            Assert.NotNull(plan);
            Assert.True(plan.IsIncremental);
            Assert.Equal(new[] { new Version(1, 4, 9, 14) }, plan.VersionsToApply);
        }

        [Theory]
        [InlineData("1.4.9.14", "1.4.9.14")]
        [InlineData("1.4.9.14", "1.4.9.13")]
        public void NonNewerVersionHasNoUpdatePlan(string current, string latest)
        {
            Assert.Null(AutoUpdater.BuildUpdatePlan(new Version(current), new Version(latest)));
        }

        [Fact]
        public void ApplicationPackageNamesKeepTheirExecutableAndArchiveExtensions()
        {
            Version version = new(1, 4, 10, 82);

            Assert.Equal("ColorVision-1.4.10.82.exe", AutoUpdater.GetReleasePackageFileName(version));
            Assert.Equal("ColorVision-Update-[1.4.10.82].cvx", AutoUpdater.GetIncrementalPackageFileName(version));
        }

        [Fact]
        public void LegacyZipNamedUpdatePackageIsNotHandledAsApplicationUpdate()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionLegacyUpdatePackageTest-").FullName;
            string packagePath = Path.Combine(tempDirectory, "ColorVision-Update-[1.4.10.82].zip");

            try
            {
                using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    using Stream stream = archive.CreateEntry("ColorVision.exe").Open();
                    stream.WriteByte(1);
                }

                Assert.False(new ZipPluginPackageFileProcessor().OpenFile(packagePath).Handled);
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void MissingIncrementalPackageRequiresFullInstallerFallback()
        {
            Assert.True(AutoUpdater.RequiresFullInstallerFallback(expectedPackageCount: 3, availablePackageCount: 2));
            Assert.False(AutoUpdater.RequiresFullInstallerFallback(expectedPackageCount: 3, availablePackageCount: 3));
        }

        [Fact]
        public void IncrementalPackageCacheRequiresACompleteReadableArchive()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionUpdatePackageTest-").FullName;
            string packagePath = Path.Combine(tempDirectory, "ColorVision-Update-[1.4.10.82].cvx");

            try
            {
                File.WriteAllText(packagePath, "not a zip archive");
                Assert.False(AutoUpdater.IsIncrementalPackageFileReady(packagePath));
                string? recoveryPath = AutoUpdater.MoveInvalidApplicationPackageToRecovery(packagePath, isIncremental: true);
                Assert.False(File.Exists(packagePath));
                Assert.NotNull(recoveryPath);
                Assert.True(File.Exists(recoveryPath));
                Assert.Equal("not a zip archive", File.ReadAllText(recoveryPath));

                using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    using Stream stream = archive.CreateEntry("ColorVision.exe").Open();
                    stream.WriteByte(1);
                }

                Assert.True(AutoUpdater.IsIncrementalPackageFileReady(packagePath));
                Assert.Null(AutoUpdater.MoveInvalidApplicationPackageToRecovery(packagePath, isIncremental: true));

                string legacyZipPath = Path.ChangeExtension(packagePath, ".zip");
                File.Copy(packagePath, legacyZipPath);
                Assert.False(AutoUpdater.IsIncrementalPackageFileReady(legacyZipPath));

                File.WriteAllText(packagePath + ".aria2", string.Empty);
                Assert.False(AutoUpdater.IsIncrementalPackageFileReady(packagePath));
                Assert.Null(AutoUpdater.MoveInvalidApplicationPackageToRecovery(packagePath, isIncremental: true));
                Assert.True(File.Exists(packagePath));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void FullInstallerCacheRequiresACompletePortableExecutable()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionFullInstallerTest-").FullName;
            string installerPath = Path.Combine(tempDirectory, "ColorVision-1.4.10.83.exe");

            try
            {
                File.WriteAllText(installerPath, "not an executable");
                Assert.False(AutoUpdater.IsFullInstallerFileReady(installerPath));
                Assert.False(AutoUpdater.IsApplicationPackageFileReady(installerPath, isIncremental: false));
                string? recoveryPath = AutoUpdater.MoveInvalidApplicationPackageToRecovery(installerPath, isIncremental: false);
                Assert.False(File.Exists(installerPath));
                Assert.NotNull(recoveryPath);
                Assert.True(File.Exists(recoveryPath));
                Assert.Equal("not an executable", File.ReadAllText(recoveryPath));

                byte[] portableExecutable = new byte[68];
                portableExecutable[0] = (byte)'M';
                portableExecutable[1] = (byte)'Z';
                BitConverter.GetBytes(64).CopyTo(portableExecutable, 0x3C);
                portableExecutable[64] = (byte)'P';
                portableExecutable[65] = (byte)'E';
                File.WriteAllBytes(installerPath, portableExecutable);

                Assert.True(AutoUpdater.IsFullInstallerFileReady(installerPath));
                Assert.Null(AutoUpdater.MoveInvalidApplicationPackageToRecovery(installerPath, isIncremental: false));

                File.WriteAllText(installerPath + ".aria2", string.Empty);
                Assert.False(AutoUpdater.IsFullInstallerFileReady(installerPath));
                Assert.Null(AutoUpdater.MoveInvalidApplicationPackageToRecovery(installerPath, isIncremental: false));
                Assert.True(File.Exists(installerPath));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void ApplicationPackageCacheRejectsAValidPackageWithTheWrongTargetVersion()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionPackageVersionTest-").FullName;
            string sourceExecutable = typeof(AutoUpdater).Assembly.Location;
            string? fileVersion = FileVersionInfo.GetVersionInfo(sourceExecutable).FileVersion;
            Assert.True(Version.TryParse(fileVersion, out Version? expectedVersion));
            Version wrongVersion = new(expectedVersion.Major, expectedVersion.Minor, expectedVersion.Build, expectedVersion.Revision + 1);
            string packagePath = Path.Combine(tempDirectory, $"ColorVision-Update-[{expectedVersion}].cvx");
            string installerPath = Path.Combine(tempDirectory, $"ColorVision-{expectedVersion}.exe");

            try
            {
                using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("ColorVision.exe");
                    using Stream source = File.OpenRead(sourceExecutable);
                    using Stream destination = entry.Open();
                    source.CopyTo(destination);
                }
                File.Copy(sourceExecutable, installerPath);

                Assert.True(AutoUpdater.IsApplicationPackageFileReady(packagePath, isIncremental: true, expectedVersion));
                Assert.False(AutoUpdater.IsApplicationPackageFileReady(packagePath, isIncremental: true, wrongVersion));
                Assert.True(AutoUpdater.IsApplicationPackageFileReady(installerPath, isIncremental: false, expectedVersion));
                Assert.False(AutoUpdater.IsApplicationPackageFileReady(installerPath, isIncremental: false, wrongVersion));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void IncrementalCacheAcceptsAriaUniqueNameWhenCanonicalDownloadIsIncomplete()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionUniqueIncrementalCacheTest-").FullName;
            string canonicalFileName = "ColorVision-Update-[1.4.10.84].cvx";
            string canonicalPath = Path.Combine(tempDirectory, canonicalFileName);
            string uniquePath = Path.Combine(tempDirectory, "ColorVision-Update-[1.4.10.84](1).cvx");

            try
            {
                File.WriteAllText(canonicalPath, "partial");
                File.WriteAllText(canonicalPath + ".aria2", string.Empty);
                WriteValidIncrementalPackage(uniquePath);

                Assert.Equal(uniquePath, AutoUpdater.FindReadyApplicationPackagePath(tempDirectory, canonicalFileName, isIncremental: true));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Fact]
        public void FullInstallerCacheDoesNotConfuseSimilarVersionWithUniqueName()
        {
            string tempDirectory = Directory.CreateTempSubdirectory("ColorVisionUniqueFullCacheTest-").FullName;
            string canonicalFileName = "ColorVision-1.4.1.1.exe";
            string otherVersionPath = Path.Combine(tempDirectory, "ColorVision-1.4.1.10(1).exe");
            string uniquePath = Path.Combine(tempDirectory, "ColorVision-1.4.1.1(2).exe");

            try
            {
                WriteValidPortableExecutable(otherVersionPath);
                Assert.Null(AutoUpdater.FindReadyApplicationPackagePath(tempDirectory, canonicalFileName, isIncremental: false));

                WriteValidPortableExecutable(uniquePath);
                Assert.Equal(uniquePath, AutoUpdater.FindReadyApplicationPackagePath(tempDirectory, canonicalFileName, isIncremental: false));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Theory]
        [InlineData(3, 2, 2, 2, (int)CombinedIncrementalCompletionAction.DownloadFullInstaller)]
        [InlineData(3, 3, 2, 1, (int)CombinedIncrementalCompletionAction.ApplyApplicationOnly)]
        [InlineData(3, 3, 2, 2, (int)CombinedIncrementalCompletionAction.ApplyCombinedUpdate)]
        public void CombinedUpdateKeepsApplicationAndPluginFailuresIndependent(
            int expectedApplicationPackages,
            int availableApplicationPackages,
            int expectedPluginPackages,
            int availablePluginPackages,
            int expectedAction)
        {
            Assert.Equal(
                (CombinedIncrementalCompletionAction)expectedAction,
                CombinedUpdateCoordinator.DetermineCombinedIncrementalCompletionAction(
                    expectedApplicationPackages,
                    availableApplicationPackages,
                    expectedPluginPackages,
                    availablePluginPackages));
        }

        [Theory]
        [InlineData(true, false, true, true, true, (int)ExitUpdateContent.Plugins)]
        [InlineData(true, true, false, true, true, (int)ExitUpdateContent.Plugins)]
        [InlineData(true, true, true, true, false, (int)ExitUpdateContent.Application)]
        [InlineData(true, true, true, true, true, (int)(ExitUpdateContent.Application | ExitUpdateContent.Plugins))]
        [InlineData(true, false, true, false, false, (int)ExitUpdateContent.None)]
        public void ExitUpdateKeepsReadyApplicationAndPluginPackagesIndependent(
            bool hasApplicationUpdate,
            bool isIncrementalApplicationUpdate,
            bool applicationPackagesReady,
            bool hasPluginUpdates,
            bool pluginPackagesReady,
            int expectedContent)
        {
            Assert.Equal(
                (ExitUpdateContent)expectedContent,
                CombinedUpdateCoordinator.DetermineExitUpdateContent(
                    hasApplicationUpdate,
                    isIncrementalApplicationUpdate,
                    applicationPackagesReady,
                    hasPluginUpdates,
                    pluginPackagesReady));
        }

        [Fact]
        public void StartupPrefetchUsesCurrentHostForPluginsWhenApplicationRequiresFullInstaller()
        {
            Version currentVersion = new(1, 4, 10, 86);
            AutoUpdatePlan fullApplicationPlan = new()
            {
                CurrentVersion = currentVersion,
                LatestVersion = new Version(1, 5, 1, 1),
                VersionsToApply = [new Version(1, 5, 1, 1)],
                IsIncremental = false,
            };

            Assert.Equal(
                currentVersion,
                CombinedUpdateCoordinator.ResolvePluginPlanHostVersion(
                    fullApplicationPlan,
                    currentVersion,
                    includeCurrentHostPluginUpdatesWhenFullApplicationUpdate: true));
        }

        [Fact]
        public void InteractiveFullInstallerDoesNotOfferAPluginPlanItCannotApplyTogether()
        {
            Version currentVersion = new(1, 4, 10, 86);
            AutoUpdatePlan fullApplicationPlan = new()
            {
                CurrentVersion = currentVersion,
                LatestVersion = new Version(1, 5, 1, 1),
                VersionsToApply = [new Version(1, 5, 1, 1)],
                IsIncremental = false,
            };

            Assert.Null(
                CombinedUpdateCoordinator.ResolvePluginPlanHostVersion(
                    fullApplicationPlan,
                    currentVersion,
                    includeCurrentHostPluginUpdatesWhenFullApplicationUpdate: false));
        }

        [Fact]
        public void IncrementalApplicationPlanChecksPluginsAgainstItsTargetVersion()
        {
            AutoUpdatePlan incrementalApplicationPlan = new()
            {
                CurrentVersion = new Version(1, 4, 10, 85),
                LatestVersion = new Version(1, 4, 10, 86),
                VersionsToApply = [new Version(1, 4, 10, 86)],
                IsIncremental = true,
            };

            Assert.Equal(
                incrementalApplicationPlan.LatestVersion,
                CombinedUpdateCoordinator.ResolvePluginPlanHostVersion(
                    incrementalApplicationPlan,
                    incrementalApplicationPlan.CurrentVersion,
                    includeCurrentHostPluginUpdatesWhenFullApplicationUpdate: false));
        }

        [Theory]
        [InlineData(true, true, true, true, true, false, true)]
        [InlineData(true, true, false, true, true, true, false)]
        [InlineData(true, false, false, true, true, false, false)]
        [InlineData(true, true, true, false, true, false, false)]
        [InlineData(true, true, false, true, true, false, true)]
        public void UpdateCheckReuseRequiresTheSameScopeAndCompatiblePluginCoverage(
            bool existingApplication,
            bool existingPlugins,
            bool existingCurrentHostPlugins,
            bool requestedApplication,
            bool requestedPlugins,
            bool requestedCurrentHostPlugins,
            bool expected)
        {
            Assert.Equal(
                expected,
                CombinedUpdateCoordinator.CanReuseUpdateCheckOptions(
                    existingApplication,
                    existingPlugins,
                    existingCurrentHostPlugins,
                    requestedApplication,
                    requestedPlugins,
                    requestedCurrentHostPlugins));
        }

        [Fact]
        public void InteractiveUpdateCheckRetriesOnlyTransientServerFailures()
        {
            Assert.False(CombinedUpdateCoordinator.ShouldRetryInteractiveCheck(UpdateServerCheckStatus.Success));
            Assert.False(CombinedUpdateCoordinator.ShouldRetryInteractiveCheck(UpdateServerCheckStatus.NoInternetConnection));
            Assert.True(CombinedUpdateCoordinator.ShouldRetryInteractiveCheck(UpdateServerCheckStatus.ServerUnavailable));
        }

        [Fact]
        public void PluginOnlySelectionDescribesRestartWithoutBackup()
        {
            UpdatePreviewDialogContext context = new() { IsChecking = false };
            context.Items.Add(new UpdatePreviewItem
            {
                Kind = UpdatePreviewItemKind.Plugin,
                IsSelectable = true,
                IsSelected = true,
            });

            string[] segments = context.SelectionSummary.Split(" · ", StringSplitOptions.None);

            Assert.Equal(2, segments.Length);
            Assert.Equal(Resources.UpdatePreviewSelectionRestartRequired, segments[1]);
        }

        [Fact]
        public void PluginOnlySelectionDescribesOptionalUpdateSnapshot()
        {
            UpdatePreviewDialogContext context = new()
            {
                IsChecking = false,
                CreateSnapshotBeforeUpdate = true,
            };
            context.Items.Add(new UpdatePreviewItem
            {
                Kind = UpdatePreviewItemKind.Plugin,
                IsSelectable = true,
                IsSelected = true,
            });

            string[] segments = context.SelectionSummary.Split(" · ", StringSplitOptions.None);

            Assert.Equal(3, segments.Length);
            Assert.Equal(Resources.UpdatePreviewSelectionCreatesSnapshot, segments[1]);
            Assert.Equal(Resources.UpdatePreviewSelectionRestartRequired, segments[2]);
        }

        private static void WriteValidIncrementalPackage(string packagePath)
        {
            using ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);
            using Stream stream = archive.CreateEntry("ColorVision.exe").Open();
            stream.WriteByte(1);
        }

        private static void WriteValidPortableExecutable(string filePath)
        {
            byte[] portableExecutable = new byte[68];
            portableExecutable[0] = (byte)'M';
            portableExecutable[1] = (byte)'Z';
            BitConverter.GetBytes(64).CopyTo(portableExecutable, 0x3C);
            portableExecutable[64] = (byte)'P';
            portableExecutable[65] = (byte)'E';
            File.WriteAllBytes(filePath, portableExecutable);
        }
    }
}
