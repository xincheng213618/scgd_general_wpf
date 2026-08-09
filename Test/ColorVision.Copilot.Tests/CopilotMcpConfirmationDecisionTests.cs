using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests
{
    public sealed class CopilotMcpConfirmationDecisionTests
    {
        private const string ConversationId = "conversation-safety-contract";
        private const string TaskId = "task-safety-contract";
        private const string WorkspacePath = @"C:\ColorVision\SafetyContract";

        [Fact]
        public void ApprovalPromptShowsSourceTaskWorkspaceImpactAndReversibility()
        {
            var action = CreateAction(executeOnApproval: false);
            try
            {
                var prompt = CopilotMcpConfirmationDecision.BuildApprovalPrompt(action);

                Assert.Contains(action.Title, prompt, StringComparison.Ordinal);
                Assert.Contains(action.RequesterLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.TaskScopeLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.WorkspaceLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ImpactLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ReversibilityLabel, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ToolName, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ArgumentsSummary, prompt, StringComparison.Ordinal);
                Assert.Contains(action.ArgumentsDigest, prompt, StringComparison.Ordinal);
                Assert.Contains("请仅在来源、任务、工作区和影响都符合你的意图时批准", prompt, StringComparison.Ordinal);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task ExactConversationTaskAndWorkspaceCanApproveAction()
        {
            var executionCount = 0;
            var action = CreateAction(
                executeOnApproval: false,
                executor: _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    CreateReviewContext(),
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Equal(0, executionCount);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Theory]
        [InlineData("another-conversation", TaskId, WorkspacePath)]
        [InlineData(ConversationId, "another-task", WorkspacePath)]
        [InlineData(ConversationId, TaskId, @"C:\ColorVision\AnotherWorkspace")]
        public async Task MismatchedConversationTaskOrWorkspaceCannotApproveAction(
            string conversationId,
            string taskId,
            string workspacePath)
        {
            var executionCount = 0;
            var action = CreateAction(
                executeOnApproval: false,
                executor: _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    new CopilotConfirmationReviewContext(conversationId, taskId, workspacePath),
                    CancellationToken.None);

                Assert.False(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);
                Assert.Equal(0, executionCount);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task InAppActionExecutesImmediatelyAfterApproval()
        {
            var executionCount = 0;
            var action = CreateAction(
                executeOnApproval: true,
                executor: _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                });

            var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                CopilotMcpConfirmationStore.Instance,
                action,
                CreateReviewContext(),
                CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(result.ExecutedImmediately);
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task AgentFrameworkActionIsApprovedWithoutDirectExecution()
        {
            var agentCallId = $"call-{Guid.NewGuid():N}";
            var exactArgumentsBinding = CopilotAgentToolInputExactBinding.Create(new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["scope"] = "current" },
            });
            var action = CopilotMcpConfirmationStore.Instance.CreateAgentFrameworkApproval(
                "Continue the hosted task",
                "Resume after a user decision.",
                "agent_tool",
                "{\"scope\":\"current\"}",
                exactArgumentsBinding,
                agentCallId,
                CreateRequestContext(),
                _ => { });
            try
            {
                var result = await CopilotMcpConfirmationDecision.ApproveAsync(
                    CopilotMcpConfirmationStore.Instance,
                    action,
                    CreateReviewContext(),
                    CancellationToken.None);

                Assert.True(result.Success);
                Assert.False(result.ExecutedImmediately);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.Contains("Agent 将在同一任务中继续执行", result.Message, StringComparison.Ordinal);

                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    @"C:\ColorVision\OtherWorkspace",
                    action.ArgumentsDigest,
                    agentCallId));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    WorkspacePath,
                    new string('0', 64),
                    agentCallId));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    WorkspacePath,
                    action.ArgumentsDigest,
                    $"call-{Guid.NewGuid():N}"));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
                Assert.True(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    CreateAgentRequest(),
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId));
                Assert.Equal(ConfirmableActionStatus.Executing, action.Status);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public void AgentFrameworkActionRequiresExactRunCapabilityCallAndSignatureScope()
        {
            var request = CreateAgentRequest();
            var agentCallId = $"call-{Guid.NewGuid():N}";
            const string executionSignature = "signature-approved";
            var approvedScope = CopilotExecutionScope.ForAgentRequest(request, runId: "run-approved")
                .WithRuntimeSnapshot("workspace-snapshot-a", capabilityRevision: 17)
                .BindToolCall("agent_tool", agentCallId, executionSignature);
            var action = CopilotMcpConfirmationStore.Instance.CreateAgentFrameworkApproval(
                "Continue the hosted task",
                "Resume after a user decision.",
                "agent_tool",
                "fields=payload",
                "{\"payload\":\"approved\"}",
                agentCallId,
                CopilotConfirmationRequestContext.ForAgent(
                    request,
                    executionScope: approvedScope),
                _ => { });

            try
            {
                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    CreateReviewContext(),
                    out _));

                var wrongRun = CopilotExecutionScope.ForAgentRequest(request, runId: "run-other")
                    .WithRuntimeSnapshot("workspace-snapshot-a", 17)
                    .BindToolCall("agent_tool", agentCallId, executionSignature);
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    request,
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId,
                    wrongRun));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var wrongCapability = CopilotExecutionScope.ForAgentRequest(request, runId: "run-approved")
                    .WithRuntimeSnapshot("workspace-snapshot-a", 18)
                    .BindToolCall("agent_tool", agentCallId, executionSignature);
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    request,
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId,
                    wrongCapability));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var wrongCall = approvedScope.BindToolCall(
                    "agent_tool",
                    $"call-{Guid.NewGuid():N}",
                    executionSignature);
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    request,
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId,
                    wrongCall));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var wrongSignature = approvedScope.BindToolCall(
                    "agent_tool",
                    agentCallId,
                    "signature-changed");
                Assert.False(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    request,
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId,
                    wrongSignature));
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                Assert.True(CopilotMcpConfirmationStore.Instance.BeginAgentFrameworkAction(
                    action.ActionId,
                    request,
                    WorkspacePath,
                    action.ArgumentsDigest,
                    agentCallId,
                    approvedScope));
                Assert.Equal(ConfirmableActionStatus.Executing, action.Status);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task ApprovedExternalActionRevalidatesCallerAndWorkspaceBeforeExecution()
        {
            const string callerSource = "tcp://127.0.0.1";
            var executionCount = 0;
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "Apply external change",
                "Apply a protected external MCP change.",
                "confirmation-required",
                "external_change",
                "{\"value\":1}",
                _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                },
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = callerSource,
                    WorkspacePath = WorkspacePath,
                    ImpactSummary = "Modifies the active workspace.",
                });
            try
            {
                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                    out _));

                var wrongCaller = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsDigest,
                    "tcp://127.0.0.2",
                    WorkspacePath,
                    CancellationToken.None);
                Assert.False(wrongCaller.Success);
                Assert.Equal("action_source_mismatch", wrongCaller.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var wrongWorkspace = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsDigest,
                    callerSource,
                    @"C:\ColorVision\OtherWorkspace",
                    CancellationToken.None);
                Assert.False(wrongWorkspace.Success);
                Assert.Equal("action_workspace_mismatch", wrongWorkspace.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var executed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsDigest,
                    callerSource,
                    WorkspacePath,
                    CancellationToken.None);
                Assert.True(executed.Success);
                Assert.Equal(1, executionCount);
                Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public void ExternalActionCannotBeReboundToAnAgentTask()
        {
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "External action",
                "External callers cannot transfer their approval to an in-app task.",
                "confirmation-required",
                "external_change",
                "{\"value\":2}",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = "tcp://127.0.0.1",
                    WorkspacePath = WorkspacePath,
                });
            try
            {
                Assert.False(CopilotMcpConfirmationStore.Instance.LinkAgentCall(
                    action.ActionId,
                    $"call-{Guid.NewGuid():N}",
                    CreateAgentRequest()));
                Assert.Equal(CopilotApprovalSourceKind.ExternalMcp, action.RequestContext.SourceKind);
                Assert.Empty(action.AgentCallId);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public async Task EmptyWorkspaceApprovalDoesNotBecomeApplicationWide()
        {
            const string callerSource = "tcp://127.0.0.1";
            var executionCount = 0;
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "No-workspace external action",
                "An approval created without a workspace must remain bound to that empty scope.",
                "confirmation-required",
                "external_no_workspace_change",
                "{\"value\":3}",
                _ =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                },
                requestContext: new CopilotConfirmationRequestContext
                {
                    SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                    RequestSource = callerSource,
                    WorkspacePath = string.Empty,
                });
            try
            {
                Assert.False(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                    out _));
                Assert.Equal(ConfirmableActionStatus.Pending, action.Status);

                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    action.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, string.Empty),
                    out _));
                var wrongWorkspace = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsDigest,
                    callerSource,
                    WorkspacePath,
                    CancellationToken.None);
                Assert.False(wrongWorkspace.Success);
                Assert.Equal("action_workspace_mismatch", wrongWorkspace.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, action.Status);

                var executed = await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(
                    action.ActionId,
                    action.ToolName,
                    action.ArgumentsDigest,
                    callerSource,
                    string.Empty,
                    CancellationToken.None);
                Assert.True(executed.Success);
                Assert.Equal(1, executionCount);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public void ConfirmActionSchemaRequiresOpaqueArgumentsDigest()
        {
            var dispatcher = new CopilotMcpToolDispatcher();
            var descriptor = Assert.Single(
                dispatcher.ListTools(),
                tool => string.Equals(tool.Name, "confirm_action", StringComparison.Ordinal));
            using var schema = JsonDocument.Parse(JsonSerializer.Serialize(descriptor.InputSchema));
            var required = schema.RootElement
                .GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString())
                .ToArray();

            Assert.Contains("arguments_digest", required);
            Assert.DoesNotContain("arguments_summary", required);
            Assert.True(schema.RootElement.GetProperty("properties").TryGetProperty("arguments_digest", out _));
        }

        [Fact]
        public async Task GenericAuditToolsDoNotDiscloseReusableActionIdentifiers()
        {
            var action = CreateAction(executeOnApproval: false);
            var dispatcher = new CopilotMcpToolDispatcher();
            try
            {
                var auditDescriptor = Assert.Single(
                    dispatcher.ListTools(),
                    tool => string.Equals(tool.Name, "get_audit_log", StringComparison.Ordinal));
                using var schema = JsonDocument.Parse(JsonSerializer.Serialize(auditDescriptor.InputSchema));
                Assert.False(schema.RootElement.GetProperty("properties").TryGetProperty("action_id", out _));

                var auditLog = await dispatcher.CallAsync(
                    "get_audit_log",
                    new Dictionary<string, JsonElement>
                    {
                        ["max_entries"] = JsonSerializer.SerializeToElement(200),
                    },
                    CancellationToken.None);
                var auditSummary = await dispatcher.CallAsync(
                    "get_audit_summary",
                    new Dictionary<string, JsonElement>
                    {
                        ["max_entries"] = JsonSerializer.SerializeToElement(200),
                    },
                    CancellationToken.None);

                Assert.True(auditLog.Success);
                Assert.True(auditSummary.Success);
                Assert.DoesNotContain(action.ActionId, auditLog.Text, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(action.ActionId, auditSummary.Text, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Approval event: True", auditLog.Text, StringComparison.Ordinal);
            }
            finally
            {
                CancelIfActive(action);
            }
        }

        [Fact]
        public void AgentFrameworkDigestUsesCompleteCanonicalToolInput()
        {
            var sharedPrefix = new string('x', 1600);
            var firstBinding = CopilotAgentToolInputExactBinding.Create(new CopilotAgentToolInput
            {
                Query = "inspect",
                Path = @"C:\ColorVision\SafetyContract\config.json",
                Cursor = "cursor-1",
                StartLine = 2,
                StartColumn = 3,
                EndLine = 9,
                Arguments = new Dictionary<string, object?>
                {
                    ["payload"] = new Dictionary<string, object?>
                    {
                        ["z"] = new object?[] { 1, true, sharedPrefix + "-first" },
                        ["a"] = new Dictionary<string, object?>
                        {
                            ["second"] = 2,
                            ["first"] = 1,
                        },
                    },
                },
            });
            var reorderedBinding = CopilotAgentToolInputExactBinding.Create(new CopilotAgentToolInput
            {
                Query = "inspect",
                Path = @"C:\ColorVision\SafetyContract\config.json",
                Cursor = "cursor-1",
                StartLine = 2,
                StartColumn = 3,
                EndLine = 9,
                Arguments = new Dictionary<string, object?>
                {
                    ["payload"] = new Dictionary<string, object?>
                    {
                        ["a"] = new Dictionary<string, object?>
                        {
                            ["first"] = 1,
                            ["second"] = 2,
                        },
                        ["z"] = new object?[] { 1, true, sharedPrefix + "-first" },
                    },
                },
            });
            var changedBinding = CopilotAgentToolInputExactBinding.Create(new CopilotAgentToolInput
            {
                Query = "inspect",
                Path = @"C:\ColorVision\SafetyContract\config.json",
                Cursor = "cursor-1",
                StartLine = 2,
                StartColumn = 3,
                EndLine = 9,
                Arguments = new Dictionary<string, object?>
                {
                    ["payload"] = new Dictionary<string, object?>
                    {
                        ["a"] = new Dictionary<string, object?>
                        {
                            ["first"] = 1,
                            ["second"] = 2,
                        },
                        ["z"] = new object?[] { 1, true, sharedPrefix + "-second" },
                    },
                },
            });

            Assert.Equal(firstBinding, reorderedBinding);
            Assert.NotEqual(firstBinding, changedBinding);
            Assert.True(firstBinding.Length > 1600);
            Assert.Equal(
                CopilotAgentToolInputExactBinding.CreateExecutionSignature("agent_tool", new CopilotAgentToolInput
                {
                    Query = "inspect",
                    Path = @"C:\ColorVision\SafetyContract\config.json",
                    Cursor = "cursor-1",
                    StartLine = 2,
                    StartColumn = 3,
                    EndLine = 9,
                    Arguments = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["a"] = new Dictionary<string, object?>
                            {
                                ["first"] = 1,
                                ["second"] = 2,
                            },
                            ["z"] = new object?[] { 1, true, sharedPrefix + "-first" },
                        },
                    },
                }),
                CopilotAgentToolInputExactBinding.CreateExecutionSignature("agent_tool", new CopilotAgentToolInput
                {
                    Query = "inspect",
                    Path = @"C:\ColorVision\SafetyContract\config.json",
                    Cursor = "cursor-1",
                    StartLine = 2,
                    StartColumn = 3,
                    EndLine = 9,
                    Arguments = new Dictionary<string, object?>
                    {
                        ["payload"] = new Dictionary<string, object?>
                        {
                            ["z"] = new object?[] { 1, true, sharedPrefix + "-first" },
                            ["a"] = new Dictionary<string, object?>
                            {
                                ["second"] = 2,
                                ["first"] = 1,
                            },
                        },
                    },
                }));
            Assert.NotEqual(
                CopilotAgentToolInputExactBinding.CreateExecutionSignature("agent_tool", new CopilotAgentToolInput
                {
                    Query = "left|right",
                    Path = "tail",
                }),
                CopilotAgentToolInputExactBinding.CreateExecutionSignature("agent_tool", new CopilotAgentToolInput
                {
                    Query = "left",
                    Path = "right|tail",
                }));

            var firstAction = CreateAgentFrameworkAction(firstBinding);
            var reorderedAction = CreateAgentFrameworkAction(reorderedBinding);
            var changedAction = CreateAgentFrameworkAction(changedBinding);
            try
            {
                Assert.Equal(firstAction.ArgumentsDigest, reorderedAction.ArgumentsDigest);
                Assert.NotEqual(firstAction.ArgumentsDigest, changedAction.ArgumentsDigest);
            }
            finally
            {
                CancelIfActive(firstAction);
                CancelIfActive(reorderedAction);
                CancelIfActive(changedAction);
            }
        }

        [Fact]
        public void AgentFrameworkApprovalSnapshotDeepFreezesMutableArguments()
        {
            var nested = new Dictionary<string, object?>
            {
                ["command"] = "approved",
            };
            var items = new List<object?>
            {
                nested,
                "stable",
            };
            var arguments = new Dictionary<string, object?>
            {
                ["payload"] = nested,
                ["items"] = items,
            };
            var mutableInput = new CopilotAgentToolInput
            {
                Query = "inspect",
                Path = @"C:\ColorVision\SafetyContract\config.json",
                Arguments = arguments,
            };

            Assert.True(CopilotAgentToolInputSnapshot.TryCreate(
                mutableInput,
                out var snapshot,
                out var error),
                error);
            var approvedBinding = CopilotAgentToolInputExactBinding.Create(snapshot);
            var approvedSignature = CopilotAgentToolInputExactBinding.CreateExecutionSignature(
                "agent_tool",
                snapshot);

            nested["command"] = "mutated";
            items.Add("late-item");
            arguments["late-field"] = true;

            Assert.Equal(approvedBinding, CopilotAgentToolInputExactBinding.Create(snapshot));
            Assert.True(CopilotAgentToolInputExactBinding.MatchesExecutionSignature(
                "agent_tool",
                snapshot,
                approvedSignature));
            Assert.False(CopilotAgentToolInputExactBinding.MatchesExecutionSignature(
                "agent_tool",
                mutableInput,
                approvedSignature));

            var frozenPayload = Assert.IsType<JsonElement>(snapshot.Arguments["payload"]);
            Assert.Equal("approved", frozenPayload.GetProperty("command").GetString());
            var frozenItems = Assert.IsType<JsonElement>(snapshot.Arguments["items"]);
            Assert.Equal(2, frozenItems.GetArrayLength());
            var writableView = Assert.IsAssignableFrom<IDictionary<string, object?>>(snapshot.Arguments);
            Assert.Throws<NotSupportedException>(() => writableView.Add("late-field", true));
        }

        [Fact]
        public void AgentFrameworkApprovalSnapshotFailsClosedForCyclicArguments()
        {
            var cycle = new List<object?>();
            cycle.Add(cycle);
            var input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["payload"] = cycle,
                },
            };

            Assert.False(CopilotAgentToolInputSnapshot.TryCreate(
                input,
                out var snapshot,
                out var error));
            Assert.Same(CopilotAgentToolInput.Empty, snapshot);
            Assert.Contains("immutable approval snapshot", error, StringComparison.Ordinal);
        }

        [Fact]
        public void AgentFrameworkApprovalReservationKeyBindsProviderCallIdAndSignature()
        {
            const string signature = "0123456789abcdef";

            Assert.Equal(
                CopilotFrameworkApprovalReservationKey.Create(" call-a ", signature),
                CopilotFrameworkApprovalReservationKey.Create("call-a", signature));
            Assert.NotEqual(
                CopilotFrameworkApprovalReservationKey.Create("call-a", signature),
                CopilotFrameworkApprovalReservationKey.Create("call-b", signature));
            Assert.NotEqual(
                CopilotFrameworkApprovalReservationKey.Create(null, signature),
                CopilotFrameworkApprovalReservationKey.Create("call-a", signature));
            Assert.Equal(
                CopilotFrameworkApprovalReservationKey.Create(null, signature),
                CopilotFrameworkApprovalReservationKey.Create(" ", signature));
        }

        [Fact]
        public Task LongArgumentsWithSameVisiblePrefixCannotReuseFirstApproval() =>
            AssertLongArgumentsCannotReuseApprovalAsync(approveFirstAction: true);

        [Fact]
        public Task LongArgumentsWithSameVisiblePrefixCannotReuseSecondApproval() =>
            AssertLongArgumentsCannotReuseApprovalAsync(approveFirstAction: false);

        private static ConfirmableAction CreateAction(
            bool executeOnApproval,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>>? executor = null)
        {
            return CopilotMcpConfirmationStore.Instance.Create(
                $"Desktop pet test {Guid.NewGuid():N}",
                "Review a pending desktop pet action.",
                "confirmation-required",
                "desktop_pet_test",
                "{\"value\":1}",
                executor ?? (_ => Task.FromResult(CopilotMcpToolCallResult.Ok("executed"))),
                executeOnApproval,
                requestContext: CreateRequestContext());
        }

        private static ConfirmableAction CreateAgentFrameworkAction(string exactArgumentsBinding)
        {
            return CopilotMcpConfirmationStore.Instance.CreateAgentFrameworkApproval(
                "Continue the hosted task",
                "Resume after a user decision.",
                "agent_tool",
                "fields=payload",
                exactArgumentsBinding,
                $"call-{Guid.NewGuid():N}",
                CreateRequestContext(),
                _ => { });
        }

        private static CopilotConfirmationRequestContext CreateRequestContext() =>
            new()
            {
                SourceKind = CopilotApprovalSourceKind.InAppAgent,
                RequestSource = "in-app-agent",
                ConversationId = ConversationId,
                TaskId = TaskId,
                TaskLabel = "检查并应用桌宠模板",
                WorkspacePath = WorkspacePath,
                ImpactSummary = "将修改当前工作区的桌宠设置。",
                Reversibility = CopilotApprovalReversibility.ManualOnly,
                ReversibilitySummary = "需要手动恢复原设置。",
            };

        private static CopilotConfirmationReviewContext CreateReviewContext() =>
            new(ConversationId, TaskId, WorkspacePath);

        private static async Task AssertLongArgumentsCannotReuseApprovalAsync(bool approveFirstAction)
        {
            var executionCount = 0;
            var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = WorkspacePath,
                },
                SetLanguageHandler = (_, _) =>
                {
                    executionCount++;
                    return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
                },
            });
            var commonPrefix = $"language-{Guid.NewGuid():N}-" + new string('x', 1200);
            var firstResult = await dispatcher.CallAsync(
                "set_language",
                new Dictionary<string, JsonElement>
                {
                    ["language"] = JsonSerializer.SerializeToElement(commonPrefix + "-first"),
                },
                CancellationToken.None);
            var secondResult = await dispatcher.CallAsync(
                "set_language",
                new Dictionary<string, JsonElement>
                {
                    ["language"] = JsonSerializer.SerializeToElement(commonPrefix + "-second"),
                },
                CancellationToken.None);
            var firstAction = Assert.Single(
                CopilotMcpConfirmationStore.Instance.GetPendingActions(),
                action => string.Equals(action.ActionId, firstResult.ApprovalActionId, StringComparison.Ordinal));
            var secondAction = Assert.Single(
                CopilotMcpConfirmationStore.Instance.GetPendingActions(),
                action => string.Equals(action.ActionId, secondResult.ApprovalActionId, StringComparison.Ordinal));

            try
            {
                Assert.True(firstResult.RequiresApproval);
                Assert.True(secondResult.RequiresApproval);
                Assert.Equal(firstAction.ArgumentsSummary, secondAction.ArgumentsSummary);
                Assert.NotEqual(firstAction.ArgumentsDigest, secondAction.ArgumentsDigest);
                Assert.Contains($"arguments_digest: {firstAction.ArgumentsDigest}", firstResult.Text, StringComparison.Ordinal);
                Assert.Contains($"arguments_digest: {secondAction.ArgumentsDigest}", secondResult.Text, StringComparison.Ordinal);
                Assert.Contains("\"arguments_digest\"", firstAction.ConfirmActionPayloadJson, StringComparison.Ordinal);

                var approvedAction = approveFirstAction ? firstAction : secondAction;
                var otherAction = approveFirstAction ? secondAction : firstAction;
                Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                    approvedAction.ActionId,
                    new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                    out _));

                var wrongDigestResult = await dispatcher.CallAsync(
                    "confirm_action",
                    CreateConfirmActionArguments(approvedAction, otherAction.ArgumentsDigest),
                    CancellationToken.None);

                Assert.False(wrongDigestResult.Success);
                Assert.Equal("action_arguments_mismatch", wrongDigestResult.ErrorCode);
                Assert.Equal(ConfirmableActionStatus.Approved, approvedAction.Status);
                Assert.Equal(0, executionCount);

                var exactDigestResult = await dispatcher.CallAsync(
                    "confirm_action",
                    CreateConfirmActionArguments(approvedAction, approvedAction.ArgumentsDigest),
                    CancellationToken.None);

                Assert.True(exactDigestResult.Success);
                Assert.Equal(ConfirmableActionStatus.Executed, approvedAction.Status);
                Assert.Equal(1, executionCount);
            }
            finally
            {
                CancelIfActive(firstAction);
                CancelIfActive(secondAction);
            }
        }

        private static IReadOnlyDictionary<string, JsonElement> CreateConfirmActionArguments(
            ConfirmableAction action,
            string argumentsDigest) =>
            new Dictionary<string, JsonElement>
            {
                ["action_id"] = JsonSerializer.SerializeToElement(action.ActionId),
                ["tool_name"] = JsonSerializer.SerializeToElement(action.ToolName),
                ["arguments_digest"] = JsonSerializer.SerializeToElement(argumentsDigest),
            };

        private static CopilotAgentRequest CreateAgentRequest() =>
            new()
            {
                ConversationId = ConversationId,
                TaskId = TaskId,
                WorkspacePath = WorkspacePath,
                UserText = "检查并应用桌宠模板",
                TaskIntentText = "检查并应用桌宠模板",
            };

        private static void CancelIfActive(ConfirmableAction action)
        {
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _);
        }
    }
}
