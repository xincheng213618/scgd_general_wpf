#pragma warning disable CA1707
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotScoutSubagentTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-Scout-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RoleCatalog_ExposesDistinctHostControlledExploreAndScoutRoles()
    {
        var catalog = CopilotSubagentRoleCatalog.Default;
        var explore = catalog.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);
        var scout = catalog.GetRequired(CopilotSubagentRoleCatalog.ScoutRoleId);

        Assert.Equal(2, catalog.Roles.Count);
        Assert.Equal("DelegateExplore", explore.ToolName);
        Assert.Equal(CopilotAgentMode.Code, explore.ChildMode);
        Assert.Equal(8, explore.MaximumToolCalls);
        Assert.Equal("DelegateScout", scout.ToolName);
        Assert.Equal(CopilotAgentMode.Web, scout.ChildMode);
        Assert.Equal(6, scout.MaximumToolCalls);
        Assert.Equal(2, catalog.Roles.Select(role => role.ToolName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task ScoutRunner_UsesFreshContextAndOnlyPublicWebTools()
    {
        Directory.CreateDirectory(_tempRoot);
        using var chatClient = new CapturingAnswerChatClient();
        var runner = new CopilotSubagentRunner(_ => chatClient);
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ScoutRoleId);
        var parent = CreateParent("Research current official documentation on https://example.com/docs", CopilotAgentMode.Web);
        parent = new CopilotAgentRequest
        {
            UserText = parent.UserText,
            Profile = parent.Profile,
            Mode = parent.Mode,
            History = [new CopilotRequestMessage("user", "PARENT_HISTORY_SECRET")],
            Attachments = [new CopilotAttachmentItem { Title = "PARENT_ATTACHMENT_SECRET", Value = "secret" }],
            ContextItems = [new CopilotContextItem { Title = "PARENT_CONTEXT_SECRET", Content = "secret" }],
            SearchRootPaths = [_tempRoot],
            ActiveDocumentPath = Path.Combine(_tempRoot, "secret.cs"),
            ProjectInstructions = [new CopilotProjectInstructionDocument { Path = Path.Combine(_tempRoot, "AGENTS.md"), Content = "PARENT_PROJECT_SECRET" }],
            ReadableLocalDirectoryPaths = [_tempRoot],
            WritableLocalRootPaths = [_tempRoot],
            ExternalMcpServers = [new CopilotMcpClientServerConfig { Name = "PARENT_MCP_SECRET" }],
        };

        var result = await runner.RunAsync(parent, role, new CopilotSubagentRunRequest
        {
            RunId = "scout-test-run",
            Task = "Compare the current official documentation and return exact source URLs.",
            RequestTokenBudget = 12_000,
        }, CancellationToken.None);

        Assert.Equal(CopilotSubagentRoleCatalog.ScoutRoleId, result.RoleId);
        Assert.Equal("scout answer", result.Answer);
        Assert.Equal(6, result.Budget.MaxToolCalls);
        Assert.Equal(2, result.Budget.MaxAgentPasses);
        var functionNames = chatClient.LastOptions?.Tools?.Select(tool => tool.Name).OrderBy(name => name).ToArray() ?? [];
        Assert.Equal(["colorvision_fetch_url", "colorvision_web_search"], functionNames);
        Assert.DoesNotContain(functionNames, name => name.Contains("todo", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("mode", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(functionNames, name => name.Contains("skill", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("fresh, read-only Scout subagent", chatClient.LastOptions?.Instructions ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Compare the current official documentation", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_HISTORY_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_ATTACHMENT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_CONTEXT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_PROJECT_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
        Assert.DoesNotContain("PARENT_MCP_SECRET", chatClient.AllMessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScoutTool_IsIntentScopedAndPersistsRoleTraceAndPublicSources()
    {
        var runner = new StaticRunner("Official documentation: https://example.com/docs");
        var tool = new CopilotDelegateScoutTool(runner);
        Assert.False(tool.IsAvailable(CreateParent("Explain dependency injection.", CopilotAgentMode.Auto)));
        var request = CreateParent("Research current official docs for https://example.com/docs", CopilotAgentMode.Web);
        Assert.True(tool.IsAvailable(request));

        var result = await tool.ExecuteAsync(request, CreateInput("Compare official documentation."), CancellationToken.None);
        var execution = new CopilotToolExecutionInfo { ToolName = tool.Name, State = CopilotToolExecutionState.Completed };
        var trace = CopilotAgentTraceEntry.FromResult(execution, result);
        var appendix = CopilotWebEvidenceSourceLedger.BuildMissingSourceAppendix(
            [new CopilotAgentStepRecord
            {
                ToolCall = new CopilotToolCall { ToolName = tool.Name },
                Execution = execution,
                Observation = CopilotToolObservation.FromResult(result),
            }],
            [tool],
            "The documentation confirms the behavior.");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(CopilotSubagentRoleCatalog.ScoutRoleId, result.DelegatedRunUsage?.RoleId);
        Assert.StartsWith("scout-", result.DelegatedRunUsage?.RunId, StringComparison.Ordinal);
        Assert.Contains("role: scout", result.Content, StringComparison.Ordinal);
        Assert.Equal("查阅了外部资料", trace.ActivityLabel);
        Assert.Equal("只读 Scout 子 Agent 已返回外部资料。", trace.ActivityDescription);
        Assert.Equal(CopilotSubagentRoleCatalog.ScoutRoleId, trace.DelegatedRoleId);
        Assert.Contains("role: scout", trace.DiagnosticDetails, StringComparison.Ordinal);
        Assert.Contains("https://example.com/docs", appendix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExploreAndScout_ShareTwoConcurrencySlotsAcrossRoles()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new BlockingRunner();
        var explore = new CopilotDelegateExploreTool(runner);
        var scout = new CopilotDelegateScoutTool(runner);
        var request = CreateParent("Inspect the workspace and research https://example.com/docs", CopilotAgentMode.Code, [_tempRoot]);

        var first = explore.ExecuteAsync(request, CreateInput("Inspect the workspace implementation."), CancellationToken.None);
        var second = scout.ExecuteAsync(request, CreateInput("Research the external documentation."), CancellationToken.None);
        await runner.TwoRunsActive.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var queued = scout.ExecuteAsync(request, CreateInput("Research a second independent source."), CancellationToken.None);
        await Task.Delay(50);

        Assert.Equal(2, runner.CallCount);
        Assert.Equal(2, runner.MaximumActiveRuns);
        Assert.Contains(CopilotSubagentRoleCatalog.ExploreRoleId, runner.RoleIds);
        Assert.Contains(CopilotSubagentRoleCatalog.ScoutRoleId, runner.RoleIds);

        runner.ReleaseAll.TrySetResult();
        var results = await Task.WhenAll(first, second, queued).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.All(results, result => Assert.True(result.Success, result.ErrorMessage));
        Assert.Equal(3, runner.CallCount);
        Assert.Equal(2, runner.MaximumActiveRuns);
    }

    [Fact]
    public async Task ExploreAndScout_ShareOneRequestScopedTokenPool()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new BudgetConsumingRunner();
        var explore = new CopilotDelegateExploreTool(runner);
        var scout = new CopilotDelegateScoutTool(runner);
        var baseRequest = CreateParent("Inspect the workspace and research https://example.com/docs", CopilotAgentMode.Code, [_tempRoot]);
        var request = new CopilotAgentRequest
        {
            UserText = baseRequest.UserText,
            Profile = baseRequest.Profile,
            Mode = baseRequest.Mode,
            SearchRootPaths = baseRequest.SearchRootPaths,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride { RequestTokenBudget = 20_000 },
        };

        var first = await explore.ExecuteAsync(request, CreateInput("Inspect workspace evidence."), CancellationToken.None);
        var second = await scout.ExecuteAsync(request, CreateInput("Inspect external evidence."), CancellationToken.None);
        var exhausted = await explore.ExecuteAsync(request, CreateInput("Inspect another workspace area."), CancellationToken.None);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.False(exhausted.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, exhausted.FailureKind);
        Assert.Equal(2, runner.CallCount);
        Assert.All(runner.TokenBudgets, budget => Assert.Equal(5_000, budget));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, true);
    }

    private static CopilotAgentRequest CreateParent(
        string userText,
        CopilotAgentMode mode,
        IReadOnlyList<string>? roots = null)
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
            Mode = mode,
            SearchRootPaths = roots ?? Array.Empty<string>(),
        };
    }

    private static CopilotAgentToolInput CreateInput(string task)
    {
        return new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["task"] = task } };
    }

    private sealed class StaticRunner : ICopilotSubagentRunner
    {
        private readonly string _answer;

        public StaticRunner(string answer)
        {
            _answer = answer;
        }

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotSubagentResult
            {
                RoleId = role.Id,
                RunId = runRequest.RunId,
                RequestTokenBudget = runRequest.RequestTokenBudget,
                QueueDurationMs = runRequest.QueueDurationMs,
                Answer = _answer,
                StopReason = CopilotAgentStopReason.Completed,
                Budget = new CopilotAgentBudgetSnapshot { ConsumedTokens = 100, ProviderCalls = 1, ToolCalls = 1 },
                Usage = new CopilotTokenUsage(80, 20, 100),
            });
        }
    }

    private sealed class BlockingRunner : ICopilotSubagentRunner
    {
        private int _activeRuns;
        private int _callCount;
        private int _maximumActiveRuns;

        public ConcurrentBag<string> RoleIds { get; } = [];

        public TaskCompletionSource TwoRunsActive { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseAll { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount => Volatile.Read(ref _callCount);

        public int MaximumActiveRuns => Volatile.Read(ref _maximumActiveRuns);

        public async Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            RoleIds.Add(role.Id);
            Interlocked.Increment(ref _callCount);
            var active = Interlocked.Increment(ref _activeRuns);
            UpdateMaximum(active);
            if (active >= 2)
                TwoRunsActive.TrySetResult();

            try
            {
                await ReleaseAll.Task.WaitAsync(cancellationToken);
                return new CopilotSubagentResult
                {
                    RoleId = role.Id,
                    RunId = runRequest.RunId,
                    RequestTokenBudget = runRequest.RequestTokenBudget,
                    Answer = "completed " + role.Id,
                    StopReason = CopilotAgentStopReason.Completed,
                    Budget = new CopilotAgentBudgetSnapshot { ConsumedTokens = 100, ProviderCalls = 1 },
                    Usage = new CopilotTokenUsage(80, 20, 100),
                };
            }
            finally
            {
                Interlocked.Decrement(ref _activeRuns);
            }
        }

        private void UpdateMaximum(int active)
        {
            var current = Volatile.Read(ref _maximumActiveRuns);
            while (active > current)
            {
                var observed = Interlocked.CompareExchange(ref _maximumActiveRuns, active, current);
                if (observed == current)
                    return;
                current = observed;
            }
        }
    }

    private sealed class BudgetConsumingRunner : ICopilotSubagentRunner
    {
        private int _callCount;

        public ConcurrentBag<int> TokenBudgets { get; } = [];

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            TokenBudgets.Add(runRequest.RequestTokenBudget);
            return Task.FromResult(new CopilotSubagentResult
            {
                RoleId = role.Id,
                RunId = runRequest.RunId,
                RequestTokenBudget = runRequest.RequestTokenBudget,
                Answer = "budget consumed",
                StopReason = CopilotAgentStopReason.Completed,
                Budget = new CopilotAgentBudgetSnapshot
                {
                    RequestTokenBudget = runRequest.RequestTokenBudget,
                    ConsumedTokens = runRequest.RequestTokenBudget,
                    ProviderCalls = 1,
                },
                Usage = new CopilotTokenUsage(runRequest.RequestTokenBudget - 1, 1, runRequest.RequestTokenBudget),
            });
        }
    }

    private sealed class CapturingAnswerChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }

        public string AllMessageText { get; private set; } = string.Empty;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            return Task.FromResult(new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, "scout answer")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Capture(messages, options);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "scout answer");
        }

        private void Capture(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options)
        {
            LastOptions = options;
            AllMessageText = string.Join("\n", messages
                .SelectMany(message => message.Contents)
                .OfType<TextContent>()
                .Select(content => content.Text));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
