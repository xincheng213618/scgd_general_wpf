using ColorVision.UI.Desktop.Help;
using ColorVision.Copilot.Mcp;
using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAlgorithmHelpReadingServiceTests
{
    [Fact]
    public async Task DefaultToolReadsTheDeliveredTopicAdvertisedByTheRealAlgorithmNode()
    {
        // Protect the delivery/default-runtime boundary, beyond the injected provider tests below.
        var documentation = Assert.Single(typeof(LocalGridDistortionNode).GetCustomAttributes(typeof(FlowNodeDocumentationAttribute), true)
            .Cast<FlowNodeDocumentationAttribute>());
        var topicId = documentation.TopicId;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
        });
        var text = new StringBuilder();
        var cursors = new HashSet<string>();
        string? cursor = null;
        string? hash = null;
        do
        {
            var arguments = new Dictionary<string, object?> { ["topic_id"] = topicId, ["max_characters"] = 6000 };
            if (cursor != null) arguments["cursor"] = cursor;
            var input = new CopilotAgentToolInput { Arguments = arguments };
            var result = await dispatcher.CallAsync("read_algorithm_help", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
            Assert.True(result.Success, result.Text);
            using var json = JsonDocument.Parse(result.Text);
            var root = json.RootElement;
            Assert.Equal(topicId, root.GetProperty("topic_id").GetString());
            hash ??= root.GetProperty("content_hash").GetString();
            Assert.Equal(hash, root.GetProperty("content_hash").GetString());
            text.Append(root.GetProperty("content").GetString());
            cursor = root.GetProperty("next_cursor").GetString();
            if (cursor != null) Assert.True(cursors.Add(cursor), "Installed help continuation must advance.");
        } while (cursor != null);
        Assert.Contains("algorithm-principles", text.ToString(), StringComparison.Ordinal);
        Assert.Contains("$$", text.ToString(), StringComparison.Ordinal);
        Assert.Equal(Hash(text.ToString()), hash);
    }

    [Fact]
    public async Task ReadsExactInstalledReferenceWithFormulaAndParameterTextWithoutExecutingMarkup()
    {
        const string markdown = "## 计算原理 {#algorithm-principles}\n\n$D=100\\frac{a-b}{b}$\n\n| 参数 | 单位 |\n| Exposure | ms |\n<GridDistortionDemo />";
        var calls = new List<string>();
        var service = new CopilotAlgorithmHelpReadingService((topic, _) => { calls.Add(topic); return Task.FromResult(Document(markdown)); });
        var result = await service.ReadAsync("algorithms.example", null, 6000, CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(new[] { "algorithms.example" }, calls);
        using var json = JsonDocument.Parse(result.Content);
        var root = json.RootElement;
        Assert.Equal("installed_algorithm_help", root.GetProperty("source").GetString());
        Assert.Equal("algorithms.example", root.GetProperty("topic_id").GetString());
        Assert.Equal("markdown_source", root.GetProperty("content_format").GetString());
        Assert.Equal(markdown, root.GetProperty("content").GetString());
        Assert.Equal(Hash(markdown), root.GetProperty("content_hash").GetString());
        Assert.Equal("https://example.com/guide", root.GetProperty("online_url").GetString());
        Assert.False(root.GetProperty("has_more").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("next_cursor").ValueKind);
        Assert.True(root.GetProperty("coverage").GetProperty("text_complete").GetBoolean());
        Assert.Contains("not rendered or executed", root.GetProperty("interpretation").GetString(), StringComparison.Ordinal);
        Assert.Empty(result.PartialResultMessage);
    }

    [Fact]
    public async Task EscapedLongTextCanBeReadCompletelyWithBudgetShrinkingAndPageResizing()
    {
        var markdown = string.Concat(Enumerable.Repeat("$公式_{n}=100\\frac{a}{b}$\n😀\"<svg />\\\u0001", 600));
        var service = Service(Document(markdown));
        var text = new StringBuilder();
        var cursors = new HashSet<string>();
        string? cursor = null;
        do
        {
            var result = await service.ReadAsync("algorithms.example", cursor, cursor == null ? 12_000 : 6000, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.InRange(result.Content.Length, 1, CopilotAlgorithmHelpReadingService.MaximumContentCharacters);
            using var json = JsonDocument.Parse(result.Content);
            var root = json.RootElement;
            var coverage = root.GetProperty("coverage");
            Assert.Equal(text.Length, coverage.GetProperty("start_offset").GetInt32());
            Assert.Equal(markdown.Length, coverage.GetProperty("total_characters").GetInt32());
            var chunk = root.GetProperty("content").GetString()!;
            Assert.NotEmpty(chunk);
            Assert.Equal(chunk.Length, coverage.GetProperty("characters_returned").GetInt32());
            Assert.False(char.IsHighSurrogate(chunk[^1]));
            Assert.False(char.IsLowSurrogate(chunk[0]));
            text.Append(chunk);
            cursor = root.GetProperty("next_cursor").GetString();
            Assert.Equal(text.Length < markdown.Length, root.GetProperty("has_more").GetBoolean());
            Assert.False(coverage.GetProperty("text_complete").GetBoolean());
            Assert.NotEmpty(result.PartialResultMessage);
            if (cursor != null) Assert.True(cursors.Add(cursor));
        } while (cursor != null);
        Assert.Equal(markdown, text.ToString());
    }

    [Fact]
    public async Task ChangedTextRejectsThePreviousCursorWithoutPublishingMixedContent()
    {
        var document = Document(new string('a', 1000));
        var service = new CopilotAlgorithmHelpReadingService((_, _) => Task.FromResult(document));
        var cursor = Next(await service.ReadAsync("algorithms.example", null, 256, CancellationToken.None));
        document = Document(new string('b', 1000));
        var result = await service.ReadAsync("algorithms.example", cursor, 6000, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
        Assert.Contains("Restart", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", null, 6000)]
    [InlineData("Algorithms.Example", null, 6000)]
    [InlineData("algorithms.example\n", null, 6000)]
    [InlineData("../outside", null, 6000)]
    [InlineData("algorithms.example", "", 6000)]
    [InlineData("algorithms.example", "cvah1:bad", 6000)]
    [InlineData("algorithms.example", null, 255)]
    [InlineData("algorithms.example", null, 12001)]
    public async Task InvalidQueriesCannotReadFiles(string topic, string? cursor, int maximum)
    {
        var calls = 0;
        var service = new CopilotAlgorithmHelpReadingService((_, _) => { calls++; throw new InvalidOperationException(); });
        var result = await service.ReadAsync(topic, cursor, maximum, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("other-topic")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("version")]
    [InlineData("end")]
    [InlineData("split-surrogate")]
    public async Task ChangedCursorScopeAndInvalidBoundariesCannotProduceAFakeCompletePage(string mutation)
    {
        var markdown = new string('a', 256) + "😀" + new string('b', 400);
        var service = Service(Document(markdown));
        var cursor = Next(await service.ReadAsync("algorithms.example", null, 256, CancellationToken.None))!;
        var source = Encoding.UTF8.GetString(Convert.FromBase64String(cursor[6..].Replace('-', '+').Replace('_', '/') + new string('=', (4 - cursor[6..].Length % 4) % 4)));
        var node = JsonNode.Parse(source)!.AsObject();
        switch (mutation)
        {
            case "other-topic": node["topic_id"] = "algorithms.other"; break;
            case "extra": node["unexpected"] = 1; break;
            case "version": node["version"] = 2; break;
            case "end": node["offset"] = markdown.Length; break;
            case "split-surrogate": node["offset"] = 257; break;
        }
        var changed = mutation == "duplicate" ? source.Replace("\"version\":1", "\"version\":1,\"version\":1", StringComparison.Ordinal) : node.ToJsonString();
        cursor = "cvah1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(changed)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var result = await service.ReadAsync("algorithms.example", cursor, 6000, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("topic")]
    [InlineData("hash")]
    public async Task InconsistentSourceEvidenceCannotBePublished(string mutation)
    {
        var document = Document("## Reference");
        document = mutation == "topic" ? document with { Topic = document.Topic with { Id = "algorithms.other" } }
            : document with { ContentHash = new string('a', 64) };
        var result = await Service(document).ReadAsync("algorithms.example", null, 6000, CancellationToken.None);
        Assert.Equal(CopilotToolFailureKind.Internal, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Theory]
    [InlineData("unknown", CopilotToolFailureKind.NotFound)]
    [InlineData("missing", CopilotToolFailureKind.NotFound)]
    [InlineData("invalid", CopilotToolFailureKind.Validation)]
    [InlineData("access", CopilotToolFailureKind.Authorization)]
    [InlineData("io", CopilotToolFailureKind.Transient)]
    public async Task MissingAndInvalidDeliveryRemainClassifiedFailures(string error, CopilotToolFailureKind kind)
    {
        Exception exception = error switch
        {
            "unknown" => new KeyNotFoundException(), "missing" => new FileNotFoundException(),
            "invalid" => new InvalidDataException(), "access" => new UnauthorizedAccessException(), _ => new IOException(),
        };
        var service = new CopilotAlgorithmHelpReadingService((_, _) => Task.FromException<AlgorithmHelpDocument>(exception));
        var result = await service.ReadAsync("algorithms.example", null, 6000, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(kind, result.FailureKind);
        Assert.Empty(result.Content);
    }

    [Fact]
    public async Task CancellationIsForwardedAndNeverBecomesMissingHelp()
    {
        using var source = new CancellationTokenSource();
        var service = new CopilotAlgorithmHelpReadingService((_, token) =>
        {
            Assert.Equal(source.Token, token);
            source.Cancel();
            return Task.FromCanceled<AlgorithmHelpDocument>(token);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync("algorithms.example", null, 6000, source.Token));
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    private static AlgorithmHelpDocument Document(string markdown) => new(
        new AlgorithmHelpTopic("algorithms.example", "算法原理", "guide.html", "https://example.com/guide"), markdown, Hash(markdown));
    private static CopilotAlgorithmHelpReadingService Service(AlgorithmHelpDocument document) => new((_, _) => Task.FromResult(document));
    private static string? Next(CopilotCapabilityResult result)
    {
        Assert.True(result.Success, result.ErrorMessage);
        using var json = JsonDocument.Parse(result.Content);
        return json.RootElement.GetProperty("next_cursor").GetString();
    }
}
