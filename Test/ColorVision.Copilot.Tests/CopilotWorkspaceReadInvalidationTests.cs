using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using Microsoft.Extensions.AI;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceReadInvalidationTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotPostMutationRead-").FullName;

    [Theory]
    [InlineData("ReadLocalFile", true, "changed")]
    [InlineData("ReadAttachedFile", true, "changed")]
    [InlineData("GrepText", true, "changed")]
    [InlineData("SearchFiles", true, "changed")]
    [InlineData("ListDirectory", true, "changed")]
    [InlineData("ReadLocalFile", false, "changed")]
    [InlineData("GrepText", false, "changed")]
    [InlineData("ReadLocalFile", true, "unchanged")]
    [InlineData("ReadLocalFile", true, "failed")]
    [InlineData("ReadLocalFile", true, "recheck")]
    [InlineData("ReadLocalFile", true, "failed-read")]
    [InlineData("ReadAttachedFile", true, "failed-read")]
    [InlineData("ReadLocalFile", false, "failed-read")]
    [InlineData("ReadLocalFile", true, "failed-read-recheck")]
    [InlineData("ReadLocalFile", true, "budget")]
    public async Task LateReadCompletionDoesNotPoisonPostMutationRefresh(string name, bool affectedScope, string scenario)
    {
        var readSucceeds = !scenario.StartsWith("failed-read", StringComparison.Ordinal);
        var writeSucceeds = scenario is not ("failed" or "recheck" or "failed-read-recheck");
        var requiresRecheck = scenario is "recheck" or "failed-read-recheck";
        var changesContent = scenario != "unchanged";
        var canRefresh = affectedScope && (readSucceeds ? writeSucceeds && changesContent || requiresRecheck : writeSucceeds) && scenario != "budget";
        var scope = Directory.CreateDirectory(Path.Combine(_workspace, "scope")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(_workspace, "scope-sibling")).FullName;
        var path = Path.Combine(scope, "camera.json");
        var changedPath = affectedScope ? path : Path.Combine(sibling, "camera.json");
        if (readSucceeds) File.WriteAllText(path, "{\"gain\":1}");
        if (!affectedScope) File.WriteAllText(changedPath, "{\"gain\":1}");
        ICopilotTool reader = name switch
        {
            "ReadAttachedFile" => new CopilotReadAttachedFileTool(),
            "GrepText" => new CopilotGrepTextTool(),
            "SearchFiles" => new CopilotSearchFilesTool(),
            "ListDirectory" => new CopilotListDirectoryTool(),
            _ => new CopilotReadLocalFileTool(),
        };
        var writer = new FixtureMutationTool(changedPath, writeSucceeds, changesContent, requiresRecheck: requiresRecheck);
        var request = new CopilotAgentRequest
        {
            ConversationId = "late-read", TaskId = name, WorkspacePath = _workspace, Mode = CopilotAgentMode.Code,
            UserText = "修改后核对文件", SearchRootPaths = [_workspace], WritableLocalRootPaths = [_workspace],
            Attachments = [new() { Type = CopilotAttachmentType.File, Value = path }], CodexHooksEnabled = false,
        };
        var hook = new HeldReadHook();
        CopilotToolResult? firstReadResult = null;
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [reader, writer], scenario == "budget" ? 3 : 8, new CopilotToolExecutor([hook]),
            new CopilotFrameworkApprovalCoordinator(), e =>
            {
                if (e.Type == CopilotAgentEventType.ToolResult && e.ToolResult?.ToolName == name)
                    Interlocked.CompareExchange(ref firstReadResult, e.ToolResult, null);
            }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var read = Assert.Single(functions, f => f.Name != "colorvision_fixture_mutation");
        var write = Assert.Single(functions, f => f.Name == "colorvision_fixture_mutation");
        AIFunctionArguments input = name switch
        {
            "GrepText" => new() { ["query"] = "gain", ["path"] = scope },
            "SearchFiles" => new() { ["query"] = "camera", ["path"] = scope },
            "ListDirectory" => new() { ["path"] = scope },
            _ => new() { ["path"] = path },
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var continuation = new PausedReadCompletionContext();
        Task<object?> firstRead;
        var previousContext = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(continuation);
            firstRead = read.InvokeAsync(input, cancellation.Token).AsTask();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        try
        {
            hook.Release.TrySetResult();
            await continuation.RunUntilAsync(() => Volatile.Read(ref firstReadResult) != null, cancellation.Token);
            // The real reader produced its old snapshot, but the bridge has not recorded it.
            Assert.Empty(bridge.StepRecords);
            Assert.False(firstRead.IsCompleted);
            await write.InvokeAsync(new AIFunctionArguments(), cancellation.Token);
            Assert.Equal(writeSucceeds, Assert.Single(bridge.StepRecords).Observation.Success);
            Assert.Equal((writeSucceeds && changesContent || requiresRecheck) ? "{\"gain\":2}" : "{\"gain\":1}", File.ReadAllText(changedPath));
            Assert.False(firstRead.IsCompleted);
            await read.InvokeAsync(input, cancellation.Token);
            Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
            continuation.Resume();
            await firstRead.WaitAsync(cancellation.Token);
            Assert.Equal(readSucceeds, firstReadResult!.Success);
            if (readSucceeds && name is "ReadLocalFile" or "ReadAttachedFile" or "GrepText")
                Assert.Contains("\"gain\":1", firstReadResult!.Content, StringComparison.Ordinal);
            var recordedBeforeRefresh = bridge.StepRecords.Count;
            var nextRead = await read.InvokeAsync(input, cancellation.Token);
            if (scenario == "budget")
            {
                Assert.True(bridge.ToolBudgetExhausted);
                Assert.Equal(recordedBeforeRefresh, bridge.StepRecords.Count);
                Assert.Contains("3-call", nextRead?.ToString(), StringComparison.Ordinal);
                Assert.Equal(1, writer.Calls);
                return;
            }
            var refreshed = bridge.StepRecords.Last().Observation;
            Assert.Equal(canRefresh, refreshed.Success);
            if (canRefresh && name is "ReadLocalFile" or "ReadAttachedFile" or "GrepText")
                Assert.Contains("\"gain\":2", refreshed.Content, StringComparison.Ordinal);
            await read.InvokeAsync(input, cancellation.Token);
            Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
            await write.InvokeAsync(new AIFunctionArguments(), cancellation.Token);
            Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
            Assert.Equal(1, writer.Calls);
        }
        finally
        {
            hook.Release.TrySetResult();
            continuation.Resume();
            await firstRead.WaitAsync(cancellation.Token);
        }
    }

    private sealed class PausedReadCompletionContext : SynchronizationContext, IDisposable
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _callbacks = new();
        private readonly SemaphoreSlim _posted = new(0);
        private bool _resumed;
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (_callbacks)
            {
                if (!_resumed) { _callbacks.Enqueue((callback, state)); _posted.Release(); return; }
            }
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
        public async Task RunUntilAsync(Func<bool> ready, CancellationToken cancellationToken)
        {
            while (!ready())
            {
                await _posted.WaitAsync(cancellationToken);
                if (ready()) return;
                (SendOrPostCallback Callback, object? State) callback;
                lock (_callbacks) callback = _callbacks.Dequeue();
                var previous = Current;
                try { SetSynchronizationContext(null); callback.Callback(callback.State); }
                finally { SetSynchronizationContext(previous); }
            }
        }
        public void Resume()
        {
            lock (_callbacks)
            {
                _resumed = true;
                while (_callbacks.TryDequeue(out var callback))
                    ThreadPool.QueueUserWorkItem(static pending => pending.Callback(pending.State), callback, preferLocal: false);
            }
        }
        public void Dispose() { Resume(); _posted.Dispose(); }
    }

    [Theory]
    [InlineData("GrepText", "colorvision_grep_text")]
    [InlineData("SearchFiles", "colorvision_search_files")]
    [InlineData("ListDirectory", "colorvision_list_directory")]
    [InlineData("ReadLocalFile", "colorvision_read_local_file")]
    public async Task RuntimeCanRepeatTheOriginalQueryAfterApplyingARealPatch(string name, string functionName)
    {
        using var solutionScope = new VerificationWorkspaceScope(_workspace);
        var provider = new PatchVerificationChatClient(functionName);
        var store = new CopilotWorkspacePatchStore();
        ICopilotTool[] tools = [new CopilotReadLocalFileTool(), new CopilotGrepTextTool(), new CopilotSearchFilesTool(), new CopilotListDirectoryTool(),
            new CopilotPreviewWorkspacePatchEnvelopeTool(store), new CopilotApplyWorkspacePatchEnvelopeTool(store)];
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "patch-verification", "Patch verification", tools);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry(tools),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(hooks: []), _ => provider,
            new EmptyExternalTools(), catalog, new CopilotAgentSkillUsageStore(Path.Combine(_workspace, "skills")));
        var conversationId = "patch-verification-" + name;
        var access = new CopilotAgentAccessContext();
        access.PrepareFullAccess(conversationId, _workspace, name, DateTimeOffset.UtcNow.AddMinutes(5));
        var request = new CopilotAgentRequest
        {
            ConversationId = conversationId, TaskId = name, WorkspacePath = _workspace,
            UserText = "搜索 camera，创建 camera.json 并使用原查询核验。", TaskIntentText = "创建并验证 camera.json。",
            Mode = CopilotAgentMode.Code, SearchRootPaths = [_workspace], WritableLocalRootPaths = [_workspace],
            AccessContext = access, HarnessFeatures = CopilotAgentHarnessFeatures.None, CodexHooksEnabled = false,
            CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
            Profile = new() { ProviderType = CopilotProviderType.OpenAICompatible, VendorType = CopilotVendorType.Custom,
                BaseUrl = "https://example.test/v1", ApiKey = "test-key", Model = "test-model", MaxTokens = 4096 },
            RunBudgetOverride = new() { MaxToolCalls = 4, MaxAgentPasses = 1, RequestTokenBudget = 32768, TotalDuration = TimeSpan.FromSeconds(30) },
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await runtime.RunAsync(request, e =>
        {
            if (e.Type == CopilotAgentEventType.ToolResult && e.ToolResult is { ToolName: "PreviewWorkspacePatchEnvelope", Success: true } preview)
                provider.ChangeSetId = preview.Content.Split('\n').Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))
                    ["change_set_id:".Length..].Trim();
        }, cancellation.Token);

        var observations = result.StepRecords.Where(s => s.ToolCall.ToolName == name).ToArray();
        Assert.Equal(2, observations.Length);
        Assert.Equal(name != "ReadLocalFile", observations[0].Observation.Success);
        Assert.All(result.StepRecords.Skip(1), step => Assert.True(step.Observation.Success, step.Observation.ErrorMessage));
        Assert.DoesNotContain("camera.json", observations[0].Observation.Content, StringComparison.Ordinal);
        Assert.Contains("camera.json", observations[1].Observation.Content, StringComparison.Ordinal);
        Assert.Single(result.StepRecords, s => s.ToolCall.ToolName == "ApplyWorkspacePatchEnvelope");
        Assert.Equal("{\"camera\":2}", File.ReadAllText(Path.Combine(_workspace, "camera.json")));
        Assert.Equal(4, result.StepRecords.Count);
    }

    [Theory]
    [InlineData("ReadLocalFile", "created", true)]
    [InlineData("ReadAttachedFile", "created", true)]
    [InlineData("ReadLocalFile", "batch-created", true)]
    [InlineData("ReadAttachedFile", "batch-created", true)]
    [InlineData("ReadLocalFile", "relative", true)]
    [InlineData("ReadLocalFile", "multiple-roots", true)]
    [InlineData("ReadLocalFile", "existing-damaged", false)]
    [InlineData("ReadLocalFile", "unrelated", false)]
    [InlineData("ReadLocalFile", "uncertain", false)]
    [InlineData("ReadLocalFile", "failed", false)]
    [InlineData("ReadLocalFile", "denied", false)]
    [InlineData("ReadLocalFile", "budget", false)]
    public async Task FailedFileReadRefreshRequiresConfirmedCreationAndCurrentPermission(string name, string scenario, bool canReadAgain)
    {
        var scope = Directory.CreateDirectory(Path.Combine(_workspace, "scope")).FullName;
        var target = Path.Combine(scenario == "denied" ? _workspace : scope, "camera.json");
        if (scenario == "existing-damaged") File.WriteAllText(target, "\0damaged-export");
        var writer = new FixtureMutationTool(scenario == "unrelated" ? Path.Combine(scope, "other.json") : target,
            scenario is not ("failed" or "uncertain"), true, requiresRecheck: scenario == "uncertain");
        ICopilotTool reader = name == "ReadAttachedFile" ? new CopilotReadAttachedFileTool() : new CopilotReadLocalFileTool();
        var request = new CopilotAgentRequest
        {
            ConversationId = "created-read", TaskId = scenario, WorkspacePath = scope, Mode = CopilotAgentMode.Code,
            UserText = "创建后读取并核验配置文件",
            SearchRootPaths = scenario == "multiple-roots" ? [Directory.CreateDirectory(Path.Combine(_workspace, "first")).FullName, scope] : [scope],
            ReadableLocalFilePaths = scenario == "batch-created" ? [target] : [],
            Attachments = [new() { Type = CopilotAttachmentType.File, Value = target }],
            WritableLocalRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [reader, writer], scenario == "budget" ? 2 : 8,
            new CopilotToolExecutor(hooks: []), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var read = Assert.Single(functions, function => function.Name != "colorvision_fixture_mutation");
        var write = Assert.Single(functions, function => function.Name == "colorvision_fixture_mutation");
        var input = scenario == "batch-created" ? new AIFunctionArguments()
            : new AIFunctionArguments { ["path"] = scenario is "relative" or "multiple-roots" ? "camera.json" : target };
        await read.InvokeAsync(input);
        Assert.False(bridge.StepRecords.Last().Observation.Success);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(scenario is not ("failed" or "uncertain"), bridge.StepRecords.Last().Observation.Success);
        var refreshedResult = await read.InvokeAsync(input);
        if (scenario == "budget")
        {
            Assert.True(bridge.ToolBudgetExhausted);
            Assert.Contains("2-call", refreshedResult?.ToString());
            Assert.Equal(2, bridge.StepRecords.Count);
            return;
        }
        var refreshed = bridge.StepRecords.Last().Observation;
        Assert.Equal(canReadAgain, refreshed.Success);
        if (canReadAgain) Assert.Contains("\"gain\":2", refreshed.Content);
        else if (scenario == "denied")
        {
            Assert.Contains("outside the allowed workspace roots", refreshed.ErrorMessage);
            Assert.Empty(refreshed.SuccessfullyReadLocalFilePaths);
        }
        else Assert.Equal(CopilotToolFailureKind.Conflict, refreshed.FailureKind);
        await read.InvokeAsync(input);
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        Assert.Equal(1, writer.Calls);
    }

    [Theory]
    [InlineData("GrepText", true)]
    [InlineData("SearchFiles", true)]
    [InlineData("ListDirectory", false)]
    public async Task DefaultScopeUsesWhatWasActuallyObservedAndRetainsTheCallBudget(string name, bool observesSecondRoot)
    {
        var first = Directory.CreateDirectory(Path.Combine(_workspace, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(_workspace, "second")).FullName;
        var target = Path.Combine(second, "camera.json");
        var writer = new FixtureMutationTool(target, true, true);
        ICopilotTool reader = name switch { "GrepText" => new CopilotGrepTextTool(), "SearchFiles" => new CopilotSearchFilesTool(), _ => new CopilotListDirectoryTool() };
        var request = new CopilotAgentRequest
        {
            ConversationId = "default-scope", TaskId = name, WorkspacePath = first, Mode = CopilotAgentMode.Code,
            UserText = "查找 camera。", SearchRootPaths = [first, second], WritableLocalRootPaths = [second], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [reader, writer], 3, new CopilotToolExecutor(hooks: []),
            new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var observe = Assert.Single(functions, f => f.Name != "colorvision_fixture_mutation");
        var write = Assert.Single(functions, f => f.Name == "colorvision_fixture_mutation");
        var input = name == "ListDirectory" ? new AIFunctionArguments() : new AIFunctionArguments { ["query"] = "camera" };
        await observe.InvokeAsync(input);
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        await observe.InvokeAsync(input);
        Assert.Equal(observesSecondRoot, bridge.StepRecords.Last().Observation.Success);
        // An accepted user update must not reset the three calls consumed above.
        bridge.NotifyUserInputAccepted();
        await observe.InvokeAsync(input);
        Assert.True(bridge.ToolBudgetExhausted);
        Assert.Equal(observesSecondRoot ? 3 : 2, bridge.StepRecords.Count(s => s.Observation.Success));
    }

    [Theory]
    [InlineData("ReadAttachedFile", "update", true, true)]
    [InlineData("GrepText", "update", true, true)]
    [InlineData("GrepText", "create", true, true)]
    [InlineData("GrepText", "delete", true, true)]
    [InlineData("SearchFiles", "create", true, true)]
    [InlineData("SearchFiles", "delete", true, true)]
    [InlineData("ListDirectory", "create", true, true)]
    [InlineData("ListDirectory", "delete", true, true)]
    [InlineData("GrepText", "update", false, false)]
    [InlineData("GrepText", "unchanged", true, false)]
    public async Task ConfirmedMutationRefreshesOnlyObservationsOfTheAffectedScope(string name, string operation, bool succeeds, bool canObserveAgain)
    {
        var scope = Directory.CreateDirectory(Path.Combine(_workspace, "scope")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(_workspace, "scope-sibling")).FullName;
        var target = Path.Combine(scope, "camera.json");
        var other = Path.Combine(sibling, "camera.json");
        if (operation != "create") File.WriteAllText(target, "{\"gain\":1}");
        File.WriteAllText(other, "{\"gain\":3}");
        var writer = new FixtureMutationTool(target, succeeds, operation != "unchanged", operation == "delete");
        ICopilotTool reader = name switch
        {
            "GrepText" => new CopilotGrepTextTool(),
            "SearchFiles" => new CopilotSearchFilesTool(),
            "ListDirectory" => new CopilotListDirectoryTool(),
            _ => new CopilotReadAttachedFileTool(),
        };
        var request = new CopilotAgentRequest
        {
            ConversationId = "post-mutation-scope", TaskId = name, WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "修改文件后重新核对搜索结果。", SearchRootPaths = [_workspace],
            Attachments = [new() { Type = CopilotAttachmentType.File, Value = target }, new() { Type = CopilotAttachmentType.File, Value = other }],
            WritableLocalRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [reader, writer], 8, new CopilotToolExecutor(hooks: []),
            new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var observe = Assert.Single(functions, f => f.Name != "colorvision_fixture_mutation");
        var write = Assert.Single(functions, f => f.Name == "colorvision_fixture_mutation");
        AIFunctionArguments Input(string root, string file) => name switch
        {
            "GrepText" => new() { ["query"] = "\"gain\":2", ["path"] = root },
            "SearchFiles" => new() { ["query"] = "camera", ["path"] = root },
            "ListDirectory" => new() { ["path"] = root },
            _ => new() { ["path"] = file },
        };
        var input = Input(scope, target);
        var otherInput = Input(sibling, other);
        await observe.InvokeAsync(input);
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        await observe.InvokeAsync(otherInput);
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(succeeds, bridge.StepRecords.Last().Observation.Success);
        await observe.InvokeAsync(input);
        var verification = bridge.StepRecords.Last().Observation;
        Assert.Equal(canObserveAgain, verification.Success);
        if (canObserveAgain)
        {
            if (operation == "delete") Assert.DoesNotContain(name == "GrepText" ? "[Match]" : "camera.json", verification.Content, StringComparison.Ordinal);
            else Assert.Contains(name is "SearchFiles" or "ListDirectory" ? "camera.json" : "\"gain\":2", verification.Content, StringComparison.Ordinal);
        }
        await observe.InvokeAsync(otherInput);
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        await observe.InvokeAsync(input);
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        Assert.Equal(1, writer.Calls);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    public async Task AcceptedUserUpdatePermitsReadingTheSameFileAgainWithinTheActiveRun(bool targetActiveTask, bool fileInitiallyExists, bool withRemainingTodo)
    {
        var path = Path.Combine(_workspace, "camera.json");
        if (fileInitiallyExists) File.WriteAllText(path, "{\"gain\":1}");
        var provider = new RepeatedReadChatClient(path, withRemainingTodo);
        ICopilotTool[] tools = [new CopilotReadLocalFileTool()];
        var catalog = new CopilotCapabilityCatalog();
        catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "steered-file-read", "Steered file read", tools);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(new CopilotToolRegistry(tools),
            new CopilotAgentContextBuilder(), new CopilotToolExecutor(), _ => provider,
            new EmptyExternalTools(), catalog, new CopilotAgentSkillUsageStore(Path.Combine(_workspace, "skills")));
        var request = new CopilotAgentRequest
        {
            ConversationId = "steered-read-conversation", TaskId = "steered-read-task", WorkspacePath = _workspace,
            UserText = withRemainingTodo ? "请先制定计划，读取 camera.json 中当前的 gain，再继续生成报告。" : "读取 camera.json 中当前的 gain。", Mode = CopilotAgentMode.Code,
            ReadableLocalFilePaths = [path], SearchRootPaths = [_workspace],
            HarnessFeatures = withRemainingTodo ? CopilotAgentHarnessFeatures.TaskLedger : CopilotAgentHarnessFeatures.None, CodexHooksEnabled = false,
            Profile = new() { ProviderType = CopilotProviderType.OpenAICompatible, VendorType = CopilotVendorType.Custom,
                BaseUrl = "https://example.test/v1", ApiKey = "test-key", Model = "test-model", MaxTokens = 4096 },
            RunBudgetOverride = new() { MaxToolCalls = 8, MaxAgentPasses = 1, RequestTokenBudget = 32768, TotalDuration = TimeSpan.FromSeconds(30) },
        };
        CopilotSteeringAdmissionResult? admission = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await runtime.RunAsync(request, e =>
        {
            if (e.Type != CopilotAgentEventType.ToolResult || e.ToolResult?.ToolName != "ReadLocalFile" || admission != null) return;
            File.WriteAllText(path, "{\"gain\":2}");
            admission = runtime.EnqueueSteeringMessage(targetActiveTask ? request.TaskId : "another-task",
                "我已补齐或更新刚才的文件，请重新读取并使用当前 gain。");
        }, cancellation.Token);

        Assert.NotNull(admission);
        Assert.Equal(targetActiveTask, admission.Value.IsAccepted);
        var reads = result.StepRecords.Where(s => s.ToolCall.ToolName == "ReadLocalFile").ToArray();
        Assert.Equal(2, reads.Length);
        Assert.Equal(fileInitiallyExists, reads[0].Observation.Success);
        if (fileInitiallyExists) Assert.Contains("\"gain\":1", reads[0].Observation.Content);
        Assert.Equal(targetActiveTask, reads[1].Observation.Success);
        if (targetActiveTask)
        {
            Assert.Contains("\"gain\":2", reads[1].Observation.Content);
            Assert.Contains("我已补齐或更新刚才的文件", provider.SecondInput);
            var remainingWork = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute", Items = [new() { Id = 1, Title = "继续生成报告" }],
            };
            Assert.Empty(CopilotAgentBlockerDetector.Detect(remainingWork, result.StepRecords, CopilotAgentStopReason.TaskPassLimit));
        }
        else Assert.Equal(CopilotToolFailureKind.Conflict, reads[1].Observation.FailureKind);
        if (withRemainingTodo)
        {
            Assert.Equal(1, result.TaskLedger.RemainingCount);
            Assert.NotNull(result.SessionCheckpoint);
            Assert.Equal(targetActiveTask ? CopilotAgentStopReason.TaskPassLimit : CopilotAgentStopReason.Blocked, result.StopReason);
            var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
            {
                AgentTaskLedger = result.TaskLedger, AgentStopReason = result.StopReason, AgentBlockers = result.Blockers,
            };
            Assert.Equal(targetActiveTask, message.HasRecoverableAgentTasks);
            if (targetActiveTask)
            {
                Assert.Empty(result.Blockers);
                Assert.DoesNotContain(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.BlockerDetected);
            }
        }
    }

    [Theory]
    [InlineData("ReadLocalFile", false)]
    [InlineData("ReadAttachedFile", false)]
    [InlineData("GrepText", false)]
    [InlineData("SearchFiles", false)]
    [InlineData("ListDirectory", false)]
    [InlineData("ReadLocalFile", true)]
    [InlineData("ReadAttachedFile", true)]
    [InlineData("GrepText", true)]
    [InlineData("SearchFiles", true)]
    [InlineData("ListDirectory", true)]
    public async Task EachNewInputAllowsOneFreshLocalObservationWithoutResettingTheBudget(string name, bool initialReadFails)
    {
        var directory = Path.Combine(_workspace, "line-a");
        var path = Path.Combine(directory, "camera.json");
        if (!initialReadFails)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"gain\":1}");
        }
        ICopilotTool tool = name switch
        {
            "ReadAttachedFile" => new CopilotReadAttachedFileTool(),
            "GrepText" => new CopilotGrepTextTool(),
            "SearchFiles" => new CopilotSearchFilesTool(),
            "ListDirectory" => new CopilotListDirectoryTool(),
            _ => new CopilotReadLocalFileTool(),
        };
        var request = new CopilotAgentRequest
        {
            ConversationId = "fresh-observation", TaskId = name, WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "检查工作区文件", SearchRootPaths = [_workspace],
            Attachments = [new() { Type = CopilotAttachmentType.File, Value = path }], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [tool], 6, new CopilotToolExecutor(hooks: []),
            new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var function = Assert.Single(bridge.CreateFunctions().OfType<AIFunction>());
        var input = name switch
        {
            "GrepText" => new AIFunctionArguments { ["query"] = "gain", ["path"] = directory },
            "SearchFiles" => new AIFunctionArguments { ["query"] = "camera", ["path"] = directory },
            "ListDirectory" => new AIFunctionArguments { ["path"] = directory },
            _ => new AIFunctionArguments { ["path"] = path },
        };
        for (var revision = 0; revision < 3; revision++)
        {
            if (revision > 0)
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, "{\"gain\":2}");
                bridge.NotifyUserInputAccepted();
            }
            await function.InvokeAsync(input);
            Assert.Equal(revision > 0 || !initialReadFails, bridge.StepRecords.Last().Observation.Success);
            await function.InvokeAsync(input);
            Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        }
        bridge.NotifyUserInputAccepted();
        await function.InvokeAsync(input);
        Assert.True(bridge.ToolBudgetExhausted);
        Assert.Equal(initialReadFails ? 2 : 3, bridge.StepRecords.Count(s => s.Observation.Success));
    }

    [Fact]
    public async Task NewUserInputDoesNotReopenCompletedWrites()
    {
        var path = Path.Combine(_workspace, "camera.json");
        File.WriteAllText(path, "{\"gain\":1}");
        var writer = new FixtureMutationTool(path, true, true);
        var request = new CopilotAgentRequest
        {
            ConversationId = "read-refresh-boundaries", TaskId = "write-once", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "更新相机配置", SearchRootPaths = [_workspace],
            WritableLocalRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [writer], 8,
            new CopilotToolExecutor(hooks: []), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var write = Assert.Single(functions, f => f.Name == "colorvision_fixture_mutation");
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        bridge.NotifyUserInputAccepted();
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
        Assert.Equal(1, writer.Calls);
    }

    [Fact]
    public async Task NewUserInputDoesNotAuthorizePreviouslyDeniedPaths()
    {
        var allowed = Directory.CreateDirectory(Path.Combine(_workspace, "allowed")).FullName;
        var path = Path.Combine(_workspace, "outside.json");
        File.WriteAllText(path, "{\"gain\":77}");
        var request = new CopilotAgentRequest
        {
            ConversationId = "denied-read-refresh", TaskId = "read-once", WorkspacePath = allowed,
            Mode = CopilotAgentMode.Code, UserText = "读取相机配置", SearchRootPaths = [allowed], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [new CopilotReadLocalFileTool()], 4,
            new CopilotToolExecutor(hooks: []), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var read = Assert.Single(bridge.CreateFunctions().OfType<AIFunction>());
        for (var revision = 0; revision < 2; revision++)
        {
            if (revision > 0) bridge.NotifyUserInputAccepted();
            await read.InvokeAsync(new AIFunctionArguments { ["path"] = path });
            var observation = bridge.StepRecords.Last().Observation;
            Assert.False(observation.Success);
            Assert.Contains("outside the allowed workspace roots", observation.ErrorMessage);
            Assert.Empty(observation.SuccessfullyReadLocalFilePaths);
            Assert.DoesNotContain("\"gain\":77", observation.Content);
        }
        Assert.Equal("{\"gain\":77}", File.ReadAllText(path));
    }

    [Fact]
    public async Task NewUserInputCannotStartASecondCopyOfAnInFlightRead()
    {
        var path = Path.Combine(_workspace, "camera.json");
        File.WriteAllText(path, "{\"gain\":1}");
        var hook = new HeldReadHook();
        var request = new CopilotAgentRequest
        {
            ConversationId = "in-flight-refresh", TaskId = "read-once", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "读取相机配置", ReadableLocalFilePaths = [path],
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [new CopilotReadLocalFileTool()], 6,
            new CopilotToolExecutor([hook]), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var function = Assert.Single(bridge.CreateFunctions().OfType<AIFunction>());
        var input = new AIFunctionArguments { ["path"] = path };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var firstRead = function.InvokeAsync(input, cancellation.Token).AsTask();
        try
        {
            await hook.Entered.Task.WaitAsync(cancellation.Token);
            bridge.NotifyUserInputAccepted();
            await function.InvokeAsync(input, cancellation.Token);
            Assert.Equal(CopilotToolFailureKind.Conflict, bridge.StepRecords.Last().Observation.FailureKind);
            Assert.Equal(1, hook.Calls);
        }
        finally { hook.Release.TrySetResult(); }
        await firstRead;
        await function.InvokeAsync(input, cancellation.Token);
        Assert.True(bridge.StepRecords.Last().Observation.Success);
        Assert.Equal(2, hook.Calls);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public async Task OnlyConfirmedContentChangesRefreshCompletedLocalReads(bool succeeds, bool changesContent, bool canReadAgain)
    {
        var target = Path.Combine(_workspace, "camera.json");
        var other = Path.Combine(_workspace, "other.json");
        await File.WriteAllTextAsync(target, "{\"gain\":1}");
        await File.WriteAllTextAsync(other, "{\"gain\":3}");
        var writer = new FixtureMutationTool(target, succeeds, changesContent);
        var request = new CopilotAgentRequest
        {
            ConversationId = "post-mutation", TaskId = "read-verification", WorkspacePath = _workspace,
            Mode = CopilotAgentMode.Code, UserText = "Read local files, apply the requested change, then verify the file.",
            SearchRootPaths = [_workspace], ReadableLocalFilePaths = [target, other],
            WritableLocalRootPaths = [_workspace], CodexHooksEnabled = false,
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(request,
            CopilotExecutionScope.ForAgentRun(request), [new CopilotReadLocalFileTool(), writer], 10,
            new CopilotToolExecutor(hooks: []), new CopilotFrameworkApprovalCoordinator(), _ => { }, () => 1);
        var functions = bridge.CreateFunctions().OfType<AIFunction>().ToArray();
        var read = Assert.Single(functions, f => f.Name == "colorvision_read_local_file");
        var write = Assert.Single(functions, f => f.Name == "colorvision_fixture_mutation");
        var input = new AIFunctionArguments { ["path"] = target };
        await read.InvokeAsync(input);
        await read.InvokeAsync(new AIFunctionArguments { ["path"] = other });
        var writeResult = await write.InvokeAsync(new AIFunctionArguments());
        Assert.True(writer.Calls == 1, writeResult?.ToString());
        await read.InvokeAsync(input);

        var verification = bridge.StepRecords.Last();
        Assert.Equal(canReadAgain, verification.Observation.Success);
        if (canReadAgain) Assert.Contains("\"gain\":2", verification.Observation.Content);
        // Neither an unrelated read nor the non-idempotent write is reopened.
        await read.InvokeAsync(new AIFunctionArguments { ["path"] = other });
        Assert.False(bridge.StepRecords.Last().Observation.Success);
        await write.InvokeAsync(new AIFunctionArguments());
        Assert.False(bridge.StepRecords.Last().Observation.Success);
        Assert.Equal(1, writer.Calls);
        // The freshly verified read is again deduplicated until another mutation.
        await read.InvokeAsync(input);
        Assert.False(bridge.StepRecords.Last().Observation.Success);
    }

    private sealed class FixtureMutationTool(string path, bool succeeds, bool changesContent, bool deleteFile = false, bool requiresRecheck = false) : ICopilotAgentDrivenTool
    {
        public string Name => "FixtureMutation";
        public string Description => "Mutates only the isolated test file.";
        public int Calls { get; private set; }
        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent)
            with { ApprovalMode = CopilotToolApprovalMode.Never, RiskLevel = CopilotToolRiskLevel.Low };
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            Calls++;
            var beforeExists = File.Exists(path);
            var before = beforeExists ? await File.ReadAllTextAsync(path, cancellationToken) : string.Empty;
            var after = deleteFile ? string.Empty : changesContent ? "{\"gain\":2}" : before;
            if (succeeds || requiresRecheck)
            {
                if (deleteFile) File.Delete(path);
                else await File.WriteAllTextAsync(path, after, cancellationToken);
            }
            return new CopilotToolResult
            {
                ToolName = Name, Success = succeeds, Content = "Synthetic mutation result.",
                FailureKind = succeeds ? CopilotToolFailureKind.None : CopilotToolFailureKind.Transient,
                ErrorMessage = succeeds ? string.Empty : "Synthetic write failure.",
                WorkspaceMutation = succeeds ? new([new(path, beforeExists, before, !deleteFile, after)]) : null,
                WorkspaceRecheckPaths = requiresRecheck ? [path] : [],
            };
        }
    }

    private sealed class EmptyExternalTools : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken) => Task.FromResult(new CopilotExternalToolLease());
    }

    private sealed class PatchVerificationChatClient(string observationFunction) : IChatClient
    {
        private int _calls;
        public string ChangeSetId { get; set; } = string.Empty;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "已创建并核验。")) { FinishReason = ChatFinishReason.Stop });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            var call = ++_calls;
            var function = call switch { 2 => "colorvision_preview_workspace_patch_envelope", 3 => "colorvision_apply_workspace_patch_envelope", _ => observationFunction };
            Dictionary<string, object?> arguments = call switch
            {
                2 => new() { ["operations"] = new[] { new { operation = "add", path = "camera.json", content = "{\"camera\":2}" } } },
                3 => new() { ["changeSetId"] = ChangeSetId },
                _ when observationFunction == "colorvision_read_local_file" => new() { ["path"] = "camera.json" },
                _ => observationFunction == "colorvision_list_directory" ? new() : new() { ["query"] = "camera" },
            };
            if (call <= 4)
                yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent($"patch-verification-{call}", function, arguments)]) { FinishReason = ChatFinishReason.ToolCalls };
            else
                yield return new ChatResponseUpdate(ChatRole.Assistant, "已创建并核验。") { FinishReason = ChatFinishReason.Stop };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    // Bind the normal approval path to the disposable workspace without restoring
    // the user's application settings or opening a real solution window.
    private sealed class VerificationWorkspaceScope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previous = Instance.GetValue(null);

        public VerificationWorkspaceScope(string workspace)
        {
            var manager = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
            var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(explorer, new DirectoryInfo(workspace));
            typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(manager, explorer);
            Instance.SetValue(null, manager);
        }

        public void Dispose() => Instance.SetValue(null, _previous);
    }

    private sealed class HeldReadHook : ICopilotToolExecutionHook
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public async Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken)
        {
            Calls++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return CopilotToolExecutionHookDecision.Proceed;
        }
        public Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RepeatedReadChatClient(string path, bool withRemainingTodo) : IChatClient
    {
        private int _calls;
        public string SecondInput { get; private set; } = string.Empty;
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "已根据读取结果回答。")) { FinishReason = ChatFinishReason.Stop });

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            cancellationToken.ThrowIfCancellationRequested();
            var call = ++_calls;
            if (withRemainingTodo)
            {
                if (call == 1)
                {
                    var addTodos = Assert.Single(options!.Tools!.OfType<AIFunction>(), function => function.Name == "todos_add");
                    var parameter = Assert.Single(addTodos.JsonSchema.GetProperty("properties").EnumerateObject());
                    yield return new ChatResponseUpdate(ChatRole.Assistant,
                        [new FunctionCallContent("add-remaining-todo", addTodos.Name, new Dictionary<string, object?>
                        {
                            [parameter.Name] = System.Text.Json.JsonSerializer.SerializeToElement(new[] { new { title = "继续生成报告", description = "保持该项未完成，以检查读取恢复后的任务状态。" } }),
                        })]) { FinishReason = ChatFinishReason.ToolCalls };
                    yield break;
                }
                call--;
            }
            if (call == 2) SecondInput = string.Join("\n", messages.Select(m => m.Text));
            if (call <= 2)
                yield return new ChatResponseUpdate(ChatRole.Assistant,
                    [new FunctionCallContent($"read-{call}", "colorvision_read_local_file", new Dictionary<string, object?> { ["path"] = path })]) { FinishReason = ChatFinishReason.ToolCalls };
            else
                yield return new ChatResponseUpdate(ChatRole.Assistant, "已根据读取结果回答。") { FinishReason = ChatFinishReason.Stop };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    public void Dispose()
    {
        var fullPath = Path.GetFullPath(_workspace);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), fullPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("CopilotPostMutationRead-", Path.GetFileName(fullPath));
        Directory.Delete(fullPath, recursive: true);
    }
}
