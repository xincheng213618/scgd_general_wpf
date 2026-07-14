#pragma warning disable CA1707
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotExploreSubagentTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Explore-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void DelegateTool_UsesStrictTaskOnlySchema()
    {
        var tool = new CopilotDelegateExploreTool(new FixedExploreRunner());

        Assert.True(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["task"] = "Find the implementation." }, out var input, out var error));
        Assert.Empty(error);
        Assert.Equal("Find the implementation.", input.Arguments["task"]);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?>(), out _, out error));
        Assert.Contains("task", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(tool.InputSchema.TryBind(new Dictionary<string, object?> { ["task"] = "Inspect", ["command"] = "whoami" }, out _, out error));
        Assert.Contains("command", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WorkspaceReadTools_ContinueFromSearchRootWithoutInitialExplicitFile()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "sample.cs");
        await File.WriteAllTextAsync(filePath, "namespace Demo;\npublic sealed class Sample {}\n");
        var request = CreateRequest("Inspect Sample", [ _tempRoot ]);
        var readTool = new CopilotReadLocalFileTool();
        var listTool = new CopilotListDirectoryTool();

        Assert.True(readTool.IsAvailable(request));
        Assert.True(listTool.IsAvailable(request));
        var read = await readTool.ExecuteAsync(request, new CopilotAgentToolInput { Path = filePath }, CancellationToken.None);
        var listing = await listTool.ExecuteAsync(request, new CopilotAgentToolInput { Path = _tempRoot }, CancellationToken.None);

        Assert.True(read.Success, read.ErrorMessage);
        Assert.Contains("public sealed class Sample", read.Content, StringComparison.Ordinal);
        Assert.True(listing.Success, listing.ErrorMessage);
        Assert.Contains("sample.cs", listing.Content, StringComparison.OrdinalIgnoreCase);
        var outsidePath = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outsidePath, "outside");
        try
        {
            var rejected = await readTool.ExecuteAsync(request, new CopilotAgentToolInput { Path = outsidePath }, CancellationToken.None);
            Assert.False(rejected.Success);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task ExploreRunner_UsesFreshContextAndOnlyFourReadOnlyWorkspaceTools()
    {
        Directory.CreateDirectory(_tempRoot);
        var filePath = Path.Combine(_tempRoot, "target.cs");
        await File.WriteAllTextAsync(filePath, "public sealed class Target {}\n");
        using var chatClient = new FunctionCallingChatClient(
            "colorvision_read_local_file",
            new Dictionary<string, object?> { ["path"] = filePath });
        var runner = new CopilotExploreSubagentRunner(_ => chatClient);
        var parentRequest = WithHistory(CreateRequest("parent task", [_tempRoot]), "PARENT_HISTORY_SECRET");

        var result = await runner.RunAsync(parentRequest, "Read target.cs and report where Target is declared.", CancellationToken.None);

        Assert.Equal("harness answer", result.Answer);
        Assert.Contains("ReadLocalFile", result.ToolNames);
        Assert.Equal(2, chatClient.StreamCallCount);
        Assert.Equal(16_384, result.Budget.RequestTokenBudget);
        Assert.Equal(8, result.Budget.MaxToolCalls);
        Assert.Equal(2, result.Budget.MaxAgentPasses);
        var functionNames = chatClient.LastOptions?.Tools?.Select(tool => tool.Name).ToArray() ?? [];
        Assert.Contains("colorvision_search_files", functionNames);
        Assert.Contains("colorvision_grep_text", functionNames);
        Assert.Contains("colorvision_read_local_file", functionNames);
        Assert.Contains("colorvision_list_directory", functionNames);
        Assert.DoesNotContain(functionNames, name => name.Contains("delegate", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("shell", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("database", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(chatClient.AllMessageText, "PARENT_HISTORY_SECRET", StringComparison.Ordinal);
        Assert.Contains("Read target.cs", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.Contains("fresh, read-only Explore subagent", chatClient.LastOptions?.Instructions ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ParentRuntime_ReturnsDelegatedResultAndAccountsForChildUsage()
    {
        Directory.CreateDirectory(_tempRoot);
        var childResult = new CopilotExploreSubagentResult
        {
            Answer = "Target is declared in target.cs:1.",
            StopReason = CopilotAgentStopReason.Completed,
            Usage = new CopilotTokenUsage(7, 3, 10),
            Budget = new CopilotAgentBudgetSnapshot
            {
                ConsumedTokens = 12,
                ProviderCalls = 2,
                UsedEstimatedUsage = true,
            },
            ToolNames = ["GrepText", "ReadLocalFile"],
        };
        var runner = new FixedExploreRunner(childResult);
        var tool = new CopilotDelegateExploreTool(runner);
        using var chatClient = new FunctionCallingChatClient(
            "colorvision_delegate_explore",
            new Dictionary<string, object?> { ["task"] = "Find Target and return exact evidence." },
            usageTokensPerCall: 10);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([tool]),
            new CopilotAgentContextBuilder(),
            _ => chatClient);

        var result = await runtime.RunAsync(CreateRequest("Investigate Target across the workspace.", [ _tempRoot ]), _ => { }, CancellationToken.None);

        var step = Assert.Single(result.StepRecords);
        Assert.Equal("DelegateExplore", step.ToolCall.ToolName);
        Assert.True(step.Observation.Success, step.Observation.ErrorMessage);
        Assert.Equal(1, runner.CallCount);
        Assert.NotNull(step.Observation.DelegatedRunUsage);
        Assert.Equal(10, step.Observation.DelegatedRunUsage!.Usage.EffectiveTotalTokens);
        Assert.Equal(20, result.Usage.EffectiveTotalTokens);
        Assert.Equal(4, result.Budget.ProviderCalls);
        Assert.True(result.Budget.ConsumedTokens >= 22);
    }

    [Fact]
    public async Task DelegateTool_ReturnsBoundedChildMetricsAndFriendlyTraceLabel()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new FixedExploreRunner(new CopilotExploreSubagentResult
        {
            Answer = "Evidence summary.",
            StopReason = CopilotAgentStopReason.Completed,
            Usage = new CopilotTokenUsage(5, 2, 7),
            Budget = new CopilotAgentBudgetSnapshot { ConsumedTokens = 9, ProviderCalls = 2 },
        });
        var tool = new CopilotDelegateExploreTool(runner);
        var result = await tool.ExecuteAsync(
            CreateRequest("Explore", [ _tempRoot ]),
            new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["task"] = "Inspect the implementation." } },
            CancellationToken.None);
        var trace = CopilotAgentTraceEntry.FromResult(new CopilotToolExecutionInfo
        {
            ToolName = tool.Name,
            State = CopilotToolExecutionState.Completed,
        }, result);

        Assert.True(result.Success);
        Assert.Equal(9, result.DelegatedRunUsage?.ConsumedTokens);
        Assert.Contains("stop_reason: Completed", result.Content, StringComparison.Ordinal);
        Assert.Contains("answer:" + Environment.NewLine + "Evidence summary.", result.Content, StringComparison.Ordinal);
        Assert.Equal("委派了代码探索", trace.ActivityLabel);
        Assert.Equal("只读 Explore 子 Agent 已返回结果。", trace.ActivityDescription);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private static CopilotAgentRequest CreateRequest(string userText, IReadOnlyList<string> roots)
    {
        return new CopilotAgentRequest
        {
            UserText = userText,
            Profile = new CopilotProfileConfig
            {
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "secret-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 256,
                MaxToolRounds = 8,
            },
            Mode = CopilotAgentMode.Code,
            SearchRootPaths = roots,
        };
    }

    private static CopilotAgentRequest WithHistory(CopilotAgentRequest request, string text)
    {
        return new CopilotAgentRequest
        {
            UserText = request.UserText,
            Profile = request.Profile,
            History = [new CopilotRequestMessage("assistant", text)],
            Mode = request.Mode,
            SearchRootPaths = request.SearchRootPaths,
        };
    }

    private sealed class FixedExploreRunner : ICopilotExploreSubagentRunner
    {
        private readonly CopilotExploreSubagentResult _result;

        public FixedExploreRunner(CopilotExploreSubagentResult? result = null)
        {
            _result = result ?? new CopilotExploreSubagentResult { Answer = "fixed answer", StopReason = CopilotAgentStopReason.Completed };
        }

        public int CallCount { get; private set; }

        public Task<CopilotExploreSubagentResult> RunAsync(CopilotAgentRequest parentRequest, string task, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FunctionCallingChatClient : IChatClient
    {
        private readonly string _functionName;
        private readonly IDictionary<string, object?> _arguments;
        private readonly int _usageTokensPerCall;
        private int _streamCallCount;

        public FunctionCallingChatClient(string functionName, IDictionary<string, object?> arguments, int usageTokensPerCall = 0)
        {
            _functionName = functionName;
            _arguments = arguments;
            _usageTokensPerCall = usageTokensPerCall;
        }

        public ChatOptions? LastOptions { get; private set; }

        public int StreamCallCount => Volatile.Read(ref _streamCallCount);

        public string AllMessageText { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "harness answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            await Task.Yield();
            var callNumber = Interlocked.Increment(ref _streamCallCount);
            if (callNumber == 1)
            {
                var contents = new List<AIContent> { new FunctionCallContent("call-1", _functionName, _arguments) };
                if (_usageTokensPerCall > 0)
                {
                    contents.Add(new UsageContent(new UsageDetails
                    {
                        InputTokenCount = _usageTokensPerCall - 1,
                        OutputTokenCount = 1,
                        TotalTokenCount = _usageTokensPerCall,
                    }));
                }
                yield return new ChatResponseUpdate(ChatRole.Assistant, contents);
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "harness answer");
        }

        private void Capture(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options)
        {
            LastOptions = options;
            var text = string.Join("\n", messages.SelectMany(message => message.Contents).OfType<TextContent>().Select(content => content.Text));
            AllMessageText += "\n" + text;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
