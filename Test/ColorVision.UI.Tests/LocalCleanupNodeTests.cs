using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Files;
using FlowEngineLib.Base;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class LocalCleanupNodeTests
{
    [Fact]
    public void NodeSettingsRoundTripIncludingCommasInFolderNames()
    {
        var node = new LocalCleanupNode
        {
            FolderPaths = [@"C:\cache,one", @"D:\缓存"], TriggerSizeBytes = 100L * 1024 * 1024 * 1024, TargetSizeBytes = 50L * 1024 * 1024 * 1024,
            FileExtensions = ".cvraw,.cvcie", IncludeSubfolders = false
        };
        node.Create();
        Assert.Equal("LocalCleanup", node.NodeType);
        Assert.Equal(["IN"], node.GetAllInputOptions().Select(port => port.Text));
        Assert.Equal(["OUT"], node.GetAllOutputOptions().Select(port => port.Text));
        using var reader = new BinaryReader(new MemoryStream(node.GetSaveData()));
        reader.ReadBytes(reader.ReadByte()); reader.ReadBytes(reader.ReadByte());
        var properties = new Dictionary<string, byte[]>();
        while (reader.BaseStream.Position < reader.BaseStream.Length)
            properties.Add(Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32())), reader.ReadBytes(reader.ReadInt32()));
        var restored = new LocalCleanupNode();
        restored.Create();
        restored.OnLoadNode(properties);
        Assert.Equal(node.FolderPaths, restored.FolderPaths);
        Assert.Equal(node.TriggerSizeBytes, restored.TriggerSizeBytes);
        Assert.Equal(node.TargetSizeBytes, restored.TargetSizeBytes);
        Assert.Equal(node.FileExtensions, restored.FileExtensions);
        Assert.Equal(node.IncludeSubfolders, restored.IncludeSubfolders);
        Assert.DoesNotContain("DeviceCode", properties.Keys);
    }

    [Fact]
    public void CleanupProtectsCurrentInputAndPassesItsIdentityThrough()
    {
        using var folder = new TestFolder();
        string input = folder.AddFile("input.cvraw", -3);
        string old = folder.AddFile("old.cvraw", -2);
        string newer = folder.AddFile("new.cvraw", -1);
        string ignored = folder.AddFile("keep.txt", -4);
        var node = new LocalCleanupNode { FolderPaths = [folder.Path], FileExtensions = "CVRAW", TriggerSizeBytes = 250, TargetSizeBytes = 100 };
        var action = new CVStartCFC("cleanup-test");
        action.Data["MasterValue"] = input;
        action.Data["MasterId"] = 42;
        try
        {
            FolderCleanupResult result = node.ExecuteSynchronously(action);
            Assert.Equal(2, result.DeletedFiles);
            Assert.Equal(200, result.DeletedBytes);
            Assert.Equal(100, result.RemainingBytes);
            Assert.Equal(1, result.ProtectedFiles);
            Assert.True(File.Exists(input));
            Assert.True(File.Exists(ignored));
            Assert.False(File.Exists(old));
            Assert.False(File.Exists(newer));
            Assert.Equal(input, action.Data["MasterValue"]);
            Assert.Equal(42, action.Data["MasterId"]);
            Assert.Equal(2, action.Data["CleanupDeletedFiles"]);
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void InvalidRootIsRejectedBeforeAnyConfiguredFolderIsChanged()
    {
        using var folder = new TestFolder();
        string file = folder.AddFile("keep.cvraw", -1);
        string driveRoot = System.IO.Path.GetPathRoot(folder.Path)!;
        Assert.Throws<ArgumentException>(() => FolderCleanupService.Execute([folder.Path, driveRoot], ".cvraw", true, 50, 0));
        Assert.True(File.Exists(file));
        Assert.Throws<ArgumentException>(() => FolderCleanupService.Execute(["relative-cache"], ".cvraw", true, 50, 0));
    }

    [Fact]
    public void CancellationStopsBeforeDeletionAndDoesNotReportSuccess()
    {
        using var folder = new TestFolder();
        string file = folder.AddFile("keep.cvraw", -1);
        int checkpoints = 0;
        Assert.Throws<OperationCanceledException>(() => FolderCleanupService.Execute([folder.Path], ".cvraw", true, 50, 0,
            checkCancellation: () => { if (++checkpoints >= 3) throw new OperationCanceledException(); }));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void LinkedDirectoriesAreSkippedAndCannotBeConfiguredAsRootsWhenLinksAreAvailable()
    {
        using var folder = new TestFolder();
        using var outside = new TestFolder();
        string target = outside.AddFile("keep.cvraw", -3);
        string local = folder.AddFile("delete.cvraw", -1);
        string link = System.IO.Path.Combine(folder.Path, "linked");
        try { Directory.CreateSymbolicLink(link, outside.Path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        try
        {
            FolderCleanupResult result = FolderCleanupService.Execute([folder.Path], ".cvraw", true, 50, 0);
            Assert.Equal(1, result.DeletedFiles);
            Assert.False(File.Exists(local));
            Assert.True(File.Exists(target));
            Assert.Throws<IOException>(() => FolderCleanupService.Execute([link], ".cvraw", true, 50, 0));
        }
        finally { Directory.Delete(link); }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(100, -1)]
    public void InvalidNodeThresholdsDoNotDeleteFiles(long trigger, long target)
    {
        using var folder = new TestFolder();
        string file = folder.AddFile("keep.cvraw", -1);
        var node = new LocalCleanupNode { FolderPaths = [folder.Path], TriggerSizeBytes = trigger, TargetSizeBytes = target };
        var action = new CVStartCFC("invalid-cleanup");
        try
        {
            Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action));
            Assert.True(File.Exists(file));
        }
        finally { action.RuntimeResources.Dispose(); }
    }

    [Fact]
    public void UnconfiguredNodeDoesNotInheritThePreprocessorsMachineSpecificFolder()
    {
        var node = new LocalCleanupNode();
        Assert.Empty(node.FolderPaths);
        var action = new CVStartCFC("unconfigured-cleanup");
        try { Assert.Throws<InvalidOperationException>(() => node.ExecuteSynchronously(action)); }
        finally { action.RuntimeResources.Dispose(); }
    }

    private sealed class TestFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ColorVision_LocalCleanup_" + Guid.NewGuid().ToString("N")));
        public TestFolder() => Directory.CreateDirectory(Path);
        public string AddFile(string name, int ageMinutes)
        {
            string file = System.IO.Path.Combine(Path, name);
            File.WriteAllBytes(file, new byte[100]);
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddMinutes(ageMinutes));
            return file;
        }
        public void Dispose()
        {
            string resolved = System.IO.Path.GetFullPath(Path);
            Assert.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), resolved, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("ColorVision_LocalCleanup_", System.IO.Path.GetFileName(resolved));
            Directory.Delete(resolved, recursive: true);
        }
    }
}
