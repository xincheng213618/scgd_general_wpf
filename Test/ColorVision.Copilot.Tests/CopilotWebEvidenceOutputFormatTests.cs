using Microsoft.Extensions.AI;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWebEvidenceOutputFormatTests
{
    private const string SourceUrl = "https://example.test/reference";

    [Theory]
    [InlineData("{\"value\":7}", false, true)]
    [InlineData("{\"value\":7}", true, true)]
    [InlineData("[7,8]", false, true)]
    [InlineData("[7,8]", true, true)]
    [InlineData(" \r\n{\"value\":7}\r\n ", false, true)]
    [InlineData(" \r\n{\"value\":7}\r\n ", true, true)]
    [InlineData("The observed value is 7.", false, false)]
    [InlineData("The observed value is 7.", true, false)]
    public async Task WebEvidenceKeepsJsonParseableAndProseCited(string answer, bool finalAnswerRecovery, bool structured)
    {
        var output = await RunWebEvidenceAsync(answer, finalAnswerRecovery, structured,
            "FetchUrl", FixtureWebTool.Content, [SourceUrl]);
        if (structured)
        {
            Assert.Equal(answer, output);
            using var parsed = JsonDocument.Parse(output);
            Assert.Contains(parsed.RootElement.ValueKind, new[] { JsonValueKind.Object, JsonValueKind.Array });
        }
        else
        {
            Assert.StartsWith(answer, output, StringComparison.Ordinal);
            Assert.Contains("来源：", output, StringComparison.Ordinal);
            Assert.Contains(SourceUrl, output, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("https://public.example/Report", "https://public.example/report", false, true)]
    [InlineData("https://public.example/Report", "https://public.example/report", true, true)]
    [InlineData("https://public.example/reference?id=ABC", "https://public.example/reference?id=abc", false, true)]
    [InlineData("https://public.example/reference?id=ABC", "https://public.example/reference?id=abc", true, true)]
    [InlineData("https://public.example/Report?id=ABC", "HTTPS://PUBLIC.EXAMPLE/Report?id=ABC", false, false)]
    [InlineData("https://public.example/Report?id=ABC", "HTTPS://PUBLIC.EXAMPLE/Report?id=ABC", true, false)]
    public async Task FinalAnswerRecognizesOnlyTheSameNormalizedSourceUrl(
        string sourceUrl, string citedUrl, bool finalAnswerRecovery, bool expectAppendix)
    {
        var answer = $"The observed value is 7. Source: <{citedUrl}>.";
        var content = CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(
            new CopilotFetchedWebPageContent(sourceUrl, "Reference", string.Empty, "value=7"));
        var output = await RunWebEvidenceAsync(answer, finalAnswerRecovery, false, "FetchUrl", content, [sourceUrl]);

        if (expectAppendix)
        {
            Assert.StartsWith(answer, output, StringComparison.Ordinal);
            Assert.Contains("来源：", output, StringComparison.Ordinal);
            Assert.Contains($"- <{sourceUrl}>", output, StringComparison.Ordinal);
        }
        else
            Assert.Equal(answer, output);
    }

    [Theory]
    [InlineData("FetchUrl", false)]
    [InlineData("FetchUrl", true)]
    [InlineData("WebSearch", false)]
    [InlineData("WebSearch", true)]
    public async Task FinalAnswerPreservesCaseDistinctReturnedSourcesInOrder(string toolName, bool queryCase)
    {
        var sources = queryCase
            ? new[] { "https://public.example/reference?id=ABC", "https://public.example/reference?id=abc" }
            : new[] { "https://public.example/Report", "https://public.example/report" };
        var content = toolName == "FetchUrl"
            ? string.Join("\n\n", sources.Select(url => CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(
                new CopilotFetchedWebPageContent(url, "Reference", string.Empty, "value=7"))))
            : $"Search results for reference:\n1. First reference\n   URL: {sources[0]}\n   Snippet: value=7\n"
                + $"2. Second reference\n   URL: {sources[1]}\n   Snippet: value=7";
        const string answer = "The observed value is 7.";
        var output = await RunWebEvidenceAsync(answer, false, false, toolName, content, sources);

        Assert.StartsWith(answer, output, StringComparison.Ordinal);
        Assert.Contains("来源：", output, StringComparison.Ordinal);
        Assert.Equal(sources, CopilotWebPageToolSupport.ExtractHttpUrls(output));
    }

    private static async Task<string> RunWebEvidenceAsync(string answer, bool finalAnswerRecovery, bool structured,
        string toolName, string content, IReadOnlyList<string> sourceUrls)
    {
        var directory = Directory.CreateTempSubdirectory("CopilotWebEvidenceOutput-");
        try
        {
            var tool = new FixtureWebTool(toolName, content);
            var client = new FixtureChatClient(answer, finalAnswerRecovery, toolName);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "web-output-fixture", "Web output fixture", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(), new CopilotToolExecutor(), _ => client, new EmptyExternalTools(),
                catalog, new CopilotAgentSkillUsageStore(directory.FullName));
            var request = new CopilotAgentRequest
            {
                ConversationId = "web-output-conversation", TaskId = "web-output-task",
                WorkspacePath = directory.FullName,
                UserText = $"Read {string.Join(" ", sourceUrls)} and return the observed value" + (structured ? " as JSON only." : "."),
                Mode = CopilotAgentMode.Code, HarnessFeatures = CopilotAgentHarnessFeatures.None,
                Profile = new CopilotProfileConfig
                {
                    VendorType = CopilotVendorType.Custom, ProviderType = CopilotProviderType.OpenAICompatible,
                    BaseUrl = "https://example.test/v1", ApiKey = "injected-test-key", Model = "fixture", MaxTokens = 4096,
                },
                RunBudgetOverride = new() { RequestTokenBudget = 32768, MaxToolCalls = 4, MaxAgentPasses = 1, TotalDuration = TimeSpan.FromSeconds(15) },
            };
            var output = new StringBuilder();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var result = await runtime.RunAsync(request, e =>
            {
                if (e.Type == CopilotAgentEventType.AnswerReset) output.Clear();
                if (e.Type == CopilotAgentEventType.AnswerDelta) output.Append(e.Text);
            }, cancellation.Token);

            Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
            var step = Assert.Single(result.StepRecords);
            Assert.True(step.Observation.Success);
            Assert.Equal(toolName, step.Execution.ToolName);
            foreach (var sourceUrl in sourceUrls)
                Assert.Contains(sourceUrl, step.Observation.Content, StringComparison.Ordinal);
            Assert.Equal(1, tool.Calls);
            Assert.Equal(finalAnswerRecovery ? 1 : 0, client.FinalAnswerCalls);
            return output.ToString();
        }
        finally
        {
            var resolved = Path.GetFullPath(directory.FullName);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("CopilotWebEvidenceOutput-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
        }
    }

    [Theory]
    [InlineData("{\"value\":7")]
    [InlineData("{\"value\":7} trailing text")]
    [InlineData("{\"value\":7}{\"value\":8}")]
    [InlineData("{\"value\":7,}")]
    [InlineData("42")]
    public void NonStructuredAnswersKeepExistingSourceAppendix(string answer)
    {
        var appendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(
            [new() { ToolCall = new() { ToolName = "FetchUrl" }, Observation = new() { Success = true, Content = FixtureWebTool.Content } }],
            [new FixtureWebTool("FetchUrl", FixtureWebTool.Content)], answer);
        Assert.Contains(SourceUrl, appendix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65)]
    public void CompleteNestedJsonDoesNotGainAnAppendixAtTheParserDefaultDepth(int depth)
    {
        var answer = new string('[', depth) + "{\"value\":7}" + new string(']', depth);
        Assert.Empty(CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(
            [new() { ToolCall = new() { ToolName = "FetchUrl" }, Observation = new() { Success = true, Content = FixtureWebTool.Content } }],
            [new FixtureWebTool("FetchUrl", FixtureWebTool.Content)], answer));
    }

    private sealed class EmptyExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class FixtureWebTool(string name, string content) : ICopilotAgentDrivenTool
    {
        public const string Content = "[Web Page Fetched] " + SourceUrl + "\nvalue=7";
        public string Name => name;
        public string Description => "Read the fixture web reference.";
        public int Calls { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Reference read.", Content = content });
        }
    }

    private sealed class FixtureChatClient(string answer, bool finalAnswerRecovery, string toolName) : IChatClient
    {
        private int _streamingCalls;
        public int FinalAnswerCalls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            FinalAnswerCalls++;
            Assert.Empty(options?.Tools ?? []);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer)) { FinishReason = ChatFinishReason.Stop });
        }
        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            var call = ++_streamingCalls;
            Assert.InRange(call, 1, 2);
            if (call == 1)
            {
                var functionName = toolName == "WebSearch" ? "web_search" : "fetch_url";
                var tool = Assert.Single(options!.Tools!.OfType<AIFunction>(), f => f.Name.Contains(functionName, StringComparison.Ordinal));
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent("fetch-fixture", tool.Name, new Dictionary<string, object?>())]) { FinishReason = ChatFinishReason.ToolCalls };
            }
            else
                yield return new ChatResponseUpdate(ChatRole.Assistant, finalAnswerRecovery ? "Partial answer" : answer)
                { FinishReason = finalAnswerRecovery ? ChatFinishReason.Length : ChatFinishReason.Stop };
        }
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
