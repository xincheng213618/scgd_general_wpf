using ColorVision.Update;
using ColorVision.UI.Plugins;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace ColorVision.UI.Tests
{
    public sealed class PluginUpdaterBatchTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionPluginUpdaterTests-{Guid.NewGuid():N}");

        public PluginUpdaterBatchTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void GeneratedBatchUsesDirectOverlayCopyWithoutRecoveryState()
        {
            string batchFilePath = Path.Combine(_tempDirectory, "update.bat");
            string baseDirectory = Path.Combine(_tempDirectory, "ColorVision 100%! & Caret^");
            File.WriteAllText(batchFilePath, string.Empty);
            ExitUpdateHandoffState handoffState = ExitUpdateHandoff.Prepare(baseDirectory, _tempDirectory, Path.Combine(_tempDirectory, "State"));

            try
            {
                PluginUpdater.GenerateBatchFile(
                    batchFilePath,
                    baseDirectory,
                    "ColorVision.exe",
                    Environment.ProcessId,
                    handoffState,
                    restartArguments: null);
            }
            finally
            {
                ExitUpdateHandoff.Clear(handoffState);
            }

            string batch = File.ReadAllText(batchFilePath, Encoding.GetEncoding(936));

            Assert.Contains("robocopy \"%STAGE%\" \"%TARGET%\" *.* /E ", batch, StringComparison.Ordinal);
            Assert.Contains("setlocal DisableDelayedExpansion", batch, StringComparison.Ordinal);
            Assert.Contains(PluginUpdater.EscapeForBatchValue(baseDirectory), batch, StringComparison.Ordinal);
            Assert.Contains("^>nul ^& rd /s /q \"%UPDATE_ROOT%\" 2^>nul", batch, StringComparison.Ordinal);
            Assert.Equal(2, batch.Split("call :schedule_cleanup", StringSplitOptions.None).Length - 1);
            Assert.Contains("taskkill /f /pid \"%ORIGINAL_PID%\"", batch, StringComparison.Ordinal);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE_STATE", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(":rollback", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("enabledelayedexpansion", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/MIR", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ColorVisionBackup_", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ManifestUpdateBatchUsesDirectoryTransactionAndRollback()
        {
            string updateRoot = Path.Combine(_tempDirectory, "Manifest Update 100%! & Caret^");
            string batchFilePath = Path.Combine(updateRoot, "update.bat");
            string baseDirectory = Path.Combine(_tempDirectory, "ColorVision Target 100%! & Caret^");
            string stagedPluginDirectory = Path.Combine(updateRoot, "ColorVision", "Plugins", "third.party");
            Directory.CreateDirectory(stagedPluginDirectory);
            Directory.CreateDirectory(baseDirectory);
            File.WriteAllText(Path.Combine(stagedPluginDirectory, "manifest.json"), "{}");
            File.WriteAllText(batchFilePath, string.Empty);
            ExitUpdateHandoffState handoffState = ExitUpdateHandoff.Prepare(baseDirectory, updateRoot, Path.Combine(_tempDirectory, "ManifestState"));

            try
            {
                PluginUpdater.GenerateBatchFile(
                    batchFilePath,
                    baseDirectory,
                    "ColorVision.exe",
                    Environment.ProcessId,
                    handoffState,
                    restartArguments: null,
                    manifestPluginIds: ["third.party"],
                    legacyStageDirectory: null);
            }
            finally
            {
                ExitUpdateHandoff.Clear(handoffState);
            }

            string batch = File.ReadAllText(batchFilePath, Encoding.GetEncoding(936));
            Assert.Contains("Preparing 1 manifest plugin directory replacement(s).", batch, StringComparison.Ordinal);
            Assert.Contains("\\Plugins\\.ColorVisionUpdate-", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("move /y \"%PLUGIN_TARGET_0%\" \"%PLUGIN_ROLLBACK_0%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Plugin directory transaction failed; rolling back switched plugins.", batch, StringComparison.Ordinal);
            Assert.Contains("persistent recovery backup was preserved", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(PluginUpdater.EscapeForBatchValue(stagedPluginDirectory), batch, StringComparison.Ordinal);
            Assert.DoesNotContain("robocopy \"%STAGE%\" \"%TARGET%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/MIR", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void PluginDeletionUsesOriginalProcessHandoff()
        {
            string batchFilePath = Path.Combine(_tempDirectory, "delete.bat");
            string programDirectory = Path.Combine(_tempDirectory, "ColorVision");
            File.WriteAllText(Path.Combine(_tempDirectory, "update.bat"), string.Empty);
            ExitUpdateHandoffState handoffState = ExitUpdateHandoff.Prepare(programDirectory, _tempDirectory, Path.Combine(_tempDirectory, "DeleteState"));

            try
            {
                PluginUpdater.GenerateDeleteBatchFile(
                    batchFilePath,
                    [Path.Combine(programDirectory, "Plugins", "Pattern")],
                    Path.Combine(programDirectory, "ColorVision.exe"),
                    Environment.ProcessId,
                    handoffState);
            }
            finally
            {
                ExitUpdateHandoff.Clear(handoffState);
            }

            string batch = File.ReadAllText(batchFilePath, Encoding.GetEncoding(936));
            Assert.Contains("taskkill /f /pid \"%ORIGINAL_PID%\"", batch, StringComparison.Ordinal);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(ExitUpdateHandoff.LaunchTokenEnvironmentVariable, batch, StringComparison.Ordinal);
            Assert.Contains("if exist \"%TARGET%\" goto fail", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Plugin deletion failed.", batch, StringComparison.Ordinal);
            Assert.Contains("update.log", batch, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EscapedBatchValueSurvivesCmdParsing()
        {
            const string value = @"C:\ColorVision 100%! & Caret^";
            string probeBatchPath = Path.Combine(_tempDirectory, "probe.bat");
            File.WriteAllText(
                probeBatchPath,
                $"@echo off{Environment.NewLine}setlocal DisableDelayedExpansion{Environment.NewLine}set \"VALUE={PluginUpdater.EscapeForBatchValue(value)}\"{Environment.NewLine}set VALUE");

            ProcessStartInfo startInfo = new("cmd.exe")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(probeBatchPath);

            using Process process = Process.Start(startInfo)!;
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            Assert.Equal(0, process.ExitCode);
            Assert.Equal($"VALUE={value}", output.TrimEnd('\r', '\n'));
        }

        [Fact]
        public void PluginDeletionTargetMustBeDirectChildOfPluginsDirectory()
        {
            string pluginsDirectory = Path.Combine(_tempDirectory, "Plugins");

            Assert.True(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, "Pattern", out string validTarget));
            Assert.Equal(Path.Combine(pluginsDirectory, "Pattern"), validTarget);
            Assert.False(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, @"..\Other", out _));
            Assert.False(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, @"Group\Pattern", out _));
            Assert.False(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, _tempDirectory, out _));
            Assert.False(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, "Pattern.", out _));
            Assert.False(PluginUpdater.TryGetPluginTargetDirectory(pluginsDirectory, "Pattern ", out _));
        }

        [Fact]
        public void RootManifestPackageIsStagedUnderManifestId()
        {
            string packagePath = CreatePluginPackage("RootPackage", "third.party", wrapped: false);
            string stagingRoot = Path.Combine(_tempDirectory, "Stage", "Plugins");

            string? pluginId = PluginUpdater.StagePluginPackage(
                packagePath,
                stagingRoot,
                Path.Combine(_tempDirectory, "Extract"));

            Assert.Equal("third.party", pluginId);
            Assert.True(File.Exists(Path.Combine(stagingRoot, "third.party", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(stagingRoot, "third.party", "ThirdParty.dll")));
        }

        [Fact]
        public void WrappedPackageDirectoryIsNormalizedToManifestId()
        {
            string packagePath = CreatePluginPackage("DifferentAssemblyName", "third.party", wrapped: true);
            string stagingRoot = Path.Combine(_tempDirectory, "Stage", "Plugins");

            string? pluginId = PluginUpdater.StagePluginPackage(
                packagePath,
                stagingRoot,
                Path.Combine(_tempDirectory, "Extract"));

            Assert.Equal("third.party", pluginId);
            Assert.True(File.Exists(Path.Combine(stagingRoot, "third.party", "manifest.json")));
            Assert.False(Directory.Exists(Path.Combine(stagingRoot, "DifferentAssemblyName")));
        }

        [Fact]
        public void BatchStagingOverlaysPluginAlreadyStagedByApplicationPackage()
        {
            string packagePath = CreatePluginPackage("MarketplacePackage", "third.party", wrapped: false);
            string stagingRoot = Path.Combine(_tempDirectory, "Stage", "Plugins");
            string existingPluginDirectory = Path.Combine(stagingRoot, "third.party");
            Directory.CreateDirectory(existingPluginDirectory);
            File.WriteAllText(Path.Combine(existingPluginDirectory, "HostOnly.dll"), "host");
            File.WriteAllText(Path.Combine(existingPluginDirectory, "ThirdParty.dll"), "old");

            int stagedCount = PluginUpdater.StagePluginPackages(
                new[] { packagePath },
                stagingRoot,
                Path.Combine(_tempDirectory, "Extract"));

            Assert.Equal(1, stagedCount);
            Assert.Equal("host", File.ReadAllText(Path.Combine(existingPluginDirectory, "HostOnly.dll")));
            Assert.Equal("test", File.ReadAllText(Path.Combine(existingPluginDirectory, "ThirdParty.dll")));
            Assert.True(File.Exists(Path.Combine(existingPluginDirectory, "manifest.json")));
        }

        [Fact]
        public void BatchStagingRejectsDuplicatePluginIds()
        {
            string firstPackage = CreatePluginPackage("First", "third.party", wrapped: false);
            string secondPackage = CreatePluginPackage("Second", "third.party", wrapped: true);

            Assert.Throws<InvalidDataException>(() => PluginUpdater.StagePluginPackages(
                new[] { firstPackage, secondPackage },
                Path.Combine(_tempDirectory, "Stage", "Plugins"),
                Path.Combine(_tempDirectory, "Extract")));
        }

        [Fact]
        public void UpdateStagingSeparatesLegacyOverlayFromManifestDirectories()
        {
            string manifestPackage = CreatePluginPackage("Manifest", "third.party", wrapped: false);
            string legacySource = Path.Combine(_tempDirectory, "LegacySource");
            Directory.CreateDirectory(Path.Combine(legacySource, "LegacyPlugin"));
            File.WriteAllText(Path.Combine(legacySource, "LegacyPlugin", "Legacy.dll"), "legacy");
            string legacyPackage = Path.Combine(_tempDirectory, "Legacy.cvxp");
            ZipFile.CreateFromDirectory(legacySource, legacyPackage);

            string manifestStage = Path.Combine(_tempDirectory, "Stage", "ColorVision", "Plugins");
            string legacyStage = Path.Combine(_tempDirectory, "Stage", "LegacyOverlay");
            PluginPackageStagingPlan plan = PluginUpdater.StagePluginPackagesForUpdate(
                [manifestPackage, legacyPackage],
                manifestStage,
                legacyStage,
                Path.Combine(_tempDirectory, "Stage", "Extract"));

            Assert.Equal(["third.party"], plan.ManifestPluginIds);
            Assert.True(plan.HasLegacyPackages);
            Assert.True(File.Exists(Path.Combine(manifestStage, "third.party", "manifest.json")));
            Assert.False(Directory.Exists(Path.Combine(manifestStage, "LegacyPlugin")));
            Assert.True(File.Exists(Path.Combine(legacyStage, "LegacyPlugin", "Legacy.dll")));
        }

        [Fact]
        public void CombinedUpdateMovesOnlyMatchingManifestDirectoriesOutOfApplicationOverlay()
        {
            string pluginsStage = Path.Combine(_tempDirectory, "Combined", "ColorVision", "Plugins");
            string manifestDirectory = Path.Combine(pluginsStage, "third.party");
            string legacyDirectory = Path.Combine(pluginsStage, "LegacyPlugin");
            string transactionStage = Path.Combine(_tempDirectory, "Combined", "ManifestPlugins");
            Directory.CreateDirectory(manifestDirectory);
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(
                Path.Combine(manifestDirectory, "manifest.json"),
                "{\"id\":\"third.party\",\"name\":\"Third Party\",\"version\":\"2.0\"}");
            File.WriteAllText(Path.Combine(manifestDirectory, "HostOnly.dll"), "host");
            File.WriteAllText(Path.Combine(legacyDirectory, "Legacy.dll"), "legacy");

            IReadOnlyList<string> pluginIds = PluginUpdater.PrepareManifestPluginDirectoriesForTransaction(
                pluginsStage,
                transactionStage);

            Assert.Equal(["third.party"], pluginIds);
            Assert.False(Directory.Exists(manifestDirectory));
            Assert.True(File.Exists(Path.Combine(transactionStage, "third.party", "HostOnly.dll")));
            Assert.True(File.Exists(Path.Combine(legacyDirectory, "Legacy.dll")));
        }

        [Fact]
        public void CombinedUpdateRejectsManifestIdThatDoesNotMatchItsDirectory()
        {
            string pluginsStage = Path.Combine(_tempDirectory, "Mismatched", "ColorVision", "Plugins");
            string manifestDirectory = Path.Combine(pluginsStage, "folder.name");
            Directory.CreateDirectory(manifestDirectory);
            File.WriteAllText(Path.Combine(manifestDirectory, "manifest.json"), "{\"id\":\"other.name\"}");

            Assert.Throws<InvalidDataException>(() => PluginUpdater.PrepareManifestPluginDirectoriesForTransaction(
                pluginsStage,
                Path.Combine(_tempDirectory, "Mismatched", "ManifestPlugins")));
            Assert.True(Directory.Exists(manifestDirectory));
        }

        [Fact]
        public void CombinedIncrementalPluginDirectoryIsSeededFromInstalledContent()
        {
            string applicationPluginsStage = Path.Combine(_tempDirectory, "Delta", "ColorVision", "Plugins");
            string deltaDirectory = Path.Combine(applicationPluginsStage, "third.party");
            string installedPluginsRoot = Path.Combine(_tempDirectory, "DeltaInstall", "Plugins");
            string installedDirectory = Path.Combine(installedPluginsRoot, "third.party");
            string packageStage = Path.Combine(_tempDirectory, "Delta", "ManifestPluginPackages");
            string transactionStage = Path.Combine(_tempDirectory, "Delta", "ManifestPlugins");
            Directory.CreateDirectory(deltaDirectory);
            Directory.CreateDirectory(installedDirectory);
            File.WriteAllText(Path.Combine(installedDirectory, "manifest.json"), "{\"id\":\"third.party\",\"version\":\"1.0\"}");
            File.WriteAllText(Path.Combine(installedDirectory, "unchanged.dll"), "unchanged");
            File.WriteAllText(Path.Combine(installedDirectory, "changed.dll"), "old");
            // The main application delta need not contain manifest.json when it did not change.
            File.WriteAllText(Path.Combine(deltaDirectory, "changed.dll"), "new");

            IReadOnlyList<string> pluginIds = PluginUpdater.PrepareCombinedManifestPluginDirectoriesForTransaction(
                applicationPluginsStage,
                packageStage,
                installedPluginsRoot,
                transactionStage);

            Assert.Equal(["third.party"], pluginIds);
            Assert.False(Directory.Exists(deltaDirectory));
            Assert.Equal("new", File.ReadAllText(Path.Combine(transactionStage, "third.party", "changed.dll")));
            Assert.Equal("unchanged", File.ReadAllText(Path.Combine(transactionStage, "third.party", "unchanged.dll")));
            Assert.True(File.Exists(Path.Combine(transactionStage, "third.party", "manifest.json")));
        }

        [Fact]
        public void FullManifestPackageOverridesInstalledAndApplicationDeltaAssembly()
        {
            string applicationPluginsStage = Path.Combine(_tempDirectory, "Full", "ColorVision", "Plugins");
            string deltaDirectory = Path.Combine(applicationPluginsStage, "third.party");
            string installedPluginsRoot = Path.Combine(_tempDirectory, "FullInstall", "Plugins");
            string installedDirectory = Path.Combine(installedPluginsRoot, "third.party");
            string packageStage = Path.Combine(_tempDirectory, "Full", "ManifestPluginPackages");
            string packageDirectory = Path.Combine(packageStage, "third.party");
            string transactionStage = Path.Combine(_tempDirectory, "Full", "ManifestPlugins");
            Directory.CreateDirectory(deltaDirectory);
            Directory.CreateDirectory(installedDirectory);
            Directory.CreateDirectory(packageDirectory);
            File.WriteAllText(Path.Combine(installedDirectory, "manifest.json"), "{\"id\":\"third.party\",\"version\":\"1.0\"}");
            File.WriteAllText(Path.Combine(installedDirectory, "obsolete.dll"), "obsolete");
            File.WriteAllText(Path.Combine(deltaDirectory, "application-only.dll"), "application delta");
            File.WriteAllText(Path.Combine(packageDirectory, "manifest.json"), "{\"id\":\"third.party\",\"version\":\"2.0\"}");
            File.WriteAllText(Path.Combine(packageDirectory, "package.dll"), "complete package");

            IReadOnlyList<string> pluginIds = PluginUpdater.PrepareCombinedManifestPluginDirectoriesForTransaction(
                applicationPluginsStage,
                packageStage,
                installedPluginsRoot,
                transactionStage);

            string preparedDirectory = Path.Combine(transactionStage, "third.party");
            Assert.Equal(["third.party"], pluginIds);
            Assert.True(File.Exists(Path.Combine(preparedDirectory, "package.dll")));
            Assert.False(File.Exists(Path.Combine(preparedDirectory, "obsolete.dll")));
            Assert.False(File.Exists(Path.Combine(preparedDirectory, "application-only.dll")));
            Assert.False(Directory.Exists(deltaDirectory));
            Assert.False(Directory.Exists(packageDirectory));
        }

        [Fact]
        public void EmptyPluginPackageIsRejected()
        {
            string packagePath = Path.Combine(_tempDirectory, "Empty.cvxp");
            using (ZipFile.Open(packagePath, ZipArchiveMode.Create))
            {
            }

            Assert.False(PluginUpdater.IsPluginPackageFileReady(packagePath));
            Assert.Throws<InvalidDataException>(() => PluginUpdater.StagePluginPackages(
                new[] { packagePath },
                Path.Combine(_tempDirectory, "Stage", "Plugins"),
                Path.Combine(_tempDirectory, "Extract")));
        }

        [Fact]
        public void PluginPackageReadinessRejectsInProgressDamagedAndWrongExtensionFiles()
        {
            string packagePath = CreatePluginPackage("Ready", "third.party", wrapped: false);
            Assert.True(PluginUpdater.IsPluginPackageFileReady(packagePath));

            File.WriteAllText(packagePath + ".aria2", string.Empty);
            Assert.False(PluginUpdater.IsPluginPackageFileReady(packagePath));
            File.Delete(packagePath + ".aria2");

            string zipPath = Path.ChangeExtension(packagePath, ".zip");
            File.Copy(packagePath, zipPath);
            Assert.True(PluginUpdater.IsPluginPackageFileReady(zipPath));

            string wrongExtensionPath = Path.ChangeExtension(packagePath, ".bin");
            File.Copy(packagePath, wrongExtensionPath);
            Assert.False(PluginUpdater.IsPluginPackageFileReady(wrongExtensionPath));

            string damagedPackagePath = Path.Combine(_tempDirectory, "Damaged.cvxp");
            File.WriteAllText(damagedPackagePath, "not a zip archive");
            Assert.False(PluginUpdater.IsPluginPackageFileReady(damagedPackagePath));
        }

        [Fact]
        public void PackageManifestCannotEscapePluginsDirectory()
        {
            string packagePath = CreatePluginPackage("Unsafe", "../Other", wrapped: false);

            Assert.Throws<InvalidDataException>(() => PluginUpdater.StagePluginPackage(
                packagePath,
                Path.Combine(_tempDirectory, "Stage", "Plugins"),
                Path.Combine(_tempDirectory, "Extract")));
        }

        private string CreatePluginPackage(string packageName, string pluginId, bool wrapped)
        {
            string sourceRoot = Path.Combine(_tempDirectory, $"Source-{Guid.NewGuid():N}");
            string pluginRoot = wrapped ? Path.Combine(sourceRoot, packageName) : sourceRoot;
            Directory.CreateDirectory(pluginRoot);
            File.WriteAllText(
                Path.Combine(pluginRoot, "manifest.json"),
                $$"""{"id":"{{pluginId}}","name":"Third Party","version":"1.0","dllpath":"ThirdParty.dll"}""");
            File.WriteAllText(Path.Combine(pluginRoot, "ThirdParty.dll"), "test");

            string packagePath = Path.Combine(_tempDirectory, $"{packageName}-{Guid.NewGuid():N}.cvxp");
            ZipFile.CreateFromDirectory(sourceRoot, packagePath);
            return packagePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
