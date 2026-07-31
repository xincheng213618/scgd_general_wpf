using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotShellCommandTool :
        ICopilotFrameworkApprovedTool,
        ICopilotFrameworkApprovedProgressReportingTool,
        ICopilotFrameworkContextualApprovalPresentation,
        ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["command"] = new { type = "string", minLength = 1, maxLength = CopilotShellCommandService.MaximumCommandCharacters, description = "Complete non-interactive command text." },
                    ["shell"] = new { type = "string", @enum = new[] { "auto", "powershell", "cmd" }, description = "Shell to use. auto follows the configured default." },
                    ["workingDirectory"] = new { type = "string", description = "Optional existing absolute directory or path relative to the active workspace. Defaults to the active workspace or application directory." },
                    ["timeoutSeconds"] = new { type = "integer", minimum = 5, maximum = 600, description = "Process timeout in seconds. Defaults to 60." },
                },
                ["required"] = new[] { "command" },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotShellCommandService _service;

        public CopilotShellCommandTool()
            : this(new CopilotShellCommandService())
        {
        }

        internal CopilotShellCommandTool(CopilotShellCommandService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "RunShellCommand";

        public string Description => "Run one bounded, non-interactive Windows PowerShell or CMD command and return its real exit code plus redacted stdout and stderr previews. When either preview is truncated, the result includes a current-conversation id for a capped temporary redacted archive readable with ReadShellCommandOutput. It can invoke installed runtimes and project scripts such as python, py, node, npm, npx, PowerShell, CMD, .cmd, and .bat. Create substantial script content as a workspace file with the patch tools, then run that file from its exact working directory instead of embedding a large program in the command. Nonzero exits and timeouts are terminal failed results with captured output. Prefer a narrower fixed diagnostic whenever one fully answers the request. Every invocation requires native approval.";

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ProtectedWrite(
            CopilotToolIdempotency.NonIdempotent,
            executionTimeout: TimeSpan.FromMinutes(10),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            CopilotToolIntentPolicy.NeedsShellExecution(request)
            && !CopilotToolIntentPolicy.NeedsBackgroundShellExecution(request);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => "system:shell";

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Shell command execution requires Microsoft Agent Framework approval.",
                ErrorMessage = "The shell process was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedProgressReportingTool.ExecuteApprovedWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteWithProgressAsync(request, toolInput, progress, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var command = ReadString(toolInput, "command", "<missing command>");
            var shellText = ReadString(toolInput, "shell", "auto");
            var shellLabel = CopilotShellCommandService.TryParseShell(shellText, out var requestedShell)
                ? requestedShell == CopilotShellKind.Auto
                    ? "Auto (configured Windows shell)"
                    : CopilotShellCommandService.GetShellLabel(requestedShell)
                : shellText;
            var workingDirectory = ReadString(toolInput, "workingDirectory", "<active workspace or application directory>");
            return new CopilotToolApprovalPresentation(
                $"Run {shellLabel} command",
                $"Shell: {shellLabel}\nWorking directory: {workingDirectory}\nCommand:\n{command}");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            return CopilotShellCommandService.CreateApprovalPresentation(request, toolInput);
        }

        private static string ReadString(CopilotAgentToolInput input, string name, string fallback)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return fallback;
            if (raw is string text && !string.IsNullOrWhiteSpace(text))
                return text;
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? fallback;
            return fallback;
        }
    }

    public sealed class CopilotReadShellCommandOutputTool :
        ICopilotAgentDrivenTool
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
                            description = "Exact current-conversation output archive id returned by RunShellCommand.",
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
                            maximum = CopilotOutputArchiveLimits.MaximumReadCharacters,
                            description = "Maximum archived characters to return. Defaults to 8192.",
                        },
                    },
                    ["required"] = new[] { "archiveId" },
                    ["additionalProperties"] = false,
                }));

        private readonly CopilotShellCommandOutputArchiveRegistry _registry;

        public CopilotReadShellCommandOutputTool()
            : this(CopilotShellCommandOutputArchiveRegistry.Shared)
        {
        }

        internal CopilotReadShellCommandOutputTool(
            CopilotShellCommandOutputArchiveRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "ReadShellCommandOutput";

        public string Description => "Read one page from a capped temporary redacted stdout or stderr archive produced when a current-conversation RunShellCommand preview was truncated. Continue from next_offset_characters. The archive exposes no file path, performs no process mutation, and is deleted when evicted, when its conversation is deleted, or when ColorVision exits.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt);

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return (CopilotToolIntentPolicy.NeedsShellExecution(request)
                    && !CopilotToolIntentPolicy.NeedsBackgroundShellExecution(request))
                || _registry.GetSnapshots(request?.ConversationId).Count > 0;
        }

        public string GetConcurrencyKey(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput) =>
            "system:shell-output-status";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            toolInput ??= CopilotAgentToolInput.Empty;
            if (!CopilotShellCommandOutputArchiveRegistry.TryReadArchiveId(
                    toolInput,
                    out var archiveId))
            {
                return Task.FromResult(
                    ValidationFailure("archiveId is required."));
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
                    CopilotOutputArchiveLimits.DefaultReadCharacters,
                    minimum: 1,
                    CopilotOutputArchiveLimits.MaximumReadCharacters,
                    out var maximumCharacters))
            {
                return Task.FromResult(
                    ValidationFailure(
                        $"maximumCharacters must be an integer from 1 through {CopilotOutputArchiveLimits.MaximumReadCharacters}."));
            }

            var result = _registry.Read(
                request.ConversationId,
                archiveId,
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
                    Summary = "The shell output archive page was not read.",
                    ErrorMessage = result.ErrorMessage,
                    FailureKind = result.FailureKind,
                });
            }

            var snapshot = result.Snapshot;
            var page = result.Page;
            var streamLabel =
                stream == CopilotShellCommandOutputStream.StandardError
                    ? "stderr"
                    : "stdout";
            var content = CopilotMcpAuditLogger.RedactText(page.Content);
            var formatted = new StringBuilder()
                .AppendLine("[Shell Command Output Archive]")
                .Append("archive_id: ").AppendLine(snapshot.Id)
                .Append("stream: ").AppendLine(streamLabel)
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
                .AppendLine("content:")
                .Append(content.Length == 0 ? "<empty>" : content)
                .ToString();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary =
                    $"Read {page.ReturnedCharacters} archived {streamLabel} character(s) from shell output archive {snapshot.Id}; "
                    + (page.EndOfAvailableOutput
                        ? "reached the archive end."
                        : "more archived output is available."),
                Content = formatted,
            });
        }

        private static bool TryReadStream(
            CopilotAgentToolInput input,
            out CopilotShellCommandOutputStream stream)
        {
            stream = CopilotShellCommandOutputStream.StandardOutput;
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
                stream = CopilotShellCommandOutputStream.StandardError;
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
                Summary = "The shell output archive request is invalid.",
                ErrorMessage = error,
                FailureKind = CopilotToolFailureKind.Validation,
            };
    }
}
