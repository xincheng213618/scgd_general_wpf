using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates.Flow.Versioning;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class StandaloneFlowFileStoreTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-StandaloneFlow-");

    [Fact]
    public void ASecondLoadedRevisionCannotOverwriteTheFirstSave()
    {
        string path = Path.Combine(directory.FullName, "flow.stn");
        File.WriteAllBytes(path, [1, 2, 3]);
        string firstBaseline = FlowSemanticHash.ComputeBinaryHash(File.ReadAllBytes(path));
        string secondBaseline = FlowSemanticHash.ComputeBinaryHash(File.ReadAllBytes(path));
        byte[] firstEdit = [4, 5, 6];
        byte[] secondEdit = [7, 8, 9];

        string savedHash = StandaloneFlowFileStore.Save(path, firstEdit, firstBaseline);
        Assert.Throws<StandaloneFlowFileConflictException>(() => StandaloneFlowFileStore.Save(path, secondEdit, secondBaseline));
        Assert.Equal(firstEdit, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));

        string reloadedBaseline = FlowSemanticHash.ComputeBinaryHash(File.ReadAllBytes(path));
        Assert.Equal(savedHash, reloadedBaseline);
        StandaloneFlowFileStore.Save(path, secondEdit, reloadedBaseline);
        Assert.Equal(secondEdit, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
    }

    [Fact]
    public void LockedDestinationKeepsTheLastGoodFileAndAllowsRetry()
    {
        string path = Path.Combine(directory.FullName, "locked.stn");
        byte[] original = [1, 2, 3];
        byte[] updated = [4, 5, 6];
        string baseline = StandaloneFlowFileStore.Save(path, original, expectedContentHash: null);

        using (File.OpenRead(path))
        {
            Assert.ThrowsAny<IOException>(() => StandaloneFlowFileStore.Save(path, updated, baseline));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
        }

        StandaloneFlowFileStore.Save(path, updated, baseline);
        Assert.Equal(updated, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
    }

    [Fact]
    public void DeletedSourceIsNotRecreatedByAnAlreadyLoadedDocument()
    {
        string path = Path.Combine(directory.FullName, "removed.stn");
        string baseline = StandaloneFlowFileStore.Save(path, [1, 2, 3], expectedContentHash: null);
        File.Delete(path);

        Assert.Throws<StandaloneFlowFileConflictException>(() => StandaloneFlowFileStore.Save(path, [4, 5, 6], baseline));
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.FullName));
    }

    [Fact]
    public void ExplicitlySelectedSaveDestinationCanReplaceAnExistingFile()
    {
        string path = Path.Combine(directory.FullName, "selected.stn");
        File.WriteAllBytes(path, [1, 2, 3]);
        byte[] updated = [4, 5, 6];
        string? confirmedRevision = StandaloneFlowFileStore.CaptureSelectedDestination(path);
        Assert.NotNull(confirmedRevision);

        StandaloneFlowFileStore.Save(path, updated, confirmedRevision);

        Assert.Equal(updated, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
    }

    [Fact]
    public void AFileCreatedAfterSelectingANewDestinationIsPreserved()
    {
        string path = Path.Combine(directory.FullName, "new.stn");
        string? selectedRevision = StandaloneFlowFileStore.CaptureSelectedDestination(path);
        Assert.Null(selectedRevision);
        byte[] otherEdit = [1, 2, 3];
        File.WriteAllBytes(path, otherEdit);

        Assert.ThrowsAny<IOException>(() => StandaloneFlowFileStore.Save(path, [4, 5, 6], selectedRevision));

        Assert.Equal(otherEdit, File.ReadAllBytes(path));
        Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
    }

    [Fact]
    public async Task ContendedSaveCanBeRetriedAfterTheWriterReleasesThePath()
    {
        string path = Path.Combine(directory.FullName, "contended.stn");
        byte[] original = [1, 2, 3];
        byte[] updated = [4, 5, 6];
        string baseline = StandaloneFlowFileStore.Save(path, original, expectedContentHash: null);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        Task owner = Task.Run(() =>
        {
            using var mutex = new Mutex(false, StandaloneFlowFileStore.GetSaveMutexName(path.ToUpperInvariant()));
            Assert.True(mutex.WaitOne(0));
            try
            {
                entered.SetResult();
                release.Wait();
            }
            finally { mutex.ReleaseMutex(); }
        });

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Throws<IOException>(() => StandaloneFlowFileStore.Save(path, updated, baseline));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(path, Assert.Single(Directory.EnumerateFileSystemEntries(directory.FullName)));
        }
        finally
        {
            release.Set();
            await owner;
        }

        StandaloneFlowFileStore.Save(path, updated, baseline);
        Assert.Equal(updated, File.ReadAllBytes(path));
    }

    public void Dispose()
    {
        foreach (FileInfo file in directory.EnumerateFiles())
            file.Delete();
        directory.Delete();
    }
}
