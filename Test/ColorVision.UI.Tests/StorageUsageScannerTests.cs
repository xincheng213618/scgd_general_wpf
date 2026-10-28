using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class StorageUsageScannerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ColorVisionStorageUsageTests-").FullName;

    [Fact]
    public void UsageCountsCurrentFilesAndDeduplicatesOverlappingAllowlistedRoots()
    {
        string raw = CreateFile("frame.cvraw", 7);
        string image = CreateFile("nested/image.png", 11);
        CreateFile("private.txt", 17);
        StorageUsageSnapshot result = StorageUsageScanner.Scan([
            new(_root, "*.cvraw"), new(_root, "*.png", true), new(Path.Combine(_root, "nested"), "*.png")]);

        Assert.Equal(18, result.Bytes);
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { raw, image }.Order(), result.Files.Select(file => file.FullPath).Order());
        Assert.Empty(result.Issues);
        Assert.All(result.Files, file => Assert.True(File.Exists(file.FullPath)));
    }

    [Fact]
    public void InvalidRulesAndAnUnreadableFileDoNotHideTheOtherOwnersFiles()
    {
        string accepted = CreateFile("accepted.png", 5);
        CreateFile("unreadable.png", 9);
        StorageUsageSnapshot result = StorageUsageScanner.Scan([
            new("relative"), new(Path.GetPathRoot(_root)!), new(_root, "../*.png"),
            new(_root, "*.png", IncludeFile: path => Path.GetFileName(path) == "unreadable.png" ? throw new UnauthorizedAccessException("test denied") : true)]);

        Assert.Equal(accepted, Assert.Single(result.Files).FullPath);
        Assert.Equal(5, result.Bytes);
        Assert.Equal(4, result.Issues.Count);
    }

    [Fact]
    public void CancellationReturnsAnIncompleteReadOnlySnapshot()
    {
        string original = CreateFile("frame.cvraw", 13);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        StorageUsageSnapshot result = StorageUsageScanner.Scan([new(_root, Recursive: true)], cancellation.Token);

        Assert.True(result.IsCancelled);
        Assert.Empty(result.Files);
        Assert.Equal(13, new FileInfo(original).Length);
    }

    [Fact]
    public void ASourceThatIsAFileIsReportedWhileAnAbsentOptionalDirectoryIsEmpty()
    {
        string file = CreateFile("not-a-directory", 3);
        StorageUsageSnapshot result = StorageUsageScanner.Scan([new(Path.Combine(_root, "missing")), new(file)]);
        Assert.Empty(result.Files);
        Assert.Contains(file, Assert.Single(result.Issues));
    }

    [Fact]
    public void UsageDoesNotFollowDirectoryOrFileLinksWhenSupported()
    {
        string allowed = Path.Combine(_root, "allowed");
        Directory.CreateDirectory(allowed);
        string privateFile = CreateFile("outside/private.png", 31);
        try
        {
            Directory.CreateSymbolicLink(Path.Combine(allowed, "linked"), Path.GetDirectoryName(privateFile)!);
            File.CreateSymbolicLink(Path.Combine(allowed, "linked.png"), privateFile);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException) { return; }

        StorageUsageSnapshot result = StorageUsageScanner.Scan([new(allowed, Recursive: true)]);
        Assert.Empty(result.Files);
        Assert.Equal(2, result.Issues.Count);
        Assert.Equal(31, new FileInfo(privateFile).Length);
        StorageUsageSnapshot throughAncestor = StorageUsageScanner.Scan([new(Path.Combine(allowed, "linked"))]);
        Assert.Empty(throughAncestor.Files);
        Assert.NotEmpty(throughAncestor.Issues);
    }

    private string CreateFile(string name, int length)
    {
        string path = Path.GetFullPath(Path.Combine(_root, name));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[length]);
        return path;
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(_root);
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("ColorVisionStorageUsageTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(root, true);
    }
}
