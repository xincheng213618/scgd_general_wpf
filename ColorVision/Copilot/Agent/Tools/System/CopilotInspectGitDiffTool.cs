using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectGitDiffTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkContextualApprovalPresentation, ICopilotAgentDrivenTool
    {
        private static readonly CopilotToolInputSchema Schema = CopilotToolInputSchema.FromJsonSchema(
            JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object?>
                {
                    ["path"] = new
                    {
                        type = "string",
                        maxLength = 4096,
                        description = "Optional workspace-relative or absolute repository directory, existing file, or child directory inside a current request root.",
                    },
                    ["scope"] = new
                    {
                        type = "string",
                        @enum = new[] { "unstaged", "staged", "both" },
                        description = "Working-tree diff scope. Defaults to unstaged and is valid only when target is working_tree.",
                    },
                    ["target"] = new
                    {
                        type = "string",
                        @enum = new[] { "working_tree", "base_branch", "commit" },
                        description = "Review target. Defaults to working_tree.",
                    },
                    ["revision"] = new
                    {
                        type = "string",
                        maxLength = 256,
                        description = "Required base branch ref or hexadecimal commit id when target is base_branch or commit.",
                    },
                },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotGitDiffInspectionService _service;

        public CopilotInspectGitDiffTool()
            : this(new CopilotGitDiffInspectionService())
        {
        }

        internal CopilotInspectGitDiffTool(CopilotGitDiffInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectGitDiff";

        public string Description => "Read a bounded Git patch for the working tree, the merge base of a selected base branch through HEAD, or one selected commit. The optional path must remain inside a current request root. Revisions are validated and resolved before fixed git diff/show arguments are used; command text and raw Git arguments are never accepted. Native approval is required because Git can evaluate repository-defined attributes and filters.";

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Medium,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.Unknown,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
            ExecutionTimeout = TimeSpan.FromSeconds(45),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.Summary,
        };

        public CopilotToolInputSchema InputSchema => Schema;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => request != null
            && request.Mode != CopilotAgentMode.Chat
            && OperatingSystem.IsWindows()
            && (request.SearchRootPaths.Any() || request.WritableLocalRootPaths.Any())
            && CopilotToolIntentPolicy.NeedsGitDiffInspection(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = "Git diff inspection requires Microsoft Agent Framework approval.",
                ErrorMessage = "The Git process was requested without a granted native approval.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var selectedPath = string.IsNullOrWhiteSpace(toolInput.Path) ? "<current workspace root>" : BoundInline(toolInput.Path, 4096);
            var reviewTarget = ReadString(toolInput, "target", "working_tree");
            var selection = DescribeSelection(toolInput, reviewTarget);
            return new CopilotToolApprovalPresentation(
                "Inspect Git diff",
                $"Read a bounded Git patch for {selection}.\nSelected path: {selectedPath}\nNo command text or raw Git arguments are accepted, and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or filters.");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput)
        {
            var targetPath = CopilotGitProcessSupport.ResolveApprovalTarget(request, toolInput.Path);
            var reviewTarget = ReadString(toolInput, "target", "working_tree");
            var selection = DescribeSelection(toolInput, reviewTarget);
            return new CopilotToolApprovalPresentation(
                "Inspect Git diff",
                $"Read a bounded Git patch for {selection}.\nSelected path: {targetPath.SelectedPath}\nRepository root: {targetPath.RepositoryRoot}\nNo command text or raw Git arguments are accepted, and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or filters.");
        }

        private static string DescribeSelection(CopilotAgentToolInput input, string target)
        {
            if (string.Equals(target, "base_branch", StringComparison.OrdinalIgnoreCase))
                return $"the merge base of base branch '{BoundInline(ReadString(input, "revision", "<missing>"), 256)}' through HEAD";
            if (string.Equals(target, "commit", StringComparison.OrdinalIgnoreCase))
                return $"commit '{BoundInline(ReadString(input, "revision", "<missing>"), 256)}'";
            return $"the {BoundInline(ReadString(input, "scope", "unstaged"), 32)} working-tree scope";
        }

        private static string BoundInline(string? value, int maximumLength)
        {
            var normalized = new string((value ?? string.Empty)
                .Select(character => char.IsControl(character) ? ' ' : character)
                .ToArray())
                .Trim();
            return normalized.Length <= maximumLength
                ? normalized
                : normalized[..maximumLength] + "...";
        }

        private static string ReadString(CopilotAgentToolInput input, string name, string fallback)
        {
            var pair = input.Arguments.FirstOrDefault(argument => string.Equals(argument.Key, name, StringComparison.OrdinalIgnoreCase));
            if (pair.Value is string text && !string.IsNullOrWhiteSpace(text))
                return text.Trim();
            if (pair.Value is JsonElement { ValueKind: JsonValueKind.String } element)
                return element.GetString()?.Trim() ?? fallback;
            return fallback;
        }
    }
}
