using ColorVision.Copilot.Mcp;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTextFileCoordinateTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotTextCoordinates-");

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("\r")]
    public async Task SearchCoordinatesSelectTheSameSourceLine(string separator)
    {
        var path = Path.Combine(_directory.FullName, "capture.log");
        File.WriteAllText(path, "INFO start" + separator + "SUMMARY serial=CV-721 result=NG" + separator + "INFO end" + separator);
        var original = File.ReadAllBytes(path);
        var search = CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None);
        var match = Assert.Single(search.Matches);
        Assert.Equal(2, match.LineNumber);

        var result = await ReadToolAsync(path, match.LineNumber, match.LineNumber);

        Assert.True(result.Success, result.ErrorMessage);
        var scope = Assert.Single(result.LocalFileReadScopes);
        Assert.Equal(2, scope.StartLine);
        Assert.Equal(2, scope.EndLine);
        Assert.Contains("L2: SUMMARY serial=CV-721 result=NG", result.Content);
        Assert.DoesNotContain("INFO", result.Content);
        Assert.Equal(original, File.ReadAllBytes(path));
        var beyond = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 4, 4, CancellationToken.None);
        Assert.False(beyond.Success);
        Assert.Contains("beyond", beyond.ErrorMessage);
    }

    [Fact]
    public async Task MixedLineEndingsAndEmptyLinesShareOneCoordinateSystem()
    {
        var path = Path.Combine(_directory.FullName, "mixed.log");
        File.WriteAllText(path, "header\r\n\rSUMMARY result=OK\n\r\nlast\r");
        var search = CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None);
        var match = Assert.Single(search.Matches);
        Assert.Equal(3, match.LineNumber);

        var result = await ReadToolAsync(path, match.LineNumber, match.LineNumber);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("L3: SUMMARY result=OK", result.Content);
        Assert.DoesNotContain("last", result.Content);
        Assert.Equal(3, Assert.Single(result.LocalFileReadScopes).EndLine);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task CarriageReturnAtReadBufferBoundaryKeepsTheNextLineAddressable(string separator)
    {
        var path = Path.Combine(_directory.FullName, "buffer.log");
        File.WriteAllText(path, new string('x', 4095) + separator + "SUMMARY result=OK" + separator);
        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 2, 2, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("SUMMARY result=OK", result.Content);
        Assert.Equal(2, result.StartLine);
        Assert.Equal(2, result.EndLine);
    }

    [Fact]
    public async Task TruncationDoesNotSeparateCrLfOrShiftContinuationCoordinates()
    {
        var path = Path.Combine(_directory.FullName, "boundary.log");
        File.WriteAllText(path, new string('x', 999) + "\r\nSUMMARY result=OK\r\n");
        var first = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, null, null, null, 1000, CancellationToken.None);
        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(first.WasTruncated);
        Assert.Equal(1, first.ContinuationStartLine);
        Assert.Equal(1000, first.ContinuationStartColumn);
        Assert.Equal(999, first.EndColumn);

        var next = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, first.ContinuationStartLine, first.ContinuationStartColumn, null, 1000, CancellationToken.None);
        Assert.True(next.Success, next.ErrorMessage);
        Assert.Equal("\r\nSUMMARY result=OK", next.Content);
        Assert.Equal(2, next.EndLine);
        Assert.False(next.WasTruncated);
    }

    [Theory]
    [InlineData(".csv")]
    [InlineData(".tsv")]
    [InlineData(".jsonl")]
    [InlineData(".ndjson")]
    [InlineData(".CSV")]
    public async Task BusinessExportsAreDiscoverableAndReadableWithoutExpandingWriteTypes(string extension)
    {
        var path = Path.Combine(_directory.FullName, "results" + extension);
        File.WriteAllText(path, "serial,result\nCV-721,NG\n");
        var original = File.ReadAllBytes(path);
        var search = CopilotGrepTextCapability.Search([_directory.FullName], "CV-721", null, CancellationToken.None);
        Assert.True(search.ScanComplete);
        Assert.Equal(2, Assert.Single(search.Matches).LineNumber);
        var listing = CopilotListDirectoryCapability.List([_directory.FullName], _directory.FullName, CancellationToken.None);
        Assert.Contains(path, listing.SuggestedReadableLocalFilePaths);
        var fileSearch = CopilotSearchFilesCapability.Search([_directory.FullName], Path.GetFileName(path), null, true, CancellationToken.None);
        Assert.Contains(path, fileSearch.SuggestedReadableLocalFilePaths);

        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new() { SolutionDirectoryPath = _directory.FullName, SearchRootPaths = [_directory.FullName] },
        });
        var result = await dispatcher.CallAsync("read_allowed_file", new Dictionary<string, JsonElement>
        {
            ["path"] = JsonSerializer.SerializeToElement(Path.GetFileName(path)),
            ["start_line"] = JsonSerializer.SerializeToElement(2),
            ["end_line"] = JsonSerializer.SerializeToElement(2),
        }, CancellationToken.None);
        Assert.True(result.Success, result.Text);
        Assert.Contains("L2: CV-721,NG", result.Text);

        var request = new CopilotAgentRequest { WorkspacePath = _directory.FullName, WritableLocalRootPaths = [_directory.FullName] };
        Assert.False(CopilotWorkspacePatchScope.TryResolve(request, path, 20_000, out _, out _));
        var proposed = Path.Combine(_directory.FullName, "created" + extension);
        Assert.False(CopilotWorkspacePatchScope.TryResolveNewFile(request, proposed, out _, out _, out _));
        Assert.False(File.Exists(proposed));
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    private Task<CopilotToolResult> ReadToolAsync(string path, int start, int end) => new CopilotReadLocalFileTool().ExecuteAsync(new()
    {
        Mode = CopilotAgentMode.Auto, UserText = "读取日志", SearchRootPaths = [_directory.FullName],
    }, new() { Path = path, StartLine = start, EndLine = end }, CancellationToken.None);

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("utf32")]
    public async Task EveryMixedNewlineRecordCanBeReadBackByItsPhysicalLine(string encodingName)
    {
        var encoding = encodingName switch { "utf16" => Encoding.Unicode, "utf32" => Encoding.UTF32, _ => Encoding.UTF8 };
        var lines = Enumerable.Range(1, 31).Select(i => i % 7 == 0 ? string.Empty : $"record={i} 设备🔬 value={i * 3}").ToArray();
        var source = string.Concat(lines.Select((line, i) => line + ((i % 3) switch { 0 => "\r", 1 => "\r\n", _ => "\n" })));
        var path = Path.Combine(_directory.FullName, "records.log");
        await File.WriteAllTextAsync(path, source, encoding);

        for (var index = 0; index < lines.Length; index++)
        {
            var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, index + 1, index + 1, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(lines[index], result.Content);
            Assert.Equal(index + 1, result.StartLine);
            Assert.Equal(index + 1, result.EndLine);
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public async Task OutOfRangeColumnDoesNotSilentlySelectTheNextLine(string separator)
    {
        var path = Path.Combine(_directory.FullName, "column.log");
        File.WriteAllText(path, "first" + separator + "SUMMARY result=NG");
        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 1, 99, null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Contains("Requested start column 99", result.ErrorMessage);
    }

    [Theory]
    [InlineData("first\nlast", 2)]
    [InlineData("first\nlast\n", 2)]
    [InlineData("first\rlast\r", 2)]
    [InlineData("first\r\nlast\r\n", 2)]
    [InlineData("first\r\n\rlast\n\r\n", 4)]
    [InlineData("\n", 1)]
    [InlineData("", 0)]
    public async Task ObservedFileEndCarriesTheExactLineCountEvenForOutOfRangeReads(string source, int expectedLines)
    {
        var path = Path.Combine(_directory.FullName, "boundary.log");
        File.WriteAllText(path, source);
        var original = File.ReadAllBytes(path);
        var full = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);
        Assert.Equal(expectedLines, full.ObservedTotalLineCount);
        var beyond = await ReadToolAsync(path, expectedLines + 10, expectedLines + 20);
        Assert.False(beyond.Success);
        Assert.Contains($"({expectedLines} at read time)", beyond.ErrorMessage);
        Assert.Contains("end_of_file_observed: true", beyond.Content);
        Assert.Contains($"observed_total_lines: {expectedLines}", beyond.Content);
        Assert.Empty(beyond.SuccessfullyReadLocalFilePaths);
        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16")]
    [InlineData("utf32")]
    public async Task TailReadReportsEofWithoutInventingItForAnEarlierFocusedRange(string encodingName)
    {
        var path = Path.Combine(_directory.FullName, "tail.log");
        var encoding = encodingName switch { "utf16" => Encoding.Unicode, "utf32" => Encoding.UTF32, _ => Encoding.UTF8 };
        File.WriteAllText(path, string.Concat(Enumerable.Range(1, 2200).Select(i => $"frame={i} 相机🔬" + ((i % 3) switch { 0 => "\r\n", 1 => "\r", _ => "\n" }))), encoding);
        var middle = await ReadToolAsync(path, 1700, 1720);
        Assert.True(middle.Success);
        Assert.DoesNotContain("end_of_file_observed", middle.Content);
        Assert.DoesNotContain("observed_total_lines", middle.Content);
        var tail = await ReadToolAsync(path, 2200, 2220);
        Assert.True(tail.Success);
        Assert.Contains("end_of_file_observed: true", tail.Content);
        Assert.Contains("observed_total_lines: 2200", tail.Content);
        Assert.Contains("L2200: frame=2200", tail.Content);
    }

    [Fact]
    public async Task BoundedAndInvalidColumnReadsDoNotClaimAnUnobservedFileEnd()
    {
        var path = Path.Combine(_directory.FullName, "bounded.log");
        File.WriteAllText(path, new string('x', 21_000) + "\nlast\n");
        var bounded = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);
        Assert.True(bounded.WasTruncated);
        Assert.Null(bounded.ObservedTotalLineCount);
        var invalidColumn = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 1, 30_000, null, CancellationToken.None);
        Assert.False(invalidColumn.Success);
        Assert.Null(invalidColumn.ObservedTotalLineCount);
        var exactRange = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 2, 2, CancellationToken.None);
        Assert.True(exactRange.Success);
        Assert.Null(exactRange.ObservedTotalLineCount);
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotTextCoordinates-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
