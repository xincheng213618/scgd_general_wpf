using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotExecuteMenuApprovalTests
{
    private const string WorkspacePath = @"C:\ColorVision\MenuApprovalTests";
    private const long CapabilityRevision = 7;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FrameworkApprovedMenuExecutesOnceAndPreservesTheHandlerOutcome(bool succeeds)
    {
        var query = "approved-menu-" + Guid.NewGuid().ToString("N");
        var calls = new List<bool>();
        var expected = succeeds
            ? CopilotMcpToolCallResult.Ok("menu command scheduled")
            : CopilotMcpToolCallResult.Fail("confirmation_required", "The menu handler refused execution.");
        var dispatcher = CreateDispatcher((_, dryRun, _) =>
        {
            calls.Add(dryRun);
            return Task.FromResult(dryRun ? CopilotMcpToolCallResult.Ok("preview") : expected);
        });
        var tool = new CopilotExecuteMenuTool(dispatcher);
        var request = CreateRequest(query);
        var scope = CopilotExecutionScope.ForAgentRequest(request)
            .WithRuntimeSnapshot("menu-test-snapshot", CapabilityRevision)
            .BindToolCall(tool.Name, "menu-test-call", "menu-test-arguments");
        request.RuntimeExecutionScope = scope;
        using var invocation = CopilotToolInvocationContext.Enter(new CopilotToolInvocation
        {
            AgentRequest = request,
            Tool = tool,
            ExecutionScope = scope,
            FrameworkApprovalGranted = true,
        });

        try
        {
            var result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(
                request, new CopilotAgentToolInput { Query = query }, CancellationToken.None);

            Assert.False(Assert.Single(calls));
            Assert.Equal(succeeds, result.Success);
            Assert.Equal(expected.Text, result.Content);
            Assert.Equal(expected.ErrorCode, result.FailureCode);
            Assert.Null(result.Approval);
            Assert.Empty(PendingActions(query));
        }
        finally
        {
            CancelPendingActions(query);
        }
    }

    [Fact]
    public async Task UnapprovedInAppMenuStillPreviewsAndExecutesOnlyAfterApproval()
    {
        var query = "unapproved-menu-" + Guid.NewGuid().ToString("N");
        var calls = new List<bool>();
        var dispatcher = CreateDispatcher((_, dryRun, _) =>
        {
            calls.Add(dryRun);
            return Task.FromResult(CopilotMcpToolCallResult.Ok(dryRun ? "preview" : "scheduled"));
        });
        var request = CreateRequest(query);
        var scope = CopilotExecutionScope.ForAgentRequest(request);

        try
        {
            var result = await dispatcher.ExecuteMenuAsync(
                Arguments(query), scope, CancellationToken.None);

            Assert.True(Assert.Single(calls));
            Assert.True(result.RequiresApproval);
            Assert.True(result.ExecuteOnApproval);
            var action = Assert.Single(PendingActions(query));
            Assert.Equal(result.ApprovalActionId, action.ActionId);
            Assert.True(CopilotMcpConfirmationStore.Instance.LinkAgentCall(
                action.ActionId, "unapproved-menu-call", request, scope));

            var executed = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(
                action.ActionId,
                new CopilotConfirmationReviewContext(request.ConversationId, request.TaskId, WorkspacePath),
                CancellationToken.None);

            Assert.True(executed.Success);
            Assert.Equal(new[] { true, false }, calls);
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.Empty(PendingActions(query));
        }
        finally
        {
            CancelPendingActions(query);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelledMenuCannotPublishApprovalAfterNonCooperativePreviewReturns(bool inAppAgent)
    {
        var query = "cancelled-menu-" + Guid.NewGuid().ToString("N");
        var releasePreview = new TaskCompletionSource<CopilotMcpToolCallResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<bool>();
        var dispatcher = CreateDispatcher((_, dryRun, _) =>
        {
            calls.Add(dryRun);
            return releasePreview.Task;
        });
        var request = CreateRequest(query);
        var scope = inAppAgent
            ? CopilotExecutionScope.ForAgentRequest(request)
            : CopilotExecutionScope.ForExternalMcpSession(query, query, WorkspacePath);
        using var cancellation = new CancellationTokenSource();
        var call = dispatcher.ExecuteMenuAsync(Arguments(query), scope, cancellation.Token);
        try
        {
            Assert.False(call.IsCompleted);
            cancellation.Cancel();
            releasePreview.SetResult(inAppAgent
                ? CopilotMcpToolCallResult.Ok("late successful menu preview")
                : CopilotMcpToolCallResult.Fail("confirmation_required", "late menu confirmation request"));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call.WaitAsync(TimeSpan.FromSeconds(2)));

            Assert.Equal(inAppAgent, Assert.Single(calls));
            Assert.Empty(PendingActions(query));
        }
        finally
        {
            releasePreview.TrySetResult(CopilotMcpToolCallResult.Ok("cleanup"));
            CancelPendingActions(query);
        }
    }

    [Fact]
    public async Task AlreadyCancelledApprovedMenuCannotReachTheHandler()
    {
        var query = "cancelled-approved-menu-" + Guid.NewGuid().ToString("N");
        var calls = 0;
        var dispatcher = CreateDispatcher((_, _, _) =>
        {
            calls++;
            return Task.FromResult(CopilotMcpToolCallResult.Ok("unexpected execution"));
        });
        var scope = CopilotExecutionScope.ForAgentRequest(CreateRequest(query))
            .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.ExecuteMenuAsync(
            Arguments(query), scope, cancellation.Token));

        Assert.Equal(0, calls);
        Assert.Empty(PendingActions(query));
    }

    private static CopilotMcpToolDispatcher CreateDispatcher(
        Func<string, bool, CancellationToken, Task<CopilotMcpToolCallResult>> handler) => new(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot { SolutionDirectoryPath = WorkspacePath },
            CapabilityRevisionProvider = () => CapabilityRevision,
            ExecuteMenuHandler = handler,
        });

    private static CopilotAgentRequest CreateRequest(string query) => new()
    {
        ConversationId = "menu-test-conversation",
        TaskId = "menu-test-task",
        WorkspacePath = WorkspacePath,
        UserText = query,
        Mode = CopilotAgentMode.Auto,
    };

    private static Dictionary<string, JsonElement> Arguments(string query) => new()
    {
        ["query"] = JsonSerializer.SerializeToElement(query),
        ["dry_run"] = JsonSerializer.SerializeToElement(false),
    };

    private static ConfirmableAction[] PendingActions(string query) => CopilotMcpConfirmationStore.Instance.GetPendingActions()
        .Where(action => action.Description.Contains(query, StringComparison.Ordinal))
        .ToArray();

    private static void CancelPendingActions(string query)
    {
        foreach (var action in PendingActions(query))
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Menu approval test cleanup.");
    }
}
