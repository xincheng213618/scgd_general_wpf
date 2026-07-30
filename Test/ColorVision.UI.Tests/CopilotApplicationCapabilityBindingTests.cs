using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotApplicationCapabilityBindingTests
{
    private const string WorkspacePath = @"C:\ColorVision\CapabilityBinding";

    [Fact]
    public async Task ApprovedApplicationCapabilityReceivesExactActiveInvocationScope()
    {
        var invoker = new RecordingApprovedInvoker();
        var tool = new ApprovedApplicationBridgeTool(invoker);
        var request = CreateRequest(capabilityRevision: 7);
        var executionScope = request.RuntimeExecutionScope.BindToolCall(
            tool.Name,
            "provider-call-1",
            "signature-1");
        var invocation = CreateInvocation(tool, request, executionScope, "provider-call-1");

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            invocation,
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Same(outcome.Invocation.ExecutionScope, invoker.ExecutionScope);
        Assert.True(invoker.ExecutionScope!.HasToolCallBinding);
        Assert.Equal("provider-call-1", invoker.ExecutionScope.ProviderCallId);
        Assert.False(string.IsNullOrWhiteSpace(invoker.ExecutionScope.ArgumentsSignature));
        Assert.True(invoker.ExecutionScope.MatchesOperationBinding(outcome.Invocation.ExecutionScope));
    }

    [Fact]
    public async Task ApprovedApplicationCapabilityCannotBeForgedOutsideToolExecutor()
    {
        var invoker = new RecordingApprovedInvoker();
        var request = CreateRequest(capabilityRevision: 7);

        var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
            invoker,
            "create_flow",
            CreateArguments(),
            request,
            frameworkApprovalGranted: true,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("approved_execution_context_missing", result.ErrorCode);
        Assert.Null(invoker.ExecutionScope);
    }

    [Fact]
    public async Task ApplicationCapabilityRevalidatesCatalogRevisionBeforeExecuting()
    {
        var currentRevision = 7L;
        var handlerCalls = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = WorkspacePath,
                SearchRootPaths = [WorkspacePath],
            },
            CapabilityRevisionProvider = () => currentRevision,
            CreateFlowHandler = (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                handlerCalls++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("created"));
            },
        });
        var tool = new ApprovedApplicationBridgeTool(dispatcher);
        var request = CreateRequest(capabilityRevision: 7);

        var first = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(
                tool,
                request,
                request.RuntimeExecutionScope.BindToolCall(tool.Name, "provider-call-1", "signature-1"),
                "provider-call-1"),
            _ => { },
            CancellationToken.None);

        currentRevision = 8;
        var second = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(
                tool,
                request,
                request.RuntimeExecutionScope.BindToolCall(tool.Name, "provider-call-2", "signature-2"),
                "provider-call-2"),
            _ => { },
            CancellationToken.None);

        Assert.True(first.Result.Success);
        Assert.False(second.Result.Success);
        Assert.Equal(CopilotToolFailureKind.Authorization, second.Result.FailureKind);
        Assert.Equal("approved_capability_revision_changed", second.Result.FailureCode);
        Assert.Contains("revision 7 -> 8", second.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(1, handlerCalls);
    }

    private static CopilotAgentRequest CreateRequest(long capabilityRevision)
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation-1",
            TaskId = "task-1",
            UserText = "Create the requested test flow.",
            WorkspacePath = WorkspacePath,
            Mode = CopilotAgentMode.Code,
        };
        request.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRequest(request, runId: "run-1")
            .WithRuntimeSnapshot("workspace-snapshot-1", capabilityRevision);
        return request;
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentRequest request,
        CopilotExecutionScope executionScope,
        string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "test-runtime",
            Tool = tool,
            AgentRequest = request,
            ExecutionScope = executionScope,
            ToolInput = CopilotAgentToolInput.Empty,
            ToolCall = new CopilotToolCall
            {
                ToolName = tool.Name,
                ToolInput = CopilotAgentToolInput.Empty,
            },
            FrameworkApprovalGranted = true,
        };
    }

    private static Dictionary<string, JsonElement> CreateArguments() =>
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = JsonSerializer.SerializeToElement("CapabilityBindingFlow"),
        };

    private sealed class ApprovedApplicationBridgeTool(
        ICopilotApplicationCapabilityInvoker invoker) : ICopilotFrameworkApprovedTool
    {
        public string Name => "ApprovedApplicationBridge";

        public string Description => "Test bridge for an approved application capability.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                ErrorMessage = "The test bridge requires the approved execution path.",
            });

        public async Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                invoker,
                "create_flow",
                CreateArguments(),
                request,
                frameworkApprovalGranted: true,
                cancellationToken);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = result.Content,
                Content = result.Content,
                ErrorMessage = result.Success ? string.Empty : result.Content,
                FailureKind = result.FailureKind,
                FailureCode = result.ErrorCode,
            };
        }
    }

    private sealed class RecordingApprovedInvoker :
        ICopilotApplicationCapabilityInvoker,
        ICopilotApprovedApplicationCapabilityInvoker
    {
        public CopilotExecutionScope? ExecutionScope { get; private set; }

        public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The public unapproved channel must not be used.");

        public Task<CopilotApplicationCapabilityCallResult> InvokeApprovedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ExecutionScope = executionScope;
            return Task.FromResult(new CopilotApplicationCapabilityCallResult
            {
                Success = true,
                Content = "approved",
            });
        }
    }
}
