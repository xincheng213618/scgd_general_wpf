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
                        description = "Diff scope. Defaults to unstaged.",
                    },
                },
                ["additionalProperties"] = false,
            }));
        private readonly CopilotGitDiffInspectionService _service;

        public CopilotInspectGitDiffTool()
            : this(new CopilotGitDiffInspectionService())
        {
        }

        public CopilotInspectGitDiffTool(CopilotGitDiffInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectGitDiff";

        public string Description => "Read a bounded staged, unstaged, or combined Git patch for the current request workspace using fixed git diff arguments. The optional path must remain inside a current request root. It never accepts command text or raw Git arguments. Native approval is required because Git can evaluate repository-defined attributes and filters while comparing worktree content.";

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
            && (request.SearchRootPaths.Any() || request.WritableLocalRootPaths.Any());

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

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return _service.ExecuteAsync(request, toolInput, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var target = string.IsNullOrWhiteSpace(toolInput.Path) ? "<current workspace root>" : toolInput.Path.Trim();
            var scope = ReadString(toolInput, "scope", "unstaged");
            return new CopilotToolApprovalPresentation(
                "Inspect Git diff",
                $"Read the bounded {scope} Git diff for {target}. No command text or raw Git arguments are accepted, and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or filters while comparing worktree content.");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput)
        {
            var target = CopilotGitProcessSupport.ResolveApprovalTarget(request, toolInput.Path);
            var scope = ReadString(toolInput, "scope", "unstaged");
            return new CopilotToolApprovalPresentation(
                "Inspect Git diff",
                $"Read the bounded {scope} Git diff.\nSelected path: {target.SelectedPath}\nRepository root: {target.RepositoryRoot}\nNo command text or raw Git arguments are accepted, and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or filters while comparing worktree content.");
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
