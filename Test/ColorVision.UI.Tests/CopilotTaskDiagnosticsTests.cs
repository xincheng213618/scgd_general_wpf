using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTaskDiagnosticsTests
{
    [Fact]
    public void TasksCommandIsReadOnlyAndAvailableDuringAnActiveRun()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/tasks");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Tasks, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/tasks");
        Assert.Null(CopilotLocalCommandCatalog.Parse("/tasks all"));
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
                    "active",
                    "Active task",
                    CopilotAgentMode.Auto,
                    CopilotHostedRunState.Running,
                    now.AddMinutes(-2),
                    now.AddSeconds(-65),
                    IsCheckpointReady: true,
                    QueuePosition: 0),
                new CopilotTaskRunDiagnosticSnapshot(
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
            Assert.Equal("Active task", snapshot.Runs[0].Title);
            Assert.Equal(CopilotHostedRunState.Running, snapshot.Runs[0].State);
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
