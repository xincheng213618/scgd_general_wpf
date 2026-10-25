using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecentLogCoverageTests
{
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
