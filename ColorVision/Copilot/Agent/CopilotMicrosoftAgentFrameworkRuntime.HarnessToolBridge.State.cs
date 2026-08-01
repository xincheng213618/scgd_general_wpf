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
            internal sealed class UserQuestionAIFunction : AIFunction
            {
                private static readonly JsonSerializerOptions SerializerOptions = new()
                {
                    PropertyNameCaseInsensitive = true,
                };
                private static readonly JsonElement Schema = JsonDocument.Parse(
                    """
                    {
                      "type": "object",
                      "properties": {
                        "header": {
                          "type": "string",
                          "description": "Short UI label, 1-12 characters.",
                          "minLength": 1,
                          "maxLength": 12
                        },
                        "question": {
                          "type": "string",
                          "description": "One concise clarification question whose answer materially changes the outcome.",
                          "minLength": 1,
                          "maxLength": 500
                        },
                        "options": {
                          "type": "array",
                          "description": "Two or three mutually exclusive choices. Put the recommended choice first and suffix its label with '(Recommended)'.",
                          "minItems": 2,
                          "maxItems": 3,
                          "items": {
                            "type": "object",
                            "properties": {
                              "label": {
                                "type": "string",
                                "description": "Short choice label.",
                                "minLength": 1,
                                "maxLength": 80
                              },
                              "description": {
                                "type": "string",
                                "description": "One short sentence explaining the impact or tradeoff.",
                                "maxLength": 240
                              }
                            },
                            "required": ["label", "description"],
                            "additionalProperties": false
                          }
                        }
                      },
                      "required": ["header", "question", "options"],
                      "additionalProperties": false
                    }
                    """).RootElement.Clone();

                private readonly CopilotUserQuestionCoordinator _coordinator;
                private readonly CopilotAgentRequest _request;
                private readonly Action<CopilotAgentEvent> _emit;

                public UserQuestionAIFunction(
                    CopilotUserQuestionCoordinator coordinator,
                    CopilotAgentRequest request,
                    Action<CopilotAgentEvent> emit)
                {
                    _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
                    _request = request ?? throw new ArgumentNullException(nameof(request));
                    _emit = emit ?? throw new ArgumentNullException(nameof(emit));
                }

                public override string Name => "AskUserQuestion";

                public override string Description =>
                    "Pause the current main Agent task to ask one structured clarification question. "
                    + "Use only when 2-3 materially different valid choices remain; this is not approval. "
                    + "Call this function alone in a provider response. "
                    + "The user may select an option or type a different answer.";

                public override JsonElement JsonSchema => Schema;

                protected override async ValueTask<object?> InvokeCoreAsync(
                    AIFunctionArguments arguments,
                    CancellationToken cancellationToken)
                {
                    CopilotUserQuestionInput? input;
                    try
                    {
                        input = JsonSerializer.Deserialize<CopilotUserQuestionInput>(
                            JsonSerializer.Serialize(arguments),
                            SerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        return FormatRejected("The structured question arguments are invalid: " + ex.Message);
                    }

                    try
                    {
                        var resolved = await _coordinator.AskAsync(
                            _request,
                            input ?? new CopilotUserQuestionInput(),
                            _emit,
                            cancellationToken).ConfigureAwait(false);
                        return JsonSerializer.Serialize(new
                        {
                            outcome = "answered",
                            answer = resolved.Answer,
                        });
                    }
                    catch (ArgumentException ex)
                    {
                        return FormatRejected(ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatRejected(ex.Message);
                    }
                }

                private static string FormatRejected(string error)
                {
                    return JsonSerializer.Serialize(new
                    {
                        outcome = "rejected",
                        error = CopilotUserFacingErrorFormatter.Sanitize(error),
                    });
                }
            }

            private sealed class HarnessToolFunction : AIFunction
            {
                private readonly HarnessToolBridge _owner;
                private readonly ICopilotTool _tool;

                public HarnessToolFunction(HarnessToolBridge owner, ICopilotTool tool)
                {
                    _owner = owner;
                    _tool = tool;
                }

                public override string Name => ToFunctionName(_tool.Name);

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
