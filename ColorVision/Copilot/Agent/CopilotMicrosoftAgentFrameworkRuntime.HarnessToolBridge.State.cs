#pragma warning disable MAAI001
#pragma warning disable CA1859
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
            private sealed class HarnessToolFunction : AIFunction
            {
                private readonly HarnessToolBridge _owner;
                private readonly ICopilotTool _tool;
                private readonly string _functionName;

                public HarnessToolFunction(HarnessToolBridge owner, ICopilotTool tool, string functionName)
                {
                    _owner = owner;
                    _tool = tool;
                    _functionName = functionName;
                }

                public override string Name => _functionName;

                public override string Description => BuildFunctionDescription(_tool);

                public override JsonElement JsonSchema => _tool.InputSchema.JsonSchema;

                protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
                {
                    var providerCallId = FunctionInvokingChatClient.CurrentContext?.CallContent?.CallId;
                    if (!_tool.InputSchema.TryBind(arguments, out var toolInput, out var error))
                        return _owner.RecordRejectedToolCall(_tool, arguments, error, providerCallId);

                    return await _owner.ExecuteAsync(_tool, toolInput, providerCallId, cancellationToken);
                }
            }

            public sealed class FrameworkApprovalReservation
            {
                public string CallId { get; init; } = string.Empty;

                public int Round { get; init; }

                public int Attempt { get; init; } = 1;

                public int MaxAttempts { get; init; } = 1;

                public string Signature { get; init; } = string.Empty;

                public string ProviderCallId { get; init; } = string.Empty;

                public ICopilotTool Tool { get; init; } = null!;

                public CopilotAgentToolInput ToolInput { get; init; } = CopilotAgentToolInput.Empty;

                public string PreviousObservationProgressSignature { get; init; } =
                    string.Empty;

                public CopilotExecutionScope ExecutionScope { get; init; } = CopilotExecutionScope.Empty;

                public DateTimeOffset StartedAtUtc { get; init; }

                public string ApprovalActionId { get; set; } = string.Empty;

                public string ApprovalArgumentsDigest { get; set; } = string.Empty;

                public bool ApprovedByFullAccess { get; set; }

                public bool ApprovedByExecPolicy { get; set; }

                public CopilotApprovalPromptCategory? ApprovalPromptCategoryOverride { get; set; }

                public string ApprovalPromptReasonOverride { get; set; } = string.Empty;

                public CopilotApprovalPromptCategory EffectiveApprovalPromptCategory =>
                    ApprovalPromptCategoryOverride ?? Tool.Capability.ApprovalPromptCategory;

                internal IReadOnlyList<CopilotToolExecutionHookRun> PermissionHookRuns { get; set; } =
                    Array.Empty<CopilotToolExecutionHookRun>();

                internal IReadOnlyList<CopilotToolExecutionHookBinding> HookBindings { get; set; } =
                    Array.Empty<CopilotToolExecutionHookBinding>();
            }

            private sealed class ToolAttemptState
            {
                public int AttemptCount { get; set; }

                public int RejectedCount { get; set; }

                public bool InProgress { get; set; }

                public CopilotToolExecutionOutcome? LastOutcome { get; set; }
            }

            private sealed class UnavailableTool(string name) : ICopilotTool
            {
                public string Name { get; } = name;

                public string Description => "Represents a model-requested function that is unavailable in the current request.";

                public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
                    CopilotToolIdempotency.Unknown,
                    auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

                public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

                public bool CanHandle(CopilotAgentRequest request) => false;

                public Task<CopilotToolResult> ExecuteAsync(
                    CopilotAgentRequest request,
                    CopilotAgentToolInput toolInput,
                    CancellationToken cancellationToken)
                {
                    return Task.FromResult(new CopilotToolResult
                    {
                        ToolName = Name,
                        Success = false,
                        Summary = $"{Name} is unavailable.",
                        ErrorMessage = "Unavailable functions cannot be executed.",
                        FailureKind = CopilotToolFailureKind.NotFound,
                    });
                }
            }
        }
    }
}
