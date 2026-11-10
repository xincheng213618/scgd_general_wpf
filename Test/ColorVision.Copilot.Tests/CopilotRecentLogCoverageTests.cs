using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotRecentLogCoverageTests
{
    // Native DLLs write log4z .log files beside the managed .txt logs; hardware errors live only there.
    [Fact]
    public async Task NativeSourceShowsTheNewestRunAndSearchesEarlierRunsForAKeyword()
    {
        var directory = Directory.CreateTempSubdirectory("CopilotNativeLog-");
        try
        {
            string Write(string name, string text, int minutesAgo)
            {
                var path = Path.Combine(directory.FullName, name);
                File.WriteAllText(path, text);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
                return path;
            }

            var older = Write("ColorVision_Main_old.log", "LOG_ERROR spectrometer missing", 30);
            var newest = Write("ColorVision_Main_new.log", "LOG_INFO camera started", 10);
            Write("ColorVision_Main_empty.log", string.Empty, 1);
            Write("20261110.txt", "LOG_ERROR spectrometer missing in managed log", 0);

            var latest = await CopilotRecentLogSupport.CaptureNativeAsync([directory.FullName], null, 20, 4000, CancellationToken.None);
            Assert.Equal(newest, latest.FilePath);

            var found = await CopilotRecentLogSupport.CaptureNativeAsync([directory.FullName], "spectrometer", 20, 4000, CancellationToken.None);
            Assert.Equal(older, found.FilePath);
            Assert.Contains("earlier run", found.Summary, StringComparison.Ordinal);

            var missing = await CopilotRecentLogSupport.CaptureNativeAsync([directory.FullName], "lens", 20, 4000, CancellationToken.None);
            Assert.Equal(newest, missing.FilePath);
            Assert.Contains("searched the 2 newest runs", missing.Summary, StringComparison.Ordinal);

            // A match before the recent window in the newest run wins over an older run's match.
            File.WriteAllText(newest, "LOG_ERROR spectrometer timeout" + string.Concat(Enumerable.Repeat("\nLOG_INFO frame", 50)));
            File.SetLastWriteTimeUtc(newest, DateTime.UtcNow.AddMinutes(-10));
            var earlierInNewest = await CopilotRecentLogSupport.CaptureNativeAsync([directory.FullName], "spectrometer", 20, 4000, CancellationToken.None);
            Assert.Equal(newest, earlierInNewest.FilePath);
            Assert.Contains("earlier in the newest run", earlierInNewest.Summary, StringComparison.Ordinal);
            Assert.Contains("spectrometer timeout", earlierInNewest.Content, StringComparison.Ordinal);
            Assert.Equal(CopilotRecentLogMode.NativeRecentLines, CopilotRecentLogSupport.ResolveToolMode(" Native "));
            Assert.Equal(CopilotRecentLogMode.RecentLines, CopilotRecentLogSupport.ResolveToolMode(null));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task KeywordWithoutMatchesSaysTheLinesAreNotMatches()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "INFO camera connected\nERROR flow timeout\n");

            var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(path, maxChars: 1_000, query: "spectrometer");

            Assert.True(snapshot.Success, snapshot.ErrorMessage);
            Assert.Contains("matched no lines", snapshot.Summary, StringComparison.Ordinal);
            Assert.Contains("not matches", snapshot.Content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Native log4z logs are ANSI (GBK); per-line decoding must also handle a file that mixes both.
    [Fact]
    public async Task MixedEncodingLogKeepsBothUtf8AndNativeAnsiLinesReadable()
    {
        var path = Path.GetTempFileName();
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var bytes = new List<byte>();
            bytes.AddRange(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble());
            bytes.AddRange(Encoding.UTF8.GetBytes("INFO 相机已连接\r\n"));
            bytes.AddRange(Encoding.GetEncoding(936).GetBytes("LOG_ERROR 没有扫描到光谱仪!\r\n"));
            bytes.AddRange(Encoding.UTF8.GetBytes("INFO 流程完成\n"));
            await File.WriteAllBytesAsync(path, bytes.ToArray());

            var snapshot = await CopilotRecentLogSupport.CaptureFileAsync(path, maxChars: 1_000);

            Assert.True(snapshot.Success, snapshot.ErrorMessage);
            Assert.Contains("INFO 相机已连接", snapshot.Content, StringComparison.Ordinal);
            Assert.Contains("LOG_ERROR 没有扫描到光谱仪!", snapshot.Content, StringComparison.Ordinal);
            Assert.Contains("INFO 流程完成", snapshot.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("�", snapshot.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("﻿", snapshot.Content, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

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
