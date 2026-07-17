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

    public void Dispose()
    {
        Environments.DirAppData = _originalDirAppData;

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }
}
