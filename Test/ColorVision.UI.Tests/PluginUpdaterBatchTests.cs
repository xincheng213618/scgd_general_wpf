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

            PluginUpdater.GenerateBatchFile(
                batchFilePath,
                baseDirectory,
                "ColorVision.exe",
                restartArguments: null);

            string batch = File.ReadAllText(batchFilePath, Encoding.GetEncoding(936));

            Assert.Contains("robocopy \"%STAGE%\" \"%TARGET%\" *.* /E ", batch, StringComparison.Ordinal);
            Assert.Contains("setlocal DisableDelayedExpansion", batch, StringComparison.Ordinal);
            Assert.Contains(PluginUpdater.EscapeForBatchValue(baseDirectory), batch, StringComparison.Ordinal);
            Assert.Contains("^>nul ^& rd /s /q \"%SELF_DIR%\" 2^>nul", batch, StringComparison.Ordinal);
            Assert.Equal(2, batch.Split("call :schedule_cleanup", StringSplitOptions.None).Length - 1);
            Assert.DoesNotContain("UPDATE_STATE", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(":rollback", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("enabledelayedexpansion", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/MIR", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ColorVisionBackup_", batch, StringComparison.OrdinalIgnoreCase);
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
