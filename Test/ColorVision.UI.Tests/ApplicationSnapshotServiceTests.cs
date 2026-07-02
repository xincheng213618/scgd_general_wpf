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
    public void ListSnapshotsDeletesCorruptedZipFiles()
    {
        string snapshotDirectory = Path.Combine(_tempDirectory, "Snapshots", "Application");
        Directory.CreateDirectory(snapshotDirectory);
        string corruptedSnapshotPath = Path.Combine(snapshotDirectory, "bad.zip");
        File.WriteAllText(corruptedSnapshotPath, "not a zip");

        IReadOnlyList<ApplicationSnapshotInfo> snapshots = ApplicationSnapshotService.Instance.ListSnapshots();

        Assert.Empty(snapshots);
        Assert.False(File.Exists(corruptedSnapshotPath));
        Assert.False(Directory.Exists(Path.Combine(snapshotDirectory, "Corrupted")));
    }

    public void Dispose()
    {
        Environments.DirAppData = _originalDirAppData;

        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, true);
    }
}
