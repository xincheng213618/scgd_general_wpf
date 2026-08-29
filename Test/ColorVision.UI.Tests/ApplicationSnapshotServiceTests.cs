using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using ColorVision.UI;
using ColorVision.Update;

namespace ColorVision.UI.Tests;

public sealed class ApplicationSnapshotServiceTests : IDisposable
{
    private readonly string _originalDirAppData = Environments.DirAppData;
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionSnapshotTests", Guid.NewGuid().ToString("N"));

    public ApplicationSnapshotServiceTests()
    {
        Environments.DirAppData = _tempDirectory;
    }

    [Fact]
    public void ListSnapshotsKeepsUnreadableZipFilesForManualRecovery()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "Snapshots", "Application");
        Directory.CreateDirectory(snapshotDirectory);
        string corruptedSnapshotPath = Path.Combine(snapshotDirectory, "bad.zip");
        File.WriteAllText(corruptedSnapshotPath, "not a zip");

        IReadOnlyList<ApplicationSnapshotInfo> snapshots = ApplicationSnapshotService.Instance.ListSnapshots();

        Assert.Empty(snapshots);
        Assert.True(File.Exists(corruptedSnapshotPath));
        Assert.Equal("not a zip", File.ReadAllText(corruptedSnapshotPath));
    }

    [Fact]
    public void SnapshotDirectoryIsScopedToTheCurrentInstallation()
    {
        string legacyDirectory = Path.Combine(_tempDirectory, "Snapshots", "Application");

        Assert.NotEqual(legacyDirectory, ApplicationSnapshotService.Instance.SnapshotDirectory);
        Assert.StartsWith(legacyDirectory + Path.DirectorySeparatorChar, ApplicationSnapshotService.Instance.SnapshotDirectory);
    }

    [Fact]
    public void UpdateSnapshotIsSkippedBeforeConfigurationInitialization()
    {
        Assert.False(ApplicationSnapshotService.ShouldCreateUpdateSnapshot(null));
    }

    [Fact]
    public void UpdateSnapshotUsesConfiguredSettingAfterConfigurationInitialization()
    {
        HybridConfigServiceAdapter configService = new();
        configService.Register(new ApplicationSnapshotConfig { CreateSnapshotBeforeUpdate = true });

        Assert.True(ApplicationSnapshotService.ShouldCreateUpdateSnapshot(configService));
    }

    [Fact]
    public void AutomaticSnapshotIsEnabledByDefault()
    {
        Assert.True(new ApplicationSnapshotConfig().CreateAutomaticSnapshotAfterHealthyStartup);
    }

    [Fact]
    public void AutomaticSnapshotUsesOneFixedFileAndRecognizesCurrentVersion()
    {
        ApplicationSnapshotService service = ApplicationSnapshotService.Instance;
        Directory.CreateDirectory(service.SnapshotDirectory);
        string snapshotPath = Path.Combine(service.SnapshotDirectory, "autosave.zip");
        using (ZipArchive archive = ZipFile.Open(snapshotPath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry manifestEntry = archive.CreateEntry("snapshot-manifest.json");
            using Stream manifestStream = manifestEntry.Open();
            JsonSerializer.Serialize(manifestStream, new ApplicationSnapshotManifest
            {
                SnapshotKind = "Automatic",
                CreatedAt = DateTime.Now,
                Version = "1.2.3.4",
                ProgramDirectory = AppDomain.CurrentDomain.BaseDirectory,
            });
        }

        ApplicationSnapshotInfo snapshot = service.GetSnapshotInfo(snapshotPath);

        Assert.True(snapshot.IsAutomatic);
        Assert.Equal("自动存档", snapshot.SnapshotTypeText);
        Assert.False(ApplicationSnapshotService.ShouldCreateAutomaticSnapshot(snapshotPath, "1.2.3.4"));
        Assert.True(ApplicationSnapshotService.ShouldCreateAutomaticSnapshot(snapshotPath, "1.2.3.5"));
    }

    [Fact]
    public void RebuiltSnapshotPreservesPreviousFileInRecovery()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "Snapshots", "Application");
        Directory.CreateDirectory(snapshotDirectory);
        string snapshotPath = Path.Combine(snapshotDirectory, "default.zip");
        string completedSnapshotPath = Path.Combine(snapshotDirectory, "completed.tmp");
        File.WriteAllText(snapshotPath, "previous snapshot");
        File.WriteAllText(completedSnapshotPath, "new snapshot");

        ApplicationSnapshotService.PromoteCompletedSnapshot(completedSnapshotPath, snapshotPath);

        Assert.Equal("new snapshot", File.ReadAllText(snapshotPath));
        Assert.False(File.Exists(completedSnapshotPath));
        string recoveryPath = Assert.Single(Directory.EnumerateFiles(Path.Combine(snapshotDirectory, "Recovery"), "*.zip"));
        Assert.Equal("previous snapshot", File.ReadAllText(recoveryPath));
    }

    [Fact]
    public void AutomaticSnapshotReplacementDoesNotAccumulatePreviousVersions()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "Snapshots", "Automatic");
        Directory.CreateDirectory(snapshotDirectory);
        string snapshotPath = Path.Combine(snapshotDirectory, "autosave.zip");
        string completedSnapshotPath = Path.Combine(snapshotDirectory, "completed.tmp");
        File.WriteAllText(snapshotPath, "previous automatic snapshot");
        File.WriteAllText(completedSnapshotPath, "new automatic snapshot");

        ApplicationSnapshotService.PromoteCompletedAutomaticSnapshot(completedSnapshotPath, snapshotPath);

        Assert.Equal("new automatic snapshot", File.ReadAllText(snapshotPath));
        Assert.False(File.Exists(completedSnapshotPath));
        Assert.False(Directory.Exists(Path.Combine(snapshotDirectory, "Recovery")));
        Assert.Single(Directory.EnumerateFiles(snapshotDirectory));
    }

    [Fact]
    public void RestoreBatchWaitsForOnlyTheOriginalProcess()
    {
        ExitUpdateHandoffState handoffState = new(
            Path.Combine(_tempDirectory, "update.pending"),
            Path.Combine(_tempDirectory, "reopen.requested"),
            "token",
            _tempDirectory);

        string batch = ApplicationSnapshotService.CreateRestoreBatch(
            Path.Combine(_tempDirectory, "stage"),
            Path.Combine(_tempDirectory, "program"),
            "ColorVision.exe",
            12345,
            handoffState);

        Assert.Contains("taskkill /f /pid \"%ORIGINAL_PID%\"", batch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("taskkill /f /im", batch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("explorer.exe", batch, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dllhost.exe", batch, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Snapshot restore failed.", batch, StringComparison.Ordinal);
        Assert.Contains("update.log", batch, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RestoreStageSkipsShellExtensionFilesWithoutDeletingOtherFiles()
    {
        string stageDirectory = Path.Combine(_tempDirectory, "stage");
        Directory.CreateDirectory(stageDirectory);
        string shellExtensionPath = Path.Combine(stageDirectory, "ColorVision.ShellExtension.dll");
        string applicationPath = Path.Combine(stageDirectory, "ColorVision.exe");
        File.WriteAllText(shellExtensionPath, "extension");
        File.WriteAllText(applicationPath, "application");

        int removedCount = ApplicationSnapshotService.RemoveShellExtensionFilesFromRestoreStage(stageDirectory);

        Assert.Equal(1, removedCount);
        Assert.False(File.Exists(shellExtensionPath));
        Assert.True(File.Exists(applicationPath));
    }

    [Fact]
    public void SnapshotFilterSkipsTransientFilesButKeepsRuntimeFiles()
    {
        string programDirectory = Path.Combine(_tempDirectory, "program");

        Assert.False(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "log", "ColorVision.log")));
        Assert.False(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "ColorVision.pdb")));
        Assert.False(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "cache.tmp")));
        Assert.False(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "update.bat")));
        Assert.True(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "ColorVision.exe")));
        Assert.True(ApplicationSnapshotService.ShouldIncludeSnapshotFile(programDirectory, Path.Combine(programDirectory, "runtimes", "win-x64", "native", "runtime.dll")));
    }

    [Fact]
    public async Task DeletingDefaultSnapshotDoesNotRecreateIt()
    {
        ApplicationSnapshotService service = ApplicationSnapshotService.Instance;
        Directory.CreateDirectory(service.SnapshotDirectory);
        File.WriteAllText(service.DefaultSnapshotPath, "snapshot");
        ApplicationSnapshotInfo snapshot = new()
        {
            FilePath = service.DefaultSnapshotPath,
            FileName = "default.zip",
            Version = "1.0.0",
            VersionTarget = string.Empty,
            CreatedAt = DateTime.Now,
            SizeBytes = new FileInfo(service.DefaultSnapshotPath).Length,
            IsDefault = true,
            IsUpdate = false,
            IsAutomatic = false,
        };

        await service.DeleteSnapshotAsync(snapshot);

        Assert.False(File.Exists(service.DefaultSnapshotPath));
    }

    [Fact]
    public void AutomaticUpdateSnapshotRetentionKeepsOnlyNewestThree()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "retention");
        Directory.CreateDirectory(snapshotDirectory);
        for (int index = 0; index < 5; index++)
        {
            string snapshotPath = Path.Combine(snapshotDirectory, $"ColorVision-update-1.0.0-{index}.zip");
            using (ZipFile.Open(snapshotPath, ZipArchiveMode.Create))
            {
            }
            File.SetCreationTime(snapshotPath, new DateTime(2026, 7, 1).AddDays(index));
        }

        string userSnapshotPath = Path.Combine(snapshotDirectory, "ColorVision-1.0.0-user.zip");
        using (ZipFile.Open(userSnapshotPath, ZipArchiveMode.Create))
        {
        }

        int removedCount = ApplicationSnapshotService.TrimAutomaticUpdateSnapshots(snapshotDirectory, 3);

        Assert.Equal(2, removedCount);
        Assert.Equal(3, Directory.EnumerateFiles(snapshotDirectory, "ColorVision-update-*.zip").Count());
        Assert.True(File.Exists(userSnapshotPath));
    }

    public void Dispose()
    {
        Environments.DirAppData = _originalDirAppData;

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }
}
