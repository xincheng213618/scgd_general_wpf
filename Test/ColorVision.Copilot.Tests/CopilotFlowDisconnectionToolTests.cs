using ColorVision.Copilot.Mcp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotFlowDisconnectionToolTests
{
    private const string Revision = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceNode = "12345678-1234-1234-1234-123456789abc";
    private const string TargetNode = "87654321-4321-4321-4321-cba987654321";
    private const string Workspace = @"C:\ColorVision\DisconnectionToolTests";

    [Fact]
    public async Task AgentApprovalPreviewsAndExplicitlyReviewsTheExactDisconnection()
    {
        using var session = new Session();
        var previewTool = new CopilotPreviewFlowPatchTool(session.Dispatcher);
        var tool = new CopilotApplyFlowPatchTool(session.Dispatcher);
        Assert.Same(previewTool.InputSchema, tool.InputSchema);
        Assert.Equal(new[] { "add_node", "set_property", "connect", "disconnect" },
            tool.InputSchema.JsonSchema.GetProperty("properties").GetProperty("operation").GetProperty("enum").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(CopilotToolAccess.ReadOnly, previewTool.Capability.Access);
        Assert.Equal(CopilotToolAccess.Write, tool.Capability.Access);
        var input = Input();
        Assert.Throws<InvalidOperationException>(() => tool.CreateApprovalPresentation(input));
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var handle = await coordinator.RequestApprovalAsync(tool, Request(), input, "disconnect-approval-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        try
        {
            var preview = Assert.Single(session.PreviewRequests);
            Assert.Equal("disconnect", preview.Operation);
            Assert.Equal(SourceNode, preview.SourceNodeId);
            Assert.Equal(TargetNode, preview.TargetNodeId);
            Assert.Contains("断开", handle.Action.Description, StringComparison.Ordinal);
            foreach (var field in new[] { SourceNode, "out:0", TargetNode, "in:0" })
                Assert.Contains(field, handle.Action.Description, StringComparison.Ordinal);
            Assert.Equal(CopilotApprovalReviewTextEncoder.Encode(session.PreviewText), handle.Action.ReviewDetails);
            Assert.Empty(session.ApplyRequests);
        }
        finally
        {
            coordinator.Cancel(handle);
            await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task ExternalConfirmationBindsTheFrozenTupleDigestAndCallerBeforeApplyingOnce()
    {
        using var session = new Session();
        var arguments = Arguments();
        var preview = await session.Call("preview_flow_patch", arguments);
        Assert.True(preview.Success, preview.Text);
        Assert.False(preview.RequiresApproval);
        Assert.Empty(session.ApplyRequests);
        var pending = await session.Call("apply_flow_patch", arguments);
        var action = session.Action(pending);
        var reviewed = session.PreviewRequests.Last();
        Assert.True(pending.RequiresApproval);
        Assert.False(pending.ExecuteOnApproval);
        Assert.Contains("断开", action.Description, StringComparison.Ordinal);
        Assert.Equal(Digest(arguments), action.ArgumentsDigest);
        Assert.Equal(CopilotApprovalReviewTextEncoder.Encode(session.PreviewText), action.ReviewDetails);
        arguments["source_port_id"] = JsonSerializer.SerializeToElement("out:9");
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(action.ActionId, ReviewContext(), out _));

        var tampered = await session.Call("confirm_action", Confirm(action, Digest(arguments)));
        Assert.False(tampered.Success);
        Assert.Equal("action_arguments_mismatch", tampered.ErrorCode);
        var foreign = await session.Dispatcher.CallExternalAsync("confirm_action", Confirm(action, action.ArgumentsDigest),
            "different-disconnection-caller-" + Guid.NewGuid().ToString("N"), CancellationToken.None);
        Assert.False(foreign.Success);
        Assert.Equal("action_source_mismatch", foreign.ErrorCode);
        Assert.Empty(session.ApplyRequests);

        var result = await session.Call("confirm_action", Confirm(action, action.ArgumentsDigest));
        Assert.True(result.Success, result.Text);
        Assert.Same(reviewed, Assert.Single(session.ApplyRequests));
        Assert.Equal("out:0", session.ApplyRequests[0].SourcePortId);
        Assert.Equal("in:0", session.ApplyRequests[0].TargetPortId);
        Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
        Assert.False((await session.Call("confirm_action", Confirm(action, action.ArgumentsDigest))).Success);
        Assert.Single(session.ApplyRequests);
    }

    [Fact]
    public async Task RejectedDisconnectionNeverReachesTheApplyHandler()
    {
        using var session = new Session();
        var action = session.Action(await session.Call("apply_flow_patch", Arguments()));
        Assert.True(CopilotMcpConfirmationStore.Instance.Reject(action.ActionId, ReviewContext(), out _));
        Assert.False((await session.Call("confirm_action", Confirm(action, action.ArgumentsDigest))).Success);
        Assert.Equal(ConfirmableActionStatus.Rejected, action.Status);
        Assert.Empty(session.ApplyRequests);
    }

    [Fact]
    public async Task FailedPreviewCannotCreateExternalOrAgentApprovalAndCannotApply()
    {
        using var session = new Session(CopilotMcpToolCallResult.Fail("edge_not_found", "The exact connection is not present.", CopilotToolFailureKind.Validation));
        var pending = await session.Call("apply_flow_patch", Arguments());
        Assert.False(pending.Success);
        Assert.Equal("edge_not_found", pending.ErrorCode);
        Assert.False(pending.RequiresApproval);
        Assert.Empty(session.Pending);
        var callId = "disconnect-failed-preview-" + Guid.NewGuid().ToString("N");
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CopilotFrameworkApprovalCoordinator().RequestApprovalAsync(
            new CopilotApplyFlowPatchTool(session.Dispatcher), Request(), Input(), callId, CancellationToken.None));
        Assert.DoesNotContain(CopilotMcpConfirmationStore.Instance.GetPendingActions(), action => action.AgentCallId == callId);
        Assert.Empty(session.ApplyRequests);
    }

    [Theory]
    [InlineData("missing", "expected_revision")]
    [InlineData("missing", "source_node_id")]
    [InlineData("missing", "source_port_id")]
    [InlineData("missing", "target_node_id")]
    [InlineData("missing", "target_port_id")]
    [InlineData("alias", "source_node_id")]
    [InlineData("alias", "target_node_id")]
    [InlineData("empty-guid", "source_node_id")]
    [InlineData("revision", "expected_revision")]
    [InlineData("source-leadingzero", "source_port_id")]
    [InlineData("target-whitespace", "target_port_id")]
    [InlineData("camera", "inherit_camera_defaults")]
    [InlineData("unsupported", "remove_all")]
    public async Task InvalidDisconnectionArgumentsCannotReachPreviewOrApply(string mutation, string field)
    {
        using var session = new Session();
        var arguments = Arguments();
        if (mutation == "missing") arguments.Remove(field);
        else arguments[field] = mutation switch
        {
            "empty-guid" => JsonSerializer.SerializeToElement(Guid.Empty.ToString()),
            "revision" => JsonSerializer.SerializeToElement("legacy-revision"),
            "source-leadingzero" => JsonSerializer.SerializeToElement("out:00"),
            "target-whitespace" => JsonSerializer.SerializeToElement(" in:0 "),
            "camera" or "unsupported" => JsonSerializer.SerializeToElement(false),
            _ => JsonSerializer.SerializeToElement("legacy-node-alias"),
        };
        foreach (var tool in new[] { "preview_flow_patch", "apply_flow_patch" })
        {
            var result = await session.Call(tool, arguments);
            Assert.False(result.Success);
            Assert.False(result.RequiresApproval);
            Assert.Equal(CopilotToolFailureKind.Validation, result.FailureKind);
        }
        Assert.Empty(session.PreviewRequests);
        Assert.Empty(session.ApplyRequests);
        Assert.Empty(session.Pending);
    }

    [Fact]
    public async Task ExistingConnectKeepsItsLegacyAliasesAndRevisionContract()
    {
        using var session = new Session();
        var arguments = Arguments();
        arguments["operation"] = JsonSerializer.SerializeToElement("connect");
        arguments["expected_revision"] = JsonSerializer.SerializeToElement("legacy-revision");
        arguments["source_node_id"] = JsonSerializer.SerializeToElement("legacy-source");
        arguments["target_node_id"] = JsonSerializer.SerializeToElement("legacy-target");
        Assert.True((await session.Call("preview_flow_patch", arguments)).Success);
        Assert.Equal("legacy-source", Assert.Single(session.PreviewRequests).SourceNodeId);
        Assert.Empty(session.ApplyRequests);
    }

    [Fact]
    public async Task FrameworkApprovedDisconnectionUsesTheExistingApprovedExecutionChannel()
    {
        using var session = new Session();
        var tool = new CopilotApplyFlowPatchTool(session.Dispatcher);
        var request = Request();
        var scope = CopilotExecutionScope.ForAgentRequest(request).WithRuntimeSnapshot("disconnection-snapshot", CopilotCapabilityCatalog.Shared.GetSnapshot().Revision)
            .BindToolCall(tool.Name, "disconnection-call", "disconnection-arguments");
        request.RuntimeExecutionScope = scope;
        using var invocation = CopilotToolInvocationContext.Enter(new CopilotToolInvocation
        {
            AgentRequest = request, Tool = tool, ExecutionScope = scope, FrameworkApprovalGranted = true,
        });
        var result = await ((ICopilotFrameworkApprovedTool)tool).ExecuteApprovedAsync(request, Input(), CancellationToken.None);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Null(result.Approval);
        Assert.Equal("disconnect", Assert.Single(session.ApplyRequests).Operation);
        Assert.Empty(session.PreviewRequests);
    }

    private static CopilotAgentRequest Request() => new()
    {
        ConversationId = "disconnect-conversation", TaskId = "disconnect-task", WorkspacePath = Workspace,
        Mode = CopilotAgentMode.Auto, UserText = "断开这条精确连线",
    };
    private static CopilotAgentToolInput Input() => new() { Arguments = Arguments().ToDictionary(item => item.Key, item => (object?)item.Value) };
    private static Dictionary<string, JsonElement> Arguments() => new()
    {
        ["operation"] = JsonSerializer.SerializeToElement("disconnect"), ["expected_revision"] = JsonSerializer.SerializeToElement(Revision),
        ["source_node_id"] = JsonSerializer.SerializeToElement(SourceNode), ["source_port_id"] = JsonSerializer.SerializeToElement("out:0"),
        ["target_node_id"] = JsonSerializer.SerializeToElement(TargetNode), ["target_port_id"] = JsonSerializer.SerializeToElement("in:0"),
    };
    private static Dictionary<string, JsonElement> Confirm(ConfirmableAction action, string digest) => new()
    {
        ["action_id"] = JsonSerializer.SerializeToElement(action.ActionId), ["tool_name"] = JsonSerializer.SerializeToElement(action.ToolName),
        ["arguments_digest"] = JsonSerializer.SerializeToElement(digest),
    };
    private static CopilotConfirmationReviewContext ReviewContext() => new(string.Empty, string.Empty, Workspace);
    private static string Digest(IReadOnlyDictionary<string, JsonElement> arguments) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SortedDictionary<string, string>(arguments.ToDictionary(item => item.Key, item => item.Value.GetString()!), StringComparer.Ordinal))))).ToLowerInvariant();

    private sealed class Session : IDisposable
    {
        private readonly string _source = "disconnection-test-" + Guid.NewGuid().ToString("N");
        public string PreviewText { get; } = $"Existing connection: {SourceNode}/out:0 -> {TargetNode}/in:0";
        public List<CopilotFlowPatchRequest> PreviewRequests { get; } = [];
        public List<CopilotFlowPatchRequest> ApplyRequests { get; } = [];
        public CopilotMcpToolDispatcher Dispatcher { get; }
        public ConfirmableAction[] Pending => CopilotMcpConfirmationStore.Instance.GetPendingActions()
            .Where(action => action.RequestContext.RequestSource == _source).ToArray();
        public Session(CopilotMcpToolCallResult? preview = null)
        {
            Dispatcher = new(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot { SolutionDirectoryPath = Workspace },
                PreviewFlowPatchHandler = (request, _) => { PreviewRequests.Add(request); return Task.FromResult(preview ?? CopilotMcpToolCallResult.Ok(PreviewText)); },
                ApplyFlowPatchHandler = (request, _) => { ApplyRequests.Add(request); return Task.FromResult(CopilotMcpToolCallResult.Ok("disconnected")); },
            });
        }
        public Task<CopilotMcpToolCallResult> Call(string tool, IReadOnlyDictionary<string, JsonElement> arguments) =>
            Dispatcher.CallExternalAsync(tool, arguments, _source, CancellationToken.None);
        public ConfirmableAction Action(CopilotMcpToolCallResult result) => Assert.Single(Pending, action => action.ActionId == result.ApprovalActionId);
        public void Dispose()
        {
            foreach (var action in Pending)
                CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _, "Disconnection test cleanup.");
        }
    }
}
