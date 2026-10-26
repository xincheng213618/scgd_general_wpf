using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecentLogCoverageTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TruncatedRecentLogsPreserveUnicodeScalarBoundaries(bool lineLimit)
    {
        var path = Path.GetTempFileName();
        try
        {
            var text = new string('a', lineLimit ? 8191 : 1000) + "\U00020BB7" + new string('b', lineLimit ? 12 : 999);
            await File.WriteAllTextAsync(path, text);

            var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(path, maxChars: lineLimit ? 20_000 : 1_000);

            Assert.True(snapshot.Success, snapshot.ErrorMessage);
            Assert.True(snapshot.ContentWasTruncated);
            Assert.Equal(snapshot.Content, string.Concat(snapshot.Content.EnumerateRunes().Select(rune => rune.ToString())));

            await File.WriteAllTextAsync(path, "RECOVERED \U00020BB7 suffix");
            var complete = await CopilotRecentLogSupport.CaptureFileAsync(path, maxChars: 1_000);
            Assert.True(complete.Success, complete.ErrorMessage);
            Assert.False(complete.ContentWasTruncated);
            Assert.Contains("RECOVERED \U00020BB7 suffix", complete.Content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(CopilotRecentLogMode.RecentLines)]
    [InlineData(CopilotRecentLogMode.FullDay)]
    public async Task FilteredMatchesDoNotHideAnOversizedLineThatCouldContainAnotherMatch(CopilotRecentLogMode mode)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, new string('x', 9000) + " CAM_FAIL primary failure\nCAM_FAIL visible match\n");

            var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(path, "CAM_FAIL", mode, maxLines: 2);

            Assert.True(snapshot.Success, snapshot.ErrorMessage);
            Assert.Equal(1, snapshot.FilteredLineCount);
            Assert.Contains("CAM_FAIL visible match", snapshot.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("primary failure", snapshot.Content, StringComparison.Ordinal);
            Assert.True(snapshot.ContentWasTruncated, "A displayed match cannot establish that an oversized nonmatching prefix contained no other match.");
            Assert.NotEmpty(CopilotRecentLogCapability.ToCapabilityResult(snapshot).ToToolResult("GetRecentLog").PartialResultMessage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task AFilteredBoundedTailReportsWhetherTheRequestedRecentWindowWasCovered(int requestedLines, bool incomplete)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, new string('x', 5 * 1024 * 1024) + "\nCAM_FAIL visible match\n");

            var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(path, "CAM_FAIL", maxLines: requestedLines);

            Assert.True(snapshot.Success, snapshot.ErrorMessage);
            Assert.False(snapshot.TotalLineCountIsExact);
            Assert.Equal(1, snapshot.FilteredLineCount);
            Assert.Equal(incomplete, snapshot.ContentWasTruncated);
            var result = CopilotRecentLogCapability.ToCapabilityResult(snapshot).ToToolResult("GetRecentLog");
            Assert.Equal(incomplete, !string.IsNullOrWhiteSpace(result.PartialResultMessage));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
