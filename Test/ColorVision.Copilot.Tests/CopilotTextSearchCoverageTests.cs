using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTextSearchCoverageTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotTextSearchCoverageTests-");

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void OversizedLogDoesNotProveThatTheQueryIsAbsent(bool exactFile, bool validMatch)
    {
        var path = Path.Combine(_directory.FullName, "large.log");
        using (var stream = File.Create(path))
            stream.SetLength(8 * 1024 * 1024 + 1);
        if (validMatch)
            File.WriteAllText(Path.Combine(_directory.FullName, "small.log"), "SUMMARY result=OK");

        var result = Search(exactFile ? path : _directory.FullName);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.ScanComplete);
        Assert.False(result.ResultsComplete);
        Assert.Contains("large.log", result.Content);
        Assert.Contains("8 MiB", result.Content);
        Assert.Contains("[Search Warning]", result.Content);
        Assert.Equal(validMatch ? 1 : 0, result.Matches.Count);
        Assert.DoesNotContain("completed search scope", result.Content);
    }

    [Theory]
    [InlineData("utf8", false)]
    [InlineData("utf8-bom", false)]
    [InlineData("utf16-le", false)]
    [InlineData("utf16-be", false)]
    [InlineData("utf32-le", false)]
    [InlineData("utf32-be", false)]
    [InlineData("utf8", true)]
    [InlineData("utf8-bom", true)]
    [InlineData("utf16-le", true)]
    [InlineData("utf16-be", true)]
    [InlineData("utf32-le", true)]
    [InlineData("utf32-be", true)]
    public void InvalidTextDoesNotSupplySuccessfulMatchesOrAbsenceEvidence(string encodingName, bool nul)
    {
        Encoding encoding = encodingName switch
        {
            "utf8" => new UTF8Encoding(false),
            "utf8-bom" => new UTF8Encoding(true),
            "utf16-le" => Encoding.Unicode,
            "utf16-be" => Encoding.BigEndianUnicode,
            "utf32-le" => Encoding.UTF32,
            "utf32-be" => new UTF32Encoding(true, true),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingName)),
        };
        var path = Path.Combine(_directory.FullName, "damaged.log");
        var body = encoding.GetBytes("SUMMARY result=NG\n" + new string('x', 6000) + (nul ? "\0tail" : string.Empty));
        byte[] bytes = nul ? [.. encoding.GetPreamble(), .. body] : [.. encoding.GetPreamble(), .. body, 0xff];
        File.WriteAllBytes(path, bytes);
        File.WriteAllText(Path.Combine(_directory.FullName, "valid.log"), "SUMMARY result=OK");

        var result = Search(_directory.FullName);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.ScanComplete);
        Assert.False(result.ResultsComplete);
        Assert.EndsWith("valid.log", Assert.Single(result.Matches).FullPath);
        Assert.DoesNotContain(path, result.SuggestedReadableLocalFilePaths);
        Assert.Contains("damaged.log", result.Content);
        Assert.Contains(nul ? "NUL" : "encoding", result.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Search Warning]", result.Content);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void ContinuationRetainsUnreadableFileEvidenceFromEarlierPages()
    {
        var lockedPath = Path.Combine(_directory.FullName, "a-locked.log");
        File.WriteAllText(lockedPath, "SUMMARY result=NG");
        File.WriteAllLines(Path.Combine(_directory.FullName, "b-readable.log"), Enumerable.Range(1, 45).Select(n => $"SUMMARY item={n}"));
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var first = Search(_directory.FullName);
        Assert.True(first.Success);
        Assert.Equal(40, first.Matches.Count);
        Assert.False(first.ScanComplete);
        Assert.NotEmpty(first.NextCursor);
        Assert.Contains("a-locked.log", first.Content);

        var last = Search(_directory.FullName, first.NextCursor);
        Assert.True(last.Success, last.ErrorMessage);
        Assert.Equal(5, last.Matches.Count);
        Assert.False(last.ScanComplete);
        Assert.False(last.ResultsComplete);
        Assert.False(last.ResultsTruncated);
        Assert.Empty(last.NextCursor);
        Assert.Contains("earlier page", last.Content);
    }

    [Fact]
    public void CleanPaginationStillCompletesAndRejectsChangedFiles()
    {
        var path = Path.Combine(_directory.FullName, "capture.log");
        File.WriteAllLines(path, Enumerable.Range(1, 45).Select(n => $"SUMMARY item={n}"));

        var first = Search(_directory.FullName);
        var last = Search(_directory.FullName, first.NextCursor);

        Assert.False(first.ScanComplete);
        Assert.True(last.ScanComplete);
        Assert.True(last.ResultsComplete);
        Assert.Equal(Enumerable.Range(1, 45), first.Matches.Concat(last.Matches).Select(match => match.LineNumber));
        File.AppendAllText(path, "SUMMARY changed");
        Assert.False(Search(_directory.FullName, first.NextCursor).Success);
    }

    [Fact]
    public async Task ExactUnsupportedFileReturnsVisibleScopeGapThroughRealTool()
    {
        var path = Path.Combine(_directory.FullName, "capture.custom");
        File.WriteAllText(path, "SUMMARY result=NG");
        var result = await new CopilotGrepTextTool().ExecuteAsync(new CopilotAgentRequest
        {
            UserText = "搜索 capture.custom 中的 SUMMARY",
            SearchRootPaths = [_directory.FullName],
        }, new CopilotAgentToolInput { Path = "capture.custom", Query = "SUMMARY" }, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("[Scan Complete] false", result.Content);
        Assert.Contains("capture.custom", result.Content);
        Assert.Contains("extension", result.Content);
        Assert.Contains("ReadLocalFile", result.Content);
    }

    private CopilotTextSearchResult Search(string path, string? cursor = null) => CopilotGrepTextCapability.SearchWithinScope(
        [path], [_directory.FullName], "SUMMARY", null, cursor, CancellationToken.None);

    [Fact]
    public void DirectoryDisappearingDuringEnumerationReportsTheScopeGap()
    {
        var nested = Directory.CreateDirectory(Path.Combine(_directory.FullName, "unavailable"));
        File.WriteAllText(Path.Combine(_directory.FullName, "first.log"), "SUMMARY");
        var failures = new List<string>();
        using var files = CopilotWorkspaceSearchSupport.EnumerateFiles([_directory.FullName], true, CancellationToken.None,
            (path, _) => failures.Add(path)).GetEnumerator();

        Assert.True(files.MoveNext());
        // Only an empty, directly owned temporary directory is removed.
        var target = Path.GetFullPath(nested.FullName);
        Assert.Equal(_directory.FullName, Path.GetDirectoryName(target));
        Directory.Delete(target);
        Assert.False(files.MoveNext());
        Assert.Contains(target, failures);
    }

    [Fact]
    public void IgnoredDependenciesDoNotMakeTheDeclaredTextScopeIncomplete()
    {
        var dependency = Directory.CreateDirectory(Path.Combine(_directory.FullName, "node_modules"));
        File.WriteAllText(Path.Combine(dependency.FullName, "ignored.log"), "SUMMARY dependency");
        File.WriteAllText(Path.Combine(_directory.FullName, "normal.log"), "nothing matched");

        var result = Search(_directory.FullName);

        Assert.True(result.ScanComplete);
        Assert.Empty(result.Matches);
        Assert.Contains("[Search Policy]", result.Content);
        Assert.DoesNotContain("[Search Warning]", result.Content);
    }

    [Fact]
    public void CancelledSearchDoesNotReturnSuccessfulAbsenceEvidence()
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "normal.log"), "no match");
        Assert.Throws<OperationCanceledException>(() => CopilotGrepTextCapability.Search(
            [_directory.FullName], "SUMMARY", null, new CancellationToken(true)));
    }

    [Theory]
    [InlineData(":-1")]
    [InlineData(":2")]
    [InlineData("")]
    public void InvalidOrLegacyCursorCannotLoseCoverageState(string suffix)
    {
        File.WriteAllLines(Path.Combine(_directory.FullName, "capture.log"), Enumerable.Repeat("SUMMARY", 45));
        var first = Search(_directory.FullName);
        var legacy = first.NextCursor[..first.NextCursor.LastIndexOf(':')];
        var result = Search(_directory.FullName, legacy + suffix);
        Assert.False(result.Success);
        Assert.Contains("Restart", result.ErrorMessage);
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotTextSearchCoverageTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
