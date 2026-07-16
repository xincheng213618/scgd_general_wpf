using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolRegistry
    {
        private readonly IReadOnlyList<ICopilotTool> _tools;
        private readonly CopilotSubagentRoleRegistry? _subagentRoleRegistry;
        private readonly CopilotAgentExtensionBridge? _extensionBridge;

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools)
            : this(tools, null, null)
        {
        }

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools, CopilotAgentExtensionBridge extensionBridge)
            : this(tools, null, extensionBridge ?? throw new ArgumentNullException(nameof(extensionBridge)))
        {
        }

        private CopilotToolRegistry(
            IEnumerable<ICopilotTool> tools,
            CopilotSubagentRoleRegistry? subagentRoleRegistry,
            CopilotAgentExtensionBridge? extensionBridge)
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
            _subagentRoleRegistry = subagentRoleRegistry;
            _extensionBridge = extensionBridge;
        }

        public IReadOnlyList<ICopilotTool> Tools => GetCurrentTools();

        public IReadOnlyList<ICopilotTool> FindTools(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return GetCurrentTools()
                .Where(tool => IsAllowedForMode(tool, request)
                    && (IsAvailableForAgent(tool, request)
                        || tool is not ICopilotAgentDrivenTool && CopilotToolIntentPolicy.CanRetainForFollowUp(request, tool)))
                .ToArray();
        }

        internal static bool IsAllowedForMode(ICopilotTool tool, CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            return request.Mode != CopilotAgentMode.Review || tool.Capability.Access == CopilotToolAccess.ReadOnly;
        }

        internal static bool IsAvailableForAgent(ICopilotTool tool, CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(tool);
            ArgumentNullException.ThrowIfNull(request);
            return tool is ICopilotAgentDrivenTool agentDrivenTool
                ? agentDrivenTool.IsAvailable(request)
                : tool.CanHandle(request);
        }

        public static CopilotToolRegistry CreateDefault(CopilotSubagentRoleRegistry? subagentRoleRegistry = null)
        {
            return new CopilotToolRegistry(
                CreateCoreDefaultTools(),
                subagentRoleRegistry ?? CopilotSubagentRoleRegistry.Shared,
                CopilotAgentExtensionBridge.Shared);
        }

        internal static ICopilotTool[] CreateDefaultTools()
        {
            return CreateCoreDefaultTools()
                .Concat(CreateDelegateTools(CopilotSubagentRoleRegistry.Shared.GetSnapshot().Roles))
                .ToArray();
        }

        internal static ICopilotTool[] CreateBuiltInCatalogTools()
        {
            return CreateCoreDefaultTools()
                .Concat(CreateDelegateTools(CopilotSubagentRoleCatalog.CreateBuiltInRoles()))
                .ToArray();
        }

        internal static ICopilotTool[] CreateCoreDefaultTools()
        {
            var workspacePatchStore = new CopilotWorkspacePatchStore();
            var applicationCapabilities = CopilotApplicationCapabilityInvokerFactory.CreateDefault();
            return new ICopilotTool[]
            {
                new CopilotCreateFlowTool(applicationCapabilities),
                new CopilotExecuteMenuTool(applicationCapabilities),
                new CopilotSetThemeTool(),
                new CopilotSetLanguageTool(applicationCapabilities),
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
            if (_subagentRoleRegistry != null)
                currentTools = currentTools.Concat(CreateDelegateTools(_subagentRoleRegistry.GetSnapshot().Roles));

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
