using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotWorkspaceRollbackPointServiceTests
{
    [Fact]
    public void RollbackCommandAcceptsAnOptionalLatestFirstOrdinal()
    {
        var catalog = CopilotLocalCommandCatalog.Parse("/rollback");
        var selected = CopilotLocalCommandCatalog.Parse("/rollback 2");

        Assert.NotNull(catalog);
        Assert.Equal(CopilotLocalCommandKind.RollbackWorkspace, catalog.Command.Kind);
        Assert.Empty(catalog.Arguments);
        Assert.False(catalog.Command.AvailableWhileAgentRuns);
        Assert.NotNull(selected);
        Assert.Same(catalog.Command, selected.Command);
        Assert.Equal("2", selected.Arguments);
    }

    [Fact]
    public void PointsAreLatestFirstAndDescribeOnlyChangedFileNames()
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new CopilotConversationRecord();
        var earlier = new CopilotChatMessage(CopilotChatRole.Assistant, "Earlier response");
        var latest = new CopilotChatMessage(CopilotChatRole.Assistant, "Latest response");
        var earlierTrace = CreateWorkspaceTrace(
            "11111111111111111111111111111111",
            now.AddMinutes(20),
            @"C:\private\workspace\Earlier.cs");
        var latestTrace = CreateWorkspaceTrace(
            "22222222222222222222222222222222",
            now.AddMinutes(25),
            @"C:\private\workspace\Latest.cs",
            @"C:\private\workspace\LatestTests.cs");
        earlier.AgentTraceEntries.Add(earlierTrace);
        latest.AgentTraceEntries.Add(latestTrace);
        conversation.Messages.Add(earlier);
        conversation.Messages.Add(latest);

        var points = CopilotWorkspaceRollbackPointService.GetPoints(conversation);
        var report = CopilotWorkspaceRollbackPointService.Format(conversation);

        Assert.Equal(2, points.Count);
        Assert.Same(latestTrace, points[0].Trace);
        Assert.Same(latest, points[0].AssistantMessage);
        Assert.Equal(2, points[0].ChangedFileCount);
        Assert.Same(earlierTrace, points[1].Trace);
        Assert.True(CopilotWorkspaceRollbackPointService.TryResolve(conversation, "2", out var selected));
        Assert.Same(earlierTrace, selected.Trace);
        Assert.False(CopilotWorkspaceRollbackPointService.TryResolve(conversation, "0", out _));
        Assert.False(CopilotWorkspaceRollbackPointService.TryResolve(conversation, "missing", out _));
        Assert.Contains("1 · 更新  Latest.cs、更新  LatestTests.cs", report, StringComparison.Ordinal);
        Assert.Contains("2 · 更新  Earlier.cs", report, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\private", report, StringComparison.Ordinal);
        Assert.Contains("精确绑定的原生审批", report, StringComparison.Ordinal);
        Assert.Contains("不调用模型", report, StringComparison.Ordinal);
        Assert.Contains("非快照操作不在范围内", report, StringComparison.Ordinal);
    }

    [Fact]
    public void PointsExcludeExpiredRolledBackDuplicateAndActiveAuthorities()
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = new CopilotConversationRecord();
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed changes");
        var duplicateId = "33333333333333333333333333333333";
        var expired = CreateWorkspaceTrace(
            "44444444444444444444444444444444",
            now.AddMinutes(-1),
            @"C:\workspace\Expired.cs");
        var rolledBack = CreateWorkspaceTrace(
            "55555555555555555555555555555555",
            now.AddMinutes(20),
            @"C:\workspace\RolledBack.cs");
        var duplicateEarlier = CreateWorkspaceTrace(
            duplicateId,
            now.AddMinutes(20),
            @"C:\workspace\Earlier.cs");
        var duplicateLatest = CreateWorkspaceTrace(
            duplicateId,
            now.AddMinutes(20),
            @"C:\workspace\Latest.cs");
        var activeId = "66666666666666666666666666666666";
        var activeApply = CreateWorkspaceTrace(
            activeId,
            now.AddMinutes(20),
            @"C:\workspace\Active.cs");
        var completedId = "77777777777777777777777777777777";
        var completedApply = CreateWorkspaceTrace(
            completedId,
            now.AddMinutes(20),
            @"C:\workspace\Completed.cs");
        message.AgentTraceEntries.Add(expired);
        message.AgentTraceEntries.Add(rolledBack);
        message.AgentTraceEntries.Add(duplicateEarlier);
        message.AgentTraceEntries.Add(duplicateLatest);
        message.AgentTraceEntries.Add(activeApply);
        message.AgentTraceEntries.Add(CreateRollbackTrace(
            activeId,
            now.AddMinutes(20),
            CopilotToolExecutionState.AwaitingApproval));
        message.AgentTraceEntries.Add(completedApply);
        message.AgentTraceEntries.Add(CreateRollbackTrace(
            completedId,
            now.AddMinutes(20),
            CopilotToolExecutionState.Completed));
        conversation.Messages.Add(message);
        Assert.True(conversation.MarkWorkspaceChangeSetRolledBack(rolledBack.WorkspaceChangeSetId));

        var points = CopilotWorkspaceRollbackPointService.GetPoints(conversation);

        var point = Assert.Single(points);
        Assert.Same(duplicateLatest, point.Trace);
        Assert.DoesNotContain(points, candidate => ReferenceEquals(candidate.Trace, expired));
        Assert.DoesNotContain(points, candidate => ReferenceEquals(candidate.Trace, rolledBack));
        Assert.DoesNotContain(points, candidate => ReferenceEquals(candidate.Trace, duplicateEarlier));
        Assert.DoesNotContain(points, candidate => ReferenceEquals(candidate.Trace, activeApply));
        Assert.DoesNotContain(points, candidate => ReferenceEquals(candidate.Trace, completedApply));
    }

    [Fact]
    public void EmptyReportExplainsWhichChangesAreReversible()
    {
        var report = CopilotWorkspaceRollbackPointService.Format(new CopilotConversationRecord());

        Assert.Contains("没有仍可安全回滚", report, StringComparison.Ordinal);
        Assert.Contains("安全工作区补丁", report, StringComparison.Ordinal);
        Assert.Contains("尚未撤销且未过期", report, StringComparison.Ordinal);
    }

    private static CopilotAgentTraceEntry CreateWorkspaceTrace(
        string id,
        DateTimeOffset expiresAtUtc,
        params string[] paths)
    {
        return CreateTrace(
            "ApplyWorkspacePatchEnvelope",
            id,
            expiresAtUtc,
            CopilotToolExecutionState.Completed,
            paths);
    }

    private static CopilotAgentTraceEntry CreateRollbackTrace(
        string id,
        DateTimeOffset expiresAtUtc,
        CopilotToolExecutionState state)
    {
        return CreateTrace(
            "RollbackWorkspacePatchEnvelope",
            id,
            expiresAtUtc,
            state,
            []);
    }

    private static CopilotAgentTraceEntry CreateTrace(
        string toolName,
        string id,
        DateTimeOffset expiresAtUtc,
        CopilotToolExecutionState state,
        string[] paths)
    {
        var lines = new List<string>
        {
            "[Workspace Change Set Result]",
            $"change_set_id: workspace-change-set:{id}",
            $"file_count: {paths.Length}",
            "state: Applied",
            $"expires_at_utc: {expiresAtUtc:O}",
        };
        for (var index = 0; index < paths.Length; index++)
        {
            lines.Add($"file_{index + 1}_operation: Update");
            lines.Add($"file_{index + 1}_path: {paths[index]}");
            lines.Add($"file_{index + 1}_before_sha256: before");
            lines.Add($"file_{index + 1}_after_sha256: after");
        }

        return CopilotAgentTraceEntry.FromResult(
            new CopilotToolExecutionInfo
            {
                CallId = Guid.NewGuid().ToString("N"),
                Round = 1,
                ToolName = toolName,
                State = state,
                StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
                CompletedAtUtc = DateTimeOffset.UtcNow,
            },
            new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = "Workspace change set.",
                Content = string.Join(Environment.NewLine, lines),
            });
    }
}
