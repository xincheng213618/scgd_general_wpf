using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal sealed record CopilotSharedCapabilityDefinition(
        string Id,
        string AgentToolName,
        string McpToolName,
        CopilotToolInputSchema? SharedInputSchema = null);

    internal static class CopilotSharedCapabilityCatalog
    {
        private static CopilotToolInputSchema SearchDocsInputSchema { get; } =
            CopilotToolInputSchema.Query("Focused ColorVision documentation search terms.", required: true);
        private static CopilotToolInputSchema SearchFilesInputSchema { get; } = new(
        [
            new CopilotToolParameter { Name = "query", Description = "Literal file name or workspace-relative path fragment to locate; not a natural-language instruction or glob.", Type = CopilotToolParameterType.Text, Required = true },
            new CopilotToolParameter { Name = "path", Description = "Optional workspace-relative or absolute directory to search within.", Type = CopilotToolParameterType.Text },
            new CopilotToolParameter { Name = "cursor", Description = "Optional opaque next_cursor returned by the preceding page for the same query and path. Never invent or modify it.", Type = CopilotToolParameterType.Text },
        ]);
        private static CopilotToolInputSchema GrepTextInputSchema { get; } = new(
        [
            new CopilotToolParameter { Name = "query", Description = "Single-line literal text to find, including spaces and punctuation; not a regex or natural-language instruction.", Type = CopilotToolParameterType.Text, Required = true },
            new CopilotToolParameter { Name = "path", Description = "Optional workspace-relative or absolute file or directory to search within.", Type = CopilotToolParameterType.Text },
            new CopilotToolParameter { Name = "cursor", Description = "Optional opaque next_cursor returned by the preceding page for the same query and path. Never invent or modify it.", Type = CopilotToolParameterType.Text },
        ]);
        private static CopilotToolInputSchema SavedTemplateContextInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["template_code"] = new { type = "string", description = "Exact template code supplied by the attached saved-template reference." },
                ["template_name"] = new { type = "string", description = "Exact saved template name supplied by the attached saved-template reference." },
            },
            "template_code",
            "template_name");
        private static CopilotToolInputSchema TemplateTypeContextInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["template_code"] = new { type = "string", description = "Exact template code supplied by the attached template-type reference." },
            },
            "template_code");
        private static CopilotToolInputSchema FlowGraphInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["node_id"] = new { type = "string", description = "Optional stable node instance id or node id to focus." },
                ["include_properties"] = new { type = "boolean", description = "Include redacted node property values. Defaults to false." },
                ["max_nodes"] = new { type = "integer", minimum = 1, maximum = 200, description = "Maximum nodes to return. Defaults to 80." },
            });
        private static CopilotToolInputSchema FlowNodeCatalogInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["query"] = new { type = "string", description = "Optional title, category, runtime type, node type, or device-code search text such as 相机 or camera." },
                ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, description = "Maximum matching node types to return. Defaults to 30." },
            });

        public static CopilotSharedCapabilityDefinition SearchDocs { get; } =
            new("docs.search", "SearchDocs", "search_docs", SearchDocsInputSchema);
        public static CopilotSharedCapabilityDefinition SearchFiles { get; } =
            new("workspace.search-files", "SearchFiles", "search_files", SearchFilesInputSchema);
        public static CopilotSharedCapabilityDefinition GrepText { get; } =
            new("workspace.grep-text", "GrepText", "grep_text", GrepTextInputSchema);
        public static CopilotSharedCapabilityDefinition ReadAllowedFile { get; } =
            new("workspace.read-file", "ReadLocalFile", "read_allowed_file");
        public static CopilotSharedCapabilityDefinition ListAllowedDirectory { get; } =
            new("workspace.list-directory", "ListDirectory", "list_allowed_directory");
        public static CopilotSharedCapabilityDefinition RecentLog { get; } =
            new("diagnostics.recent-log", "GetRecentLog", "get_recent_log");
        public static CopilotSharedCapabilityDefinition SavedTemplateContext { get; } =
            new("template.saved-context", "InspectSavedTemplate", "get_saved_template_context", SavedTemplateContextInputSchema);
        public static CopilotSharedCapabilityDefinition TemplateTypeContext { get; } =
            new("template.type-context", "InspectTemplateType", "get_template_type_context", TemplateTypeContextInputSchema);
        public static CopilotSharedCapabilityDefinition FlowGraph { get; } =
            new("flow.graph", "InspectFlowGraph", "get_flow_graph", FlowGraphInputSchema);
        public static CopilotSharedCapabilityDefinition FlowNodeCatalog { get; } =
            new("flow.node-catalog", "SearchFlowNodeCatalog", "get_flow_node_catalog", FlowNodeCatalogInputSchema);
        public static CopilotSharedCapabilityDefinition PreviewFlowPatch { get; } =
            new("flow.preview-patch", "PreviewFlowPatch", "preview_flow_patch", CopilotFlowPatchSchema.Value);
        public static CopilotSharedCapabilityDefinition ApplyFlowPatch { get; } =
            new("flow.apply-patch", "ApplyFlowPatch", "apply_flow_patch", CopilotFlowPatchSchema.Value);
        public static CopilotSharedCapabilityDefinition ExecuteMenu { get; } =
            new("application.execute-menu", "ExecuteMenu", "execute_menu");
        public static CopilotSharedCapabilityDefinition CreateFlow { get; } =
            new("application.create-flow", "CreateFlow", "create_flow");
        public static CopilotSharedCapabilityDefinition PreviewTemplatePatch { get; } =
            new("template.preview-patch", "TemplatePatch", "preview_template_patch");
        public static CopilotSharedCapabilityDefinition ApplyTemplatePatch { get; } =
            new("template.apply-patch", "ApplyTemplatePatch", "apply_template_patch");
        public static CopilotSharedCapabilityDefinition SetTheme { get; } =
            new("application.set-theme", "SetTheme", "set_theme");
        public static CopilotSharedCapabilityDefinition SetLanguage { get; } =
            new("application.set-language", "SetLanguage", "set_language");

        public static IReadOnlyList<CopilotSharedCapabilityDefinition> All { get; } =
        [
            SearchDocs,
            SearchFiles,
            GrepText,
            ReadAllowedFile,
            ListAllowedDirectory,
            RecentLog,
            SavedTemplateContext,
            TemplateTypeContext,
            FlowGraph,
            FlowNodeCatalog,
            PreviewFlowPatch,
            ApplyFlowPatch,
            ExecuteMenu,
            CreateFlow,
            PreviewTemplatePatch,
            ApplyTemplatePatch,
            SetTheme,
            SetLanguage,
        ];

        public static void ValidateAgentSurface(IEnumerable<ICopilotTool> tools)
        {
            var names = (tools ?? Array.Empty<ICopilotTool>())
                .Select(tool => tool.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ValidateSurface(
                "Agent",
                All.Where(definition => !names.Contains(definition.AgentToolName))
                    .Select(definition => definition.AgentToolName));
        }

        public static void ValidateMcpSurface(IEnumerable<CopilotMcpToolDescriptor> tools)
        {
            var names = (tools ?? Array.Empty<CopilotMcpToolDescriptor>())
                .Select(tool => tool.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            ValidateSurface(
                "MCP",
                All.Where(definition => !names.Contains(definition.McpToolName))
                    .Select(definition => definition.McpToolName));
        }

        public static void ValidateBinding(string agentToolName, string mcpToolName)
        {
            if (All.Any(definition =>
                    string.Equals(definition.AgentToolName, agentToolName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(definition.McpToolName, mcpToolName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Agent tool '{agentToolName}' is not bound to MCP capability '{mcpToolName}' in the shared capability catalog.");
        }

        private static void ValidateSurface(string surface, IEnumerable<string> missingNames)
        {
            var missing = missingNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            if (missing.Length == 0)
                return;

            throw new InvalidOperationException(
                $"The {surface} tool surface is missing shared capabilities: {string.Join(", ", missing)}.");
        }

        private static CopilotToolInputSchema CreateSchema(
            IReadOnlyDictionary<string, object?> properties,
            params string[] required)
        {
            return CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(
                new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = properties,
                    ["required"] = required,
                    ["additionalProperties"] = false,
                }));
        }
    }
}
