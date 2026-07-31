using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTaskDiagnosticsTests
{
    [Fact]
    public void TasksCommandSupportsConfirmedStopDuringAnActiveRun()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/tasks");
        var stopInvocation = CopilotLocalCommandCatalog.Parse("/tasks stop 2");
        var resumeInvocation = CopilotLocalCommandCatalog.Parse("/tasks resume 1");
        var taskSuggestions = CopilotLocalCommandCatalog.Suggest("/tasks ");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Tasks, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal("/tasks [stop N|resume N]", invocation.Command.Usage);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/tasks");
        Assert.NotNull(stopInvocation);
        Assert.Equal("stop 2", stopInvocation.Arguments);
        Assert.NotNull(resumeInvocation);
        Assert.Equal("resume 1", resumeInvocation.Arguments);
        Assert.Equal(["/tasks stop", "/tasks resume"], taskSuggestions.Select(item => item.Name));
        Assert.All(taskSuggestions, item => Assert.True(item.AcceptsArguments));
        Assert.Equal(["/tasks stop ", "/tasks resume "], taskSuggestions.Select(item => item.CompletionText));
    }

    [Fact]
    public void PsCommandUsesTheSameTaskControlContract()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/ps");
        var stopInvocation = CopilotLocalCommandCatalog.Parse("/ps stop 3");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Tasks, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal("/ps [stop N|resume N]", invocation.Command.Usage);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/p"), command => command.Name == "/ps");
        Assert.NotNull(stopInvocation);
        Assert.Equal("stop 3", stopInvocation.Arguments);
    }

    [Theory]
    [InlineData(null, 0, 0)]
    [InlineData("", 0, 0)]
    [InlineData("stop 1", 1, 1)]
    [InlineData("STOP 12", 1, 12)]
    [InlineData("resume 2", 2, 2)]
    [InlineData("RESUME 9", 2, 9)]
    [InlineData("stop", 3, 0)]
    [InlineData("resume", 3, 0)]
    [InlineData("stop 0", 3, 0)]
    [InlineData("resume -1", 3, 0)]
    [InlineData("stop 1 extra", 3, 0)]
    [InlineData("all", 3, 0)]
    public void TaskCommandParserRequiresAnExactPositivePosition(
        string? arguments,
        int expectedAction,
        int expectedPosition)
    {
        var request = CopilotTaskDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotTaskCommandAction)expectedAction, request.Action);
        Assert.Equal(expectedPosition, request.Position);
    }

    [Fact]
    public void ReportSeparatesHostedRunsFromTasksThatNeedAttention()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var report = CopilotTaskDiagnostics.Format(new CopilotTaskDiagnosticSnapshot(
            now,
            HostShutdown: false,
            MaximumQueuedRuns: 3,
            Runs:
            [
                new CopilotTaskRunDiagnosticSnapshot(
                    "run:active-secret",
                    "active",
                    "Active task",
                    CopilotAgentMode.Auto,
                    CopilotHostedRunState.Running,
                    now.AddMinutes(-2),
                    now.AddSeconds(-65),
                    IsCheckpointReady: true,
                    QueuePosition: 0),
                new CopilotTaskRunDiagnosticSnapshot(
                    "run:queued-secret",
                    "queued",
                    "Queued task",
                    CopilotAgentMode.Code,
                    CopilotHostedRunState.Queued,
                    now.AddSeconds(-10),
                    StartedAtUtc: null,
                    IsCheckpointReady: false,
                    QueuePosition: 1),
            ],
            TotalAttentionTasks: 1,
            AttentionTasks:
            [
                new CopilotTaskAttentionDiagnosticSnapshot(
                    "paused",
                    "Paused task",
                    "已暂停",
                    RemainingCount: 2,
                    CanResume: true),
            ]));

        Assert.Contains("宿主：运行中 · 排队 1/3 · 待处理 1", report, StringComparison.Ordinal);
        Assert.Contains("[运行中] Active task · 自动 · 已运行 1 分 05 秒 · 恢复点已就绪", report, StringComparison.Ordinal);
        Assert.Contains("[排队 1] Queued task · 代码 · 已等待 10 秒", report, StringComparison.Ordinal);
        Assert.Contains("[已暂停] Paused task · 剩余 2 项 · 可继续", report, StringComparison.Ordinal);
        Assert.Contains("/tasks stop N", report, StringComparison.Ordinal);
        Assert.Contains("/tasks resume N", report, StringComparison.Ordinal);
        Assert.DoesNotContain("run:active-secret", report, StringComparison.Ordinal);
        Assert.DoesNotContain("run:queued-secret", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaptureReadsTheHostQueueAndExcludesScheduledConversationsFromAttention()
    {
        var host = new CopilotAgentTaskHost();
        var activeConversation = CreateAttentionConversation("active", "Active task");
        var queuedConversation = CreateAttentionConversation("queued", "Queued task");
        var attentionConversation = CreateAttentionConversation("attention", "Paused task");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start(activeConversation.Id, CopilotAgentMode.Auto, async _ =>
        {
            started.SetResult();
            await release.Task;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(host.TrySchedule(
            queuedConversation.Id,
            CopilotAgentMode.Code,
            _ => Task.CompletedTask,
            out var queuedRun));
        Assert.NotNull(queuedRun);

        try
        {
            var snapshot = CopilotTaskDiagnostics.Capture(
                host,
                [activeConversation, queuedConversation, attentionConversation],
                DateTimeOffset.UtcNow);

            Assert.Equal(2, snapshot.Runs.Count);
            Assert.Equal(activeRun.Id, snapshot.Runs[0].RunId);
            Assert.Equal("Active task", snapshot.Runs[0].Title);
            Assert.Equal(CopilotHostedRunState.Running, snapshot.Runs[0].State);
            Assert.Equal(queuedRun.Id, snapshot.Runs[1].RunId);
            Assert.Equal("Queued task", snapshot.Runs[1].Title);
            Assert.Equal(CopilotHostedRunState.Queued, snapshot.Runs[1].State);
            Assert.Equal(1, snapshot.Runs[1].QueuePosition);
            var attention = Assert.Single(snapshot.AttentionTasks);
            Assert.Equal("Paused task", attention.Title);
            Assert.Equal(1, snapshot.TotalAttentionTasks);
        }
        finally
        {
            release.TrySetResult();
            await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
            await queuedRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void AttentionOutputIsBoundedAndReportsOmittedTasks()
    {
        var conversations = Enumerable.Range(1, CopilotTaskDiagnostics.MaximumAttentionTasks + 5)
            .Select(index => CreateAttentionConversation($"conversation-{index}", $"Task {index}"))
            .ToArray();

        var snapshot = CopilotTaskDiagnostics.Capture(
            new CopilotAgentTaskHost(),
            conversations,
            DateTimeOffset.UtcNow);
        var report = CopilotTaskDiagnostics.Format(snapshot);

        Assert.Equal(CopilotTaskDiagnostics.MaximumAttentionTasks + 5, snapshot.TotalAttentionTasks);
        Assert.Equal(CopilotTaskDiagnostics.MaximumAttentionTasks, snapshot.AttentionTasks.Count);
        Assert.Contains("另有 5 条未显示", report, StringComparison.Ordinal);
    }

    [Fact]
    public void StopSelectionAndConfirmationUseTheExactSnapshotWithoutLeakingTaskContent()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new CopilotTaskRunDiagnosticSnapshot(
            "run:secret-first",
            "conversation-1",
            "Build package",
            CopilotAgentMode.Code,
            CopilotHostedRunState.Running,
            now.AddMinutes(-1),
            now.AddSeconds(-30),
            IsCheckpointReady: true,
            QueuePosition: 0);
        var second = new CopilotTaskRunDiagnosticSnapshot(
            "run:secret-second",
            "conversation-2",
            "Deploy package",
            CopilotAgentMode.Auto,
            CopilotHostedRunState.Queued,
            now.AddSeconds(-10),
            StartedAtUtc: null,
            IsCheckpointReady: false,
            QueuePosition: 1);
        var snapshot = new CopilotTaskDiagnosticSnapshot(
            now,
            HostShutdown: false,
            MaximumQueuedRuns: 3,
            Runs: [first, second],
            TotalAttentionTasks: 0,
            AttentionTasks: []);

        Assert.Same(first, CopilotTaskDiagnostics.FindRun(snapshot, 1));
        Assert.Same(second, CopilotTaskDiagnostics.FindRun(snapshot, 2));
        Assert.Null(CopilotTaskDiagnostics.FindRun(snapshot, 0));
        Assert.Null(CopilotTaskDiagnostics.FindRun(snapshot, 3));
        Assert.Null(CopilotTaskDiagnostics.FindAttentionTask(snapshot, 1));

        var confirmation = CopilotTaskDiagnostics.FormatStopConfirmation(first, 1);

        Assert.Contains("停止任务 #1", confirmation, StringComparison.Ordinal);
        Assert.Contains("Build package", confirmation, StringComparison.Ordinal);
        Assert.Contains("安全暂停", confirmation, StringComparison.Ordinal);
        Assert.Contains("其他任务不会改变", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain(first.RunId, confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain(first.ConversationId, confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt secret", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\private\attachment.txt", confirmation, StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeSelectionUsesOnlyTheNeedsAttentionNumbering()
    {
        var now = DateTimeOffset.UtcNow;
        var first = new CopilotTaskAttentionDiagnosticSnapshot(
            "conversation-paused",
            "Paused task",
            "已暂停",
            RemainingCount: 2,
            CanResume: true);
        var second = new CopilotTaskAttentionDiagnosticSnapshot(
            "conversation-blocked",
            "Blocked task",
            "任务受阻",
            RemainingCount: 1,
            CanResume: false);
        var snapshot = new CopilotTaskDiagnosticSnapshot(
            now,
            HostShutdown: false,
            MaximumQueuedRuns: 3,
            Runs:
            [
                new CopilotTaskRunDiagnosticSnapshot(
                    "run:active",
                    "conversation-active",
                    "Active task",
                    CopilotAgentMode.Auto,
                    CopilotHostedRunState.Running,
                    now,
                    now,
                    IsCheckpointReady: false,
                    QueuePosition: 0),
            ],
            TotalAttentionTasks: 2,
            AttentionTasks: [first, second]);

        Assert.Same(first, CopilotTaskDiagnostics.FindAttentionTask(snapshot, 1));
        Assert.Same(second, CopilotTaskDiagnostics.FindAttentionTask(snapshot, 2));
        Assert.Null(CopilotTaskDiagnostics.FindAttentionTask(snapshot, 0));
        Assert.Null(CopilotTaskDiagnostics.FindAttentionTask(snapshot, 3));
        Assert.Equal("conversation-paused", CopilotTaskDiagnostics.FindAttentionTask(snapshot, 1)?.ConversationId);
    }

    [Fact]
    public async Task StopRequestPrefersCheckpointPauseForAnActiveAgent()
    {
        var host = new CopilotAgentTaskHost();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Start("conversation-active", CopilotAgentMode.Code, async hostedRun =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, hostedRun.CancellationToken);
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(host.MarkCheckpointReady(run.Id));

        var outcome = CopilotTaskDiagnostics.RequestStop(host, run.Id);

        Assert.Equal(CopilotTaskStopRequestOutcome.PauseRequested, outcome);
        Assert.Equal(CopilotHostedRunState.PauseRequested, run.State);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => run.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task StopRequestCancelsOnlyTheExactQueuedRun()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start("conversation-active", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(host.TrySchedule(
            "conversation-queued",
            CopilotAgentMode.Code,
            _ => Task.CompletedTask,
            out var queuedRun));
        Assert.NotNull(queuedRun);

        var outcome = CopilotTaskDiagnostics.RequestStop(host, queuedRun.Id);

        Assert.Equal(CopilotTaskStopRequestOutcome.CancelRequested, outcome);
        Assert.Equal(CopilotHostedRunState.Running, activeRun.State);
        Assert.Equal(CopilotHostedRunState.Completed, queuedRun.State);
        Assert.Equal(CopilotTaskStopRequestOutcome.NotFound, CopilotTaskDiagnostics.RequestStop(host, queuedRun.Id));

        releaseActive.SetResult();
        await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static CopilotConversationRecord CreateConversation(string id, string title)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
        conversation.Id = id;
        conversation.Title = title;
        return conversation;
    }

    private static CopilotConversationRecord CreateAttentionConversation(string id, string title)
    {
        var conversation = CreateConversation(id, title);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Paused")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Continue",
                        IsComplete = false,
                    },
                ],
            },
        });
        return conversation;
    }
}
