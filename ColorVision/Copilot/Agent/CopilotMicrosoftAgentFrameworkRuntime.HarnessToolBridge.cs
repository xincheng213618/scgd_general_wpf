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
            private readonly CopilotAgentRequest _request;
            private readonly CopilotExecutionScope _executionScope;
            private readonly IReadOnlyDictionary<string, ICopilotTool> _tools;
            private readonly CopilotToolExecutor _toolExecutor;
            private readonly CopilotFrameworkApprovalCoordinator _approvalCoordinator;
            private readonly Action<CopilotAgentEvent> _emit;
            private readonly Func<long> _capabilityRevisionProvider;
            private readonly List<CopilotAgentStepRecord> _stepRecords = new();
            private readonly Dictionary<string, ToolAttemptState> _attemptsBySignature = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<CopilotFrameworkApprovalReservationKey, FrameworkApprovalReservation> _approvedCalls = new();
            private readonly CopilotProviderToolCallLedger _providerToolCalls = new();
            private readonly object _syncRoot = new();
            private readonly int _maxToolCalls;
            private readonly Action<CopilotDelegatedRunUsage>? _recordDelegatedRunUsage;
            private readonly CopilotAgentToolBudgetCompletionGate _toolBudgetCompletionGate;
            private CopilotTokenUsage _delegatedUsage;
            private int _reservedToolCalls;

            public HarnessToolBridge(
                CopilotAgentRequest request,
                CopilotExecutionScope executionScope,
                IReadOnlyList<ICopilotTool> tools,
                int maxToolCalls,
                CopilotToolExecutor toolExecutor,
                CopilotFrameworkApprovalCoordinator approvalCoordinator,
                Action<CopilotAgentEvent> emit,
                Func<long> capabilityRevisionProvider,
                Action<CopilotDelegatedRunUsage>? recordDelegatedRunUsage = null,
                Action? onToolBudgetExhausted = null)
            {
                _request = request;
                _executionScope = executionScope ?? throw new ArgumentNullException(nameof(executionScope));
                _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
                _maxToolCalls = Math.Max(1, maxToolCalls);
                _toolExecutor = toolExecutor;
                _approvalCoordinator = approvalCoordinator;
                _emit = emit;
                _capabilityRevisionProvider = capabilityRevisionProvider ?? throw new ArgumentNullException(nameof(capabilityRevisionProvider));
                _recordDelegatedRunUsage = recordDelegatedRunUsage;
                _toolBudgetCompletionGate = new CopilotAgentToolBudgetCompletionGate(onToolBudgetExhausted);
            }

            public IReadOnlyList<CopilotAgentStepRecord> StepRecords
            {
                get
                {
                    lock (_syncRoot)
                        return _stepRecords.OrderBy(step => step.Round).ToArray();
                }
            }

            public bool ToolBudgetExhausted
            {
                get => _toolBudgetCompletionGate.IsExhausted;
            }

            public CopilotTokenUsage DelegatedUsage
            {
                get
                {
                    lock (_syncRoot)
                        return _delegatedUsage;
                }
            }

            public IList<AITool> CreateFunctions()
            {
                var functions = new List<AITool>();
                foreach (var tool in _tools.Values)
                {
                    var function = new HarnessToolFunction(this, tool);
                    functions.Add(RequiresNativeApproval(tool) ? new ApprovalRequiredAIFunction(function) : function);
                }
                return functions;
            }

            public void RecordUnknownToolCall(FunctionCallContent functionCall)
            {
                ArgumentNullException.ThrowIfNull(functionCall);
                CopilotToolExecutionOutcome outcome;
                lock (_syncRoot)
                {
                    if (_reservedToolCalls >= _maxToolCalls)
                    {
                        SignalToolBudgetExhausted();
                        return;
                    }

                    var round = ++_reservedToolCalls;
                    var occurredAtUtc = DateTimeOffset.UtcNow;
                    var toolName = NormalizeUnknownToolName(functionCall.Name);
                    var tool = new UnavailableTool(toolName);
                    var arguments = functionCall.Arguments == null
                        ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, object?>(functionCall.Arguments, StringComparer.OrdinalIgnoreCase);
                    var toolInput = CreateNamesOnlyToolInput(arguments);
                    var unknownCallId = string.IsNullOrWhiteSpace(functionCall.CallId)
                        ? Guid.NewGuid().ToString("N")
                        : functionCall.CallId.Trim();
                    var invocation = new CopilotToolInvocation
                    {
                        CallId = unknownCallId,
                        Round = round,
                        Attempt = 1,
                        MaxAttempts = 1,
                        RuntimeName = "agent-framework",
                        Tool = tool,
                        AgentRequest = _request,
                        ExecutionScope = _executionScope.BindToolCall(
                            tool.Name,
                            unknownCallId,
                            BuildExecutionSignature(tool.Name, toolInput)),
                        ToolInput = toolInput,
                        ToolCall = new CopilotToolCall
                        {
                            ToolName = toolName,
                            ToolInput = toolInput,
                            Reason = "Rejected because the model requested a function that is unavailable in the current request.",
                        },
                    };
                    var result = new CopilotToolResult
                    {
                        ToolName = toolName,
                        Success = false,
                        Summary = $"{toolName} is not available in the current Agent request.",
                        ErrorMessage = "The model requested a function that was not included in the current request-scoped tool surface.",
                        FailureKind = CopilotToolFailureKind.NotFound,
                    };
                    var execution = new CopilotToolExecutionInfo
                    {
                        CallId = invocation.CallId,
                        Round = round,
                        Attempt = 1,
                        MaxAttempts = 1,
                        RuntimeName = invocation.RuntimeName,
                        ToolName = toolName,
                        Access = CopilotToolAccess.Write,
                        RiskLevel = CopilotToolRiskLevel.High,
                        ApprovalMode = CopilotToolApprovalMode.Always,
                        Idempotency = CopilotToolIdempotency.Unknown,
                        ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                        ArgumentSummary = CreateRejectedArgumentSummary(arguments),
                        State = CopilotToolExecutionState.Failed,
                        FailureKind = CopilotToolFailureKind.NotFound,
                        RetryEligible = false,
                        StartedAtUtc = occurredAtUtc,
                        CompletedAtUtc = occurredAtUtc,
                        TimeoutMs = Math.Max(1, (long)tool.Capability.EffectiveExecutionTimeout.TotalMilliseconds),
                    };
                    outcome = new CopilotToolExecutionOutcome
                    {
                        Invocation = invocation,
                        Result = result,
                        Execution = execution,
                    };
                    _stepRecords.Add(outcome.StepRecord);
                }

                CopilotToolExecutionAuditLogger.Record(outcome);
                _emit(CopilotAgentEvent.FromToolResult(outcome.Result, outcome.Execution));
            }

            private string RecordRejectedToolCall(
                ICopilotTool tool,
                IReadOnlyDictionary<string, object?> arguments,
                string error,
                string? callId = null)
            {
                CopilotToolExecutionOutcome outcome;
                lock (_syncRoot)
                {
                    if (_reservedToolCalls >= _maxToolCalls)
                    {
                        SignalToolBudgetExhausted();
                        return FormatRejectedToolCall(tool.Name, $"{error} The request has reached its {_maxToolCalls}-call tool limit.");
                    }

                    var round = ++_reservedToolCalls;
                    var occurredAtUtc = DateTimeOffset.UtcNow;
                    var toolInput = CreateNamesOnlyToolInput(arguments);
                    var invocation = new CopilotToolInvocation
                    {
                        CallId = string.IsNullOrWhiteSpace(callId) ? Guid.NewGuid().ToString("N") : callId.Trim(),
                        Round = round,
                        Attempt = 1,
                        MaxAttempts = 1,
                        RuntimeName = "agent-framework",
                        Tool = tool,
                        AgentRequest = _request,
                        ToolInput = toolInput,
                        ToolCall = CreateToolCall(tool, toolInput),
                    };
                    var capability = tool.Capability;
                    var result = new CopilotToolResult
                    {
                        ToolName = tool.Name,
                        Success = false,
                        Summary = $"{tool.Name} arguments were rejected before execution.",
                        ErrorMessage = error,
                        FailureKind = CopilotToolFailureKind.Validation,
                    };
                    var execution = new CopilotToolExecutionInfo
                    {
                        CallId = invocation.CallId,
                        Round = round,
                        Attempt = 1,
                        MaxAttempts = 1,
                        RuntimeName = invocation.RuntimeName,
                        ToolName = tool.Name,
                        Access = capability.Access,
                        RiskLevel = capability.RiskLevel,
                        ApprovalMode = capability.ApprovalMode,
                        Idempotency = capability.Idempotency,
                        ConcurrencyMode = CopilotToolExecutor.ResolveConcurrencyMode(tool),
                        ArgumentSummary = CreateRejectedArgumentSummary(arguments),
                        State = CopilotToolExecutionState.Failed,
                        FailureKind = CopilotToolFailureKind.Validation,
                        RetryEligible = false,
                        StartedAtUtc = occurredAtUtc,
                        CompletedAtUtc = occurredAtUtc,
                        TimeoutMs = Math.Max(1, (long)capability.EffectiveExecutionTimeout.TotalMilliseconds),
                    };
                    outcome = new CopilotToolExecutionOutcome
                    {
                        Invocation = invocation,
                        Result = result,
                        Execution = execution,
                    };
                    _stepRecords.Add(outcome.StepRecord);
                }

                CopilotToolExecutionAuditLogger.Record(outcome);
                _emit(CopilotAgentEvent.FromToolResult(outcome.Result, outcome.Execution));
                return CopilotFrameworkToolResultFormatter.Format(outcome);
            }

            private string RecordGuardRejectedToolCall(
                ICopilotTool tool,
                CopilotAgentToolInput toolInput,
                string signature,
                string error,
                string? callId = null)
            {
                CopilotToolExecutionOutcome outcome;
                lock (_syncRoot)
                {
                    if (_reservedToolCalls >= _maxToolCalls)
                    {
                        SignalToolBudgetExhausted();
                        return FormatRejectedToolCall(tool.Name, $"{error} The request has reached its {_maxToolCalls}-call tool limit.");
                    }

                    var round = ++_reservedToolCalls;
                    var attempt = 1;
                    if (_attemptsBySignature.TryGetValue(signature, out var state))
                    {
                        state.RejectedCount++;
                        attempt = state.InProgress
                            ? Math.Max(1, state.AttemptCount)
                            : Math.Max(1, state.AttemptCount + state.RejectedCount);
                    }

                    var maxAttempts = Math.Max(attempt, GetMaximumAttempts(tool));
                    var occurredAtUtc = DateTimeOffset.UtcNow;
                    var invocation = new CopilotToolInvocation
                    {
                        CallId = string.IsNullOrWhiteSpace(callId) ? Guid.NewGuid().ToString("N") : callId.Trim(),
                        Round = round,
                        Attempt = attempt,
                        MaxAttempts = maxAttempts,
                        RuntimeName = "agent-framework",
                        Tool = tool,
                        AgentRequest = _request,
                        ToolInput = toolInput,
                        ToolCall = CreateToolCall(tool, toolInput),
                    };
                    var capability = tool.Capability;
                    var result = new CopilotToolResult
                    {
                        ToolName = tool.Name,
                        Success = false,
                        Summary = $"{tool.Name} was not executed because the identical call made no new progress.",
                        ErrorMessage = error,
                        FailureKind = CopilotToolFailureKind.Conflict,
                    };
                    var execution = new CopilotToolExecutionInfo
                    {
                        CallId = invocation.CallId,
                        Round = round,
                        Attempt = attempt,
                        MaxAttempts = maxAttempts,
                        RuntimeName = invocation.RuntimeName,
                        ToolName = tool.Name,
                        Access = capability.Access,
                        RiskLevel = capability.RiskLevel,
                        ApprovalMode = capability.ApprovalMode,
                        Idempotency = capability.Idempotency,
                        ConcurrencyMode = CopilotToolExecutor.ResolveConcurrencyMode(tool),
                        ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(tool, toolInput),
                        State = CopilotToolExecutionState.Failed,
                        FailureKind = CopilotToolFailureKind.Conflict,
                        RetryEligible = false,
                        StartedAtUtc = occurredAtUtc,
                        CompletedAtUtc = occurredAtUtc,
                        TimeoutMs = Math.Max(1, (long)capability.EffectiveExecutionTimeout.TotalMilliseconds),
                    };
                    outcome = new CopilotToolExecutionOutcome
                    {
                        Invocation = invocation,
                        Result = result,
                        Execution = execution,
                    };
                    _stepRecords.Add(outcome.StepRecord);
                }

                CopilotToolExecutionAuditLogger.Record(outcome);
                _emit(CopilotAgentEvent.FromToolResult(outcome.Result, outcome.Execution));
                return CopilotFrameworkToolResultFormatter.Format(outcome);
            }

            private static bool RequiresNativeApproval(ICopilotTool tool)
            {
                return tool.Capability.RequiresNativeApproval;
            }

            public static string ToFunctionName(string toolName)
            {
                var snakeCase = Regex.Replace(toolName ?? string.Empty, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
                snakeCase = Regex.Replace(snakeCase, "[^a-z0-9]+", "_").Trim('_');
                return "colorvision_" + snakeCase;
            }

            private static string BuildFunctionDescription(ICopilotTool tool)
            {
                var access = tool.Capability.Access == CopilotToolAccess.ReadOnly
                    ? "This function is read-only."
                    : "This function can change application state and must match the user's explicit request.";
                return $"{tool.Description} {access}";
            }

            private static string BuildExecutionSignature(string toolName, CopilotAgentToolInput toolInput)
            {
                return CopilotAgentToolInputExactBinding.CreateExecutionSignature(toolName, toolInput);
            }

            private static string CreateRejectedArgumentSummary(IReadOnlyDictionary<string, object?> arguments)
            {
                var names = arguments.Keys
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => new string(name.Trim().Where(character => !char.IsControl(character)).Take(120).ToArray()))
                    .Where(name => name.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .Take(64)
                    .ToArray();
                var summary = names.Length == 0 ? "(none)" : "fields=" + string.Join(",", names);
                return summary.Length <= 800 ? summary : summary[..800];
            }

            private static CopilotAgentToolInput CreateNamesOnlyToolInput(IReadOnlyDictionary<string, object?> arguments)
            {
                return new CopilotAgentToolInput
                {
                    Arguments = arguments.Keys
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(64)
                        .ToDictionary(name => name, _ => (object?)null, StringComparer.OrdinalIgnoreCase),
                };
            }

            private static string NormalizeUnknownToolName(string? toolName)
            {
                var normalized = new string((toolName ?? string.Empty)
                    .Trim()
                    .Where(character => !char.IsControl(character))
                    .Take(120)
                    .ToArray());
                return normalized.Length == 0 ? "unknown_function" : normalized;
            }

            private bool TryReserveAttempt(
                ICopilotTool tool,
                string signature,
                out int round,
                out int attempt,
                out string previousObservationProgressSignature,
                out string error)
            {
                round = 0;
                attempt = 0;
                previousObservationProgressSignature = string.Empty;
                if (_reservedToolCalls >= _maxToolCalls)
                {
                    SignalToolBudgetExhausted();
                    error = $"The request reached its {_maxToolCalls}-call tool limit. Continue with the collected observations and provide the final answer.";
                    return false;
                }

                var maxAttempts = GetMaximumAttempts(tool);
                if (!_attemptsBySignature.TryGetValue(signature, out var state))
                {
                    state = new ToolAttemptState { AttemptCount = 1, InProgress = true };
                    _attemptsBySignature.Add(signature, state);
                }
                else
                {
                    var callLabel = RequiresNativeApproval(tool) ? "protected tool call" : "tool call";
                    if (state.InProgress)
                    {
                        error = $"This exact {callLabel} and argument set is already running or awaiting approval.";
                        return false;
                    }

                    if (state.AttemptCount >= maxAttempts)
                    {
                        error = $"This exact {callLabel} and argument set reached its {maxAttempts}-attempt retry limit.";
                        return false;
                    }

                    if (state.LastOutcome?.Execution.RetryEligible != true)
                    {
                        error = $"This exact {callLabel} and argument set already completed or failed with a non-retryable result. Use the existing observation or choose different arguments.";
                        return false;
                    }

                    previousObservationProgressSignature =
                        CopilotToolRetryPolicy.NormalizeObservationProgressSignature(
                            state.LastOutcome.Result.ObservationProgressSignature);
                    state.AttemptCount++;
                    state.InProgress = true;
                }

                attempt = state.AttemptCount;
                round = ++_reservedToolCalls;
                _toolBudgetCompletionGate.TrackReservedRound(round);
                error = string.Empty;
                return true;
            }

            private void SignalToolBudgetExhausted()
            {
                _toolBudgetCompletionGate.MarkExhausted();
            }

            private int GetMaximumAttempts(ICopilotTool tool)
            {
                if (tool is ICopilotRepeatableObservationTool repeatableObservation
                    && tool.Capability.Access == CopilotToolAccess.ReadOnly)
                {
                    return Math.Min(
                        Math.Clamp(
                            repeatableObservation.MaximumObservationAttempts,
                            2,
                            CopilotToolRetryPolicy.MaximumRepeatableObservationAttempts),
                        _maxToolCalls);
                }
                return tool.Capability.Idempotency == CopilotToolIdempotency.Idempotent
                    ? Math.Min(CopilotToolRetryPolicy.MaximumAttemptsPerCall, _maxToolCalls)
                    : 1;
            }

            private void RecordOutcome(string signature, CopilotToolExecutionOutcome outcome)
            {
                if (!_attemptsBySignature.TryGetValue(signature, out var state))
                {
                    state = new ToolAttemptState { AttemptCount = Math.Max(1, outcome.Invocation.Attempt) };
                    _attemptsBySignature.Add(signature, state);
                }

                state.InProgress = false;
                state.LastOutcome = outcome;
                _toolBudgetCompletionGate.CompleteRound(outcome.Invocation.Round);
            }

            private static string FormatRejectedToolCall(string toolName, string error)
            {
                return CopilotFrameworkToolResultFormatter.FormatRejected(toolName, error);
           }


        }
    }
}
