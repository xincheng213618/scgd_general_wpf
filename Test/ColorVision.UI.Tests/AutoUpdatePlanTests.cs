using ColorVision.Update;
using System.IO;
using System.IO.Compression;
using Resources = ColorVision.Properties.Resources;

namespace ColorVision.UI.Tests
{
    public sealed class AutoUpdatePlanTests
    {
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
                Assert.True(AutoUpdater.DeleteInvalidIncrementalPackageFile(packagePath));
                Assert.False(File.Exists(packagePath));

                using (ZipArchive archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
                {
                    using Stream stream = archive.CreateEntry("ColorVision.exe").Open();
                    stream.WriteByte(1);
                }

                Assert.True(AutoUpdater.IsIncrementalPackageFileReady(packagePath));
                Assert.False(AutoUpdater.DeleteInvalidIncrementalPackageFile(packagePath));

                File.WriteAllText(packagePath + ".aria2", string.Empty);
                Assert.False(AutoUpdater.IsIncrementalPackageFileReady(packagePath));
                Assert.False(AutoUpdater.DeleteInvalidIncrementalPackageFile(packagePath));
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
                Assert.True(AutoUpdater.DeleteInvalidFullInstallerFile(installerPath));
                Assert.False(File.Exists(installerPath));

                byte[] portableExecutable = new byte[68];
                portableExecutable[0] = (byte)'M';
                portableExecutable[1] = (byte)'Z';
                BitConverter.GetBytes(64).CopyTo(portableExecutable, 0x3C);
                portableExecutable[64] = (byte)'P';
                portableExecutable[65] = (byte)'E';
                File.WriteAllBytes(installerPath, portableExecutable);

                Assert.True(AutoUpdater.IsFullInstallerFileReady(installerPath));
                Assert.False(AutoUpdater.DeleteInvalidFullInstallerFile(installerPath));

                File.WriteAllText(installerPath + ".aria2", string.Empty);
                Assert.False(AutoUpdater.IsFullInstallerFileReady(installerPath));
                Assert.False(AutoUpdater.DeleteInvalidFullInstallerFile(installerPath));
                Assert.True(File.Exists(installerPath));
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
