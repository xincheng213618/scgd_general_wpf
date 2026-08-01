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
        internal sealed class HarnessToolBridge
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

            public bool TryBeginApproval(
                ToolApprovalRequestContent request,
                out FrameworkApprovalReservation reservation,
                out string error)
            {
                reservation = null!;
                if (request.ToolCall is not FunctionCallContent functionCall)
                {
                    error = "The approval request does not contain a function call.";
                    return false;
                }

                var tool = _tools.Values.FirstOrDefault(candidate => string.Equals(ToFunctionName(candidate.Name), functionCall.Name, StringComparison.OrdinalIgnoreCase));
                if (tool == null || !RequiresNativeApproval(tool))
                {
                    error = $"Function {functionCall.Name} is not registered as a natively approved ColorVision tool.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(functionCall.CallId))
                {
                    error = "The protected Agent Framework tool call is missing its provider call id.";
                    return false;
                }

                var arguments = functionCall.Arguments == null
                    ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object?>(functionCall.Arguments, StringComparer.OrdinalIgnoreCase);
                if (!tool.InputSchema.TryBind(arguments, out var toolInput, out error))
                {
                    RecordRejectedToolCall(tool, arguments, error, functionCall.CallId);
                    return false;
                }
                if (!CopilotAgentToolInputSnapshot.TryCreate(toolInput, out var approvedToolInput, out error))
                {
                    RecordRejectedToolCall(tool, arguments, error, functionCall.CallId);
                    return false;
                }

                var signature = BuildExecutionSignature(tool.Name, approvedToolInput);
                string? reservationError = null;
                lock (_syncRoot)
                {
                    if (!_providerToolCalls.TryReserveApproval(functionCall.CallId, signature, out error))
                    {
                        return false;
                    }
                    if (!TryReserveAttempt(
                            tool,
                            signature,
                            out var round,
                            out var attempt,
                            out var previousObservationProgressSignature,
                            out error))
                    {
                        reservationError = error;
                    }
                    else
                    {
                        reservation = new FrameworkApprovalReservation
                        {
                            CallId = functionCall.CallId.Trim(),
                            Round = round,
                            Attempt = attempt,
                            MaxAttempts = GetMaximumAttempts(tool),
                            Signature = signature,
                            ProviderCallId = string.IsNullOrWhiteSpace(functionCall.CallId) ? string.Empty : functionCall.CallId.Trim(),
                            Tool = tool,
                            ToolInput = approvedToolInput,
                            PreviousObservationProgressSignature =
                                previousObservationProgressSignature,
                            ExecutionScope = _executionScope.BindToolCall(
                                tool.Name,
                                functionCall.CallId,
                                signature),
                            StartedAtUtc = DateTimeOffset.UtcNow,
                        };
                    }
                }

                if (reservationError != null)
                {
                    RecordGuardRejectedToolCall(tool, approvedToolInput, signature, reservationError, functionCall.CallId);
                    error = reservationError;
                    return false;
                }

                error = string.Empty;
                return true;
            }

            public async Task<CopilotToolPermissionRequestOutcome> EvaluatePermissionRequestAsync(
                FrameworkApprovalReservation reservation,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(reservation);
                var outcome = await _toolExecutor.EvaluatePermissionRequestAsync(
                    CreateInvocation(reservation, frameworkApprovalGranted: false),
                    cancellationToken);
                reservation.PermissionHookRuns = outcome.HookRuns;
                reservation.HookBindings = outcome.HookBindings;
                return outcome;
            }

            public void PublishAwaitingApproval(FrameworkApprovalReservation reservation, Mcp.ConfirmableAction action)
            {
                reservation.ApprovalActionId = action.ActionId;
                reservation.ApprovalArgumentsDigest = action.ArgumentsDigest;
                var result = new CopilotToolResult
                {
                    ToolName = reservation.Tool.Name,
                    Success = true,
                    Summary = $"{reservation.Tool.Name} is waiting for explicit ColorVision approval.",
                    Approval = new CopilotToolApprovalInfo
                    {
                        ActionId = action.ActionId,
                        Title = action.Title,
                        RiskLevel = action.RiskLevel,
                        ExpiresAtUtc = action.ExpiresAt,
                        ExecuteOnApproval = false,
                    },
                };
                _emit(CopilotAgentEvent.FromToolResult(
                    result,
                    CreateApprovalExecutionInfo(reservation, CopilotToolExecutionState.AwaitingApproval, action.ActionId),
                    reservation.PermissionHookRuns));
            }

            public void Approve(FrameworkApprovalReservation reservation)
            {
                lock (_syncRoot)
                    _approvedCalls[CopilotFrameworkApprovalReservationKey.Create(
                        reservation.ProviderCallId,
                        reservation.Signature)] = reservation;
            }

            public void CancelOutstandingApprovals()
            {
                FrameworkApprovalReservation[] outstanding;
                lock (_syncRoot)
                {
                    outstanding = _approvedCalls.Values.ToArray();
                    _approvedCalls.Clear();
                }

                foreach (var reservation in outstanding)
                {
                    CancelApproval(
                        reservation,
                        "The approved action was not executed before the Agent run ended.");
                }
            }

            public void CancelApproval(
                FrameworkApprovalReservation reservation,
                string reason)
            {
                ArgumentNullException.ThrowIfNull(reservation);
                var cancellation = CopilotFrameworkApprovalDecision.Cancelled(reason);
                _approvalCoordinator.Cancel(
                    reservation.ApprovalActionId,
                    cancellation.Reason);
                Reject(reservation, cancellation);
            }

            public void Reject(FrameworkApprovalReservation reservation, CopilotFrameworkApprovalDecision decision)
            {
                ArgumentNullException.ThrowIfNull(decision);
                if (decision.IsApproved)
                    throw new ArgumentException("An approved decision cannot be recorded as a rejected tool call.", nameof(decision));

                var failureKind = decision.Kind == CopilotFrameworkApprovalDecisionKind.Cancelled
                    ? CopilotToolFailureKind.Cancelled
                    : CopilotToolFailureKind.Authorization;
                var result = new CopilotToolResult
                {
                    ToolName = reservation.Tool.Name,
                    Success = false,
                    Summary = decision.FormatToolSummary(reservation.Tool.Name),
                    ErrorMessage = decision.Reason,
                    FailureKind = failureKind,
                    FailureCode = decision.FailureCode,
                };
                var execution = CreateApprovalExecutionInfo(
                    reservation,
                    decision.Kind == CopilotFrameworkApprovalDecisionKind.Cancelled
                        ? CopilotToolExecutionState.Cancelled
                        : CopilotToolExecutionState.Denied,
                    reservation.ApprovalActionId,
                    DateTimeOffset.UtcNow,
                    failureKind);
                var invocation = CreateInvocation(reservation, frameworkApprovalGranted: false);
                var outcome = new CopilotToolExecutionOutcome
                {
                    Invocation = invocation,
                    Result = result,
                    Execution = execution,
                    HookRuns = reservation.PermissionHookRuns,
                };
                CopilotToolExecutionAuditLogger.Record(outcome);
                lock (_syncRoot)
                {
                    _stepRecords.Add(outcome.StepRecord);
                    RecordOutcome(reservation.Signature, outcome);
                }
                _emit(CopilotAgentEvent.FromToolResult(result, execution, outcome.HookRuns));
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

            private async Task<string> ExecuteAsync(
                ICopilotTool tool,
                CopilotAgentToolInput toolInput,
                string? providerCallId,
                CancellationToken cancellationToken)
            {
                var signature = BuildExecutionSignature(tool.Name, toolInput);
                var callResult = await _providerToolCalls.ExecuteOnceAsync(
                    providerCallId,
                    signature,
                    () => ExecuteReservedAsync(tool, toolInput, providerCallId, signature, cancellationToken),
                    cancellationToken);
                if (callResult.HasConflict)
                {
                    return CopilotFrameworkToolResultFormatter.FormatRejected(
                        tool.Name,
                        callResult.Error,
                        "duplicate_call_id_conflict",
                        CopilotToolFailureKind.Conflict);
                }

                return callResult.Content;
            }

            private async Task<string> ExecuteReservedAsync(
                ICopilotTool tool,
                CopilotAgentToolInput toolInput,
                string? providerCallId,
                string signature,
                CancellationToken cancellationToken)
            {
                int round;
                int attempt;
                int maxAttempts;
                string previousObservationProgressSignature;
                FrameworkApprovalReservation? approvalReservation;
                string? reservationError = null;
                lock (_syncRoot)
                {
                    if (_approvedCalls.Remove(
                        CopilotFrameworkApprovalReservationKey.Create(providerCallId, signature),
                        out approvalReservation))
                    {
                        round = approvalReservation.Round;
                        attempt = approvalReservation.Attempt;
                        maxAttempts = approvalReservation.MaxAttempts;
                        previousObservationProgressSignature =
                            approvalReservation.PreviousObservationProgressSignature;
                    }
                    else
                    {
                        if (!TryReserveAttempt(
                                tool,
                                signature,
                                out round,
                                out attempt,
                                out previousObservationProgressSignature,
                                out var error))
                        {
                            reservationError = error;
                            maxAttempts = 0;
                        }
                        else
                        {
                            maxAttempts = GetMaximumAttempts(tool);
                        }
                    }
                }

                if (reservationError != null)
                    return RecordGuardRejectedToolCall(tool, toolInput, signature, reservationError, providerCallId);

                var invocationCallId = string.IsNullOrWhiteSpace(providerCallId)
                    ? Guid.NewGuid().ToString("N")
                    : providerCallId.Trim();
                var invocation = approvalReservation == null
                    ? new CopilotToolInvocation
                    {
                        CallId = invocationCallId,
                        Round = round,
                        Attempt = attempt,
                        MaxAttempts = maxAttempts,
                        RuntimeName = "agent-framework",
                        Tool = tool,
                        AgentRequest = _request,
                        ExecutionScope = _executionScope.BindToolCall(
                            tool.Name,
                            invocationCallId,
                            signature),
                        ToolInput = toolInput,
                        ToolCall = CreateToolCall(tool, toolInput),
                        PreviousObservationProgressSignature =
                            previousObservationProgressSignature,
                    }
                    : CreateInvocation(approvalReservation, frameworkApprovalGranted: true);
                if (approvalReservation != null
                    && !CanBeginApprovedExecution(
                        approvalReservation,
                        out var approvalFailureCode,
                        out var approvalFailureReason))
                {
                    _approvalCoordinator.Cancel(
                        approvalReservation.ApprovalActionId,
                        approvalFailureReason);
                    var decision = CopilotFrameworkApprovalDecision.PolicyDenied(
                        approvalFailureReason,
                        approvalFailureCode);
                    Reject(approvalReservation, decision);
                    return CopilotFrameworkToolResultFormatter.FormatRejected(
                        tool.Name,
                        decision.Reason,
                        approvalFailureCode,
                        CopilotToolFailureKind.Authorization);
                }

                CopilotToolExecutionOutcome outcome;
                try
                {
                    outcome = await _toolExecutor.ExecuteAsync(invocation, _emit, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    if (approvalReservation != null)
                    {
                        _approvalCoordinator.Cancel(
                            approvalReservation.ApprovalActionId,
                            "The approved Agent Framework action was cancelled before completion.");
                    }
                    throw;
                }

                if (approvalReservation != null)
                    _approvalCoordinator.Complete(approvalReservation.ApprovalActionId, outcome.Result);

                lock (_syncRoot)
                {
                    _stepRecords.Add(outcome.StepRecord);
                    RecordOutcome(signature, outcome);
                    if (outcome.Result.DelegatedRunUsage != null)
                        _delegatedUsage = _delegatedUsage.Add(outcome.Result.DelegatedRunUsage.Usage);
                }
                if (outcome.Result.DelegatedRunUsage != null)
                    _recordDelegatedRunUsage?.Invoke(outcome.Result.DelegatedRunUsage);

                return CopilotFrameworkToolResultFormatter.Format(outcome);
            }

            private bool CanBeginApprovedExecution(
                FrameworkApprovalReservation reservation,
                out string failureCode,
                out string failureReason)
            {
                if (!CopilotAgentToolInputExactBinding.MatchesExecutionSignature(
                    reservation.Tool.Name,
                    reservation.ToolInput,
                    reservation.Signature))
                {
                    failureCode = "approval_operation_binding_changed";
                    failureReason = "The approved tool call arguments no longer match the exact operation binding.";
                    return false;
                }

                if (!CopilotCapabilityRevisionAuthorization.TryValidate(
                    reservation.ExecutionScope,
                    _capabilityRevisionProvider,
                    out failureReason))
                {
                    failureCode = "approval_capability_revision_changed";
                    return false;
                }

                var currentWorkspacePath = GetCurrentWorkspacePath();
                var canBegin = reservation.ApprovedByFullAccess
                    ? CopilotAgentAccessPolicy.CanAutoApprove(
                        _request,
                        reservation.Tool,
                        currentWorkspacePath)
                    : _approvalCoordinator.BeginIfRequired(
                        reservation.ApprovalActionId,
                        _request,
                        currentWorkspacePath,
                        reservation.ApprovalArgumentsDigest,
                        reservation.CallId,
                        reservation.ExecutionScope);
                if (canBegin)
                {
                    failureCode = string.Empty;
                    failureReason = string.Empty;
                    return true;
                }

                failureCode = "approval_no_longer_executable";
                failureReason = "The approved Agent Framework action no longer matches the active task, workspace, access policy, or approval state.";
                return false;
            }

            private CopilotToolInvocation CreateInvocation(FrameworkApprovalReservation reservation, bool frameworkApprovalGranted)
            {
                return new CopilotToolInvocation
                {
                    CallId = reservation.CallId,
                    Round = reservation.Round,
                    Attempt = reservation.Attempt,
                    MaxAttempts = reservation.MaxAttempts,
                    RuntimeName = "agent-framework",
                    Tool = reservation.Tool,
                    AgentRequest = _request,
                    ExecutionScope = reservation.ExecutionScope,
                    ToolInput = reservation.ToolInput,
                    ToolCall = CreateToolCall(reservation.Tool, reservation.ToolInput),
                    FrameworkApprovalGranted = frameworkApprovalGranted,
                    ApprovalActionId = reservation.ApprovalActionId,
                    PreviousObservationProgressSignature =
                        reservation.PreviousObservationProgressSignature,
                    InitialHookRuns = reservation.PermissionHookRuns,
                    InitialHookBindings = reservation.HookBindings,
                };
            }

            private static CopilotToolCall CreateToolCall(ICopilotTool tool, CopilotAgentToolInput toolInput)
            {
                return new CopilotToolCall
                {
                    ToolName = tool.Name,
                    ToolInput = toolInput,
                    Reason = "Selected by Microsoft Agent Framework.",
                };
            }

            private CopilotToolExecutionInfo CreateApprovalExecutionInfo(
                FrameworkApprovalReservation reservation,
                CopilotToolExecutionState state,
                string approvalActionId,
                DateTimeOffset? completedAtUtc = null,
                CopilotToolFailureKind failureKind = CopilotToolFailureKind.None)
            {
                var capability = reservation.Tool.Capability;
                return new CopilotToolExecutionInfo
                {
                    CallId = reservation.CallId,
                    Round = reservation.Round,
                    Attempt = reservation.Attempt,
                    MaxAttempts = reservation.MaxAttempts,
                    RuntimeName = "agent-framework",
                    ToolName = reservation.Tool.Name,
                    Access = capability.Access,
                    RiskLevel = capability.RiskLevel,
                    ApprovalMode = capability.ApprovalMode,
                    Idempotency = capability.Idempotency,
                    ConcurrencyMode = CopilotToolExecutor.ResolveConcurrencyMode(reservation.Tool),
                    ConcurrencyKey = CopilotToolExecutor.ResolveConcurrencyKey(reservation.Tool, _request, reservation.ToolInput),
                    ApprovalActionId = approvalActionId,
                    ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(reservation.Tool, reservation.ToolInput),
                    State = state,
                    FailureKind = failureKind,
                    RetryEligible = false,
                    StartedAtUtc = reservation.StartedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = completedAtUtc.HasValue ? Math.Max(0, (long)(completedAtUtc.Value - reservation.StartedAtUtc).TotalMilliseconds) : 0,
                    QueueDurationMs = 0,
                    TimeoutMs = Math.Max(1, (long)capability.EffectiveExecutionTimeout.TotalMilliseconds),
                };
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
