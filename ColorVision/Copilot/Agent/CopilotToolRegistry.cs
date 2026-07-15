using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolRegistry
    {
        private readonly IReadOnlyList<ICopilotTool> _tools;
        private readonly CopilotSubagentRoleRegistry? _subagentRoleRegistry;

        public CopilotToolRegistry(IEnumerable<ICopilotTool> tools)
            : this(tools, null)
        {
        }

        private CopilotToolRegistry(IEnumerable<ICopilotTool> tools, CopilotSubagentRoleRegistry? subagentRoleRegistry)
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
            }

            var duplicateName = registeredTools
                .GroupBy(tool => tool.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateName))
                throw new ArgumentException($"A Copilot tool named '{duplicateName}' is already registered.", nameof(tools));

            _tools = registeredTools;
            _subagentRoleRegistry = subagentRoleRegistry;
        }

        public IReadOnlyList<ICopilotTool> Tools => GetCurrentTools();

        public IReadOnlyList<ICopilotTool> FindTools(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return GetCurrentTools()
                .Where(tool => IsAvailableForAgent(tool, request)
                    || tool is not ICopilotAgentDrivenTool && CopilotToolIntentPolicy.CanRetainForFollowUp(request, tool))
                .ToArray();
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
            return new CopilotToolRegistry(CreateCoreDefaultTools(), subagentRoleRegistry ?? CopilotSubagentRoleRegistry.Shared);
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

        private static ICopilotTool[] CreateCoreDefaultTools()
        {
            var workspacePatchStore = new CopilotWorkspacePatchStore();
            return new ICopilotTool[]
            {
                new CopilotCreateFlowTool(),
                new CopilotExecuteMenuTool(),
                new CopilotSetThemeTool(),
                new CopilotSetLanguageTool(),
                new CopilotTemplatePatchTool(),
                new CopilotApplyTemplatePatchTool(),
                new CopilotSearchDocsTool(),
                new CopilotFetchUrlTool(),
                new CopilotSearchFilesTool(),
                new CopilotGrepTextTool(),
                new CopilotWebSearchTool(),
                new CopilotReadLocalFileTool(),
                new CopilotListDirectoryTool(),
                new CopilotReadAttachedFileTool(),
                new CopilotGetRecentLogTool(),
                new CopilotInspectFlowGraphTool(),
                new CopilotSearchFlowNodeCatalogTool(),
                new CopilotPreviewFlowPatchTool(),
                new CopilotApplyFlowPatchTool(),
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
                new CopilotPreviewCreateWorkspaceFileTool(workspacePatchStore),
                new CopilotApplyCreateWorkspaceFileTool(workspacePatchStore),
                new CopilotPreviewWorkspacePatchTool(workspacePatchStore),
                new CopilotApplyWorkspacePatchTool(workspacePatchStore),
                new CopilotPreviewWorkspaceChangeSetTool(workspacePatchStore),
                new CopilotApplyWorkspaceChangeSetTool(workspacePatchStore),
                new CopilotRollbackWorkspacePatchTool(workspacePatchStore),
                new CopilotRollbackWorkspaceChangeSetTool(workspacePatchStore),
                new CopilotRollbackWorkspacePatchEnvelopeTool(workspacePatchStore),
                new CopilotWorkspaceValidationTool(),
            };
        }

        private IReadOnlyList<ICopilotTool> GetCurrentTools()
        {
            if (_subagentRoleRegistry == null)
                return _tools;
            return _tools.Concat(CreateDelegateTools(_subagentRoleRegistry.GetSnapshot().Roles)).ToArray();
        }

        private static IEnumerable<ICopilotTool> CreateDelegateTools(IEnumerable<CopilotSubagentRoleDescriptor> roles)
        {
            return (roles ?? Array.Empty<CopilotSubagentRoleDescriptor>())
                .Select(role => (ICopilotTool)new CopilotRegisteredSubagentTool(role));
        }
    }
}
