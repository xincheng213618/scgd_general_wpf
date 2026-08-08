#pragma warning disable MAAI001
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
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
                return FormatToolResult(outcome);
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
                return FormatToolResult(outcome);
            }
        }
    }
}
