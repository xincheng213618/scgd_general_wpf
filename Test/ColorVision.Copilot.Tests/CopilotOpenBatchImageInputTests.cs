using ColorVision.ImageEditor.BatchProcessing;
using OpenCvSharp;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotOpenBatchImageInputTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("CopilotBatchWindowTool-").FullName;

    [Fact]
    public async Task EmptyInputKeepsTheOpenOnlyContract()
    {
        var calls = 0;
        var tool = CreateTool((items, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.Empty(items);
            calls++;
            return Task.FromResult(0);
        });

        var result = await tool.ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(1, calls);
        Assert.Empty(result.SuccessfullyReadLocalFilePaths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcePrefillPreservesRootAndSameFormatFilesWithoutDecoding(bool recursive)
    {
        var nested = Directory.CreateDirectory(Path.Combine(_root, "nested")).FullName;
        var first = Path.Combine(_root, "first.png");
        var second = Path.Combine(nested, "second.png");
        File.WriteAllBytes(first, [0]);
        File.WriteAllBytes(second, [0]);
        BatchImageItem[]? prepared = null;
        var loader = new ListingLoader();
        var tool = CreateTool((items, _) =>
        {
            prepared = items.ToArray();
            return Task.FromResult(items.Count);
        }, loader);

        var result = await tool.ExecuteAsync(Request(), Input(tool, new[] { _root }, recursive), CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(prepared);
        Assert.Equal(recursive ? 2 : 1, prepared.Length);
        Assert.Contains(prepared, item => item.FilePath == first);
        Assert.All(prepared, item => Assert.Equal(_root, item.SourceRoot));
        Assert.Equal(0, loader.LoadCalls);
        Assert.Contains("No images were converted", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidOrUnauthorizedInputCannotReachTheWindow()
    {
        var calls = 0;
        var tool = CreateTool((_, _) => { calls++; return Task.FromResult(0); });
        var request = Request();
        var unknown = Path.Combine(_root, "missing.png");

        var missing = await tool.ExecuteAsync(request, Input(tool, [unknown]), CancellationToken.None);
        var denied = await tool.ExecuteAsync(new CopilotAgentRequest(), Input(tool, [_root]), CancellationToken.None);
        var malformed = await tool.ExecuteAsync(request, new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["recursive"] = "yes" },
        }, CancellationToken.None);

        Assert.False(missing.Success);
        Assert.False(denied.Success);
        Assert.False(malformed.Success);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task CancelledDiscoveryCannotSubmitPartialInputsToTheWindow()
    {
        for (var index = 0; index < 4; index++) File.WriteAllBytes(Path.Combine(_root, $"{index}.png"), [0]);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        var loader = new ListingLoader(() => { if (++reads == 2) cancellation.Cancel(); });
        var calls = 0;
        var tool = CreateTool((_, _) => { calls++; return Task.FromResult(0); }, loader);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tool.ExecuteAsync(Request(), Input(tool, [_root]), cancellation.Token));

        Assert.Equal(2, reads);
        Assert.Equal(0, calls);
        Assert.Equal(0, loader.LoadCalls);
    }

    [Fact]
    public async Task BusyWindowRejectionIsReportedAsAFailure()
    {
        var tool = CreateTool((_, _) => throw new InvalidOperationException("The batch window is processing images."));

        var result = await tool.ExecuteAsync(Request(), CopilotAgentToolInput.Empty, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("processing images", result.ErrorMessage, StringComparison.Ordinal);
    }

    private CopilotAgentRequest Request() => new()
    {
        UserText = "打开批量图像窗口并列出这些文件", Mode = CopilotAgentMode.Auto,
        SearchRootPaths = [_root], ReadableLocalDirectoryPaths = [_root],
    };

    private static CopilotOpenBatchImageProcessingTool CreateTool(
        Func<IReadOnlyList<BatchImageItem>, CancellationToken, Task<int>> opener, ListingLoader? loader = null) =>
        new(new BatchImageProcessor([loader ?? new ListingLoader()]), opener);

    private static CopilotAgentToolInput Input(CopilotOpenBatchImageProcessingTool tool, string[] sources, bool recursive = false)
    {
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?>
        {
            ["sources"] = sources, ["recursive"] = recursive,
        }, out var input, out var error), error);
        return input;
    }

    private sealed class ListingLoader(Action? extensionsRead = null) : IBatchImageLoader
    {
        public int LoadCalls { get; private set; }
        public IReadOnlyCollection<string> Extensions
        {
            get { extensionsRead?.Invoke(); return [".png"]; }
        }
        public Mat Load(string filePath)
        {
            LoadCalls++;
            throw new InvalidOperationException("Preparing window inputs must never decode an image.");
        }
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(resolved), ignoreCase: true);
        Assert.StartsWith("CopilotBatchWindowTool-", Path.GetFileName(resolved), StringComparison.Ordinal);
        Directory.Delete(resolved, recursive: true);
    }
}
