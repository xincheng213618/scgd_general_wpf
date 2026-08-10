using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSubmitCodeReviewFindingsTool : ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["findings"] = new
                    {
                        type = "array",
                        minItems = 0,
                        maxItems = CopilotCodeReviewFindingsResultProtocol.MaximumFindings,
                        description = "All actionable findings grounded in the latest model-visible InspectGitDiff result. Submit an empty array when there are no actionable findings.",
                        items = new
                        {
                            type = "object",
                            properties = new Dictionary<string, object?>
                            {
                                ["priority"] = new { type = "string", @enum = new[] { "P0", "P1", "P2", "P3" }, description = "Finding priority, with P0 most severe." },
                                ["title"] = new { type = "string", minLength = 1, maxLength = CopilotCodeReviewFindingsResultProtocol.MaximumTitleCharacters, description = "Short actionable title without a location prefix." },
                                ["body"] = new { type = "string", minLength = 1, maxLength = CopilotCodeReviewFindingsResultProtocol.MaximumBodyCharacters, description = "Why this is a defect, its concrete impact, and concise remediation." },
                                ["path"] = new { type = "string", minLength = 1, maxLength = 2048, description = "Repository-relative changed path using forward slashes." },
                                ["side"] = new { type = "string", @enum = new[] { "new", "old" }, description = "Use new for added/context lines and old for deleted lines." },
                                ["line_start"] = new { type = "integer", minimum = 1, description = "First line in a visible diff hunk." },
                                ["line_end"] = new { type = "integer", minimum = 1, description = "Last line in the same visible diff hunk." },
                            },
                            required = new[] { "priority", "title", "body", "path", "side", "line_start", "line_end" },
                            additionalProperties = false,
                        },
                    },
                },
                ["required"] = new[] { "findings" },
                ["additionalProperties"] = false,
            }));

        public string Name => "SubmitCodeReviewFindings";

        public string Description => "Submit the complete structured outcome of Review mode after the latest InspectGitDiff evidence (and requested validation). Every non-empty finding is rejected unless its repository-relative path and old/new line range occur in a hunk the model actually received. Call exactly once after all evidence, using an empty findings array when no actionable issue remains. This records review metadata only and never reads or changes workspace files.";

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Low,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Idempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
            ExecutionTimeout = TimeSpan.FromSeconds(5),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.Summary,
        };

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) =>
            request?.Mode == CopilotAgentMode.Review
            && request.ReviewEvidenceContext != null;

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) =>
            "review-findings:" + (request?.TaskId ?? string.Empty);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAvailable(request))
            {
                return Task.FromResult(Failure(
                    "Structured findings are available only during a prepared Review turn.",
                    "The request has no Review evidence context."));
            }
            if (!TryReadFindings(toolInput, out var findings, out var error))
                return Task.FromResult(Failure("The structured findings input is invalid.", error));
            if (!request.ReviewEvidenceContext!.TryCreateSubmission(findings, out var content, out error))
                return Task.FromResult(Failure("The findings could not be grounded in Review evidence.", error));

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = findings.Count == 0
                    ? "Submitted a structured no-findings result for the latest model-visible Git diff."
                    : $"Submitted {findings.Count} structured line-level code review finding(s) for the latest model-visible Git diff.",
                Content = content,
            });
        }

        private static bool TryReadFindings(
            CopilotAgentToolInput input,
            out IReadOnlyList<CopilotCodeReviewFinding> findings,
            out string error)
        {
            findings = Array.Empty<CopilotCodeReviewFinding>();
            error = string.Empty;
            input ??= CopilotAgentToolInput.Empty;
            if (input.Arguments.Count != 1
                || !input.Arguments.TryGetValue("findings", out var rawFindings)
                || rawFindings == null)
            {
                error = "input.findings must be the only top-level argument.";
                return false;
            }

            try
            {
                var element = rawFindings is JsonElement jsonElement
                    ? jsonElement
                    : JsonSerializer.SerializeToElement(rawFindings);
                if (element.ValueKind != JsonValueKind.Array
                    || element.GetArrayLength() > CopilotCodeReviewFindingsResultProtocol.MaximumFindings)
                {
                    error = $"input.findings must be an array of at most {CopilotCodeReviewFindingsResultProtocol.MaximumFindings} items.";
                    return false;
                }

                var parsed = new List<CopilotCodeReviewFinding>(element.GetArrayLength());
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object
                        || item.EnumerateObject().Count() != 7
                        || !TryReadString(item, "priority", out var priority)
                        || !TryReadString(item, "title", out var title)
                        || !TryReadString(item, "body", out var body)
                        || !TryReadString(item, "path", out var path)
                        || !TryReadString(item, "side", out var side)
                        || !TryReadInt32(item, "line_start", out var lineStart)
                        || !TryReadInt32(item, "line_end", out var lineEnd))
                    {
                        error = "Each finding must contain exactly priority, title, body, path, side, line_start, and line_end.";
                        return false;
                    }

                    parsed.Add(new CopilotCodeReviewFinding(
                        priority,
                        title,
                        body,
                        path,
                        side,
                        lineStart,
                        lineEnd));
                }

                findings = parsed;
                return true;
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                error = "input.findings is not valid structured JSON: " + ex.Message;
                return false;
            }
        }

        private static bool TryReadString(JsonElement element, string name, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(name, out var property)
                || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static bool TryReadInt32(JsonElement element, string name, out int value)
        {
            value = 0;
            return element.TryGetProperty(name, out var property)
                && property.ValueKind == JsonValueKind.Number
                && property.TryGetInt32(out value);
        }

        private CopilotToolResult Failure(string summary, string error) => new()
        {
            ToolName = Name,
            Success = false,
            Summary = summary,
            ErrorMessage = error,
            FailureKind = CopilotToolFailureKind.Validation,
            FailureCode = "code_review_findings_invalid",
        };
    }
}
