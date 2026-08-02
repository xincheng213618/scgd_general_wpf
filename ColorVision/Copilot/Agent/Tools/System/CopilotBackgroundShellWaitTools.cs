using ColorVision.Copilot.Mcp;
using System;
using System.Collections;
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
                            description = "Optional literal text to find case-insensitively in the bounded redacted preview or capped temporary redacted stdout/stderr archive. Omit to wait only for a terminal state.",
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

        public string Description => "Wait for at most 30 seconds until one exact current-conversation background command reaches a terminal state or its bounded redacted preview or capped temporary redacted stdout/stderr archive contains an optional literal marker. Output growth and terminal transitions wake the wait without periodic polling; archive matching is incremental and can find earlier output omitted from the rolling preview. Bounded preview changes and pre-truncation character growth are reported through the live tool-progress stream while waiting. A timeout is an observed running state, not proof of readiness. Use ReadBackgroundShellCommandOutput for omitted archived evidence. This tool performs no process mutation, exposes no archive path, and never exposes another conversation.";

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
            long lastObservedStandardOutputCharacters = 0;
            long lastObservedStandardErrorCharacters = 0;
            progress?.Report($"正在观察后台命令 {backgroundId}");
            void ReportSnapshot(CopilotBackgroundShellCommandSnapshot snapshot)
            {
                if (progress == null)
                    return;
                var standardOutputChanged = !string.Equals(
                        lastStandardOutput,
                        snapshot.StandardOutput,
                        StringComparison.Ordinal);
                if (standardOutputChanged)
                {
                    lastStandardOutput = snapshot.StandardOutput;
                    CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress,
                        "后台命令",
                        snapshot.StandardOutput,
                        isError: false);
                }
                else if (snapshot.ObservedStandardOutputCharacters
                    != lastObservedStandardOutputCharacters)
                {
                    progress.Report(
                        "后台命令 stdout 已观察 "
                        + snapshot.ObservedStandardOutputCharacters.ToString(
                            CultureInfo.InvariantCulture)
                        + " 个字符（限长预览未变化）");
                }
                lastObservedStandardOutputCharacters =
                    snapshot.ObservedStandardOutputCharacters;

                var standardErrorChanged = !string.Equals(
                        lastStandardError,
                        snapshot.StandardError,
                        StringComparison.Ordinal);
                if (standardErrorChanged)
                {
                    lastStandardError = snapshot.StandardError;
                    CopilotProcessExecutionSupport.ReportLatestOutput(
                        progress,
                        "后台命令",
                        snapshot.StandardError,
                        isError: true);
                }
                else if (snapshot.ObservedStandardErrorCharacters
                    != lastObservedStandardErrorCharacters)
                {
                    progress.Report(
                        "后台命令 stderr 已观察 "
                        + snapshot.ObservedStandardErrorCharacters.ToString(
                            CultureInfo.InvariantCulture)
                        + " 个字符（限长预览未变化）");
                }
                lastObservedStandardErrorCharacters =
                    snapshot.ObservedStandardErrorCharacters;
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

        internal static string CreateObservationProgressSignature(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            var content = string.Join(
                "\0",
                snapshot.State.ToString(),
                snapshot.ExitCode?.ToString(CultureInfo.InvariantCulture)
                    ?? string.Empty,
                snapshot.ObservedStandardOutputCharacters.ToString(
                    CultureInfo.InvariantCulture),
                snapshot.ObservedStandardErrorCharacters.ToString(
                    CultureInfo.InvariantCulture),
                snapshot.StandardOutputTruncated ? "1" : "0",
                snapshot.StandardErrorTruncated ? "1" : "0",
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

    public sealed class CopilotWaitForBackgroundShellCommandsTool :
        ICopilotAgentDrivenTool,
        ICopilotProgressReportingTool,
        ICopilotRepeatableObservationTool
    {
        private const int MaximumBackgroundIdCharacters = 128;

        private static readonly CopilotToolInputSchema Schema =
            CopilotToolInputSchema.FromJsonSchema(
                JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["backgroundIds"] = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string",
                                minLength = 1,
                                maxLength = MaximumBackgroundIdCharacters,
                            },
                            minItems = 1,
                            maxItems =
                                CopilotBackgroundShellCommandRegistry.MaximumGroupWaitCommands,
                            uniqueItems = true,
                            description = "One through four exact current-conversation background command ids.",
                        },
                        ["mode"] = new
                        {
                            type = "string",
                            @enum = new[] { "any", "all" },
                            description = "Return after any selected command reaches a terminal state, or after all do. Defaults to all.",
                        },
                        ["timeoutSeconds"] = new
                        {
                            type = "integer",
                            minimum = CopilotBackgroundShellCommandRegistry.MinimumObservationTimeoutSeconds,
                            maximum = CopilotBackgroundShellCommandRegistry.MaximumObservationTimeoutSeconds,
                            description = "Maximum group observation interval. Defaults to 10 seconds.",
                        },
                    },
                    ["required"] = new[] { "backgroundIds" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotWaitForBackgroundShellCommandsTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotWaitForBackgroundShellCommandsTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "WaitForBackgroundShellCommands";

        public string Description => "Wait for at most 30 seconds until any or all of 1-4 exact current-conversation background commands reach terminal states. Completion tasks wake the group without periodic polling, and all ids are validated before waiting so another conversation is never exposed. This read-only tool reports bounded terminal metadata without duplicating each command's output; use WaitForBackgroundShellCommand for one command's output marker and InspectBackgroundShellCommands or ReadBackgroundShellCommandOutput for output evidence.";

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
                || _registry.GetSnapshots(request?.ConversationId).Count > 1;
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
            if (!TryReadBackgroundIds(
                    toolInput,
                    out var backgroundIds,
                    out var backgroundIdsError))
            {
                return ValidationFailure(backgroundIdsError);
            }
            if (!TryReadMode(toolInput, out var mode))
                return ValidationFailure("mode must be any or all.");
            if (!TryReadOptionalInt(
                    toolInput,
                    "timeoutSeconds",
                    CopilotBackgroundShellCommandRegistry.DefaultObservationTimeoutSeconds,
                    out var timeoutSeconds))
            {
                return ValidationFailure(
                    $"timeoutSeconds must be an integer from {CopilotBackgroundShellCommandRegistry.MinimumObservationTimeoutSeconds} through {CopilotBackgroundShellCommandRegistry.MaximumObservationTimeoutSeconds}.");
            }

            var modeText = mode.ToString().ToLowerInvariant();
            var lastTerminalCount = 0;
            progress?.Report(
                $"正在等待 {backgroundIds.Count} 个后台命令（{modeText}）");
            void ReportSnapshots(
                IReadOnlyList<CopilotBackgroundShellCommandSnapshot> snapshots)
            {
                if (progress == null)
                    return;
                var terminalCount = snapshots.Count(snapshot => !snapshot.IsActive);
                if (terminalCount == lastTerminalCount)
                    return;
                lastTerminalCount = terminalCount;
                progress.Report(
                    $"后台命令已结束 {terminalCount}/{snapshots.Count}（{modeText}）");
            }

            var result = await _registry.WaitForTerminalGroupAsync(
                    request.ConversationId,
                    backgroundIds,
                    mode,
                    timeoutSeconds,
                    progress == null ? null : ReportSnapshots,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background command group could not be observed.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                };
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = result.Observation
                    == CopilotBackgroundShellCommandObservation.Terminal
                    ? mode == CopilotBackgroundShellCommandGroupWaitMode.Any
                        ? $"{result.TerminalCount} of {result.Snapshots.Count} background commands reached a terminal state."
                        : $"All {result.Snapshots.Count} background commands reached terminal states."
                    : $"{result.TerminalCount} of {result.Snapshots.Count} background commands had reached terminal states when the bounded group observation timed out.",
                Content = CopilotBackgroundShellCommandFormatter.FormatGroupWaitResult(
                    result),
                ObservationCanRepeat =
                    result.Observation
                        == CopilotBackgroundShellCommandObservation.TimedOut
                    && result.Snapshots.Any(snapshot => snapshot.IsActive),
                ObservationProgressSignature =
                    CreateObservationProgressSignature(
                        result.Mode,
                        result.Snapshots),
            };
        }

        internal static string CreateObservationProgressSignature(
            CopilotBackgroundShellCommandGroupWaitMode mode,
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot> snapshots)
        {
            var content = mode.ToString() + "\0" + string.Join(
                "\u001e",
                snapshots
                    .OrderBy(snapshot => snapshot.Id, StringComparer.Ordinal)
                    .Select(snapshot =>
                        snapshot.Id
                        + "\0"
                        + CopilotWaitForBackgroundShellCommandTool
                            .CreateObservationProgressSignature(snapshot)));
            return Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(content)))
                .ToLowerInvariant();
        }

        private static bool TryReadBackgroundIds(
            CopilotAgentToolInput input,
            out IReadOnlyList<string> backgroundIds,
            out string error)
        {
            backgroundIds = Array.Empty<string>();
            error =
                $"backgroundIds must contain 1 through {CopilotBackgroundShellCommandRegistry.MaximumGroupWaitCommands} unique non-empty ids.";
            if (!input.Arguments.TryGetValue("backgroundIds", out var raw)
                || raw == null)
            {
                return false;
            }

            IEnumerable? values = raw switch
            {
                JsonElement { ValueKind: JsonValueKind.Array } element =>
                    element.EnumerateArray().Select(item => (object)item).ToArray(),
                IEnumerable enumerable and not string => enumerable,
                _ => null,
            };
            if (values == null)
                return false;

            var parsed = new List<string>();
            foreach (var item in values)
            {
                var backgroundId = item switch
                {
                    string text => text.Trim(),
                    JsonElement { ValueKind: JsonValueKind.String } element =>
                        (element.GetString() ?? string.Empty).Trim(),
                    _ => string.Empty,
                };
                if (backgroundId.Length is < 1 or > MaximumBackgroundIdCharacters)
                    return false;
                parsed.Add(backgroundId);
                if (parsed.Count
                    > CopilotBackgroundShellCommandRegistry.MaximumGroupWaitCommands)
                {
                    return false;
                }
            }
            if (parsed.Count == 0
                || parsed.Distinct(StringComparer.Ordinal).Count() != parsed.Count)
            {
                return false;
            }

            backgroundIds = parsed;
            error = string.Empty;
            return true;
        }

        private static bool TryReadMode(
            CopilotAgentToolInput input,
            out CopilotBackgroundShellCommandGroupWaitMode mode)
        {
            mode = CopilotBackgroundShellCommandGroupWaitMode.All;
            if (!input.Arguments.TryGetValue("mode", out var raw) || raw == null)
                return true;
            var text = raw switch
            {
                string value => value,
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    element.GetString() ?? string.Empty,
                _ => string.Empty,
            };
            if (string.Equals(text, "any", StringComparison.OrdinalIgnoreCase))
            {
                mode = CopilotBackgroundShellCommandGroupWaitMode.Any;
                return true;
            }
            return string.Equals(text, "all", StringComparison.OrdinalIgnoreCase);
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
                Summary = "The background command group observation request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }

}
