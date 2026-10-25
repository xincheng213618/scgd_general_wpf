using ColorVision.ImageEditor.BatchProcessing;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotBatchImageSourceResolverTests
{
    [Fact]
    public void ExactFileAuthorizationDoesNotAuthorizeItsSiblingOrParent()
    {
        using var fixture = new SourceFixture();
        string selected = fixture.CreateFile("selected.png");
        string sibling = fixture.CreateFile("sibling.png");
        var request = new CopilotAgentRequest { ReadableLocalFilePaths = [selected] };

        Assert.True(CopilotBatchImageSourceResolver.TryResolveSource(request, selected, out var resolved, out var error), error);
        Assert.Equal(selected, resolved);
        Assert.False(CopilotBatchImageSourceResolver.TryResolveSource(request, sibling, out _, out _));
        Assert.False(CopilotBatchImageSourceResolver.TryResolveSource(request, fixture.Root, out _, out _));
        Assert.False(CopilotBatchImageSourceResolver.TryResolveSource(request, "selected.png", out _, out _));

        Assert.True(CopilotBatchImageSourceResolver.TryResolveItems(request, [selected], false, CreateProcessor(),
            CancellationToken.None, out var items, out error), error);
        Assert.Equal(selected, Assert.Single(items).FilePath);
        Assert.Equal(fixture.Root, items[0].SourceRoot);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectoryAuthorizationAllowsOtherFilesAndTheDirectory(bool useSearchRoot)
    {
        using var fixture = new SourceFixture();
        string selected = fixture.CreateFile("selected.png");
        string sibling = fixture.CreateFile("sibling.png");
        var request = new CopilotAgentRequest
        {
            ReadableLocalFilePaths = [selected],
            SearchRootPaths = useSearchRoot ? [fixture.Root] : [],
            ReadableLocalDirectoryPaths = useSearchRoot ? [] : [fixture.Root],
        };

        Assert.True(CopilotBatchImageSourceResolver.TryResolveSource(request, sibling, out var resolved, out var error), error);
        Assert.Equal(sibling, resolved);
        Assert.True(CopilotBatchImageSourceResolver.TryResolveSource(request, fixture.Root, out resolved, out error), error);
        Assert.Equal(fixture.Root, resolved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectoryDiscoveryPreservesPngInputsRootsAndRequestedRecursionWithoutLoadingImages(bool recursive)
    {
        using var fixture = new SourceFixture();
        string first = fixture.CreateFile("a.png");
        string nested = fixture.CreateFile(Path.Combine("nested", "b.png"));
        fixture.CreateFile("ignored.txt");
        var request = new CopilotAgentRequest { SearchRootPaths = [fixture.Root] };

        Assert.True(CopilotBatchImageSourceResolver.TryResolveItems(request, [fixture.Root, fixture.Root], recursive,
            CreateProcessor(), CancellationToken.None, out var items, out var error), error);

        Assert.Equal(recursive ? new[] { first, nested } : new[] { first }, items.Select(item => item.FilePath));
        Assert.All(items, item => Assert.Equal(fixture.Root, item.SourceRoot));
    }

    [Fact]
    public void ExactFileAuthorizationRequiresAnExistingFileAndRejectsReparsePoints()
    {
        using var fixture = new SourceFixture();
        string target = fixture.CreateFile("target.png");
        string link = Path.Combine(fixture.Root, "link.png");
        string missing = Path.Combine(fixture.Root, "missing.png");
        File.CreateSymbolicLink(link, target);
        try
        {
            var request = new CopilotAgentRequest { ReadableLocalFilePaths = [link, missing] };
            Assert.False(CopilotBatchImageSourceResolver.TryResolveSource(request, link, out _, out var error));
            Assert.Contains("reparse point", error, StringComparison.OrdinalIgnoreCase);
            Assert.False(CopilotBatchImageSourceResolver.TryResolveSource(request, missing, out _, out error));
            Assert.Contains("does not exist", error, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(link); }
    }

    private static BatchImageProcessor CreateProcessor() => new([new MetadataOnlyLoader()]);

    private sealed class MetadataOnlyLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions => [".png"];
        public OpenCvSharp.Mat Load(string filePath) => throw new InvalidOperationException("Source discovery must not decode images.");
    }

    private sealed class SourceFixture : IDisposable
    {
        private const string Prefix = "copilot-batch-sources-";
        public string Root { get; } = Directory.CreateTempSubdirectory(Prefix).FullName;

        public string CreateFile(string relativePath)
        {
            string path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, []);
            return path;
        }

        public void Dispose()
        {
            string fullPath = Path.GetFullPath(Root);
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), fullPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(Prefix, Path.GetFileName(fullPath), StringComparison.Ordinal);
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
