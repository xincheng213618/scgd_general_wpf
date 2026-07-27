using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotFrameworkApprovedTool
    {
        private static readonly string[] ContextLineSeparators = { "\r\n", "\n" };
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotExecuteMenuTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotExecuteMenuTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "ExecuteMenu";

        public string Description => "Execute a generic main-menu command by exact menu selector, name, or path after explicit approval. For an attached @ menu reference, copy its ExecuteMenu query value exactly into input.query. Prefer dedicated tools such as SetTheme, ConvertBatchImages, or OpenBatchImageProcessing when available; never use this generic fallback for batch image conversion or processing.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Unknown;

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Exact menu name or menu path requested by the user.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || Application.Current == null)
                return false;

            if (CopilotFlowCreationSupport.HasCreateIntent(request.UserText))
                return false;

            if (CopilotToolIntentPolicy.NeedsShellExecution(request) && !HasReferencedMenu(request))
                return false;

            if (ShouldDeferToDedicatedTool(request))
                return false;

            if (!CopilotApplicationCapability.HasMenuIntent(request.UserText))
                return false;

            return HasReferencedMenu(request)
                || CopilotApplicationCapability.HasMenuCandidates(request.UserText);
        }

        internal static bool ShouldDeferToDedicatedTool(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CopilotToolIntentPolicy.NeedsBatchImageProcessing(request)
                && !HasReferencedMenu(request);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: false, cancellationToken);
        }

        async Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return await ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: true, cancellationToken);
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            bool frameworkApprovalGranted,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var sourceText = toolInput?.Query?.Trim();
            if (string.IsNullOrWhiteSpace(sourceText)
                && TryGetReferencedMenuSelector(request, out var referencedSelector))
            {
                sourceText = referencedSelector;
            }
            if (string.IsNullOrWhiteSpace(sourceText))
                sourceText = request.UserText;

            var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["query"] = JsonSerializer.SerializeToElement(sourceText),
                ["dry_run"] = JsonSerializer.SerializeToElement(false),
            };
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                "execute_menu",
                arguments,
                request,
                frameworkApprovalGranted,
                cancellationToken);
            var isWaitingForApproval = result.IsApprovalRequired;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || isWaitingForApproval,
                Summary = isWaitingForApproval
                    ? "Menu command is waiting for explicit ColorVision approval."
                    : result.Success ? "Menu command request accepted by ColorVision." : "Menu command execution failed.",
                Content = result.Content,
                ErrorMessage = result.Success || isWaitingForApproval ? string.Empty : result.Content,
                FailureKind = result.FailureKind,
                FailureCode = result.Success || isWaitingForApproval ? string.Empty : CopilotToolFailureCode.Normalize(result.ErrorCode),
                Approval = result.Approval,
            };
        }

        internal static bool HasReferencedMenu(CopilotAgentRequest request)
        {
            return CopilotReferenceContextSupport.HasReference(
                request,
                "composer-menu:",
                "[ColorVision menu reference]");
        }

        internal static bool TryGetReferencedMenuSelector(
            CopilotAgentRequest request,
            out string selector)
        {
            ArgumentNullException.ThrowIfNull(request);
            const string Prefix = "ExecuteMenu query:";
            var selectors = CopilotReferenceContextSupport
                .EnumerateReferenceContents(
                    request,
                    "composer-menu:",
                    "[ColorVision menu reference]")
                .SelectMany(content => (content ?? string.Empty)
                    .Split(ContextLineSeparators, StringSplitOptions.RemoveEmptyEntries))
                .Where(line => line.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
                .Select(line => line.TrimStart()[Prefix.Length..].Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            selector = selectors.Length == 1 ? selectors[0] : string.Empty;
            return selector.Length > 0;
        }
    }
}
