using ColorVision.UI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public class CopilotDelegateSubagentTool : ICopilotAgentDrivenTool, ICopilotCapabilityCatalogIdentity, ICopilotCapabilityCatalogVersionIdentity, ICopilotProgressReportingTool
    {
        private readonly CopilotSubagentRoleDescriptor _role;
        private readonly ICopilotSubagentRunner _runner;

        protected CopilotDelegateSubagentTool(CopilotSubagentRoleDescriptor role, ICopilotSubagentRunner runner)
        {
            _role = role ?? throw new ArgumentNullException(nameof(role));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        }

        public string Name => _role.ToolName;

        public string Description => _role.Description;

        public string CatalogCapabilityKey => _role.Id;

        public string CatalogVersionFingerprint => _role.CapabilityFingerprint;

        internal CopilotSubagentRoleDescriptor Role => _role;

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Low,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Idempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
            ExecutionTimeout = TimeSpan.FromSeconds(100),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.RedactedExcerpt,
        };

        public CopilotToolInputSchema InputSchema { get; } = CreateInputSchema();

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null && _role.IsAvailable(request);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            return $"subagent:{_role.Id}:" + (toolInput?.GetStableArgumentsJson() ?? string.Empty);
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, progress: null, cancellationToken);
        }

        public Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            return ExecuteCoreAsync(request, toolInput, progress, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.CodexAgentsEnabled)
            {
                return Failure(
                    CopilotToolFailureKind.Authorization,
                    "Codex agents.enabled=false disables subagent tools for this submitted turn.",
                    "codex_agents_disabled");
            }
            if (!TryReadArguments(
                toolInput?.Arguments,
                out var task,
                out var agent,
                out var resumeFromRunId,
                out var model,
                out var reasoningEffort,
                out var validationError))
                return Failure(CopilotToolFailureKind.Validation, validationError);

            CopilotCodexCustomSubagentDefinition? customSubagent = null;
            if (agent.Length > 0)
            {
                customSubagent = CopilotCodexCustomSubagentSelection.Find(request.CodexCustomSubagents, agent);
                if (customSubagent == null)
                {
                    var availableNames = request.CodexCustomSubagents
                        .Select(definition => definition.Name)
                        .Take(8)
                        .ToArray();
                    return Failure(
                        CopilotToolFailureKind.Validation,
                        availableNames.Length == 0
                            ? $"Argument 'agent' names '{agent}', but this submitted request has no custom subagents."
                            : $"Argument 'agent' names unknown custom subagent '{agent}'. Available: {string.Join(", ", availableNames)}.");
                }
                agent = customSubagent.Name;
            }

            var requestedProfile = new CopilotSubagentRunRequest
            {
                Agent = agent,
                Model = model,
                ReasoningEffort = reasoningEffort,
            };
            var effectiveModel = CopilotSubagentRunner.ResolveChildModel(request, requestedProfile);
            var effectiveReasoningEffort = CopilotSubagentRunner.ResolveChildReasoningEffort(request, requestedProfile);
            var effectiveReasoningEffortToken = FormatEffectiveReasoningEffort(effectiveReasoningEffort);

            var coordinator = CopilotSubagentCoordination.GetCoordinator(request);
            CopilotAgentSessionCheckpoint? resumeCheckpoint = null;
            if (resumeFromRunId.Length > 0
                && !coordinator.TryResolveCompletedRun(
                    _role.Id,
                    resumeFromRunId,
                    agent,
                    effectiveModel,
                    effectiveReasoningEffortToken,
                    out resumeCheckpoint,
                    out var resumeFailureKind,
                    out var resumeError))
            {
                return Failure(resumeFailureKind, resumeError);
            }
            using var lease = await coordinator.TryAcquireAsync(_role.Id, cancellationToken);
            if (lease == null)
                return Failure(CopilotToolFailureKind.Conflict, "The request-scoped subagent token budget is exhausted.");

            var childRun = new CopilotSubagentRunRequest
            {
                RunId = lease.RunId,
                ResumeFromRunId = resumeFromRunId,
                ResumeCheckpoint = resumeCheckpoint,
                Task = task,
                Agent = agent,
                Model = model,
                ReasoningEffort = reasoningEffort,
                RequestTokenBudget = lease.RequestTokenBudget,
                QueueDurationMs = lease.QueueDurationMs,
            };
            if (progress != null)
            {
                childRun.ProgressUpdated = (phase, budget, activeToolName) =>
                    ReportSubagentProgress(request, progress, childRun, phase, budget, activeToolName);
                ReportSubagentProgress(request, progress, childRun, phase: null, budget: null, activeToolName: null);
            }
            CopilotSubagentResult result;
            using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                lease.CancellationToken);
            try
            {
                result = await _runner.RunAsync(request, _role, childRun, runCancellation.Token);
                lease.CompleteCancellationWindow();
                lease.Commit(Math.Max(result.Budget.ConsumedTokens, result.Usage.EffectiveTotalTokens));
                if (lease.WasCancellationRequested && !cancellationToken.IsCancellationRequested)
                    return Cancelled(request, childRun);
                if (childRun.ResumeCheckpoint == null || result.SessionResumed)
                {
                    coordinator.RecordCompleted(
                        _role.Id,
                        childRun.RunId,
                        childRun.Agent,
                        effectiveModel,
                        effectiveReasoningEffortToken,
                        result.SessionCheckpoint);
                }
            }
            catch (OperationCanceledException) when (lease.WasCancellationRequested
                && !cancellationToken.IsCancellationRequested)
            {
                lease.CompleteCancellationWindow();
                lease.Commit(lease.RequestTokenBudget);
                return Cancelled(request, childRun);
            }
            catch
            {
                lease.Commit(lease.RequestTokenBudget);
                throw;
            }

            var hasAnswer = !string.IsNullOrWhiteSpace(result.Answer);
            var resumeFailed = childRun.ResumeCheckpoint != null && !result.SessionResumed;
            var success = hasAnswer
                && !resumeFailed
                && result.StopReason == CopilotAgentStopReason.Completed
                && result.HasSuccessfulEvidence;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = success,
                Summary = success
                    ? SuccessSummary()
                    : hasAnswer
                        ? $"{_role.DisplayName} 子 Agent 在 {result.StopReason} 前返回了部分结果；该结果已保留，但不能视为已完成调查。"
                        : $"{_role.DisplayName} 子 Agent 没有返回可用结果。",
                Content = FormatResultContent(
                    result,
                    childRun,
                    effectiveModel,
                    effectiveReasoningEffortToken),
                ErrorMessage = success
                    ? string.Empty
                    : resumeFailed
                        ? string.IsNullOrWhiteSpace(result.ResumeFailureReason)
                            ? $"{_role.DisplayName} did not resume the requested Agent Framework session; no fresh fallback result was accepted."
                            : result.ResumeFailureReason
                        : result.StopReason == CopilotAgentStopReason.Completed && !result.HasSuccessfulEvidence
                            ? $"{_role.DisplayName} completed without successful request-scoped tool evidence; generated text is not accepted as a delegated result."
                            : hasAnswer
                                ? $"{_role.DisplayName} stopped with {result.StopReason}; its partial answer is evidence only and does not complete the delegated task."
                                : $"{_role.DisplayName} stopped with {result.StopReason} and produced no displayable answer.",
                FailureKind = success
                    ? CopilotToolFailureKind.None
                    : result.StopReason is CopilotAgentStopReason.Cancelled or CopilotAgentStopReason.Paused
                        ? CopilotToolFailureKind.Cancelled
                        : CopilotToolFailureKind.Internal,
                DelegatedRunUsage = new CopilotDelegatedRunUsage
                {
                    RoleId = _role.Id,
                    AgentName = childRun.Agent,
                    RunId = childRun.RunId,
                    ResumeFromRunId = childRun.ResumeFromRunId,
                    Model = effectiveModel,
                    ReasoningEffort = effectiveReasoningEffortToken,
                    RequestTokenBudget = childRun.RequestTokenBudget,
                    QueueDurationMs = childRun.QueueDurationMs,
                    StopReason = result.StopReason,
                    ToolCalls = result.Budget.ToolCalls,
                    DeliveredSteeringCount = Math.Max(0, result.DeliveredSteeringCount),
                    UndeliveredSteeringCount = Math.Max(0, result.UndeliveredSteeringCount),
                    PeakEstimatedInputTokens = result.Budget.PeakEstimatedInputTokens,
                    ProviderRetryCount = result.Budget.ProviderRetryCount,
                    ProviderRateLimitRetryCount = result.Budget.ProviderRateLimitRetryCount,
                    ProviderRetryDelayMs = result.Budget.ProviderRetryDelayMs,
                    ProviderFirstContentTimeoutCount =
                        result.Budget.ProviderFirstContentTimeoutCount,
                    ProviderStreamInactivityTimeoutCount =
                        result.Budget.ProviderStreamInactivityTimeoutCount,
                    ProviderResponseCount = result.Budget.ProviderResponseCount,
                    ProviderFirstResponseLatencyTotalMs = result.Budget.ProviderFirstResponseLatencyTotalMs,
                    ProviderFirstResponseLatencyMaxMs = result.Budget.ProviderFirstResponseLatencyMaxMs,
                    ProviderCallDurationTotalMs = result.Budget.ProviderCallDurationTotalMs,
                    ProviderStreamChunkCount = result.Budget.ProviderStreamChunkCount,
                    ProviderStreamInterChunkLatencyCount = result.Budget.ProviderStreamInterChunkLatencyCount,
                    ProviderStreamInterChunkLatencyTotalMs = result.Budget.ProviderStreamInterChunkLatencyTotalMs,
                    ProviderStreamInterChunkLatencyMaxMs = result.Budget.ProviderStreamInterChunkLatencyMaxMs,
                    ContextRecoveryCount = result.Budget.ContextRecoveryCount,
                    ContextRecoveryEstimatedInputTokensBefore = result.Budget.ContextRecoveryEstimatedInputTokensBefore,
                    ContextRecoveryEstimatedInputTokensAfter = result.Budget.ContextRecoveryEstimatedInputTokensAfter,
                    Usage = result.Usage,
                    ConsumedTokens = result.Budget.ConsumedTokens,
                    ProviderCalls = result.Budget.ProviderCalls,
                    UsedEstimatedUsage = result.Budget.UsedEstimatedUsage,
                    RegisteredToolCount = result.Budget.RegisteredToolCount,
                    AvailableToolCount = result.Budget.AvailableToolCount,
                    AvailableToolDefinitionCharacters = result.Budget.AvailableToolDefinitionCharacters,
                    HarnessInstructionCharacters = result.Budget.HarnessInstructionCharacters,
                },
                DelegatedAnswer = new CopilotDelegatedAnswer
                {
                    Text = result.Answer,
                    StopReason = result.StopReason,
                    HasSuccessfulEvidence = result.HasSuccessfulEvidence,
                    WasTruncated = result.WasTruncated,
                },
            };
        }

        private void ReportSubagentProgress(
            CopilotAgentRequest request,
            CopilotToolProgressContext progress,
            CopilotSubagentRunRequest runRequest,
            CopilotSubagentRunPhase? phase,
            CopilotAgentBudgetSnapshot? budget,
            string? activeToolName)
        {
            progress.Report(new CopilotToolProgressUpdate
            {
                Message = !string.IsNullOrWhiteSpace(activeToolName)
                    ? $"{_role.DisplayName} 子 Agent 正在执行 {activeToolName}"
                    : phase switch
                    {
                        CopilotSubagentRunPhase.Exploration => $"{_role.DisplayName} 子 Agent 正在调查",
                        CopilotSubagentRunPhase.Finalization => $"{_role.DisplayName} 子 Agent 正在整理结果",
                        _ => $"{_role.DisplayName} 子 Agent 已启动",
                    },
                DelegatedRun = new CopilotDelegatedRunProgress
                {
                    RoleId = _role.Id,
                    AgentName = runRequest.Agent,
                    RunId = runRequest.RunId,
                    ResumeFromRunId = runRequest.ResumeFromRunId,
                    Model = CopilotSubagentRunner.ResolveChildModel(request, runRequest),
                    ReasoningEffort = FormatEffectiveReasoningEffort(
                        CopilotSubagentRunner.ResolveChildReasoningEffort(request, runRequest)),
                    RequestTokenBudget = runRequest.RequestTokenBudget,
                    QueueDurationMs = runRequest.QueueDurationMs,
                    ConsumedTokens = Math.Max(0, budget?.ConsumedTokens ?? 0),
                    ProviderCalls = Math.Max(0, budget?.ProviderCalls ?? 0),
                    ToolCalls = Math.Max(0, budget?.ToolCalls ?? 0),
                },
            });
        }

        private CopilotToolResult Cancelled(
            CopilotAgentRequest request,
            CopilotSubagentRunRequest runRequest)
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"{_role.DisplayName} 子 Agent 已按用户请求停止；父 Agent 将继续运行。",
                ErrorMessage = "The delegated subagent was stopped by the user. Continue the parent task without retrying it unless the user explicitly asks.",
                FailureKind = CopilotToolFailureKind.Cancelled,
                DelegatedRunUsage = new CopilotDelegatedRunUsage
                {
                    RoleId = _role.Id,
                    AgentName = runRequest.Agent,
                    RunId = runRequest.RunId,
                    ResumeFromRunId = runRequest.ResumeFromRunId,
                    Model = CopilotSubagentRunner.ResolveChildModel(request, runRequest),
                    ReasoningEffort = FormatEffectiveReasoningEffort(
                        CopilotSubagentRunner.ResolveChildReasoningEffort(request, runRequest)),
                    RequestTokenBudget = runRequest.RequestTokenBudget,
                    QueueDurationMs = runRequest.QueueDurationMs,
                    StopReason = CopilotAgentStopReason.Cancelled,
                },
                DelegatedAnswer = new CopilotDelegatedAnswer
                {
                    StopReason = CopilotAgentStopReason.Cancelled,
                },
                SuppressModelOutput = !request.CodexInterruptMessageEnabled,
            };
        }

        private static CopilotToolInputSchema CreateInputSchema()
        {
            using var document = JsonDocument.Parse("""
                {
                  "type": "object",
                  "properties": {
                    "task": {
                      "type": "string",
                      "description": "Self-contained read-only investigation for the specialized subagent, including the evidence the parent needs back.",
                      "minLength": 1,
                      "maxLength": 4000
                    },
                    "resume_from": {
                      "type": "string",
                      "description": "Optional run_id from a completed same-role subagent in this parent request. The host resumes its serialized transcript and tool state with fresh authorization checks.",
                      "minLength": 1,
                      "maxLength": 128,
                      "pattern": "^[A-Za-z0-9-]+$"
                    },
                    "agent": {
                      "type": "string",
                      "description": "Optional custom agent name from the submitted trusted configuration snapshot. It supplies additional instructions and runtime defaults but cannot change this delegate tool's fixed read-only capabilities, sandbox, approvals, MCP servers, or skills.",
                      "minLength": 1,
                      "maxLength": 64,
                      "pattern": "^[A-Za-z][A-Za-z0-9_-]*$"
                    },
                    "model": {
                      "type": "string",
                      "description": "Optional model for this spawned subagent. It overrides agents.default_subagent_model while retaining the parent provider, endpoint, credentials, sandbox, and approval boundaries.",
                      "minLength": 1,
                      "maxLength": 256
                    },
                    "reasoning_effort": {
                      "type": "string",
                      "description": "Optional reasoning effort for this spawned subagent. It overrides agents.default_subagent_reasoning_effort when the selected provider supports reasoning metadata.",
                      "enum": ["minimal", "low", "medium", "high", "xhigh", "max", "ultra"]
                    }
                  },
                  "required": ["task"],
                  "additionalProperties": false
                }
                """);
            return CopilotToolInputSchema.FromJsonSchema(document.RootElement);
        }

        private string FormatResultContent(
            CopilotSubagentResult result,
            CopilotSubagentRunRequest runRequest,
            string effectiveModel,
            string effectiveReasoningEffort)
        {
            var builder = new StringBuilder();
            builder.Append('[').Append(_role.DisplayName).AppendLine(" subagent result]");
            builder.Append("role: ").AppendLine(_role.Id);
            builder.Append("agent: ").AppendLine(string.IsNullOrWhiteSpace(runRequest.Agent) ? "none" : runRequest.Agent);
            builder.Append("run_id: ").AppendLine(runRequest.RunId);
            builder.Append("resumed_from: ").AppendLine(string.IsNullOrWhiteSpace(runRequest.ResumeFromRunId) ? "none" : runRequest.ResumeFromRunId);
            builder.Append("resume_succeeded: ").AppendLine(string.IsNullOrWhiteSpace(runRequest.ResumeFromRunId)
                ? "not_requested"
                : result.SessionResumed ? "true" : "false");
            var resumeAvailable = (runRequest.ResumeCheckpoint == null || result.SessionResumed)
                && result.SessionCheckpoint?.IsStructurallyValid() == true;
            builder.Append("resume_available: ").AppendLine(resumeAvailable ? "true" : "false");
            if (resumeAvailable)
            {
                builder.Append("resume_hint: use resume_from=\"")
                    .Append(runRequest.RunId)
                    .AppendLine("\" with the same delegate tool and the same agent/model/reasoning_effort overrides");
            }
            builder.Append("stop_reason: ").AppendLine(result.StopReason.ToString());
            builder.Append("model: ").AppendLine(effectiveModel);
            builder.Append("reasoning_effort: ").AppendLine(effectiveReasoningEffort);
            builder.Append("request_token_budget: ").AppendLine(runRequest.RequestTokenBudget.ToString());
            builder.Append("queue_ms: ").AppendLine(Math.Max(0, runRequest.QueueDurationMs).ToString());
            builder.Append("budget_finalization: ").AppendLine(result.UsedBudgetFinalization ? "true" : "false");
            builder.Append("preselected_evidence: ").AppendLine(result.UsedPreselectedEvidence ? "true" : "false");
            builder.Append("steering_delivered: ").AppendLine(Math.Max(0, result.DeliveredSteeringCount).ToString());
            builder.Append("steering_undelivered: ").AppendLine(Math.Max(0, result.UndeliveredSteeringCount).ToString());
            if (result.UndeliveredSteeringCount > 0)
                builder.AppendLine("steering_warning: one or more user steering instructions were not delivered; do not claim they were applied");
            builder.Append("successful_tool_evidence: ").AppendLine(result.HasSuccessfulEvidence ? "true" : "false");
            builder.Append("output_truncated: ").AppendLine(result.WasTruncated ? "true" : "false");
            builder.Append("tools_used: ").AppendLine(result.ToolNames.Count == 0 ? "none" : string.Join(", ", result.ToolNames));
            builder.AppendLine("answer:");
            builder.Append(result.Answer);
            return builder.ToString();
        }

        private CopilotToolResult Failure(
            CopilotToolFailureKind failureKind,
            string errorMessage,
            string failureCode = "")
        {
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"{_role.DisplayName} 子 Agent 未启动。",
                ErrorMessage = errorMessage,
                FailureKind = failureKind,
                FailureCode = failureCode,
            };
        }

        private string SuccessSummary()
        {
            return _role.ContextScope == CopilotSubagentContextScope.PublicWeb
                ? $"只读 {_role.DisplayName} 子 Agent 已返回外部资料。"
                : $"只读 {_role.DisplayName} 子 Agent 已返回调查结果。";
        }

        private static bool TryReadArguments(
            IReadOnlyDictionary<string, object?>? arguments,
            out string task,
            out string agent,
            out string resumeFromRunId,
            out string model,
            out string reasoningEffort,
            out string errorMessage)
        {
            task = string.Empty;
            agent = string.Empty;
            resumeFromRunId = string.Empty;
            model = string.Empty;
            reasoningEffort = string.Empty;
            errorMessage = string.Empty;
            if (arguments == null)
            {
                errorMessage = "Argument 'task' must be a non-empty string.";
                return false;
            }
            var taskPair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "task", StringComparison.OrdinalIgnoreCase));
            task = taskPair.Value switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element => (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            if (task.Length is 0 or > CopilotSubagentRunner.MaximumTaskCharacters)
            {
                errorMessage = $"Argument 'task' must contain 1 to {CopilotSubagentRunner.MaximumTaskCharacters} characters.";
                return false;
            }

            var agentPair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "agent", StringComparison.OrdinalIgnoreCase));
            if (agentPair.Key != null
                && !CopilotCodexCustomSubagentSelection.TryNormalizeName(ReadString(agentPair.Value), out agent))
            {
                errorMessage = $"Argument 'agent' must be a 1 to {CopilotCodexCustomSubagentDefinition.MaximumNameCharacters} character name containing only ASCII letters, digits, '-' or '_', and must start with a letter.";
                return false;
            }

            var resumePair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "resume_from", StringComparison.OrdinalIgnoreCase));
            if (resumePair.Key != null)
            {
                resumeFromRunId = ReadString(resumePair.Value);
                if (resumeFromRunId.Length is not (> 0 and <= 128)
                    || !resumeFromRunId.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
                {
                    errorMessage = "Argument 'resume_from' must be a 1 to 128 character ASCII run id.";
                    return false;
                }
            }

            var modelPair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "model", StringComparison.OrdinalIgnoreCase));
            if (modelPair.Key != null)
            {
                if (!CopilotConfiguredModelSelection.TryNormalize(ReadString(modelPair.Value), out model))
                {
                    errorMessage = $"Argument 'model' must contain 1 to {CopilotConfiguredModelSelection.MaximumModelCharacters} non-control characters.";
                    return false;
                }
            }

            var effortPair = arguments.FirstOrDefault(candidate => string.Equals(candidate.Key, "reasoning_effort", StringComparison.OrdinalIgnoreCase));
            if (effortPair.Key != null)
            {
                if (!CopilotCodexReasoningEffortSelection.TryParse(ReadString(effortPair.Value), out var parsedEffort))
                {
                    errorMessage = "Argument 'reasoning_effort' must be one of: minimal, low, medium, high, xhigh, max, ultra.";
                    return false;
                }
                reasoningEffort = CopilotCodexReasoningEffortSelection.GetConfigToken(parsedEffort);
            }
            return true;
        }

        private static string ReadString(object? value) => value switch
        {
            string text => text.Trim(),
            JsonElement { ValueKind: JsonValueKind.String } element => (element.GetString() ?? string.Empty).Trim(),
            _ => string.Empty,
        };

        private static string FormatEffectiveReasoningEffort(CopilotCodexReasoningEffort effort)
        {
            return effort == CopilotCodexReasoningEffort.Unspecified
                ? "model_default"
                : CopilotCodexReasoningEffortSelection.GetConfigToken(effort);
        }
    }

    public sealed class CopilotDelegateExploreTool : CopilotDelegateSubagentTool
    {
        public CopilotDelegateExploreTool()
            : this(new CopilotSubagentRunner())
        {
        }

        public CopilotDelegateExploreTool(ICopilotSubagentRunner runner)
            : base(CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId), runner)
        {
        }
    }

    public sealed class CopilotDelegateScoutTool : CopilotDelegateSubagentTool
    {
        public CopilotDelegateScoutTool()
            : this(new CopilotSubagentRunner())
        {
        }

        public CopilotDelegateScoutTool(ICopilotSubagentRunner runner)
            : base(CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ScoutRoleId), runner)
        {
        }
    }

    internal sealed class CopilotRegisteredSubagentTool : CopilotDelegateSubagentTool
    {
        public CopilotRegisteredSubagentTool(CopilotSubagentRoleDescriptor role)
            : base(role, new CopilotSubagentRunner())
        {
        }
    }
}
