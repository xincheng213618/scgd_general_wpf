using ColorVision.Copilot;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackgroundShellCommandTests
{
    [Theory]
    [InlineData("", (int)CopilotBackgroundShellCommandAction.List, 0)]
    [InlineData("2", (int)CopilotBackgroundShellCommandAction.Inspect, 2)]
    [InlineData("stop 3", (int)CopilotBackgroundShellCommandAction.Stop, 3)]
    [InlineData("clear", (int)CopilotBackgroundShellCommandAction.Clear, 0)]
    [InlineData("stop", (int)CopilotBackgroundShellCommandAction.Invalid, 0)]
    [InlineData("resume 1", (int)CopilotBackgroundShellCommandAction.Invalid, 0)]
    public void PsParserSeparatesBackgroundCommandsFromAgentTaskRecovery(
        string arguments,
        int expectedAction,
        int expectedPosition)
    {
        var request = CopilotBackgroundShellCommandDiagnostics.ParseCommand(arguments);
        var invocation = Assert.IsType<CopilotLocalCommandInvocation>(
            CopilotLocalCommandCatalog.Parse("/ps " + arguments));

        Assert.Equal(expectedAction, (int)request.Action);
        Assert.Equal(expectedPosition, request.Position);
        Assert.Equal(
            CopilotLocalCommandKind.BackgroundCommands,
            invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal("/ps [N|stop N|clear]", invocation.Command.Usage);
    }

    [Fact]
    public void BackgroundIntentUsesManagedStartInsteadOfForegroundShell()
    {
        var request = CreateRequest("run npm run dev in background");
        var foreground = new CopilotShellCommandTool();
        var background = new CopilotStartBackgroundShellCommandTool();
        var contract = CopilotAgentExecutionContract.Create(
            request,
            [foreground, background]);

        Assert.True(CopilotToolIntentPolicy.NeedsShellExecution(request));
        Assert.True(CopilotToolIntentPolicy.NeedsBackgroundShellExecution(request));
        Assert.False(foreground.IsAvailable(request));
        Assert.True(background.IsAvailable(request));
        Assert.Equal(CopilotAgentExecutionRequirement.ShellExecution, contract.Requirement);
        Assert.Equal(["StartBackgroundShellCommand"], contract.AcceptedToolNames);
        Assert.Contains(
            "StartBackgroundShellCommand",
            contract.BuildInitialInstruction(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DefaultToolCatalogContainsSeparateStartInspectAndStopSurfaces()
    {
        var tools = CopilotToolRegistry.CreateCoreDefaultTools();
        var start = Assert.Single(
            tools,
            tool => tool.Name == "StartBackgroundShellCommand");
        var inspect = Assert.Single(
            tools,
            tool => tool.Name == "InspectBackgroundShellCommands");
        var stop = Assert.Single(
            tools,
            tool => tool.Name == "StopBackgroundShellCommand");

        Assert.True(start.Capability.RequiresNativeApproval);
        Assert.Equal(CopilotToolAccess.ReadOnly, inspect.Capability.Access);
        Assert.False(inspect.Capability.RequiresNativeApproval);
        Assert.True(stop.Capability.RequiresNativeApproval);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, start.Capability.AuditArgumentMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, stop.Capability.AuditArgumentMode);
    }

    [Fact]
    public async Task ManagedLifecycleIsConversationScopedAndStopRequiresApproval()
    {
        var launcher = new FakeBackgroundLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);
        var startTool = new CopilotStartBackgroundShellCommandTool(registry);
        var inspectTool = new CopilotInspectBackgroundShellCommandsTool(registry);
        var stopTool = new CopilotStopBackgroundShellCommandTool(registry);
        var request = CreateRequest("run PowerShell in background");
        var input = CreateStartInput("Write-Output ready; Start-Sleep 30");
        try
        {
            var unapprovedStart = await startTool.ExecuteAsync(
                request,
                input,
                CancellationToken.None);
            Assert.False(unapprovedStart.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, unapprovedStart.FailureKind);

            var approval = startTool.CreateApprovalPresentation(request, input);
            Assert.Contains("Maximum lifetime: 600 seconds", approval.ReviewDetails);
            Assert.Contains("Write-Output ready; Start-Sleep 30", approval.ReviewDetails);
            Assert.Equal(CopilotApprovalReversibility.ManualOnly, approval.Reversibility);

            var started = await ((ICopilotFrameworkApprovedTool)startTool)
                .ExecuteApprovedAsync(request, input, CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);
            Assert.Contains("background_id: bg:", started.Content, StringComparison.Ordinal);
            var snapshot = Assert.Single(registry.GetSnapshots(request.ConversationId));
            Assert.Equal(CopilotBackgroundShellCommandState.Running, snapshot.State);
            Assert.Equal(4_242, snapshot.ProcessId);
            Assert.Equal(request.TaskId, snapshot.TaskId);
            Assert.DoesNotContain("Start-Sleep 30", snapshot.StandardOutput);

            launcher.LastProcess!.SetOutput(
                new string(
                    'x',
                    CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters + 1_000)
                + "ready token=background-secret\n",
                "warning\n");
            var boundedSnapshot = Assert.Single(
                registry.GetSnapshots(request.ConversationId));
            Assert.True(
                boundedSnapshot.StandardOutput.Length
                <= CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters);
            Assert.Contains(
                "...<earlier background output truncated>...",
                boundedSnapshot.StandardOutput,
                StringComparison.Ordinal);
            Assert.Contains("token=<redacted>", boundedSnapshot.StandardOutput);
            Assert.DoesNotContain("background-secret", boundedSnapshot.StandardOutput);
            var inspectInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["backgroundId"] = snapshot.Id,
                },
            };
            var inspected = await inspectTool.ExecuteAsync(
                request,
                inspectInput,
                CancellationToken.None);
            Assert.True(inspected.Success);
            Assert.Contains("state: running", inspected.Content, StringComparison.Ordinal);
            Assert.Contains("ready", inspected.Content, StringComparison.Ordinal);
            Assert.Contains("warning", inspected.Content, StringComparison.Ordinal);

            var otherConversation = CreateRequest(
                "check background output",
                conversationId: "conversation-other");
            var crossConversation = await inspectTool.ExecuteAsync(
                otherConversation,
                inspectInput,
                CancellationToken.None);
            Assert.False(crossConversation.Success);
            Assert.Equal(CopilotToolFailureKind.NotFound, crossConversation.FailureKind);

            var stopInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["backgroundId"] = snapshot.Id,
                },
            };
            var unapprovedStop = await stopTool.ExecuteAsync(
                request,
                stopInput,
                CancellationToken.None);
            Assert.False(unapprovedStop.Success);
            Assert.Equal(CopilotToolFailureKind.Authorization, unapprovedStop.FailureKind);

            var stopped = await ((ICopilotFrameworkApprovedTool)stopTool)
                .ExecuteApprovedAsync(request, stopInput, CancellationToken.None);
            Assert.True(stopped.Success, stopped.ErrorMessage);
            Assert.Equal(1, launcher.LastProcess.StopCount);
            var stoppedSnapshot = Assert.Single(registry.GetSnapshots(request.ConversationId));
            Assert.Equal(CopilotBackgroundShellCommandState.Stopped, stoppedSnapshot.State);
            Assert.Equal(1, registry.ClearCompleted(request.ConversationId));
            Assert.Empty(registry.GetSnapshots(request.ConversationId));
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task RegistryBoundsActiveCommandsAndShutdownStopsEveryProcess()
    {
        var launcher = new FakeBackgroundLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);
        var request = CreateRequest("run PowerShell in background");
        try
        {
            for (var index = 0;
                 index < CopilotBackgroundShellCommandRegistry.MaximumActivePerConversation;
                 index++)
            {
                var result = await registry.StartAsync(
                    request,
                    CreateStartInput($"Write-Output {index}; Start-Sleep 30"),
                    CancellationToken.None);
                Assert.True(result.Success, result.ErrorMessage);
            }

            var rejected = await registry.StartAsync(
                request,
                CreateStartInput("Write-Output overflow; Start-Sleep 30"),
                CancellationToken.None);

            Assert.False(rejected.Success);
            Assert.Equal(CopilotToolFailureKind.Transient, rejected.FailureKind);
            Assert.Contains(
                CopilotBackgroundShellCommandRegistry.MaximumActivePerConversation.ToString(),
                rejected.ErrorMessage,
                StringComparison.Ordinal);
        }
        finally
        {
            await registry.ShutdownAsync();
        }

        Assert.Equal(
            CopilotBackgroundShellCommandRegistry.MaximumActivePerConversation,
            launcher.Processes.Count);
        Assert.All(launcher.Processes, process => Assert.Equal(1, process.StopCount));
        Assert.All(launcher.Processes, process => Assert.True(process.IsDisposed));
    }

    [Fact]
    public async Task ConcurrentStartReservationsAreScopedToTheirConversation()
    {
        var launcher = new GatedBackgroundLauncher();
        var registry = new CopilotBackgroundShellCommandRegistry(launcher);
        var starts = Enumerable.Range(0, 5)
            .Select(index => registry.StartAsync(
                CreateRequest(
                    "run PowerShell in background",
                    conversationId: $"conversation-background-{index}"),
                CreateStartInput($"Write-Output {index}; Start-Sleep 30"),
                CancellationToken.None))
            .ToArray();
        try
        {
            var pendingStarts = launcher.PendingCount;
            launcher.ReleaseAll();
            var results = await Task.WhenAll(starts);

            Assert.Equal(5, pendingStarts);
            Assert.All(results, result => Assert.True(result.Success, result.ErrorMessage));
        }
        finally
        {
            launcher.ReleaseAll();
            await Task.WhenAll(starts);
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task RealPowerShellProcessCompletesAndPublishesBoundedOutput()
    {
        if (!OperatingSystem.IsWindows()
            || string.IsNullOrWhiteSpace(
                CopilotShellCommandService.FindTrustedShellExecutable(
                    CopilotShellKind.PowerShell)))
        {
            return;
        }

        var registry = new CopilotBackgroundShellCommandRegistry();
        var request = CreateRequest("run PowerShell in background");
        try
        {
            var started = await registry.StartAsync(
                request,
                CreateStartInput(
                    "Write-Output 'background-evidence'; Start-Sleep -Milliseconds 150"),
                CancellationToken.None);
            Assert.True(started.Success, started.ErrorMessage);

            CopilotBackgroundShellCommandSnapshot snapshot;
            var stopwatch = Stopwatch.StartNew();
            do
            {
                await Task.Delay(50);
                snapshot = Assert.Single(registry.GetSnapshots(request.ConversationId));
            }
            while (snapshot.IsActive && stopwatch.Elapsed < TimeSpan.FromSeconds(10));

            Assert.False(snapshot.IsActive);
            Assert.Equal(CopilotBackgroundShellCommandState.Completed, snapshot.State);
            Assert.Equal(0, snapshot.ExitCode);
            Assert.Contains("background-evidence", snapshot.StandardOutput, StringComparison.Ordinal);
            Assert.True(snapshot.ProcessTreeContained);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public void DiagnosticsShowOnlyBoundedRedactedProcessEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 31, 4, 0, 0, TimeSpan.Zero);
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = "Dev server";
        var snapshot = new CopilotBackgroundShellCommandSnapshot(
            "bg:123",
            conversation.Id,
            "task:456",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "npm run dev",
            new string('a', 64),
            now.AddMinutes(-2),
            null,
            7_654,
            ProcessTreeContained: true,
            CopilotBackgroundShellCommandState.Running,
            ExitCode: null,
            StandardOutput: "ready",
            StandardError: string.Empty);

        var list = CopilotBackgroundShellCommandDiagnostics.FormatList(
            conversation,
            [snapshot],
            now);
        var details = CopilotBackgroundShellCommandDiagnostics.FormatDetails(
            snapshot,
            1,
            now);
        var confirmation = CopilotBackgroundShellCommandDiagnostics.FormatStopConfirmation(
            snapshot,
            1);

        Assert.Contains("1 条运行中 / 1 条保留", list, StringComparison.Ordinal);
        Assert.Contains("#1 · 运行中 · PID 7654", list, StringComparison.Ordinal);
        Assert.Contains("npm run dev", list, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.TaskId, list, StringComparison.Ordinal);
        Assert.Contains("stdout（限长、脱敏）", details, StringComparison.Ordinal);
        Assert.Contains("ready", details, StringComparison.Ordinal);
        Assert.Contains("启动成功不等于服务已就绪", details, StringComparison.Ordinal);
        Assert.Contains("停止后台命令 #1", confirmation, StringComparison.Ordinal);
        Assert.Contains("不会自动撤销", confirmation, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreateRequest(
        string userText,
        string conversationId = "conversation-background")
    {
        var workspace = Path.GetFullPath(Path.GetTempPath());
        return new CopilotAgentRequest
        {
            ConversationId = conversationId,
            TaskId = "task-background",
            WorkspacePath = workspace,
            UserText = userText,
            TaskIntentText = userText,
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [workspace],
            WritableLocalRootPaths = [workspace],
            PreferredShell = CopilotShellKind.PowerShell,
        };
    }

    private static CopilotAgentToolInput CreateStartInput(string command)
    {
        return new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "powershell",
                ["workingDirectory"] = Path.GetFullPath(Path.GetTempPath()),
                ["lifetimeSeconds"] = 600,
            },
        };
    }

    private sealed class FakeBackgroundLauncher : ICopilotBackgroundShellProcessLauncher
    {
        public List<FakeBackgroundProcess> Processes { get; } = new();

        public FakeBackgroundProcess? LastProcess => Processes.LastOrDefault();

        public Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = new FakeBackgroundProcess(4_242 + Processes.Count);
            Processes.Add(process);
            return Task.FromResult<ICopilotBackgroundShellProcess>(process);
        }
    }

    private sealed class GatedBackgroundLauncher : ICopilotBackgroundShellProcessLauncher
    {
        private readonly object _syncRoot = new();
        private readonly List<TaskCompletionSource<ICopilotBackgroundShellProcess>>
            _pending = new();

        public int PendingCount
        {
            get
            {
                lock (_syncRoot)
                    return _pending.Count;
            }
        }

        public Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completion =
                new TaskCompletionSource<ICopilotBackgroundShellProcess>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_syncRoot)
                _pending.Add(completion);
            return completion.Task;
        }

        public void ReleaseAll()
        {
            TaskCompletionSource<ICopilotBackgroundShellProcess>[] pending;
            lock (_syncRoot)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }
            for (var index = 0; index < pending.Length; index++)
            {
                pending[index].TrySetResult(
                    new FakeBackgroundProcess(5_000 + index));
            }
        }
    }

    private sealed class FakeBackgroundProcess : ICopilotBackgroundShellProcess
    {
        private readonly TaskCompletionSource<CopilotBackgroundShellProcessCompletion>
            _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string _standardOutput = string.Empty;
        private string _standardError = string.Empty;

        public FakeBackgroundProcess(int processId)
        {
            ProcessId = processId;
        }

        public int ProcessId { get; }

        public bool ProcessTreeContained => true;

        public Task<CopilotBackgroundShellProcessCompletion> Completion =>
            _completion.Task;

        public int StopCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public void SetOutput(string standardOutput, string standardError)
        {
            _standardOutput = standardOutput;
            _standardError = standardError;
        }

        public (string StandardOutput, string StandardError) GetOutputSnapshot() =>
            (_standardOutput, _standardError);

        public Task<CopilotBackgroundShellProcessCompletion> StopAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StopCount++;
            _completion.TrySetResult(new CopilotBackgroundShellProcessCompletion(
                CopilotBackgroundShellCommandState.Stopped,
                ExitCode: 1,
                DateTimeOffset.UtcNow,
                _standardOutput,
                _standardError));
            return _completion.Task;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
