using ColorVision.Update;
using ColorVision.UI.Plugins;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Tests
{
    public sealed class PluginRecoveryBackupServiceTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionPluginRecoveryTests-{Guid.NewGuid():N}");

        public PluginRecoveryBackupServiceTests()
        {
            Directory.CreateDirectory(_tempDirectory);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Fact]
        public void VerifiedBackupCapturesManifestCatalogAndOriginalContent()
        {
            string programDirectory = Path.Combine(_tempDirectory, "Install A 100%! & ^");
            string pluginDirectory = CreatePlugin(programDirectory, "third.party", "1.2.3", "old payload");
            string backupRoot = Path.Combine(_tempDirectory, "Backups");
            PluginRecoveryBackupService service = new(backupRoot);

            PluginRecoveryBackupInfo? created = service.CreateVerifiedBackup("third.party", pluginDirectory);

            Assert.NotNull(created);
            Assert.Equal("third.party", created.PluginId);
            Assert.Equal("Third Party", created.PluginName);
            Assert.Equal("1.2.3", created.Version);
            Assert.Equal(3, created.FileCount);
            Assert.True(created.TotalBytes > 0);
            Assert.Equal(64, created.DirectoryHash.Length);
            Assert.Equal(ExitUpdateHandoff.GetInstallationKey(programDirectory), created.InstallationKey);
            Assert.StartsWith(Path.GetFullPath(backupRoot), created.BackupDirectory, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("old payload", File.ReadAllText(Path.Combine(created.PayloadDirectory, "payload.txt")));

            using JsonDocument metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(created.BackupDirectory, "backup.json")));
            Assert.Equal(1, metadata.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.Equal("third.party", metadata.RootElement.GetProperty("PluginId").GetString());
            Assert.Equal(3, metadata.RootElement.GetProperty("Files").GetArrayLength());
            Assert.Equal(64, metadata.RootElement.GetProperty("Manifest").GetProperty("Sha256").GetString()!.Length);

            File.WriteAllText(Path.Combine(pluginDirectory, "payload.txt"), "new payload");
            File.Delete(Path.Combine(pluginDirectory, "obsolete.dll"));
            PluginRecoveryBackupInfo? available = service.GetAvailableBackup("third.party", pluginDirectory);

            Assert.NotNull(available);
            Assert.Equal(created.BackupDirectory, available.BackupDirectory);
            Assert.Equal("old payload", File.ReadAllText(Path.Combine(available.PayloadDirectory, "payload.txt")));
            Assert.True(File.Exists(Path.Combine(available.PayloadDirectory, "obsolete.dll")));
        }

        [Fact]
        public void CorruptedBackupIsNotAvailable()
        {
            string programDirectory = Path.Combine(_tempDirectory, "Install");
            string pluginDirectory = CreatePlugin(programDirectory, "third.party", "1.0", "payload");
            PluginRecoveryBackupService service = new(Path.Combine(_tempDirectory, "Backups"));
            PluginRecoveryBackupInfo created = service.CreateVerifiedBackup("third.party", pluginDirectory)!;

            File.WriteAllText(Path.Combine(created.PayloadDirectory, "payload.txt"), "corrupted");

            Assert.Null(service.GetAvailableBackup("third.party", pluginDirectory));
            Assert.False(service.TryReadBackupMetadata(created.BackupDirectory, out _));
            Assert.Throws<InvalidDataException>(() => service.ReadBackupMetadata(created.BackupDirectory));
        }

        [Fact]
        public void BackupLookupIsIsolatedByExactInstallationDirectory()
        {
            string firstProgramDirectory = Path.Combine(_tempDirectory, "Install A");
            string secondProgramDirectory = Path.Combine(_tempDirectory, "Install B");
            string firstPluginDirectory = CreatePlugin(firstProgramDirectory, "third.party", "1.0", "first");
            string secondPluginDirectory = CreatePlugin(secondProgramDirectory, "third.party", "1.0", "second");
            PluginRecoveryBackupService service = new(Path.Combine(_tempDirectory, "Backups"));

            PluginRecoveryBackupInfo created = service.CreateVerifiedBackup("third.party", firstPluginDirectory)!;

            Assert.NotNull(service.GetAvailableBackup("third.party", firstPluginDirectory));
            Assert.Null(service.GetAvailableBackup("third.party", secondPluginDirectory));
            Assert.Single(service.GetAvailableBackups(firstProgramDirectory));
            Assert.Empty(service.GetAvailableBackups(secondProgramDirectory));
            Assert.NotEqual(
                ExitUpdateHandoff.GetInstallationKey(firstProgramDirectory),
                ExitUpdateHandoff.GetInstallationKey(secondProgramDirectory));
            Assert.Contains(created.InstallationKey, created.BackupDirectory, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RetentionKeepsThreeVerifiedBackupsWithoutDeletingCorruptOrCreatingEntries()
        {
            string programDirectory = Path.Combine(_tempDirectory, "RetentionInstall");
            string pluginDirectory = CreatePlugin(programDirectory, "third.party", "1.0", "payload-1");
            PluginRecoveryBackupService service = new(Path.Combine(_tempDirectory, "Backups"));
            PluginRecoveryBackupInfo corruptBackup = service.CreateVerifiedBackup("third.party", pluginDirectory)!;
            File.WriteAllText(Path.Combine(corruptBackup.PayloadDirectory, "payload.txt"), "corrupted");
            string pluginBackupRoot = Path.GetDirectoryName(corruptBackup.BackupDirectory)!;
            string creatingDirectory = Path.Combine(pluginBackupRoot, "interrupted.creating");
            Directory.CreateDirectory(creatingDirectory);

            var createdValidBackups = new List<PluginRecoveryBackupInfo>();
            for (int index = 2; index <= 6; index++)
            {
                File.WriteAllText(Path.Combine(pluginDirectory, "payload.txt"), $"payload-{index}");
                createdValidBackups.Add(service.CreateVerifiedBackup("third.party", pluginDirectory)!);
            }

            List<string> completedDirectories = Directory
                .EnumerateDirectories(pluginBackupRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !path.EndsWith(".creating", StringComparison.OrdinalIgnoreCase))
                .ToList();
            int validCount = completedDirectories.Count(path => service.TryReadBackupMetadata(path, out _));

            Assert.Equal(3, validCount);
            Assert.True(Directory.Exists(corruptBackup.BackupDirectory));
            Assert.True(Directory.Exists(creatingDirectory));
            Assert.True(Directory.Exists(createdValidBackups[^1].BackupDirectory));
            Assert.Equal(createdValidBackups[^1].BackupDirectory, service.GetAvailableBackup("third.party", pluginDirectory)!.BackupDirectory);
        }

        [Fact]
        public void BackupCreationRejectsPathsOutsideDirectPluginsChild()
        {
            string outsideDirectory = Path.Combine(_tempDirectory, "Outside", "third.party");
            Directory.CreateDirectory(outsideDirectory);
            PluginRecoveryBackupService service = new(Path.Combine(_tempDirectory, "Backups"));

            Assert.Throws<ArgumentException>(() => service.CreateVerifiedBackup("third.party", outsideDirectory));
            Assert.Throws<ArgumentException>(() => service.CreateVerifiedBackup("../other", Path.Combine(_tempDirectory, "Install", "Plugins", "third.party")));
            Assert.Null(service.GetAvailableBackup("third.party", outsideDirectory));
        }

        [Fact]
        public void RestoreBatchUsesSameInstallationDirectorySwapAndSafeHandoff()
        {
            string restoreRoot = Path.Combine(_tempDirectory, "Restore 100%! & ^");
            string programDirectory = Path.Combine(_tempDirectory, "Install 100%! & ^");
            string pluginDirectory = Path.Combine(programDirectory, "Plugins", "third.party");
            string stageDirectory = Path.Combine(restoreRoot, "Plugin");
            string batchPath = Path.Combine(restoreRoot, "update.bat");
            Directory.CreateDirectory(stageDirectory);
            Directory.CreateDirectory(pluginDirectory);
            ExitUpdateHandoffState handoffState = new(
                Path.Combine(restoreRoot, "state", "update.pending"),
                Path.Combine(restoreRoot, "state", "reopen.requested"),
                "0123456789abcdef0123456789abcdef",
                restoreRoot);

            string batch = PluginRecoveryBackupService.CreateRestoreBatch(
                batchPath,
                stageDirectory,
                pluginDirectory,
                programDirectory,
                "ColorVision.exe",
                originalProcessId: 4242,
                handoffState);

            Assert.Contains("ColorVision Plugin Recovery", batch, StringComparison.Ordinal);
            Assert.Contains("\\Plugins\\.ColorVisionRecovery-", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("move /y \"%PLUGIN_TARGET_0%\" \"%PLUGIN_ROLLBACK_0%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("set \"COPY_SOURCE=" + EscapeForBatchValue(stageDirectory), batch, StringComparison.Ordinal);
            Assert.Contains("set \"ORIGINAL_PID=4242\"", batch, StringComparison.Ordinal);
            Assert.Contains("taskkill /f /pid \"%ORIGINAL_PID%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("robocopy \"%COPY_SOURCE%\" \"%PLUGIN_TARGET_0%\"", batch, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("enabledelayedexpansion", batch, StringComparison.OrdinalIgnoreCase);
        }

        private static string CreatePlugin(string programDirectory, string pluginId, string version, string payload)
        {
            string pluginDirectory = Path.Combine(programDirectory, "Plugins", pluginId);
            Directory.CreateDirectory(Path.Combine(pluginDirectory, "nested"));
            File.WriteAllText(
                Path.Combine(pluginDirectory, "manifest.json"),
                $$"""{"id":"{{pluginId}}","name":"Third Party","version":"{{version}}","requires":"1.0","dllpath":"ThirdParty.dll","author":"ColorVision","url":"https://example.invalid","manifest_version":1}""");
            File.WriteAllText(Path.Combine(pluginDirectory, "payload.txt"), payload);
            File.WriteAllText(Path.Combine(pluginDirectory, "obsolete.dll"), "obsolete");
            return pluginDirectory;
        }

        private static string EscapeForBatchValue(string value) => value.Replace("%", "%%", StringComparison.Ordinal);

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
