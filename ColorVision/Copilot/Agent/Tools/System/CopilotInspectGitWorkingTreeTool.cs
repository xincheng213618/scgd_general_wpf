using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectGitWorkingTreeTool : ICopilotFrameworkApprovedTool, ICopilotFrameworkContextualApprovalPresentation, ICopilotAgentDrivenTool
    {
        private readonly CopilotGitWorkingTreeInspectionService _service;

        public CopilotInspectGitWorkingTreeTool()
            : this(new CopilotGitWorkingTreeInspectionService())
        {
        }

        public CopilotInspectGitWorkingTreeTool(CopilotGitWorkingTreeInspectionService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string Name => "InspectGitWorkingTree";

        public string Description => "Inspect the current request's Git working tree using a fixed git status command. Returns repository root, branch, HEAD, upstream, ahead/behind counts, and bounded staged, unstaged, untracked, and conflicted paths. The optional path must remain inside a current search or writable root. It never accepts command text. Native approval is required because Git can evaluate repository-defined attributes and external filters while determining status.";

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Medium,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.Unknown,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
            ExecutionTimeout = TimeSpan.FromSeconds(15),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.Summary,
        };

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Path(
            "Optional workspace-relative or absolute repository directory, file, or child directory inside a current request root. Omit to inspect the first current workspace root.",
            required: false);

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
                Summary = "Git working-tree inspection requires Microsoft Agent Framework approval.",
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
            return new CopilotToolApprovalPresentation(
                "Inspect Git working tree",
                $"Run the fixed Git status inspection for {target}. No command text is accepted and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or external filters while determining file status.");
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput)
        {
            var target = CopilotGitProcessSupport.ResolveApprovalTarget(request, toolInput.Path);
            return new CopilotToolApprovalPresentation(
                "Inspect Git working tree",
                $"Run the fixed Git status inspection.\nSelected path: {target.SelectedPath}\nRepository root: {target.RepositoryRoot}\nNo command text is accepted and inherited Git repository selectors are cleared. Git may still evaluate repository-defined attributes or external filters while determining file status.");
        }
    }
}
