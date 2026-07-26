using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionAuditLoggerTests
{
    [Fact]
    public void RecordProjectsAgentExecutionScopeIntoAuditEntry()
    {
        CopilotToolExecutionAuditLogger.ClearForTests();
        try
        {
            var scope = CopilotExecutionScope.ForAgentRequest(
                    new CopilotAgentRequest
                    {
                        ConversationId = "conversation-7",
                        TaskId = "task-7",
                        WorkspacePath = @"C:\ColorVision\Scope",
                    },
                    runId: "run-7",
                    parentRunId: "run-parent",
                    traceId: "trace-7")
                .WithRuntimeSnapshot("workspace-snapshot-7", capabilityRevision: 42)
                .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved)
                .BindToolCall("test_tool", "provider-call-7", new string('a', 64));

            Record(scope, "provider-call-7");

            var entry = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries());
            Assert.Equal(scope.SourceKind, entry.SourceKind);
            Assert.Equal(scope.AuthorizationChannel, entry.AuthorizationChannel);
            Assert.Equal(scope.ScopeId, entry.ScopeId);
            Assert.Equal(scope.AuthorizationScopeId, entry.AuthorizationScopeId);
            Assert.Equal(scope.OperationBindingId, entry.OperationBindingId);
            Assert.Equal(scope.TraceId, entry.TraceId);
            Assert.Equal(scope.CallerIdentity, entry.CallerIdentity);
            Assert.Equal(scope.ConversationId, entry.ConversationId);
            Assert.Equal(scope.TaskId, entry.TaskId);
            Assert.Equal(scope.RunId, entry.RunId);
            Assert.Equal(scope.ParentRunId, entry.ParentRunId);
            Assert.Equal(scope.WorkspaceIdentity, entry.WorkspaceIdentity);
            Assert.Equal(scope.WorkspaceSnapshotId, entry.WorkspaceSnapshotId);
            Assert.Equal(scope.CapabilityRevision, entry.CapabilityRevision);
            Assert.Equal(scope.ToolName, entry.ScopeToolName);
            Assert.Equal(scope.ProviderCallId, entry.ProviderCallId);
            Assert.Equal(scope.ArgumentsSignature, entry.ArgumentsSignature);
        }
        finally
        {
            CopilotToolExecutionAuditLogger.ClearForTests();
        }
    }

    [Fact]
    public void RecordKeepsRawSessionTokenArgumentsAndWorkspacePathOutOfScopeProjection()
    {
        CopilotToolExecutionAuditLogger.ClearForTests();
        try
        {
            const string rawSessionToken = "raw-session-token-that-must-not-be-audited";
            const string rawArgument = "raw-argument-that-must-not-be-audited";
            const string rawWorkspacePath = @"C:\Customers\SensitiveWorkspace";
            var input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["api_token"] = rawArgument,
                },
            };
            var signature = CopilotAgentToolInputExactBinding.CreateExecutionSignature("test_tool", input);
            var scope = CopilotExecutionScope.ForExternalMcpSession(
                    rawSessionToken,
                    "test-caller",
                    rawWorkspacePath)
                .BindToolCall("test_tool", "provider-call-8", signature);

            Record(scope, "provider-call-8");

            var entry = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries());
            Assert.Equal(scope.SessionIdentity, entry.SessionIdentity);
            Assert.Equal(scope.CallerIdentity, entry.CallerIdentity);
            Assert.Equal(scope.WorkspaceIdentity, entry.WorkspaceIdentity);
            Assert.Equal(signature, entry.ArgumentsSignature);
            Assert.DoesNotContain(rawSessionToken, entry.SessionIdentity, StringComparison.Ordinal);
            Assert.DoesNotContain(rawArgument, entry.ArgumentsSignature, StringComparison.Ordinal);
            Assert.DoesNotContain(rawWorkspacePath, entry.WorkspaceIdentity, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(rawSessionToken, string.Join('|',
                entry.ScopeId,
                entry.AuthorizationScopeId,
                entry.OperationBindingId,
                entry.TraceId,
                entry.SessionIdentity,
                entry.WorkspaceIdentity,
                entry.ProviderCallId,
                entry.ArgumentsSignature), StringComparison.Ordinal);
        }
        finally
        {
            CopilotToolExecutionAuditLogger.ClearForTests();
        }
    }

    private static void Record(CopilotExecutionScope scope, string callId)
    {
        CopilotToolExecutionAuditLogger.Record(new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                ExecutionScope = scope,
            },
            Result = new CopilotToolResult
            {
                Success = true,
            },
            Execution = new CopilotToolExecutionInfo
            {
                CallId = callId,
                RuntimeName = "test",
                ToolName = "test_tool",
                State = CopilotToolExecutionState.Completed,
                StartedAtUtc = DateTimeOffset.UtcNow,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                ArgumentSummary = "fields=api_token",
            },
        });
    }
}
