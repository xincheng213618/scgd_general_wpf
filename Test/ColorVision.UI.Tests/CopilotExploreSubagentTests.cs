#pragma warning disable CA1707
using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
        Assert.Equal(CopilotToolIdempotency.Idempotent, tool.Capability.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, tool.Capability.EffectiveConcurrencyMode);
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
        var runner = new CopilotSubagentRunner(_ => chatClient);
        var parentRequest = WithHistory(CreateRequest("parent task", [_tempRoot]), "PARENT_HISTORY_SECRET");
        var role = CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId);

        var result = await runner.RunAsync(parentRequest, role, new CopilotSubagentRunRequest
        {
            RunId = "explore-test-run",
            Task = "Read target.cs and report where Target is declared.",
            RequestTokenBudget = 16_384,
        }, CancellationToken.None);

        Assert.Equal("harness answer", result.Answer);
        Assert.Equal("explore-test-run", result.RunId);
        Assert.Equal(16_384, result.RequestTokenBudget);
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
    public void ChildRequest_CapsRootsAndDropsContextOutsideDelegatedScope()
    {
        Directory.CreateDirectory(_tempRoot);
        var roots = Enumerable.Range(1, 5)
            .Select(index => Path.Combine(_tempRoot, "root-" + index))
            .ToArray();
        foreach (var root in roots)
            Directory.CreateDirectory(root);
        var activeDocumentPath = Path.Combine(roots[4], "outside.cs");
        File.WriteAllText(activeDocumentPath, "class Outside {}");
        var parent = new CopilotAgentRequest
        {
            UserText = "parent",
            Profile = CreateRequest("parent", roots).Profile,
            Mode = CopilotAgentMode.Code,
            SearchRootPaths = roots,
            ActiveDocumentPath = activeDocumentPath,
            ProjectInstructions =
            [
                new CopilotProjectInstructionDocument
                {
                    Path = Path.Combine(roots[0], "AGENTS.md"),
                    Content = "inside",
                },
                new CopilotProjectInstructionDocument
                {
                    Path = Path.Combine(roots[3], "AGENTS.md"),
                    Content = "outside delegated root cap",
                },
                new CopilotProjectInstructionDocument
                {
                    Path = Path.Combine(roots[4], "AGENTS.md"),
                    Content = "active root",
                },
            ],
        };
        var createChild = typeof(CopilotSubagentRunner).GetMethod("CreateChildRequest", BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(createChild);
        var child = Assert.IsType<CopilotAgentRequest>(createChild.Invoke(null,
        [
            parent,
            CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
            new CopilotSubagentRunRequest
            {
                RunId = "explore-context",
                Task = "inspect",
                RequestTokenBudget = 8_000,
            },
        ]));

        Assert.Equal(4, child.SearchRootPaths.Count);
        Assert.Equal(roots[4], child.SearchRootPaths[0]);
        Assert.DoesNotContain(roots[3], child.SearchRootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(activeDocumentPath, child.ActiveDocumentPath);
        Assert.Equal(["active root", "inside"], child.ProjectInstructions.Select(document => document.Content).OrderBy(content => content));
        Assert.Empty(child.History);
        Assert.Empty(child.Attachments);
        Assert.Empty(child.ContextItems);
    }

    [Fact]
    public async Task ParentRuntime_ReturnsDelegatedResultAndAccountsForChildUsage()
    {
        Directory.CreateDirectory(_tempRoot);
        var childResult = new CopilotSubagentResult
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
        var runner = new FixedExploreRunner(new CopilotSubagentResult
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
        Assert.StartsWith("explore-", result.DelegatedRunUsage?.RunId, StringComparison.Ordinal);
        Assert.Equal(16_384, result.DelegatedRunUsage?.RequestTokenBudget);
        Assert.Equal(CopilotAgentStopReason.Completed, result.DelegatedRunUsage?.StopReason);
        Assert.Contains("run_id: explore-", result.Content, StringComparison.Ordinal);
        Assert.Contains("stop_reason: Completed", result.Content, StringComparison.Ordinal);
        Assert.Contains("answer:" + Environment.NewLine + "Evidence summary.", result.Content, StringComparison.Ordinal);
        Assert.Equal("委派了代码探索", trace.ActivityLabel);
        Assert.Equal("只读 Explore 子 Agent 已返回结果。", trace.ActivityDescription);
        Assert.Equal(result.DelegatedRunUsage?.RunId, trace.DelegatedRunId);
        Assert.Equal(CopilotSubagentRoleCatalog.ExploreRoleId, trace.DelegatedRoleId);
        Assert.Equal(16_384, trace.DelegatedRequestTokenBudget);
        Assert.Equal(9, trace.DelegatedConsumedTokens);
        Assert.Contains("Child run: explore-", trace.DiagnosticDetails, StringComparison.Ordinal);
        Assert.Contains("Child budget: 9/16384 tokens", trace.DiagnosticDetails, StringComparison.Ordinal);
        var restored = JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(JsonConvert.SerializeObject(trace));
        Assert.NotNull(restored);
        Assert.False(restored.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(trace.DelegatedRunId, restored.DelegatedRunId);
        Assert.Equal(16_384, restored.DelegatedRequestTokenBudget);
        var frameworkResult = CopilotFrameworkToolResultFormatter.Format(new CopilotToolExecutionOutcome
        {
            Result = result,
            Execution = new CopilotToolExecutionInfo { ToolName = tool.Name, State = CopilotToolExecutionState.Completed },
        });
        using var frameworkDocument = JsonDocument.Parse(frameworkResult);
        var delegatedRun = frameworkDocument.RootElement.GetProperty("delegated_run");
        Assert.Equal(result.DelegatedRunUsage?.RunId, delegatedRun.GetProperty("run_id").GetString());
        Assert.Equal(CopilotSubagentRoleCatalog.ExploreRoleId, delegatedRun.GetProperty("role").GetString());
        Assert.Equal(16_384, delegatedRun.GetProperty("request_token_budget").GetInt32());
        Assert.Equal("completed", delegatedRun.GetProperty("stop_reason").GetString());
    }

    [Fact]
    public async Task DelegateTool_RunsAtMostTwoIndependentChildrenConcurrently()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new BlockingExploreRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = CreateRequest("Compare independent areas.", [_tempRoot]);

        var calls = Enumerable.Range(1, 3)
            .Select(index => tool.ExecuteAsync(request, CreateDelegateInput("Inspect area " + index), CancellationToken.None))
            .ToArray();
        await runner.TwoRunsActive.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(50);

        Assert.Equal(2, runner.CallCount);
        Assert.Equal(2, runner.MaximumActiveRuns);

        runner.ReleaseAll.TrySetResult();
        var results = await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.All(results, result => Assert.True(result.Success, result.ErrorMessage));
        Assert.Equal(3, runner.CallCount);
        Assert.Equal(3, runner.RunRequests.Select(run => run.RunId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(runner.RunRequests, run => Assert.Equal(16_384, run.RequestTokenBudget));
    }

    [Fact]
    public async Task DelegateTool_CancelsAChildWaitingForTheParallelSlot()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new BlockingExploreRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = CreateRequest("Compare independent areas.", [_tempRoot]);
        var first = tool.ExecuteAsync(request, CreateDelegateInput("Inspect first"), CancellationToken.None);
        var second = tool.ExecuteAsync(request, CreateDelegateInput("Inspect second"), CancellationToken.None);
        await runner.TwoRunsActive.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();
        var queued = tool.ExecuteAsync(request, CreateDelegateInput("Inspect queued"), cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.Equal(2, runner.CallCount);

        runner.ReleaseAll.TrySetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DelegateTool_StopsStartingChildrenWhenRequestScopedTokenPoolIsSpent()
    {
        Directory.CreateDirectory(_tempRoot);
        var runner = new BudgetConsumingExploreRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var baseRequest = CreateRequest("Inspect bounded areas.", [_tempRoot]);
        var request = new CopilotAgentRequest
        {
            UserText = baseRequest.UserText,
            Profile = baseRequest.Profile,
            Mode = baseRequest.Mode,
            SearchRootPaths = baseRequest.SearchRootPaths,
            RunBudgetOverride = new CopilotAgentRunBudgetOverride { RequestTokenBudget = 20_000 },
        };

        var first = await tool.ExecuteAsync(request, CreateDelegateInput("Inspect first"), CancellationToken.None);
        var second = await tool.ExecuteAsync(request, CreateDelegateInput("Inspect second"), CancellationToken.None);
        var exhausted = await tool.ExecuteAsync(request, CreateDelegateInput("Inspect third"), CancellationToken.None);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.False(exhausted.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, exhausted.FailureKind);
        Assert.Equal(2, runner.CallCount);
        Assert.All(runner.RunRequests, run => Assert.Equal(5_000, run.RequestTokenBudget));
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

    private static CopilotAgentToolInput CreateDelegateInput(string task)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?> { ["task"] = task },
        };
    }

    private sealed class FixedExploreRunner : ICopilotSubagentRunner
    {
        private readonly CopilotSubagentResult _result;

        public FixedExploreRunner(CopilotSubagentResult? result = null)
        {
            _result = result ?? new CopilotSubagentResult { Answer = "fixed answer", StopReason = CopilotAgentStopReason.Completed };
        }

        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(_result);
        }
    }

    private sealed class BlockingExploreRunner : ICopilotSubagentRunner
    {
        private int _activeRuns;
        private int _callCount;
        private int _maximumActiveRuns;

        public ConcurrentQueue<CopilotSubagentRunRequest> RunRequests { get; } = new();

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
            RunRequests.Enqueue(runRequest);
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
                    RunId = runRequest.RunId,
                    RequestTokenBudget = runRequest.RequestTokenBudget,
                    QueueDurationMs = runRequest.QueueDurationMs,
                    Answer = "completed " + runRequest.Task,
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

    private sealed class BudgetConsumingExploreRunner : ICopilotSubagentRunner
    {
        private int _callCount;

        public ConcurrentQueue<CopilotSubagentRunRequest> RunRequests { get; } = new();

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RunRequests.Enqueue(runRequest);
            Interlocked.Increment(ref _callCount);
            return Task.FromResult(new CopilotSubagentResult
            {
                RunId = runRequest.RunId,
                RequestTokenBudget = runRequest.RequestTokenBudget,
                QueueDurationMs = runRequest.QueueDurationMs,
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
