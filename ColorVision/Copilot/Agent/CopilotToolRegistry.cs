using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolRegistry
    {
        private readonly IReadOnlyList<ICopilotTool> _tools;
        private readonly CopilotAgentExtensionBridge? _extensionBridge;

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools)
        {
            var registeredTools = tools?.ToArray() ?? Array.Empty<ICopilotTool>();
            if (registeredTools.Any(tool => tool == null))
                throw new ArgumentException("A Copilot tool registration cannot be null.", nameof(tools));

            var invalidTool = registeredTools.FirstOrDefault(tool => string.IsNullOrWhiteSpace(tool.Name));
            if (invalidTool != null)
                throw new ArgumentException("A Copilot tool registration must have a non-empty name.", nameof(tools));

            foreach (var tool in registeredTools)
            {
                var capability = tool.Capability
                    ?? throw new ArgumentException($"Copilot tool '{tool.Name}' has no capability descriptor.", nameof(tools));
                capability.Validate(tool.Name.Trim());
                if (capability.RequiresNativeApproval && tool is not ICopilotFrameworkApprovedTool)
                {
                    throw new ArgumentException(
                        $"Copilot tool '{tool.Name}' requires native approval but does not provide an approved execution path.",
                        nameof(tools));
                }
            }

            var duplicateName = registeredTools
                .GroupBy(tool => tool.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateName))
                throw new ArgumentException($"A Copilot tool named '{duplicateName}' is already registered.", nameof(tools));

            _tools = registeredTools;
        }

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools, CopilotAgentExtensionBridge extensionBridge)
            : this(tools)
        {
            _extensionBridge = extensionBridge ?? throw new ArgumentNullException(nameof(extensionBridge));
        }

        public IReadOnlyList<ICopilotTool> Tools => GetCurrentTools();

        public IReadOnlyList<ICopilotTool> FindTools(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return GetCurrentTools()
                .Where(tool => IsAllowedForCodexAgentPolicy(tool, request)
                    && IsAllowedForCodexShellToolPolicy(tool, request)
                    && IsAllowedForCodexSandboxPolicy(tool, request)
                    && IsAllowedForMode(tool, request)
                    && (IsAvailableForAgent(tool, request)
                        || tool is not ICopilotAgentDrivenTool && CopilotToolIntentPolicy.CanRetainForFollowUp(request, tool)))
                .ToArray();
        }

        internal static bool IsAllowedForCodexAgentPolicy(
            ICopilotTool tool,
            CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            return request.CodexAgentsEnabled || tool is not CopilotDelegateSubagentTool;
        }

        internal static bool IsAllowedForCodexSandboxPolicy(
            ICopilotTool tool,
            CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            return !CopilotCodexSandboxModeSelection.IsReadOnly(request.CodexSandboxMode)
                || tool.Capability.Access == CopilotToolAccess.ReadOnly;
        }

        internal static bool IsAllowedForCodexShellToolPolicy(
            ICopilotTool tool,
            CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            return request.CodexShellToolEnabled
                || tool is not (CopilotShellCommandTool or CopilotStartBackgroundShellCommandTool);
        }

        internal static bool IsAllowedForMode(ICopilotTool tool, CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            if (tool.Capability.Access == CopilotToolAccess.ReadOnly)
                return true;
            if (request.Mode == CopilotAgentMode.Review
                && tool is CopilotWorkspaceValidationTool
                && CopilotToolIntentPolicy.NeedsWorkspaceValidation(request))
            {
                return true;
            }

            return !CopilotToolIntentPolicy.IsReadOnlyMode(request.Mode)
                && !CopilotToolIntentPolicy.ExplicitlyDisallowsWriteAccess(request);
        }

        internal static bool IsAvailableForAgent(ICopilotTool tool, CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            if (request.RequiresDelegatedWorkspaceEvidence
                && CopilotToolIntentPolicy.IsDirectWorkspaceEvidenceTool(tool))
            {
                return false;
            }

            return tool is ICopilotAgentDrivenTool agentDrivenTool
                ? agentDrivenTool.IsAvailable(request)
                : tool.CanHandle(request);
        }

        public static CopilotToolRegistry CreateDefault()
        {
            return new CopilotToolRegistry(
                CreateBuiltInCatalogTools(),
                CopilotAgentExtensionBridge.Shared);
        }

        internal static ICopilotTool[] CreateDefaultTools()
        {
            return CreateBuiltInCatalogTools();
        }

        internal static ICopilotTool[] CreateBuiltInCatalogTools()
        {
            return CreateCoreDefaultTools()
                .Concat(CreateDelegateTools(CopilotSubagentRoleCatalog.Default.Roles))
                .ToArray();
        }

        internal static ICopilotTool[] CreateCoreDefaultTools()
        {
            var workspacePatchStore = new CopilotWorkspacePatchStore(
                CopilotWorkspaceChangeSetCheckpointStore.CreateDefault());
            var applicationCapabilities = CopilotApplicationCapabilityInvokerFactory.CreateDefault();
            return new ICopilotTool[]
            {
                new CopilotCreateFlowTool(applicationCapabilities),
                new CopilotConvertBatchImagesTool(),
                new CopilotOpenBatchImageProcessingTool(),
                new CopilotExecuteMenuTool(applicationCapabilities),
                new CopilotSetThemeTool(),
                new CopilotSetLanguageTool(applicationCapabilities),
                new CopilotInspectSavedTemplateTool(applicationCapabilities),
                new CopilotInspectTemplateTypeTool(applicationCapabilities),
                new CopilotTemplatePatchTool(applicationCapabilities),
                new CopilotApplyTemplatePatchTool(applicationCapabilities),
                new CopilotSearchDocsTool(),
                new CopilotFetchUrlTool(),
                new CopilotSearchFilesTool(),
                new CopilotGrepTextTool(),
                new CopilotWebSearchTool(),
                new CopilotReadLocalFileTool(),
                new CopilotListDirectoryTool(),
                new CopilotReadAttachedFileTool(),
                new CopilotGetRecentLogTool(),
                new CopilotInspectFlowGraphTool(applicationCapabilities),
                new CopilotSearchFlowNodeCatalogTool(applicationCapabilities),
                new CopilotPreviewFlowPatchTool(applicationCapabilities),
                new CopilotApplyFlowPatchTool(applicationCapabilities),
                new CopilotQueryFlowExecutionStatsTool(),
                new CopilotQueryDatabaseSqlTool(),
                new CopilotExecuteDatabaseSqlTool(),
                new CopilotInspectWindowsSystemTool(),
                new CopilotInspectWindowsProcessesTool(),
                new CopilotInspectWindowsServicesTool(),
                new CopilotInspectTcpPortTool(),
                new CopilotInspectGitWorkingTreeTool(),
                new CopilotInspectGitDiffTool(),
                new CopilotShellCommandTool(),
                new CopilotReadShellCommandOutputTool(),
                new CopilotStartBackgroundShellCommandTool(),
                new CopilotInspectBackgroundShellCommandsTool(),
                new CopilotReadBackgroundShellCommandOutputTool(),
                new CopilotMonitorBackgroundShellCommandOutputTool(),
                new CopilotStopBackgroundShellCommandOutputMonitorTool(),
                new CopilotWaitForBackgroundShellCommandTool(),
                new CopilotWaitForBackgroundShellCommandsTool(),
                new CopilotStopBackgroundShellCommandTool(),
                new CopilotPreviewWorkspacePatchEnvelopeTool(workspacePatchStore),
                new CopilotApplyWorkspacePatchEnvelopeTool(workspacePatchStore),
                new CopilotRollbackWorkspacePatchEnvelopeTool(workspacePatchStore),
                new CopilotWorkspaceValidationTool(),
            };
        }

        private ICopilotTool[] GetCurrentTools()
        {
            IEnumerable<ICopilotTool> currentTools = _tools;
            if (_extensionBridge != null)
                currentTools = currentTools.Concat(_extensionBridge.GetSnapshot().Tools);

            var tools = currentTools.ToArray();
            var duplicateName = tools
                .GroupBy(tool => tool.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateName))
                throw new InvalidOperationException($"The active Copilot tool surface contains duplicate tool name '{duplicateName}'.");
            return tools;
        }

        private static IEnumerable<ICopilotTool> CreateDelegateTools(IEnumerable<CopilotSubagentRoleDescriptor> roles)
        {
            return (roles ?? Array.Empty<CopilotSubagentRoleDescriptor>())
                .Select(role => (ICopilotTool)new CopilotRegisteredSubagentTool(role));
        }
    }
}
