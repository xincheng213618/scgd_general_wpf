using ColorVision.UI.LogImp;
using System.IO;

namespace ColorVision.UI.Tests;

public class LogHistoryReaderTests
{
    private static readonly string[] ContinuationLogLines =
    {
        "2026-06-03 08:59:59,000 [1] INFO  Test  old entry",
        "old continuation",
        "2026-06-03 09:00:00,000 [1] INFO  Test  new entry",
        "new continuation"
    };

    [Fact]
    public void ReadDisplayText_KeepsContinuationLinesForIncludedEntries()
    {
        var logText = string.Join(Environment.NewLine, ContinuationLogLines);

        var result = LogHistoryReader.ReadDisplayText(
            new StringReader(logText),
            LogLoadState.SinceStartup,
            reverse: false,
            maxChars: -1,
            today: new DateTime(2026, 6, 3),
            startupTime: new DateTime(2026, 6, 3, 9, 0, 0));

        Assert.DoesNotContain("old continuation", result);
        Assert.Contains("new entry", result);
        Assert.Contains("new continuation", result);
    }

    [Fact]
    public void ReadDisplayText_WithMaxCharsKeepsNewestForwardEntries()
    {
        var logText = BuildLogText(80);

        var result = LogHistoryReader.ReadDisplayText(
            new StringReader(logText),
            LogLoadState.AllToday,
            reverse: false,
            maxChars: 1050,
            today: new DateTime(2026, 6, 3),
            startupTime: DateTime.MinValue);

        Assert.True(result.Length <= 1050);
        Assert.DoesNotContain("msg-000", result);
        Assert.Contains("msg-079", result);
    }

    [Fact]
    public void ReadDisplayText_WithMaxCharsKeepsNewestReverseEntriesAtTop()
    {
        var logText = BuildLogText(80);

        var result = LogHistoryReader.ReadDisplayText(
            new StringReader(logText),
            LogLoadState.AllToday,
            reverse: true,
            maxChars: 1050,
            today: new DateTime(2026, 6, 3),
            startupTime: DateTime.MinValue);

        Assert.True(result.Length <= 1050);
        Assert.StartsWith(Timestamp(79), result);
        Assert.DoesNotContain("msg-000", result);
    }

    private static string BuildLogText(int count)
    {
        return string.Join(Environment.NewLine, Enumerable.Range(0, count).Select(index =>
            $"{Timestamp(index)} [1] INFO  Test  msg-{index:000} {new string('x', 80)}"));
    }

    private static string Timestamp(int index)
    {
        return $"2026-06-03 10:{index / 60:00}:{index % 60:00},000";
    }
}
