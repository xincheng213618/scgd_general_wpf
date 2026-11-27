#pragma warning disable CA1707,CA1861
using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Models;
using System.Globalization;

namespace ColorVision.UI.Tests;

public class LogEntryParserTests
{
    [Theory]
    [InlineData("en-US", "2024-02-29 10:23:18,507", 2024)]
    [InlineData("en-US", "2023-02-29 10:23:18,507", 0)]
    [InlineData("en-US", "2024-02-29 24:23:18,507", 0)]
    [InlineData("th-TH", "2567-02-29 10:23:18,507", 2024)]
    [InlineData("en-US", "2024-02-29 10.23.18,507", 2024)]
    public void TryParseLogTimestamp_PreservesCalendarAndSeparatorRules(string cultureName, string prefix, int expectedYear)
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
            culture.DateTimeFormat.TimeSeparator = prefix[13].ToString();
            CultureInfo.CurrentCulture = culture;

            bool parsed = LogEntryParser.TryParseLogTimestamp(prefix + " [1] INFO Test  ready", out DateTime timestamp);

            Assert.Equal(expectedYear > 0, parsed);
            Assert.Equal(expectedYear == 0 ? default : new DateTime(expectedYear, 2, 29, 10, 23, 18, 507), timestamp);
            Assert.Equal(DateTimeKind.Unspecified, timestamp.Kind);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Theory]
    [InlineData("2026-06-03 10:00:00,000 [1] ERROR Test  failed", LogEntryLevel.Error)]
    [InlineData("2026-06-03 10:00:00,000 [1] FATAL Test  stopped", LogEntryLevel.Fatal)]
    [InlineData("2026-06-03 10:00:00,000 [1] WARN  Test  slow", LogEntryLevel.Warning)]
    [InlineData("2026-06-03 10:00:00,000 [1] INFO  Test  ready", LogEntryLevel.Info)]
    [InlineData("message without a level", LogEntryLevel.Unknown)]
    public void DetectLevel_UsesBoundedLogLevelTokens(string line, LogEntryLevel expectedLevel)
    {
        Assert.Equal(expectedLevel, LogEntryParser.DetectLevel(line));
    }

    [Fact]
    public void FromLines_GroupsContinuationLinesWithEntryLevel()
    {
        var entries = LogEntryParser.FromLines(new[]
        {
            "2026-06-03 10:00:00,000 [1] ERROR Test  failed",
            "stack line 1",
            "stack line 2",
            "2026-06-03 10:00:01,000 [1] INFO  Test  recovered"
        });

        Assert.Equal(2, entries.Count);
        Assert.Equal(LogEntryLevel.Error, entries[0].Level);
        Assert.Contains("stack line 2", entries[0].Text);
        Assert.Equal(LogEntryLevel.Info, entries[1].Level);
    }
}
