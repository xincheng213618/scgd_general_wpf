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

    [Theory]
    [InlineData("Alpha|Gamma", true, "", "a.cs:1,a.cs:3,b.txt:1")]
    [InlineData("Alpha", false, "*.cs", "a.cs:1")]
    [InlineData("Alpha", false, "**/*.{cs,txt}", "a.cs:1,b.txt:1")]
    [InlineData(@"al\w+a\s+end", true, "*.cs", "a.cs:1")]
    public void RegexAndGlobNarrowTheSearch(string query, bool regex, string glob, string expected)
    {
        File.WriteAllLines(Path.Combine(_directory.FullName, "a.cs"), ["alpha end", "beta", "gamma"]);
        File.WriteAllLines(Path.Combine(_directory.FullName, "b.txt"), ["ALPHA"]);

        var search = CopilotGrepTextCapability.SearchWithinScope(
            [_directory.FullName], [_directory.FullName], query, null, null, new CopilotTextSearchOptions(glob, regex), CancellationToken.None);

        Assert.True(search.Success, search.ErrorMessage);
        Assert.Equal(expected, string.Join(",", search.Matches.Select(match => $"{Path.GetFileName(match.FullPath)}:{match.LineNumber}")));
    }

    [Theory]
    [InlineData("(unclosed", true, "")]
    [InlineData("a(?=b)", true, "")]
    [InlineData("x*", true, "")]
    [InlineData("alpha", false, "src/*.cs")]
    [InlineData("alpha", false, "*.{cs")]
    public void InvalidSearchOptionsFailInsteadOfReportingNoMatches(string query, bool regex, string glob)
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "a.cs"), "alpha");

        var search = CopilotGrepTextCapability.SearchWithinScope(
            [_directory.FullName], [_directory.FullName], query, null, null, new CopilotTextSearchOptions(glob, regex), CancellationToken.None);

        Assert.False(search.Success);
        Assert.NotEmpty(search.ErrorMessage);
    }

    [Theory]
    [InlineData("*.csproj", "App.csproj,Lib.csproj")]
    [InlineData("**/*.csproj", "App.csproj,Lib.csproj")]
    [InlineData("src/**/*.cs", "Deep.cs,Main.cs")]
    [InlineData("deep/*.cs", "Deep.cs")]
    [InlineData("*.{csproj,txt}", "App.csproj,Lib.csproj,note.txt")]
    [InlineData("Ma?n.cs", "Main.cs")]
    [InlineData("Main", "Main.cs")]
    public void FileSearchTreatsWildcardsAsPatternsAndEverythingElseAsLiteral(string query, string expected)
    {
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "src", "deep"));
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "lib"));
        foreach (var file in new[] { "App.csproj", "note.txt", Path.Combine("lib", "Lib.csproj"), Path.Combine("src", "Main.cs"), Path.Combine("src", "deep", "Deep.cs") })
            File.WriteAllText(Path.Combine(_directory.FullName, file), "x");

        var search = CopilotSearchFilesCapability.Search([_directory.FullName], query, null, allowPlainSearchTerms: false, CancellationToken.None);

        Assert.True(search.Success, search.ErrorMessage);
        Assert.Equal(expected, string.Join(",", search.Matches.Select(match => Path.GetFileName(match.FullPath)).Order(StringComparer.OrdinalIgnoreCase)));
    }

    [Theory]
    [InlineData("Resources.resx")]
    [InlineData("index.html")]
    [InlineData("App.vue")]
    [InlineData("knowledge.mjs")]
    public void CompletedSearchCoversTheSourceFormatsThisRepositoryUses(string fileName)
    {
        File.WriteAllText(Path.Combine(_directory.FullName, fileName), "相机曝光");

        var search = CopilotGrepTextCapability.Search([_directory.FullName], "相机曝光", null, CancellationToken.None);

        Assert.Single(search.Matches);
    }

    [Fact]
    public void BinaryDatFileDoesNotMakeASearchPartial()
    {
        File.WriteAllBytes(Path.Combine(_directory.FullName, "curve.dat"), [1, 0, 2, 0]);
        File.WriteAllText(Path.Combine(_directory.FullName, "a.txt"), "needle");

        var search = CopilotGrepTextCapability.Search([_directory.FullName], "needle", null, CancellationToken.None);

        Assert.True(search.ScanComplete);
        Assert.Single(search.Matches);
    }

    [Fact]
    public void ALiteralQueryThatLooksLikeARegexExplainsItsEmptyResult()
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "a.cs"), "alpha");

        var search = CopilotGrepTextCapability.Search([_directory.FullName], "alpha|beta", null, CancellationToken.None);

        Assert.True(search.Success);
        Assert.Contains("regex to true", search.Content);
    }

    [Fact]
    public async Task GrepToolBindsRegexAndGlobFromItsSchema()
    {
        File.WriteAllLines(Path.Combine(_directory.FullName, "a.cs"), ["alpha", "beta"]);
        File.WriteAllLines(Path.Combine(_directory.FullName, "b.txt"), ["alpha"]);
        var tool = new CopilotGrepTextTool();
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["query"] = "a", ["regex"] = "true" }, out _, out var error));
        Assert.Contains("boolean", error);
        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["query"] = "alpha|beta", ["regex"] = true, ["glob"] = "*.cs" }, out var input, out error), error);

        var result = await tool.ExecuteAsync(new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto, UserText = "search", SearchRootPaths = [_directory.FullName],
        }, input, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("a.cs:2", result.Content);
        Assert.DoesNotContain("b.txt", result.Content);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(5000)]
    public void AgentLineShowsTheMatchEvenDeepInALongLine(int prefixLength)
    {
        File.WriteAllText(Path.Combine(_directory.FullName, "wide.xaml"), new string('x', prefixLength) + "NEEDLE" + new string('y', 5000));

        var search = CopilotGrepTextCapability.Search([_directory.FullName], "needle", null, CancellationToken.None);

        Assert.Contains("NEEDLE", Assert.Single(search.Matches).AgentLine);
    }

    [Theory]
    [InlineData("%PDF-1.7\nnot really a pdf", "PDF")]
    [InlineData("PK\u0003\u0004body", "zip")]
    public async Task DocumentFormatsAreNamedInsteadOfReportedAsBadEncoding(string header, string expected)
    {
        var path = Path.Combine(_directory.FullName, "manual.dat");
        File.WriteAllText(path, header);

        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(expected, result.ErrorMessage);
    }

    [Fact]
    public async Task Gb18030FileIsReadableAndSearchableButNotPatchable()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = Path.Combine(_directory.FullName, "device.ini");
        var bytes = Encoding.GetEncoding(54936).GetBytes("[相机]\r\n曝光=100\r\n");
        File.WriteAllBytes(path, bytes);

        var read = await ReadToolAsync(path, 1, 2);
        var search = CopilotGrepTextCapability.Search([_directory.FullName], "曝光", null, CancellationToken.None);
        var request = new CopilotAgentRequest { WorkspacePath = _directory.FullName, Mode = CopilotAgentMode.Code, UserText = "patch", WritableLocalRootPaths = [_directory.FullName], SearchRootPaths = [_directory.FullName] };
        var patch = await new CopilotWorkspacePatchStore().PreviewPatchEnvelopeAsync(request, new()
        {
            Arguments = new Dictionary<string, object?> { ["operations"] = new[] { new { operation = "update", path, oldText = "100", newText = "200" } } },
        }, CancellationToken.None);

        Assert.True(read.Success, read.ErrorMessage);
        Assert.Contains("L2: 曝光=100", read.Content);
        Assert.Contains("[Encoding] GB18030", read.Content);
        Assert.True(search.ScanComplete);
        Assert.Equal(2, Assert.Single(search.Matches).LineNumber);
        Assert.Contains("GB18030", search.Content);
        Assert.False(patch.Success);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public void SearchesSkipGitIgnoredPathsUnlessScopedInsideThem()
    {
        var git = CopilotGitProcessSupport.FindTrustedGitExecutable();
        Assert.False(string.IsNullOrWhiteSpace(git));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(git!, "init -q") { WorkingDirectory = _directory.FullName, CreateNoWindow = true })!.WaitForExit();
        File.WriteAllText(Path.Combine(_directory.FullName, ".gitignore"), "dist/\n*.tmp.txt\n");
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "dist", "nested"));
        File.WriteAllText(Path.Combine(_directory.FullName, "dist", "nested", "out.txt"), "needle");
        File.WriteAllText(Path.Combine(_directory.FullName, "scratch.tmp.txt"), "needle");
        File.WriteAllText(Path.Combine(_directory.FullName, "kept.txt"), "needle");

        var whole = CopilotGrepTextCapability.Search([_directory.FullName], "needle", null, CancellationToken.None);
        var scoped = CopilotGrepTextCapability.Search([Path.Combine(_directory.FullName, "dist")], "needle", null, CancellationToken.None);
        var files = CopilotSearchFilesCapability.Search([_directory.FullName], "*.txt", null, allowPlainSearchTerms: false, CancellationToken.None);
        Assert.True(whole.ScanComplete);
        Assert.Equal("kept.txt", Path.GetFileName(Assert.Single(whole.Matches).FullPath));
        Assert.Equal("out.txt", Path.GetFileName(Assert.Single(scoped.Matches).FullPath));
        Assert.Equal("kept.txt", Path.GetFileName(Assert.Single(files.Matches).FullPath));
    }

    [Fact]
    public async Task PdfTextIsReadByPageAndContinuesByLine()
    {
        var path = Path.Combine(_directory.FullName, "manual.pdf");
        File.WriteAllBytes(path, BuildPdf("Exposure range 1 to 60000 us", "Gain range 0 to 24 dB"));

        var whole = await ReadToolAsync(path, 1, 100);
        var scope = Assert.Single(whole.LocalFileReadScopes);
        var later = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 4, 100, CancellationToken.None);

        Assert.True(whole.Success, whole.ErrorMessage);
        Assert.Contains("[Page 1 of 2]", whole.Content);
        Assert.Contains("Exposure range", whole.Content);
        Assert.Contains("[Page 2 of 2]", whole.Content);
        Assert.Contains("Gain range", whole.Content);
        Assert.False(scope.WasTruncated);
        Assert.True(later.Success, later.ErrorMessage);
        Assert.DoesNotContain("Exposure range", later.Content);
    }

    [Fact]
    public async Task PdfWithoutATextLayerSaysSoInsteadOfReturningEmptySuccess()
    {
        var path = Path.Combine(_directory.FullName, "scan.pdf");
        File.WriteAllBytes(path, BuildPdf());

        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("no extractable text", result.ErrorMessage);
    }

    [Fact]
    public void GrepDoesNotScanPdfsSoTheyCannotMakeASearchLookPartial()
    {
        File.WriteAllBytes(Path.Combine(_directory.FullName, "manual.pdf"), BuildPdf("needle"));
        File.WriteAllText(Path.Combine(_directory.FullName, "a.txt"), "needle");

        var search = CopilotGrepTextCapability.Search([_directory.FullName], "needle", null, CancellationToken.None);

        Assert.True(search.ScanComplete);
        Assert.Equal("a.txt", Path.GetFileName(Assert.Single(search.Matches).FullPath));
        Assert.False(CopilotWorkspaceSearchSupport.IsTextLikeFile("manual.pdf"));
    }

    private static byte[] BuildPdf(params string[] pageTexts)
    {
        var builder = new UglyToad.PdfPig.Writer.PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        foreach (var text in pageTexts.DefaultIfEmpty(null))
        {
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            if (text != null)
                page.AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        }
        return builder.Build();
    }

    [Fact]
    public async Task EmptyFileReadsAsEmptyWhileLaterLinesStayBeyondIt()
    {
        var path = Path.Combine(_directory.FullName, "empty.log");
        File.WriteAllText(path, string.Empty);

        var result = await ReadToolAsync(path, 1, 1);
        var beyond = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, 2, 2, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("observed_total_lines: 0", result.Content);
        Assert.Contains("<empty>", result.Content);
        Assert.False(beyond.Success);
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task LiteralTruncationMarkersKeepTheirSourceLineNumbers(bool truncate, bool markerAtEnd)
    {
        const string literalMarker = "...<content truncated; kept the first 42 characters.>";
        const string summary = "SUMMARY result=NG";
        const int readBudget = 1000;
        var path = Path.Combine(_directory.FullName, "literal-marker.log");
        var source = markerAtEnd
            ? $"header\n{summary}\n{literalMarker}"
            : $"header\n{literalMarker}\n{summary}";
        if (truncate)
            source += "\n" + new string('x', readBudget);
        File.WriteAllText(path, source);
        var original = File.ReadAllBytes(path);
        var search = CopilotGrepTextCapability.Search([_directory.FullName], summary, null, CancellationToken.None);
        var summaryLine = Assert.Single(search.Matches).LineNumber;

        var result = await new CopilotReadLocalFileTool(readBudget).ExecuteAsync(new()
        {
            Mode = CopilotAgentMode.Auto, UserText = "读取日志", SearchRootPaths = [_directory.FullName],
        }, new() { Path = path }, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(truncate, Assert.Single(result.LocalFileReadScopes).WasTruncated);
        Assert.Contains($"L{(markerAtEnd ? 3 : 2)}: {literalMarker}", result.Content);
        Assert.Contains($"L{summaryLine}: {summary}", result.Content);
        if (truncate)
            Assert.EndsWith($"{Environment.NewLine}...<content truncated; kept the first {readBudget} characters.>", result.Content);
        else
            Assert.DoesNotContain($"{Environment.NewLine}{literalMarker}", result.Content);
        Assert.Equal(original, File.ReadAllBytes(path));
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
