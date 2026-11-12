using ColorVision.SocketProtocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.IntegrationDemo;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Demura;
using System.IO;
using System.Text;
using System.Threading;

namespace ProjectARVRPro.Tests;

public class DemuraResultJsonTests
{
    // CSV delivery must not inherit the tool/burning requirements, including when loading old configs.
    [Fact]
    public void CsvOnlyDefaultsOnAndFullProcessingCanBeRestoredExplicitly()
    {
        var process = new DemuraProcess();
        Assert.True(process.Config.ReturnCsvOnly);
        process.SetProcessConfig("""{"PrepareDemuraTool":true,"BurnAfterGenerate":true}""");
        Assert.True(process.Config.ReturnCsvOnly);

        process.Config.ReturnCsvOnly = false;
        var restored = new DemuraProcess();
        restored.SetProcessConfig(JsonConvert.SerializeObject(process.Config));
        Assert.False(restored.Config.ReturnCsvOnly);
        Assert.True(restored.Config.PrepareDemuraTool);
        Assert.True(restored.Config.BurnAfterGenerate);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CsvOnlyRequiresOnlyTheW128File(bool fileExists)
    {
        var process = new DemuraProcess();
        process.Config.RequireMergedBin = true;
        var view = new DemuraViewTestResult
        {
            W128 = new DemuraCsvFileResult { SourceExists = fileExists, ValueCount = 1 }
        };

        process.RebuildItems(view);

        ObjectiveTestItem item = Assert.Single(view.Items);
        Assert.Equal("W128FileExists", item.Name);
        Assert.Equal(fileExists, item.TestResult);
    }

    [Fact]
    public void FullProcessingRetainsW255ToolAndBurnRequirements()
    {
        var process = new DemuraProcess();
        process.Config.ReturnCsvOnly = false;
        var view = new DemuraViewTestResult
        {
            W128 = new DemuraCsvFileResult { SourceExists = true, ValueCount = 640 * 480 }
        };

        process.RebuildItems(view);

        Assert.False(Assert.Single(view.Items, item => item.Name == "W255FileExists").TestResult);
        Assert.False(Assert.Single(view.Items, item => item.Name == "DemuraToolPrepared").TestResult);
        Assert.False(Assert.Single(view.Items, item => item.Name == "DemuraBurnSendFile").TestResult);
    }

    [Fact]
    public async Task CsvOnlyFlowFailureDoesNotRunPgPowerOffCleanup()
    {
        var process = new DemuraProcess();
        var ctx = new IProcessExecutionContext
        {
            Result = new ProjectARVRReuslt { Msg = "采集失败" },
            ObjectiveTestResult = new ObjectiveTestResult()
        };

        Assert.False(await process.ExecuteFailure(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Equal("采集失败", ctx.Result.Msg);
        Assert.Equal("采集失败", ctx.ObjectiveTestResult.Msg);
    }

    // Protect the external file contract, including large text and JSON escaping.
    [Fact]
    public async Task W128Source_RoundTripsLargeFileAsFileNameAndDataOnly()
    {
        string path = Path.Combine(Path.GetTempPath(), $"Demura-W128-{Guid.NewGuid():N}.cvraw");
        string row = string.Join(",", Enumerable.Repeat("1890.1234", 640)) + "\r\n";
        string content = "\"W128\" \\ 文本 {[]}\r\n" + string.Concat(Enumerable.Repeat(row, 480));
        try
        {
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(true));
            DemuraTestResult? file = await DemuraProcess.ReadW128FileAsync(new DemuraCsvFileResult
            {
                SourceFile = path,
                SourceExists = true,
                PreparedFile = Path.Combine(Path.GetTempPath(), "G128.csv")
            });
            Assert.NotNull(file);
            Assert.Equal(Path.GetFileName(path), file.FileName);
            Assert.Equal(content, file.Data);

            var objective = new ObjectiveTestResult();
            objective.DemuraTestResults["Demura"] = file;
            objective.DemuraTestResults["DemuraOther"] = new DemuraTestResult { FileName = "other_W128.csv", Data = "3,4\n" };
            string json = JsonConvert.SerializeObject(new SocketResponse
            {
                EventName = "ProjectARVRResult",
                Data = objective
            });
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var reader = new JsonStreamMessageReader(stream);
            Assert.Equal(json, await reader.ReadMessageAsync());
            var root = JObject.Parse(json);
            var payload = Assert.IsType<JObject>(root["Data"]?["DemuraTestResults"]?["Demura"]);
            Assert.Equal(new[] { "FileName", "Data" }, payload.Properties().Select(property => property.Name));
            Assert.Equal(content, payload["Data"]?.Value<string>());
            ObjectiveTestResult? restored = root["Data"]?.ToObject<ObjectiveTestResult>();
            Assert.NotNull(restored);
            Assert.Equal(2, restored.DemuraTestResults.Count);
            Assert.Equal(file.FileName, restored.DemuraTestResults["Demura"].FileName);
            Assert.Equal(content, restored.DemuraTestResults["Demura"].Data);
            Assert.Equal("3,4\n", restored.DemuraTestResults["DemuraOther"].Data);
            Assert.True(restored?.TotalResult);
            Assert.Empty(ObjectiveTestResultMetricCollector.Collect(objective));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task W128Source_MissingFileHasNoPayload()
    {
        Assert.Null(await DemuraProcess.ReadW128FileAsync(new DemuraCsvFileResult()));
    }

    [Fact]
    public async Task W128Source_ReadFailureDoesNotReturnPartialFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Assert.ThrowsAsync<IOException>(() => DemuraProcess.ReadW128FileAsync(new DemuraCsvFileResult
            {
                SourceFile = path,
                SourceExists = true
            }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    // UTF-8 characters, escapes and framing state must survive every possible byte boundary.
    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public async Task JsonStreamReader_PreservesSplitStringsAndFollowingMessages(int readSize)
    {
        string first = JsonConvert.SerializeObject(new DemuraTestResult
        {
            FileName = "W128_亮度.cvraw",
            Data = "1,2\r\n\"value } {\" \\ 亮度"
        });
        const string second = "{\"EventName\":\"Next\"}";
        using var stream = new FragmentedStream(Encoding.UTF8.GetBytes(" \r\n" + first + " \n" + second), readSize);
        var reader = new JsonStreamMessageReader(stream);

        Assert.Equal(first, await reader.ReadMessageAsync());
        Assert.Equal(second, await reader.ReadMessageAsync());
        Assert.Null(await reader.ReadMessageAsync());
    }

    // Moving diagnostic fields to the view type must preserve historical flat snapshots and CSV rows.
    [Fact]
    public void HistoricalViewJson_RetainsDiagnosticsAndObjectiveCsv()
    {
        const string json = """
            {"W128":{"SourceFile":"W128_Y.cvraw","SourceExists":true,"ValueCount":307200},
             "Items":[{"Name":"W128FileExists","Value":1,"LowLimit":1,"UpLimit":1}],
             "ToolDirectory":"DemuraTool","BurnSucceeded":true}
            """;
        DemuraViewTestResult? view = JsonConvert.DeserializeObject<DemuraViewTestResult>(json);
        Assert.NotNull(view);
        Assert.Equal(307200, view.W128.ValueCount);
        Assert.Equal("DemuraTool", view.ToolDirectory);
        Assert.True(view.BurnSucceeded);
        Assert.Empty(view.FileName);
        Assert.Empty(view.Data);

        var process = new DemuraProcess();
        ObjectiveTestCsvRow row = Assert.Single(process.GetObjectiveCsvRows(new ProjectARVRReuslt { ViewResultJson = json }));
        Assert.Equal("W128FileExists", row.TestItem);
        Assert.Equal("pass", row.TestResult);

        view.FileName = "W128_Y.cvraw";
        view.Data = "1,2\r\n";
        JObject current = JObject.Parse(JsonConvert.SerializeObject(view));
        Assert.Equal(view.W128.SourceFile, current["W128"]?["SourceFile"]?.Value<string>());
        Assert.Equal(view.Data, current["Data"]?.Value<string>());
        DemuraTestResult? publicResult = current.ToObject<DemuraTestResult>();
        Assert.Equal(new[] { "FileName", "Data" }, JObject.FromObject(publicResult!).Properties().Select(property => property.Name));
    }

    private sealed class FragmentedStream(byte[] bytes, int readSize) : MemoryStream(bytes)
    {
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            base.ReadAsync(buffer, offset, Math.Min(count, readSize), cancellationToken);
    }
}
