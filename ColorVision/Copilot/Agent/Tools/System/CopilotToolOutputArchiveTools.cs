using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadToolOutputTool : ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema =
            CopilotToolInputSchema.FromJsonSchema(
                JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["archiveId"] = new
                        {
                            type = "string",
                            minLength = 1,
                            maxLength = 128,
                            description = "Exact current-conversation tool output archive id returned in content_archive.archive_id.",
                        },
                        ["offsetCharacters"] = new
                        {
                            type = "integer",
                            minimum = 0,
                            description = "Zero-based character offset in the redacted archive. Use next_offset_characters to continue. Defaults to 0.",
                        },
                        ["maximumCharacters"] = new
                        {
                            type = "integer",
                            minimum = 1,
                            maximum = CopilotOutputArchiveLimits.MaximumReadCharacters,
                            description = "Maximum archived characters to return. Defaults to 8192.",
                        },
                    },
                    ["required"] = new[] { "archiveId" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotToolOutputArchiveRegistry _registry;

        public CopilotReadToolOutputTool()
            : this(CopilotToolOutputArchiveRegistry.Shared)
        {
        }

        internal CopilotReadToolOutputTool(
            CopilotToolOutputArchiveRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => CopilotToolOutputArchivePolicy.RetrievalToolName;

        public string Description => "Read one page from a capped temporary redacted archive created when another current-conversation tool result was too large for the model context. Use the exact content_archive.archive_id and continue from next_offset_characters. The archive exposes no file path, never crosses conversations, may be truncated at its independent safety cap, and is deleted on eviction, conversation deletion, or ColorVision exit.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null;

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:tool-output-archive";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!TryReadArchiveId(toolInput, out var archiveId))
                return Task.FromResult(ValidationFailure("archiveId must be an exact tool: archive id."));
            if (!TryReadInt(
                    toolInput,
                    "offsetCharacters",
                    defaultValue: 0,
                    minimum: 0,
                    maximum: int.MaxValue,
                    out var offsetCharacters))
            {
                return Task.FromResult(ValidationFailure("offsetCharacters must be a non-negative integer."));
            }
            if (!TryReadInt(
                    toolInput,
                    "maximumCharacters",
                    CopilotOutputArchiveLimits.DefaultReadCharacters,
                    minimum: 1,
                    CopilotOutputArchiveLimits.MaximumReadCharacters,
                    out var maximumCharacters))
            {
                return Task.FromResult(ValidationFailure(
                    $"maximumCharacters must be an integer from 1 through {CopilotOutputArchiveLimits.MaximumReadCharacters}."));
            }

            var result = _registry.Read(
                request.ConversationId,
                archiveId,
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
                    Summary = "The tool output archive page was not read.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                });
            }

            var snapshot = result.Snapshot;
            var page = result.Page;
            var content = new StringBuilder()
                .AppendLine("[Tool Output Archive]")
                .Append("archive_id: ").AppendLine(snapshot.Id)
                .Append("source_tool: ").AppendLine(snapshot.ToolName)
                .Append("source_call_id: ").AppendLine(snapshot.CallId)
                .Append("offset_characters: ")
                .AppendLine(page.OffsetCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("returned_characters: ")
                .AppendLine(page.ReturnedCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("next_offset_characters: ")
                .AppendLine(page.NextOffsetCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("archived_characters: ")
                .AppendLine(page.ArchivedCharacters.ToString(CultureInfo.InvariantCulture))
                .Append("end_of_output: ")
                .AppendLine(page.EndOfAvailableOutput ? "true" : "false")
                .Append("archive_truncated: ")
                .AppendLine(page.ArchiveTruncated ? "true" : "false")
                .AppendLine("content_redacted: true")
                .AppendLine("content:")
                .Append(page.Content.Length == 0 ? "<empty>" : page.Content)
                .ToString();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary =
                    $"Read {page.ReturnedCharacters} redacted character(s) from archived {snapshot.ToolName} output; "
                    + (page.EndOfAvailableOutput
                        ? "reached the archive end."
                        : "more archived output is available."),
                Content = content,
            });
        }

        private static bool TryReadArchiveId(
            CopilotAgentToolInput input,
            out string archiveId)
        {
            archiveId = string.Empty;
            if (!input.Arguments.TryGetValue("archiveId", out var raw)
                || raw == null)
            {
                return false;
            }

            archiveId = raw switch
            {
                string text => text.Trim(),
                JsonElement { ValueKind: JsonValueKind.String } element =>
                    (element.GetString() ?? string.Empty).Trim(),
                _ => string.Empty,
            };
            return archiveId.Length is > 5 and <= 128
                && archiveId.StartsWith("tool:", StringComparison.Ordinal);
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
                Summary = "The tool output archive request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }
}
