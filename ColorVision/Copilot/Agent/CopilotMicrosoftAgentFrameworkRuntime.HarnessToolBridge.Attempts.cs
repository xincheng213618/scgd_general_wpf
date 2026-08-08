#pragma warning disable MAAI001
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
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

            private string FormatToolResult(CopilotToolExecutionOutcome outcome)
            {
                return CopilotFrameworkToolResultFormatter.Format(
                    outcome,
                    _request.ToolOutputTokenLimitOverride);
            }

            private string FormatRejectedToolCall(
                string toolName,
                string error,
                string failureCode = "",
                CopilotToolFailureKind failureKind = CopilotToolFailureKind.None)
            {
                return CopilotFrameworkToolResultFormatter.FormatRejected(
                    toolName,
                    error,
                    failureCode,
                    failureKind,
                    _request.ToolOutputTokenLimitOverride);
            }
        }
    }
}
