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
        var directory = Directory.CreateTempSubdirectory("CopilotWebEvidenceOutput-");
        try
        {
            var tool = new FixtureWebTool();
            var client = new FixtureChatClient(answer, finalAnswerRecovery);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "web-output-fixture", "Web output fixture", [tool]);
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry([tool]),
                new CopilotAgentContextBuilder(), new CopilotToolExecutor(), _ => client, new EmptyExternalTools(),
                catalog, new CopilotAgentSkillUsageStore(directory.FullName));
            var request = new CopilotAgentRequest
            {
                ConversationId = "web-output-conversation", TaskId = "web-output-task",
                WorkspacePath = directory.FullName,
                UserText = $"Read {SourceUrl} and return the observed value" + (structured ? " as JSON only." : "."),
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
            Assert.Contains(SourceUrl, step.Observation.Content, StringComparison.Ordinal);
            Assert.Equal(1, tool.Calls);
            Assert.Equal(finalAnswerRecovery ? 1 : 0, client.FinalAnswerCalls);
            if (structured)
            {
                Assert.Equal(answer, output.ToString());
                using var parsed = JsonDocument.Parse(output.ToString());
                Assert.Contains(parsed.RootElement.ValueKind, new[] { JsonValueKind.Object, JsonValueKind.Array });
            }
            else
            {
                Assert.StartsWith(answer, output.ToString(), StringComparison.Ordinal);
                Assert.Contains("来源：", output.ToString(), StringComparison.Ordinal);
                Assert.Contains(SourceUrl, output.ToString(), StringComparison.Ordinal);
            }
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
            [new FixtureWebTool()], answer);
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
            [new FixtureWebTool()], answer));
    }

    private sealed class EmptyExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class FixtureWebTool : ICopilotAgentDrivenTool
    {
        public const string Content = "[Web Page Fetched] " + SourceUrl + "\nvalue=7";
        public string Name => "FetchUrl";
        public string Description => "Read the fixture web reference.";
        public int Calls { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Reference read.", Content = Content });
        }
    }

    private sealed class FixtureChatClient(string answer, bool finalAnswerRecovery) : IChatClient
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
                var tool = Assert.Single(options!.Tools!.OfType<AIFunction>(), f => f.Name.Contains("fetch_url", StringComparison.Ordinal));
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
