using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotStartBackgroundShellCommandTool :
        ICopilotFrameworkApprovedTool,
        ICopilotFrameworkContextualApprovalPresentation,
        ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["command"] = new
                    {
                        type = "string",
                        minLength = 1,
                        maxLength = CopilotShellCommandService.MaximumCommandCharacters,
                        description = "Complete non-interactive command text. The command must keep its root shell alive for as long as the background work is active.",
                    },
                    ["shell"] = new
                    {
                        type = "string",
                        @enum = new[] { "auto", "powershell", "cmd" },
                        description = "Shell to use. auto follows the configured Windows shell.",
                    },
                    ["workingDirectory"] = new
                    {
                        type = "string",
                        description = "Optional existing absolute directory or path relative to the active workspace.",
                    },
                    ["lifetimeSeconds"] = new
                    {
                        type = "integer",
                        minimum = CopilotBackgroundShellCommandRegistry.MinimumLifetimeSeconds,
                        maximum = CopilotBackgroundShellCommandRegistry.MaximumLifetimeSeconds,
                        description = "Maximum application-session lifetime before the whole process tree is terminated. Defaults to 3600 seconds.",
                    },
                },
                ["required"] = new[] { "command" },
                ["additionalProperties"] = false,
            }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotStartBackgroundShellCommandTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotStartBackgroundShellCommandTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "StartBackgroundShellCommand";

        public string Description => "Start one approved PowerShell or CMD command as an application-managed background process tree. The process is isolated to the current conversation, has bounded redacted output, expires automatically, and survives the Agent turn but never the ColorVision process. Starting confirms only that the process launched; use WaitForBackgroundShellCommand, InspectBackgroundShellCommands, or a specialized diagnostic before claiming readiness. Every start requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent,
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsBackgroundShellExecution(request);

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(ApprovalRequired(Name));

        async Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            var result = await _registry.StartAsync(request, toolInput, cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success || result.Snapshot == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background shell command did not start.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                };
            }

            var snapshot = result.Snapshot;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"Background command {snapshot.Id} started with PID {snapshot.ProcessId}.",
                Content = CopilotBackgroundShellCommandFormatter.FormatToolSnapshot(
                    snapshot,
                    includeOutput: false),
            };
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentToolInput toolInput)
        {
            return new CopilotToolApprovalPresentation(
                "Start background shell command",
                "The exact shell command and resolved working directory must be reviewed in the active request context.");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput)
        {
            ArgumentNullException.ThrowIfNull(request);
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!CopilotShellCommandService.TryResolveExecution(
                    request,
                    toolInput,
                    out var execution,
                    out var failure))
            {
                return new CopilotToolApprovalPresentation(
                    "Background shell command cannot be approved",
                    failure?.ErrorMessage ?? "The shell execution context is invalid.");
            }

            var lifetimeSeconds = ReadOptionalInt(
                toolInput,
                "lifetimeSeconds",
                CopilotBackgroundShellCommandRegistry.DefaultLifetimeSeconds);
            var commandDigest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(execution.CommandText)))
                .ToLowerInvariant();
            var review = new StringBuilder()
                .AppendLine($"Shell: {CopilotShellCommandService.GetShellLabel(execution.Shell)}")
                .Append("Working directory: ");
            CopilotApprovalReviewTextEncoder.Append(review, execution.WorkingDirectory);
            review.AppendLine()
                .AppendLine($"Maximum lifetime: {lifetimeSeconds} seconds")
                .AppendLine($"Command characters: {execution.CommandText.Length}")
                .AppendLine($"Command SHA-256: {commandDigest}")
                .AppendLine(@"Review encoding: backslashes are doubled; line endings, tabs, Unicode format, and invisible control characters are escaped.")
                .AppendLine()
                .AppendLine("Complete command (review-escaped):");
            CopilotApprovalReviewTextEncoder.Append(review, execution.CommandText);

            return new CopilotToolApprovalPresentation(
                $"Start background {CopilotShellCommandService.GetShellLabel(execution.Shell)} command",
                "Review the complete command, working directory, and maximum lifetime before approving.",
                ImpactSummary: "命令会在 Agent 本轮结束后继续运行；ColorVision 会捕获限长输出，并在停止、到期或应用退出时终止其进程树。",
                Reversibility: CopilotApprovalReversibility.ManualOnly,
                ReversibilitySummary: "可通过 /ps stop N 或 StopBackgroundShellCommand 终止进程树；命令已经产生的文件、网络或系统状态不会自动撤销。")
            {
                ReviewDetails = review.ToString(),
            };
        }

        private static int ReadOptionalInt(
            CopilotAgentToolInput input,
            string name,
            int defaultValue)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return defaultValue;
            if (raw is int intValue)
                return intValue;
            if (raw is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
                return (int)longValue;
            if (raw is JsonElement element
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out var elementValue))
            {
                return elementValue;
            }
            return 0;
        }

        private static CopilotToolResult ApprovalRequired(string toolName) =>
            new()
            {
                ToolName = toolName,
                Success = false,
                Summary = "Background shell command start requires Microsoft Agent Framework approval.",
                ErrorMessage = "The background process was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            };
    }

    public sealed class CopilotInspectBackgroundShellCommandsTool : ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["backgroundId"] = new
                    {
                        type = "string",
                        description = "Optional exact background command id returned by StartBackgroundShellCommand. Omit to list this conversation's commands.",
                    },
                    ["includeOutput"] = new
                    {
                        type = "boolean",
                        description = "Include bounded redacted stdout and stderr. Defaults to true.",
                    },
                },
                ["additionalProperties"] = false,
            }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotInspectBackgroundShellCommandsTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotInspectBackgroundShellCommandsTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "InspectBackgroundShellCommands";

        public string Description => "Inspect only the current conversation's application-managed background commands, including running/completed state, PID, exit code, and optional bounded redacted output. This does not reveal commands from another conversation and performs no process mutation.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return CopilotToolIntentPolicy.NeedsBackgroundShellInspection(request)
                || _registry.GetSnapshots(request?.ConversationId).Count > 0;
        }

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell-status";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            toolInput ??= CopilotAgentToolInput.Empty;
            var backgroundId = CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                toolInput,
                out var requestedId)
                ? requestedId
                : string.Empty;
            var includeOutput = ReadOptionalBoolean(toolInput, "includeOutput", defaultValue: true);
            var snapshots = _registry.GetSnapshots(request.ConversationId, backgroundId);
            if (backgroundId.Length > 0 && snapshots.Count == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background command was not found in the current conversation.",
                    ErrorMessage = "Use InspectBackgroundShellCommands without backgroundId to list valid current-conversation ids.",
                    FailureKind = CopilotToolFailureKind.NotFound,
                });
            }

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = snapshots.Count == 0
                    ? "The current conversation has no retained background commands."
                    : $"Inspected {snapshots.Count} current-conversation background command(s).",
                Content = CopilotBackgroundShellCommandFormatter.FormatToolSnapshots(
                    snapshots,
                    includeOutput),
            });
        }

        private static bool ReadOptionalBoolean(
            CopilotAgentToolInput input,
            string name,
            bool defaultValue)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return defaultValue;
            if (raw is bool boolean)
                return boolean;
            if (raw is JsonElement element
                && element.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                return element.GetBoolean();
            }
            return defaultValue;
        }
    }

    public sealed class CopilotWaitForBackgroundShellCommandTool :
        ICopilotAgentDrivenTool,
        ICopilotProgressReportingTool,
        ICopilotRepeatableObservationTool
    {
        private static readonly CopilotToolInputSchema Schema =
            CopilotToolInputSchema.FromJsonSchema(
                JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["backgroundId"] = new
                        {
                            type = "string",
                            minLength = 1,
                            description = "Exact current-conversation background command id.",
                        },
                        ["outputContains"] = new
                        {
                            type = "string",
                            minLength = 1,
                            maxLength = CopilotBackgroundShellCommandRegistry.MaximumOutputPatternCharacters,
                            description = "Optional literal text to find case-insensitively in bounded redacted stdout or stderr. Omit to wait only for a terminal state.",
                        },
                        ["timeoutSeconds"] = new
                        {
                            type = "integer",
                            minimum = CopilotBackgroundShellCommandRegistry.MinimumObservationTimeoutSeconds,
                            maximum = CopilotBackgroundShellCommandRegistry.MaximumObservationTimeoutSeconds,
                            description = "Maximum observation interval. Defaults to 10 seconds.",
                        },
                    },
                    ["required"] = new[] { "backgroundId" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotWaitForBackgroundShellCommandTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotWaitForBackgroundShellCommandTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "WaitForBackgroundShellCommand";

        public string Description => "Wait for at most 30 seconds until one exact current-conversation background command reaches a terminal state or its bounded redacted stdout/stderr contains an optional literal marker. Bounded redacted output changes are reported through the live tool-progress stream while waiting. A timeout is an observed running state, not proof of readiness. This tool performs no process mutation and never exposes another conversation.";

        public int MaximumObservationAttempts => 4;

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return CopilotToolIntentPolicy.NeedsBackgroundShellInspection(request)
                || _registry.GetSnapshots(request?.ConversationId).Count > 0;
        }

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell-status";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            ExecuteCoreAsync(
                request,
                toolInput,
                progress: null,
                cancellationToken);

        public Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);
            return ExecuteCoreAsync(
                request,
                toolInput,
                progress,
                cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext? progress,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                    toolInput,
                    out var backgroundId))
            {
                return ValidationFailure("backgroundId is required.");
            }
            if (!TryReadOptionalString(
                    toolInput,
                    "outputContains",
                    out var outputContains))
            {
                return ValidationFailure(
                    $"outputContains must be a non-empty string no longer than {CopilotBackgroundShellCommandRegistry.MaximumOutputPatternCharacters} characters.");
            }
            if (!TryReadOptionalInt(
                    toolInput,
                    "timeoutSeconds",
                    CopilotBackgroundShellCommandRegistry.DefaultObservationTimeoutSeconds,
                    out var timeoutSeconds))
            {
                return ValidationFailure(
                    $"timeoutSeconds must be an integer from {CopilotBackgroundShellCommandRegistry.MinimumObservationTimeoutSeconds} through {CopilotBackgroundShellCommandRegistry.MaximumObservationTimeoutSeconds}.");
            }

            var lastStandardOutput = string.Empty;
            var lastStandardError = string.Empty;
            progress?.Report($"正在观察后台命令 {backgroundId}");
            void ReportSnapshot(CopilotBackgroundShellCommandSnapshot snapshot)
            {
                if (progress == null)
                    return;
                if (!string.Equals(
                        lastStandardOutput,
                        snapshot.StandardOutput,
                        StringComparison.Ordinal))
                {
                    lastStandardOutput = snapshot.StandardOutput;
                    CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress,
                        "后台命令",
                        snapshot.StandardOutput,
                        isError: false);
                }
                if (!string.Equals(
                        lastStandardError,
                        snapshot.StandardError,
                        StringComparison.Ordinal))
                {
                    lastStandardError = snapshot.StandardError;
                    CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress,
                        "后台命令",
                        snapshot.StandardError,
                        isError: true);
                }
            }

            var result = await _registry.WaitForObservationAsync(
                    request.ConversationId,
                    backgroundId,
                    outputContains,
                    timeoutSeconds,
                    progress == null ? null : ReportSnapshot,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success || result.Snapshot == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background command could not be observed.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = result.Observation switch
                {
                    CopilotBackgroundShellCommandObservation.OutputMatched =>
                        $"Background command {result.Snapshot.Id} produced the requested output marker.",
                    CopilotBackgroundShellCommandObservation.Terminal =>
                        $"Background command {result.Snapshot.Id} reached {result.Snapshot.State.ToString().ToLowerInvariant()}.",
                    _ =>
                        $"Background command {result.Snapshot.Id} was still running when the bounded observation timed out.",
                },
                Content = CopilotBackgroundShellCommandFormatter.FormatWaitResult(
                    result),
                ObservationCanRepeat =
                    result.Observation
                        == CopilotBackgroundShellCommandObservation.TimedOut
                    && result.Snapshot.IsActive,
                ObservationProgressSignature =
                    CreateObservationProgressSignature(result.Snapshot),
            };
        }

        private static string CreateObservationProgressSignature(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            var content = string.Join(
                "\0",
                snapshot.State.ToString(),
                snapshot.ExitCode?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty,
                snapshot.StandardOutput,
                snapshot.StandardError);
            return Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(content)))
                .ToLowerInvariant();
        }

        private static bool TryReadOptionalString(
            CopilotAgentToolInput input,
            string name,
            out string value)
        {
            value = string.Empty;
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return true;
            if (raw is string text)
                value = text.Replace("\0", string.Empty, StringComparison.Ordinal);
            else if (raw is JsonElement element
                && element.ValueKind == JsonValueKind.String)
            {
                value = (element.GetString() ?? string.Empty)
                    .Replace("\0", string.Empty, StringComparison.Ordinal);
            }
            else
            {
                return false;
            }
            return value.Length is > 0
                and <= CopilotBackgroundShellCommandRegistry.MaximumOutputPatternCharacters;
        }

        private static bool TryReadOptionalInt(
            CopilotAgentToolInput input,
            string name,
            int defaultValue,
            out int value)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
            {
                value = defaultValue;
                return true;
            }
            if (raw is int intValue)
                value = intValue;
            else if (raw is long longValue
                && longValue is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)longValue;
            }
            else if (raw is JsonElement element
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out var elementValue))
            {
                value = elementValue;
            }
            else
            {
                value = 0;
                return false;
            }
            return value is >= CopilotBackgroundShellCommandRegistry.MinimumObservationTimeoutSeconds
                and <= CopilotBackgroundShellCommandRegistry.MaximumObservationTimeoutSeconds;
        }

        private CopilotToolResult ValidationFailure(string error) =>
            new()
            {
                ToolName = Name,
                Success = false,
                Summary = "The background command observation request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }

    public sealed class CopilotStopBackgroundShellCommandTool :
        ICopilotFrameworkApprovedTool,
        ICopilotFrameworkContextualApprovalPresentation,
        ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["backgroundId"] = new
                    {
                        type = "string",
                        minLength = 1,
                        description = "Exact current-conversation background command id.",
                    },
                },
                ["required"] = new[] { "backgroundId" },
                ["additionalProperties"] = false,
            }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotStopBackgroundShellCommandTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotStopBackgroundShellCommandTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "StopBackgroundShellCommand";

        public string Description => "Stop one exact application-managed background process tree belonging to the current conversation. It cannot target arbitrary PIDs or another conversation. Every stop requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.Idempotent,
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsBackgroundShellStop(request)
            && _registry.GetSnapshots(request?.ConversationId).Any(snapshot => snapshot.IsActive);

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Stopping a background process tree requires Microsoft Agent Framework approval.",
                ErrorMessage = "The stop was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });

        async Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            if (!CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                    toolInput,
                    out var backgroundId))
            {
                return ValidationFailure(Name, "backgroundId is required.");
            }

            var result = await _registry.StopAsync(
                    request.ConversationId,
                    backgroundId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success || result.Snapshot == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background process tree was not stopped.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = result.Snapshot.State == CopilotBackgroundShellCommandState.Stopped
                    ? $"Background command {result.Snapshot.Id} was stopped."
                    : $"Background command {result.Snapshot.Id} was already {result.Snapshot.State.ToString().ToLowerInvariant()}.",
                Content = CopilotBackgroundShellCommandFormatter.FormatToolSnapshot(
                    result.Snapshot,
                    includeOutput: true),
            };
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentToolInput toolInput)
        {
            var backgroundId = CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                toolInput,
                out var requestedId)
                ? requestedId
                : "<missing backgroundId>";
            return new CopilotToolApprovalPresentation(
                "Stop background process tree",
                $"Background command: {backgroundId}");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput)
        {
            if (!CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                    toolInput,
                    out var backgroundId))
            {
                return new CopilotToolApprovalPresentation(
                    "Background command cannot be stopped",
                    "backgroundId is required.");
            }

            var snapshot = _registry.GetSnapshots(
                    request?.ConversationId,
                    backgroundId)
                .SingleOrDefault();
            if (snapshot == null)
            {
                return new CopilotToolApprovalPresentation(
                    "Background command cannot be stopped",
                    "The id is not owned by the current conversation.");
            }

            return new CopilotToolApprovalPresentation(
                $"Stop background command {snapshot.Id}",
                $"PID {snapshot.ProcessId} · {snapshot.CommandPreview}",
                ImpactSummary: "将终止该后台命令及其仍在运行的子进程；限长输出和完成状态会保留到本次应用会话结束。",
                Reversibility: CopilotApprovalReversibility.NotReversible,
                ReversibilitySummary: "被终止的进程不会自动重新启动；命令已经产生的外部状态不会撤销。");
        }

        private static CopilotToolResult ValidationFailure(
            string toolName,
            string error) =>
            new()
            {
                ToolName = toolName,
                Success = false,
                Summary = "The background command request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }

    internal static class CopilotBackgroundShellCommandFormatter
    {
        public static string FormatWaitResult(
            CopilotBackgroundShellCommandWaitResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.Snapshot == null)
                return "[Background Shell Observation]\nobservation: unavailable";

            var observation = result.Observation switch
            {
                CopilotBackgroundShellCommandObservation.OutputMatched =>
                    "output_matched",
                CopilotBackgroundShellCommandObservation.Terminal =>
                    "terminal",
                _ => "timed_out",
            };
            return new StringBuilder()
                .AppendLine("[Background Shell Observation]")
                .Append("observation: ").AppendLine(observation)
                .Append("elapsed_ms: ")
                .AppendLine(Math.Max(
                    0,
                    (long)result.Elapsed.TotalMilliseconds).ToString(
                        CultureInfo.InvariantCulture))
                .AppendLine()
                .Append(FormatToolSnapshot(result.Snapshot, includeOutput: true))
                .ToString()
                .TrimEnd();
        }

        public static string FormatToolSnapshots(
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot> snapshots,
            bool includeOutput)
        {
            if (snapshots?.Count is not > 0)
                return "[Background Shell Commands]\ncount: 0";

            return string.Join(
                Environment.NewLine + Environment.NewLine,
                snapshots.Take(8).Select(snapshot =>
                    FormatToolSnapshot(snapshot, includeOutput)));
        }

        public static string FormatToolSnapshot(
            CopilotBackgroundShellCommandSnapshot snapshot,
            bool includeOutput)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder()
                .AppendLine("[Background Shell Command]")
                .Append("background_id: ").AppendLine(snapshot.Id)
                .Append("state: ").AppendLine(snapshot.State.ToString().ToLowerInvariant())
                .Append("pid: ").AppendLine(snapshot.ProcessId.ToString(CultureInfo.InvariantCulture))
                .Append("shell: ").AppendLine(CopilotShellCommandService.GetShellLabel(snapshot.Shell))
                .Append("working_directory: ").AppendLine(snapshot.WorkingDirectory)
                .Append("started_at_utc: ").AppendLine(snapshot.StartedAtUtc.ToString("O", CultureInfo.InvariantCulture))
                .Append("process_tree: ").AppendLine(snapshot.ProcessTreeContained ? "windows_job_object" : "best_effort")
                .Append("command_preview: ").AppendLine(snapshot.CommandPreview)
                .Append("command_sha256: ").AppendLine(snapshot.CommandSha256);
            if (snapshot.CompletedAtUtc.HasValue)
            {
                builder.Append("completed_at_utc: ")
                    .AppendLine(snapshot.CompletedAtUtc.Value.ToString("O", CultureInfo.InvariantCulture));
            }
            if (snapshot.ExitCode.HasValue)
            {
                builder.Append("exit_code: ")
                    .AppendLine(snapshot.ExitCode.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (includeOutput)
            {
                builder.AppendLine("stdout:")
                    .AppendLine(string.IsNullOrWhiteSpace(snapshot.StandardOutput)
                        ? "<empty>"
                        : snapshot.StandardOutput.TrimEnd())
                    .AppendLine("stderr:")
                    .Append(string.IsNullOrWhiteSpace(snapshot.StandardError)
                        ? "<empty>"
                        : snapshot.StandardError.TrimEnd());
            }
            return builder.ToString().TrimEnd();
        }
    }
}
