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

        public string Description => "Inspect only the current conversation's application-managed background commands, including running/completed state, PID, exit code, optional bounded redacted preview, and temporary redacted archive availability. This does not reveal commands from another conversation and performs no process mutation.";

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
                BackgroundShellCommands = snapshots
                    .Select(CopilotBackgroundShellCommandEvidence.FromSnapshot)
                    .ToArray(),
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

    public sealed class CopilotReadBackgroundShellCommandOutputTool :
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
                            description = "Exact current-conversation background command id.",
                        },
                        ["stream"] = new
                        {
                            type = "string",
                            @enum = new[] { "stdout", "stderr" },
                            description = "Archived output stream to read. Defaults to stdout.",
                        },
                        ["offsetCharacters"] = new
                        {
                            type = "integer",
                            minimum = 0,
                            description = "Zero-based character offset in the selected redacted archive. Use next_offset_characters to continue. Defaults to 0.",
                        },
                        ["maximumCharacters"] = new
                        {
                            type = "integer",
                            minimum = 1,
                            maximum = CopilotBackgroundShellCommandRegistry.MaximumArchiveReadCharacters,
                            description = "Maximum archived characters to return. Defaults to 8192.",
                        },
                    },
                    ["required"] = new[] { "backgroundId" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotBackgroundShellCommandRegistry _registry;

        public CopilotReadBackgroundShellCommandOutputTool()
            : this(CopilotBackgroundShellCommandRegistry.Shared)
        {
        }

        internal CopilotReadBackgroundShellCommandOutputTool(
            CopilotBackgroundShellCommandRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "ReadBackgroundShellCommandOutput";

        public string Description => "Read one page from a capped temporary redacted stdout or stderr archive for an exact current-conversation background command. Use this when its bounded preview was truncated or omitted evidence is required, and continue from next_offset_characters. Reaching the current archive end while command_active is true does not prove the command is finished. The archive exposes no file path, performs no process mutation, and is deleted when the retained command is cleared or ColorVision exits.";

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
            if (!CopilotBackgroundShellCommandRegistry.TryReadBackgroundId(
                    toolInput,
                    out var backgroundId))
            {
                return Task.FromResult(
                    ValidationFailure("backgroundId is required."));
            }
            if (!TryReadStream(toolInput, out var stream))
            {
                return Task.FromResult(
                    ValidationFailure("stream must be stdout or stderr."));
            }
            if (!TryReadInt(
                    toolInput,
                    "offsetCharacters",
                    defaultValue: 0,
                    minimum: 0,
                    maximum: int.MaxValue,
                    out var offsetCharacters))
            {
                return Task.FromResult(
                    ValidationFailure(
                        "offsetCharacters must be a non-negative integer."));
            }
            if (!TryReadInt(
                    toolInput,
                    "maximumCharacters",
                    CopilotBackgroundShellCommandRegistry.DefaultArchiveReadCharacters,
                    minimum: 1,
                    CopilotBackgroundShellCommandRegistry.MaximumArchiveReadCharacters,
                    out var maximumCharacters))
            {
                return Task.FromResult(
                    ValidationFailure(
                        $"maximumCharacters must be an integer from 1 through {CopilotBackgroundShellCommandRegistry.MaximumArchiveReadCharacters}."));
            }

            var result = _registry.ReadOutputArchive(
                request.ConversationId,
                backgroundId,
                stream,
                offsetCharacters,
                maximumCharacters,
                cancellationToken);
            if (!result.Success
                || result.Snapshot == null
                || result.Page == null)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The background output archive page was not read.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                });
            }

            var snapshot = result.Snapshot;
            var page = result.Page;
            var streamLabel =
                stream == CopilotBackgroundShellOutputStream.StandardError
                    ? "stderr"
                    : "stdout";
            var content = CopilotMcpAuditLogger.RedactText(page.Content);
            var formatted = new StringBuilder()
                .AppendLine("[Background Shell Output Archive]")
                .Append("background_id: ").AppendLine(snapshot.Id)
                .Append("stream: ").AppendLine(streamLabel)
                .Append("state: ")
                .AppendLine(snapshot.State.ToString().ToLowerInvariant())
                .Append("offset_characters: ")
                .AppendLine(page.OffsetCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("returned_characters: ")
                .AppendLine(page.ReturnedCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("next_offset_characters: ")
                .AppendLine(page.NextOffsetCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("archived_characters: ")
                .AppendLine(page.ArchivedCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("end_of_available_output: ")
                .AppendLine(page.EndOfAvailableOutput ? "true" : "false")
                .Append("archive_truncated: ")
                .AppendLine(page.ArchiveTruncated ? "true" : "false")
                .Append("command_active: ")
                .AppendLine(snapshot.IsActive ? "true" : "false")
                .AppendLine("content:")
                .Append(content.Length == 0 ? "<empty>" : content)
                .ToString();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary =
                    $"Read {page.ReturnedCharacters} archived {streamLabel} character(s) from background command {snapshot.Id}; "
                    + (page.EndOfAvailableOutput
                        ? snapshot.IsActive
                            ? "reached the currently available end while the command remains active."
                            : "reached the archive end."
                        : "more archived output is available."),
                Content = formatted,
                BackgroundShellCommands =
                [
                    CopilotBackgroundShellCommandEvidence.FromSnapshot(snapshot),
                ],
            });
        }

        private static bool TryReadStream(
            CopilotAgentToolInput input,
            out CopilotBackgroundShellOutputStream stream)
        {
            stream = CopilotBackgroundShellOutputStream.StandardOutput;
            if (!input.Arguments.TryGetValue("stream", out var raw) || raw == null)
                return true;

            var value = raw switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            if (string.Equals(value, "stdout", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(value, "stderr", StringComparison.OrdinalIgnoreCase))
            {
                stream = CopilotBackgroundShellOutputStream.StandardError;
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

            return value >= minimum && value <= maximum;
        }

        private CopilotToolResult ValidationFailure(string error) =>
            new()
            {
                ToolName = Name,
                Success = false,
                Summary = "The background output archive request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }

}
