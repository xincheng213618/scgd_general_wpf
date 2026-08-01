using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotMonitorBackgroundShellCommandOutputTool :
        ICopilotAgentDrivenTool
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
                            maxLength = 128,
                            description = "Exact running current-conversation background command id.",
                        },
                        ["description"] = new
                        {
                            type = "string",
                            minLength = 1,
                            maxLength = CopilotBackgroundShellCommandRegistry.MaximumOutputMonitorDescriptionCharacters,
                            description = "Short reason for monitoring this output. It is metadata, not a command or instruction.",
                        },
                        ["stream"] = new
                        {
                            type = "string",
                            @enum = new[] { "stdout", "stderr" },
                            description = "Redacted archived output stream to monitor. Defaults to stdout.",
                        },
                        ["lifetimeSeconds"] = new
                        {
                            type = "integer",
                            minimum = CopilotBackgroundShellCommandRegistry.MinimumOutputMonitorLifetimeSeconds,
                            maximum = CopilotBackgroundShellCommandRegistry.MaximumOutputMonitorLifetimeSeconds,
                            description = "How long the live monitor remains attached unless the command exits or the monitor is stopped. Defaults to 600 seconds.",
                        },
                    },
                    ["required"] = new[] { "backgroundId", "description" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotMonitorBackgroundShellCommandOutputTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotMonitorBackgroundShellCommandOutputTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry
                ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "MonitorBackgroundShellCommandOutput";

        public string Description => "Attach a bounded live line monitor to stdout or stderr of one running current-conversation background command. Monitoring starts at the current redacted archive end, so it never replays earlier output. New complete lines are debounced, line- and batch-capped, rate-limited, and may be injected only into an active Agent run for the same conversation. If no Agent run is active, output remains available through ReadBackgroundShellCommandOutput and is not replayed automatically. The monitor stops at terminal state, timeout, explicit stop, archive failure/truncation, or sustained overload; command completion remains a separate metadata-only event.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) =>
            IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            request?.Mode != CopilotAgentMode.Plan
            && (CopilotToolIntentPolicy
                    .NeedsBackgroundShellExecution(request)
                || CopilotToolIntentPolicy
                    .NeedsBackgroundShellInspection(request)
                || _registry.GetSnapshots(request?.ConversationId)
                    .Any(snapshot => snapshot.IsActive));

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell-monitor";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                    toolInput,
                    out var backgroundId))
            {
                return Task.FromResult(
                    ValidationFailure("backgroundId is required."));
            }
            if (!TryReadRequiredString(
                    toolInput,
                    "description",
                    CopilotBackgroundShellCommandRegistry
                        .MaximumOutputMonitorDescriptionCharacters,
                    out var description))
            {
                return Task.FromResult(
                    ValidationFailure(
                        $"description must be a non-empty string no longer than {CopilotBackgroundShellCommandRegistry.MaximumOutputMonitorDescriptionCharacters} characters."));
            }
            if (!TryReadStream(toolInput, out var stream))
            {
                return Task.FromResult(
                    ValidationFailure("stream must be stdout or stderr."));
            }
            if (!TryReadInt(
                    toolInput,
                    "lifetimeSeconds",
                    CopilotBackgroundShellCommandRegistry
                        .DefaultOutputMonitorLifetimeSeconds,
                    CopilotBackgroundShellCommandRegistry
                        .MinimumOutputMonitorLifetimeSeconds,
                    CopilotBackgroundShellCommandRegistry
                        .MaximumOutputMonitorLifetimeSeconds,
                    out var lifetimeSeconds))
            {
                return Task.FromResult(
                    ValidationFailure(
                        $"lifetimeSeconds must be an integer from {CopilotBackgroundShellCommandRegistry.MinimumOutputMonitorLifetimeSeconds} through {CopilotBackgroundShellCommandRegistry.MaximumOutputMonitorLifetimeSeconds}."));
            }

            var result = _registry.StartOutputMonitor(
                request.ConversationId,
                backgroundId,
                stream,
                description,
                lifetimeSeconds);
            if (!result.Success || result.Snapshot == null)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background output monitor was not started.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                });
            }

            var snapshot = result.Snapshot;
            var streamLabel =
                snapshot.Stream
                    == CopilotBackgroundShellOutputStream.StandardError
                    ? "stderr"
                    : "stdout";
            var content = new StringBuilder()
                .AppendLine("[Background Shell Output Monitor]")
                .Append("monitor_id: ").AppendLine(snapshot.Id)
                .Append("background_id: ").AppendLine(snapshot.BackgroundId)
                .Append("stream: ").AppendLine(streamLabel)
                .Append("state: ")
                .AppendLine(snapshot.State.ToString().ToLowerInvariant())
                .Append("description: ").AppendLine(snapshot.Description)
                .AppendLine("starts_at_current_archive_end: true")
                .Append("expires_at_utc: ")
                .AppendLine(snapshot.ExpiresAtUtc.ToString("O"))
                .Append("already_running: ")
                .AppendLine(result.AlreadyRunning ? "true" : "false")
                .ToString();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = result.AlreadyRunning
                    ? $"Background command {snapshot.BackgroundId} already has an active {streamLabel} monitor {snapshot.Id}."
                    : $"Started {streamLabel} monitor {snapshot.Id} for background command {snapshot.BackgroundId}.",
                Content = content,
            });
        }

        private static bool TryReadRequiredString(
            CopilotAgentToolInput input,
            string name,
            int maximumCharacters,
            out string value)
        {
            value = string.Empty;
            if (!input.Arguments.TryGetValue(name, out var raw)
                || raw == null)
            {
                return false;
            }

            value = raw switch
            {
                string text => text,
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    element.GetString() ?? string.Empty,
                _ => string.Empty,
            };
            value = value.Replace(
                "\0",
                string.Empty,
                StringComparison.Ordinal);
            return !string.IsNullOrWhiteSpace(value)
                && value.Length <= maximumCharacters;
        }

        private static bool TryReadStream(
            CopilotAgentToolInput input,
            out CopilotBackgroundShellOutputStream stream)
        {
            stream = CopilotBackgroundShellOutputStream.StandardOutput;
            if (!input.Arguments.TryGetValue("stream", out var raw)
                || raw == null)
            {
                return true;
            }

            var value = raw switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            if (string.Equals(
                    value,
                    "stdout",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(
                    value,
                    "stderr",
                    StringComparison.OrdinalIgnoreCase))
            {
                stream =
                    CopilotBackgroundShellOutputStream.StandardError;
                return true;
            }
            return false;
        }

        private static bool TryReadInt(
            CopilotAgentToolInput input,
            string name,
            int defaultValue,
            int minimum,
            int maximum,
            out int value)
        {
            if (!input.Arguments.TryGetValue(name, out var raw)
                || raw == null)
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

            return value >= minimum && value <= maximum;
        }

        private CopilotToolResult ValidationFailure(string error) =>
            new()
            {
                ToolName = Name,
                Success = false,
                Summary = "The background output monitor request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }

    public sealed class CopilotStopBackgroundShellCommandOutputMonitorTool :
        ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema =
            CopilotToolInputSchema.FromJsonSchema(
                JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["monitorId"] = new
                        {
                            type = "string",
                            minLength = 1,
                            maxLength = 128,
                            description = "Exact current-conversation output monitor id.",
                        },
                    },
                    ["required"] = new[] { "monitorId" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotStopBackgroundShellCommandOutputMonitorTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotStopBackgroundShellCommandOutputMonitorTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry
                ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name =>
            "StopBackgroundShellCommandOutputMonitor";

        public string Description => "Stop one exact current-conversation background output monitor without stopping or mutating its process. Already terminal monitors return their retained state. No native approval is required because only the in-memory observation is changed.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) =>
            IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            request?.Mode != CopilotAgentMode.Plan
            && (CopilotToolIntentPolicy
                    .NeedsBackgroundShellExecution(request)
                || CopilotToolIntentPolicy
                    .NeedsBackgroundShellInspection(request)
                || _registry.GetOutputMonitorSnapshots(
                        request?.ConversationId)
                    .Any());

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:background-shell-monitor";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!TryReadMonitorId(toolInput, out var monitorId))
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background output monitor stop request is invalid.",
                    ErrorMessage = "monitorId is required.",
                    FailureKind = CopilotToolFailureKind.Validation,
                });
            }

            var result = _registry.StopOutputMonitor(
                request.ConversationId,
                monitorId);
            if (!result.Success || result.Snapshot == null)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background output monitor was not stopped.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                });
            }

            var snapshot = result.Snapshot;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = snapshot.State
                    == CopilotBackgroundShellOutputMonitorState.Stopped
                    ? $"Background output monitor {snapshot.Id} was stopped without stopping command {snapshot.BackgroundId}."
                    : $"Background output monitor {snapshot.Id} was already {snapshot.State.ToString().ToLowerInvariant()}.",
                Content = new StringBuilder()
                    .AppendLine("[Background Shell Output Monitor]")
                    .Append("monitor_id: ").AppendLine(snapshot.Id)
                    .Append("background_id: ")
                    .AppendLine(snapshot.BackgroundId)
                    .Append("state: ")
                    .AppendLine(snapshot.State.ToString().ToLowerInvariant())
                    .Append("published_events: ")
                    .AppendLine(snapshot.PublishedEvents.ToString(
                        CultureInfo.InvariantCulture))
                    .Append("suppressed_events: ")
                    .AppendLine(snapshot.SuppressedEvents.ToString(
                        CultureInfo.InvariantCulture))
                    .AppendLine("command_stopped: false")
                    .ToString(),
            });
        }

        private static bool TryReadMonitorId(
            CopilotAgentToolInput input,
            out string monitorId)
        {
            monitorId = string.Empty;
            if (!input.Arguments.TryGetValue("monitorId", out var raw)
                || raw == null)
            {
                return false;
            }
            monitorId = raw switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            return monitorId.Length > 0;
        }
    }
}
