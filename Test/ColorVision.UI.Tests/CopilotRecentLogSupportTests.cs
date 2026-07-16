#pragma warning disable CA1707
using ColorVision.Copilot;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotRecentLogSupportTests : IDisposable
{
    private readonly string _tempRoot;

    public CopilotRecentLogSupportTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVisionCopilotLogs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task CaptureFileAsync_ReturnsOnlyRequestedRecentLines()
    {
        var filePath = Path.Combine(_tempRoot, "recent.txt");
        await File.WriteAllLinesAsync(filePath, CreateNumberedLines(100));

        var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(
            filePath,
            mode: CopilotRecentLogMode.RecentLines,
            maxLines: 10,
            maxChars: 2000,
            cancellationToken: CancellationToken.None);

        Assert.True(snapshot.Success);
        Assert.Equal(100, snapshot.TotalLineCount);
        Assert.Equal(10, snapshot.DisplayedLineCount);
        Assert.Contains("line-099", snapshot.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("line-089", snapshot.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureFileAsync_UsesBoundedTailScanForLargeRecentLog()
    {
        var filePath = Path.Combine(_tempRoot, "large-recent.txt");
        await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        await using (var writer = new StreamWriter(stream))
        {
            for (var index = 0; index < 500_000; index++)
                await writer.WriteLineAsync($"entry-{index:D6}");
        }

        var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(
            filePath,
            mode: CopilotRecentLogMode.RecentLines,
            maxLines: 10,
            maxChars: 2000,
            cancellationToken: CancellationToken.None);

        Assert.True(snapshot.Success);
        Assert.False(snapshot.TotalLineCountIsExact);
        Assert.Equal(10, snapshot.DisplayedLineCount);
        Assert.Contains("entry-499999", snapshot.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("entry-000000", snapshot.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureFileAsync_BoundsOversizedSingleLineAndKeepsNewestEvidence()
    {
        var filePath = Path.Combine(_tempRoot, "large-line.txt");
        await File.WriteAllTextAsync(
            filePath,
            new string('x', 100_000) + Environment.NewLine + "SECOND-LAST" + Environment.NewLine + "LAST-SENTINEL");

        var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(
            filePath,
            mode: CopilotRecentLogMode.RecentLines,
            maxLines: 3,
            maxChars: 512,
            cancellationToken: CancellationToken.None);

        Assert.True(snapshot.Success);
        Assert.True(snapshot.ContentWasTruncated);
        Assert.Contains("older or oversized log entries omitted", snapshot.Content, StringComparison.Ordinal);
        Assert.Contains("LAST-SENTINEL", snapshot.Content, StringComparison.Ordinal);
        Assert.True(snapshot.Content.Length < 1000);
    }

    [Fact]
    public async Task CaptureFileAsync_FilterReturnsLatestMatchesAndReportsTotalMatchCount()
    {
        var filePath = Path.Combine(_tempRoot, "filtered.txt");
        await File.WriteAllLinesAsync(filePath, CreateNumberedLines(200, "ERROR-"));

        var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(
            filePath,
            query: "ERROR",
            mode: CopilotRecentLogMode.FullDay,
            maxLines: 300,
            maxChars: 20000,
            cancellationToken: CancellationToken.None);

        Assert.True(snapshot.Success);
        Assert.Equal(200, snapshot.FilteredLineCount);
        Assert.Equal(120, snapshot.DisplayedLineCount);
        Assert.True(snapshot.ContentWasTruncated);
        Assert.Contains("ERROR-199", snapshot.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR-000", snapshot.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureFileAsync_PropagatesCancellationDuringScan()
    {
        var filePath = Path.Combine(_tempRoot, "cancellation.txt");
        await File.WriteAllLinesAsync(filePath, CreateNumberedLines(250_000));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CopilotRecentLogSupport.CaptureFileAsync(
            filePath,
            mode: CopilotRecentLogMode.FullDay,
            maxLines: 300,
            maxChars: 20000,
            cancellationToken: cancellation.Token));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }

    private static string[] CreateNumberedLines(int count, string prefix = "line-")
    {
        var lines = new string[count];
        for (var index = 0; index < count; index++)
            lines[index] = $"{prefix}{index:D3}";
        return lines;
    }
}
