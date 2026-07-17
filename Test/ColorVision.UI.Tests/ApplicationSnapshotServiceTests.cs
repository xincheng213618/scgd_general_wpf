using System;
using System.Collections.Generic;
using System.IO;
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
    public void UnreadableDefaultSnapshotIsMovedToRecoveryInsteadOfDeleted()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "Snapshots", "Application");
        Directory.CreateDirectory(snapshotDirectory);
        string defaultSnapshotPath = Path.Combine(snapshotDirectory, "default.zip");
        File.WriteAllText(defaultSnapshotPath, "recover me");

        string recoveryPath = ApplicationSnapshotService.MoveUnreadableSnapshotToRecovery(defaultSnapshotPath);

        Assert.False(File.Exists(defaultSnapshotPath));
        Assert.True(File.Exists(recoveryPath));
        Assert.Equal(Path.Combine(snapshotDirectory, "Recovery"), Path.GetDirectoryName(recoveryPath));
        Assert.Equal("recover me", File.ReadAllText(recoveryPath));
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

    public void Dispose()
    {
        Environments.DirAppData = _originalDirAppData;

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }
}
