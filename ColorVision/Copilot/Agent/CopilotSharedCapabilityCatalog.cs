using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotSharedAgentToolNames
    {
        public const string SearchDocs = "SearchDocs";
        public const string SearchFiles = "SearchFiles";
        public const string GrepText = "GrepText";
        public const string ReadLocalFile = "ReadLocalFile";
        public const string ListDirectory = "ListDirectory";
        public const string GetRecentLog = "GetRecentLog";
        public const string InspectSavedTemplate = "InspectSavedTemplate";
        public const string InspectTemplateType = "InspectTemplateType";
        public const string InspectFlowGraph = "InspectFlowGraph";
        public const string InspectFlowIncident = "InspectFlowIncident";
        public const string ListFlowIncidents = "ListFlowIncidents";
        public const string InspectRecordedFlowTemplate = "InspectRecordedFlowTemplate";
        public const string InspectFlowRunHistory = "InspectFlowRunHistory";
        public const string SearchFlowNodeCatalog = "SearchFlowNodeCatalog";
        public const string PreviewFlowPatch = "PreviewFlowPatch";
        public const string ApplyFlowPatch = "ApplyFlowPatch";
        public const string ExecuteMenu = "ExecuteMenu";
        public const string CreateFlow = "CreateFlow";
        public const string TemplatePatch = "TemplatePatch";
        public const string ApplyTemplatePatch = "ApplyTemplatePatch";
        public const string SetTheme = "SetTheme";
        public const string SetLanguage = "SetLanguage";
    }

    internal enum CopilotSharedCapabilitySafetyClass
    {
        ReadOnly,
        LowRiskWrite,
        ApprovalRequiredWrite,
    }

    internal enum CopilotSharedCapabilityExecutionRoute
    {
        Unspecified,
        SurfaceCapabilityAdapter,
        WorkspaceAuthorizationAdapter,
        ApplicationCapabilityRuntime,
    }

    internal sealed record CopilotSharedCapabilityPresentation(
        string TraceCategory,
        string RunningLabel,
        string CompletedLabel,
        string SuccessSummary,
        bool IsSearch = false)
    {
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(TraceCategory)
            && !string.IsNullOrWhiteSpace(RunningLabel)
            && !string.IsNullOrWhiteSpace(CompletedLabel)
            && !string.IsNullOrWhiteSpace(SuccessSummary);
    }

    internal sealed record CopilotSharedCapabilityMcpMetadata(
        string Category,
        string UsageHint,
        bool DestructiveHint = false,
        bool OpenWorldHint = false)
    {
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(Category)
            && !string.IsNullOrWhiteSpace(UsageHint);
    }

    internal sealed record CopilotSharedCapabilityApprovalMetadata(
        CopilotApprovalReversibility Reversibility,
        string ReversibilitySummary)
    {
        public static CopilotSharedCapabilityApprovalMetadata None { get; } =
            new(CopilotApprovalReversibility.Unknown, string.Empty);

        public bool IsValid =>
            Enum.IsDefined(Reversibility)
            && (Reversibility == CopilotApprovalReversibility.Unknown
                || !string.IsNullOrWhiteSpace(ReversibilitySummary));

        public bool HasPresentation =>
            Reversibility != CopilotApprovalReversibility.Unknown
            || !string.IsNullOrWhiteSpace(ReversibilitySummary);
    }

    internal sealed record CopilotSharedCapabilityDefinition(
        string Id,
        string AgentToolName,
        string McpToolName,
        CopilotToolInputSchema AgentInputSchema,
        CopilotToolInputSchema McpInputSchema,
        CopilotToolCapabilityDescriptor AgentCapability,
        string AgentDescription,
        string McpDescription,
        CopilotSharedCapabilityApprovalMetadata ApprovalMetadata,
        CopilotSharedCapabilityMcpMetadata McpMetadata,
        CopilotSharedCapabilityPresentation Presentation,
        CopilotSharedCapabilityExecutionRoute ExecutionRoute,
        Func<ICopilotApplicationCapabilityInvoker, ICopilotTool> AgentToolFactory,
        Func<CopilotMcpToolDispatcher, CopilotScopedMcpToolHandler> McpHandlerFactory,
        string InputContractDifference = "")
    {
        public bool SharesInputSchema => ReferenceEquals(AgentInputSchema, McpInputSchema);

        public ICopilotTool CreateAgentTool(
            ICopilotApplicationCapabilityInvoker applicationCapabilities)
        {
            ArgumentNullException.ThrowIfNull(applicationCapabilities);
            return AgentToolFactory(applicationCapabilities)
                ?? throw new InvalidOperationException(
                    $"Shared capability '{Id}' returned no Agent tool binding.");
        }

        public CopilotScopedMcpToolHandler CreateMcpHandler(
            CopilotMcpToolDispatcher dispatcher)
        {
            ArgumentNullException.ThrowIfNull(dispatcher);
            return McpHandlerFactory(dispatcher)
                ?? throw new InvalidOperationException(
                    $"Shared capability '{Id}' returned no MCP handler binding.");
        }

        public CopilotSharedCapabilitySafetyClass SafetyClass =>
            (AgentCapability.Access, AgentCapability.RiskLevel, AgentCapability.ApprovalMode) switch
            {
                (CopilotToolAccess.ReadOnly, CopilotToolRiskLevel.Low, CopilotToolApprovalMode.Never) =>
                    CopilotSharedCapabilitySafetyClass.ReadOnly,
                (CopilotToolAccess.Write, CopilotToolRiskLevel.Low, CopilotToolApprovalMode.Never) =>
                    CopilotSharedCapabilitySafetyClass.LowRiskWrite,
                (CopilotToolAccess.Write, CopilotToolRiskLevel.High, CopilotToolApprovalMode.Always) =>
                    CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                _ => throw new InvalidOperationException(
                    $"Shared capability '{Id}' has an unsupported Agent safety policy."),
            };

        public string McpRiskLevel => SafetyClass switch
        {
            CopilotSharedCapabilitySafetyClass.ReadOnly => "read-only",
            CopilotSharedCapabilitySafetyClass.LowRiskWrite => "low-risk-action",
            CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite => "confirmation-required",
            _ => throw new InvalidOperationException($"Unknown shared capability safety class '{SafetyClass}'."),
        };

        public bool MatchesAgentPolicy(CopilotToolCapabilityDescriptor capability) =>
            capability == AgentCapability;
    }

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
        private static CopilotToolInputSchema RecentLogInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["query"] = new { type = "string", description = "Optional case-insensitive filter text." },
                ["max_lines"] = new
                {
                    type = "integer",
                    minimum = 1,
                    maximum = CopilotRecentLogSupport.MaximumToolLogLines,
                    description = $"Maximum recent lines to inspect. Defaults to {CopilotRecentLogSupport.DefaultMaxLogLines}.",
                },
            });
        private static CopilotToolInputSchema ReadAllowedFileAgentInputSchema { get; } =
            CreateReadAllowedFileSchema(requirePath: false);
        private static CopilotToolInputSchema ReadAllowedFileMcpInputSchema { get; } =
            CreateReadAllowedFileSchema(requirePath: true);
        private static CopilotToolInputSchema ListAllowedDirectoryInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["path"] = new { type = "string", description = "Absolute path, or a path relative to an allowed root. If omitted, allowed roots are listed." },
                ["cursor"] = new { type = "string", description = "Opaque next_cursor returned by the preceding page for the same directory. Never invent or modify it." },
            });
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
        private static CopilotToolInputSchema FlowIncidentInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["incident_id"] = new { type = "integer", minimum = 1L, maximum = long.MaxValue, description = "Exact positive incident id supplied by the user, current flow-incidents context, or ListFlowIncidents. Never infer or guess an id." },
                ["max_events"] = new { type = "integer", minimum = 1, maximum = 100, @default = 40, description = "Maximum associated execution events to return. Defaults to 40." },
            },
            "incident_id");
        private static CopilotToolInputSchema FlowIncidentsInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["state"] = new { type = "string", @enum = new[] { "active", "open", "acknowledged", "resolved", "all" }, @default = "active", description = "Incident state filter. Defaults to active: unresolved incidents (all states except the exact standard Resolved state)." },
                ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, @default = 20, description = "Maximum incidents to return in one page. Defaults to 20." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Opaque next_cursor returned by the preceding page for the same filters. Pass it unchanged; never invent, trim, or modify it." },
            });
        private static CopilotToolInputSchema RecordedFlowTemplateInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact retained run id supplied by the user or InspectFlowIncident. Never infer it from the active template or incident id." },
                ["node_id"] = new { type = "string", minLength = 36, maxLength = 36, pattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", description = "Optional exact nonempty node GUID in D format from recorded evidence. Focuses this node and its saved direct neighbors, without fuzzy name matching." },
                ["max_nodes"] = new { type = "integer", minimum = 1, maximum = 200, @default = 80, description = "Maximum saved nodes in the returned prefix or focused neighborhood. Defaults to 80." },
            }, "run_record_id");
        private static CopilotToolInputSchema FlowRunHistoryInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact positive retained run id supplied by the user or InspectFlowIncident. Never infer it from the current canvas or an incident id." },
                ["node_id"] = new { type = "string", minLength = 1, maxLength = 128, description = "Optional exact recorded node id. Pass it unchanged; arbitrary nonempty node identifiers are supported without GUID or fuzzy-name assumptions." },
                ["max_runs"] = new { type = "integer", minimum = 1, maximum = 50, @default = 10, description = "Maximum previous run rows to inspect, ordered by run id descending below the target run id. Defaults to 10." },
            },
            "run_record_id");
        private static CopilotToolInputSchema FlowNodeCatalogInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["query"] = new { type = "string", description = "Optional title, category, runtime type, node type, or device-code search text such as 相机 or camera." },
                ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, description = "Maximum matching node types to return. Defaults to 30." },
            });
        private static CopilotToolInputSchema FlowPatchInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["operation"] = new { type = "string", @enum = new[] { "add_node", "set_property", "connect" }, description = "Exactly one bounded Flow graph operation." },
                ["expected_revision"] = new { type = "string", description = "Current graph revision from InspectFlowGraph." },
                ["type_key"] = new { type = "string", description = "add_node: exact type key from SearchFlowNodeCatalog." },
                ["left"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas X coordinate." },
                ["top"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas Y coordinate." },
                ["inherit_camera_defaults"] = new { type = "boolean", description = "add_node only: explicitly copy current camera acquisition settings for a catalog type with canInheritCameraDefaults. Defaults to false. Preview returns the effective settings and cameraDefaultsFingerprint." },
                ["camera_defaults_fingerprint"] = new { type = "string", description = "add_node with inherit_camera_defaults=true: copy the exact cameraDefaultsFingerprint from the preview into apply. Required when applying; changed camera settings require a new preview." },
                ["node_id"] = new { type = "string", description = "set_property: stable node instance id." },
                ["property_name"] = new { type = "string", description = "set_property: exact writable propertyName from the node catalog." },
                ["value"] = new { type = "string", description = "set_property: new value accepted by the existing STNodeProperty descriptor; an empty string is valid." },
                ["source_node_id"] = new { type = "string", description = "connect: stable source node instance id." },
                ["source_port_id"] = new { type = "string", description = "connect: source output port id such as out:0." },
                ["target_node_id"] = new { type = "string", description = "connect: stable target node instance id." },
                ["target_port_id"] = new { type = "string", description = "connect: target input port id such as in:0." },
            },
            "operation",
            "expected_revision");
        private static CopilotToolInputSchema ExecuteMenuAgentInputSchema { get; } =
            CopilotToolInputSchema.Query("Exact menu name or menu path requested by the user.", required: true);
        private static CopilotToolInputSchema ExecuteMenuMcpInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["query"] = new { type = "string", description = "Menu name or path to execute." },
                ["dry_run"] = new { type = "boolean", description = "When true, resolve the menu and report risk without executing it." },
            },
            "query");
        private static CopilotToolInputSchema CreateFlowInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["name"] = new { type = "string", description = "Optional new flow name." },
            });
        private static CopilotToolInputSchema PreviewTemplatePatchInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["template_identifier"] = new { type = "string", description = "Template name, id, key, or editor identifier." },
                ["proposed_changes"] = new { description = "Object containing proposed top-level JSON changes, or a JSON object string." },
                ["current_json"] = new { type = "string", description = "Optional current template JSON. If omitted, the active template editor context is used." },
            },
            "template_identifier",
            "proposed_changes");
        private static CopilotToolInputSchema ApplyTemplatePatchInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["preview_id"] = new { type = "string", description = "Preview id returned by preview_template_patch." },
            },
            "preview_id");
        private static CopilotToolInputSchema SetThemeInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["theme"] = new { type = "string", description = "Target theme name." },
            },
            "theme");
        private static CopilotToolInputSchema SetLanguageInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["language"] = new { type = "string", description = "Target language or culture name, for example en-US or zh-Hans." },
            },
            "language");
        private static CopilotSharedCapabilityPresentation DocumentationSearchPresentation { get; } =
            Presentation("file-search", "正在搜索文档", "搜索了文档", "已获得文档搜索结果。", isSearch: true);
        private static CopilotSharedCapabilityPresentation FileSearchPresentation { get; } =
            Presentation("file-search", "正在搜索文件", "搜索了文件", "已完成文件搜索。", isSearch: true);
        private static CopilotSharedCapabilityPresentation DirectoryPresentation { get; } =
            Presentation("file-search", "正在浏览目录", "浏览了目录", "已读取目录内容。");
        private static CopilotSharedCapabilityPresentation FileReadPresentation { get; } =
            Presentation("file-read", "正在读取文件", "读取了文件", "已读取文件内容。");
        private static CopilotSharedCapabilityPresentation LogReadPresentation { get; } =
            Presentation("file-read", "正在读取日志", "读取了日志", "已读取最近日志。");
        private static CopilotSharedCapabilityPresentation SavedTemplatePresentation { get; } =
            Presentation("application", "正在检查已保存模板", "检查了已保存模板", "已保存模板信息检查完成。");
        private static CopilotSharedCapabilityPresentation TemplateTypePresentation { get; } =
            Presentation("application", "正在检查模板类型", "检查了模板类型", "模板类型信息检查完成。");
        private static CopilotSharedCapabilityPresentation FlowGraphPresentation { get; } =
            Presentation("application", "正在检查流程", "检查了流程", "流程图检查完成。");
        private static CopilotSharedCapabilityPresentation FlowIncidentPresentation { get; } =
            Presentation("application", "正在检查流程事故", "检查了流程事故", "流程事故检查完成。");
        private static CopilotSharedCapabilityPresentation FlowIncidentsPresentation { get; } =
            Presentation("application", "正在查询流程事故", "查询了流程事故", "已获得流程事故列表。", isSearch: true);
        private static CopilotSharedCapabilityPresentation FlowRunHistoryPresentation { get; } =
            Presentation("application", "正在检查历史流程运行", "检查了历史流程运行", "历史流程运行检查完成。");
        private static CopilotSharedCapabilityPresentation RecordedFlowTemplatePresentation { get; } =
            Presentation("application", "正在检查留存流程模板", "检查了留存流程模板", "留存流程模板检查完成。");
        private static CopilotSharedCapabilityPresentation FlowNodeSearchPresentation { get; } =
            Presentation("application", "正在搜索流程节点", "搜索了流程节点", "已获得流程节点类型结果。", isSearch: true);
        private static CopilotSharedCapabilityPresentation FlowPreviewPresentation { get; } =
            Presentation("application", "正在准备流程修改", "准备了流程修改", "流程修改预览已准备。");
        private static CopilotSharedCapabilityPresentation FlowApplyPresentation { get; } =
            Presentation("application", "正在修改流程", "修改了流程", "流程修改已完成。");
        private static CopilotSharedCapabilityPresentation MenuPresentation { get; } =
            Presentation("application", "正在执行应用操作", "执行了应用操作", "应用操作已执行。");
        private static CopilotSharedCapabilityPresentation FlowCreationPresentation { get; } =
            Presentation("application", "正在创建流程", "创建了流程", "流程已创建。");
        private static CopilotSharedCapabilityPresentation TemplatePreviewPresentation { get; } =
            Presentation("application", "正在准备模板修改", "准备了模板修改", "模板修改预览已准备。");
        private static CopilotSharedCapabilityPresentation TemplateApplyPresentation { get; } =
            Presentation("application", "正在修改模板", "修改了模板", "模板修改已完成。");
        private static CopilotSharedCapabilityPresentation SettingsPresentation { get; } =
            Presentation("application", "正在修改应用设置", "修改了应用设置", "应用设置已更新。");

        public static CopilotSharedCapabilityDefinition SearchDocs { get; } =
            Shared(
                "docs.search",
                CopilotSharedAgentToolNames.SearchDocs,
                "search_docs",
                SearchDocsInputSchema,
                _ => new CopilotSearchDocsTool(),
                dispatcher => (arguments, _, token) => dispatcher.SearchDocsAsync(arguments, token),
                executionRoute: CopilotSharedCapabilityExecutionRoute.SurfaceCapabilityAdapter,
                evidenceMode: CopilotToolEvidenceMode.RedactedExcerpt,
                agentDescription: "Search the ColorVision online documentation index and return the most relevant snippets by section, page, and heading. Useful for software usage, menus, devices, plugins, developer guides, and architecture questions.",
                mcpDescription: "Search the published ColorVision documentation index. Required argument: query.",
                mcpMetadata: McpMetadata(
                    "search",
                    "Call search_docs with { \"query\": \"plugin development\" }.",
                    openWorldHint: true),
                presentation: DocumentationSearchPresentation);
        public static CopilotSharedCapabilityDefinition SearchFiles { get; } =
            Shared(
                "workspace.search-files",
                CopilotSharedAgentToolNames.SearchFiles,
                "search_files",
                SearchFilesInputSchema,
                _ => new CopilotSearchFilesTool(),
                dispatcher => (arguments, scope, token) => Task.FromResult(dispatcher.SearchFiles(arguments, scope, token)),
                executionRoute: CopilotSharedCapabilityExecutionRoute.WorkspaceAuthorizationAdapter,
                agentDescription: "Find one stable bounded page of candidate files by file name or path fragment, optionally limited to one workspace directory, with a continuation cursor when more matches remain. A completed empty search is successful evidence, not a tool failure; inspect scan_complete before concluding that a file is absent.",
                mcpDescription: "Search one stable bounded page of file names and relative paths under allowed ColorVision workspace roots. Required argument: query. Optional: path, cursor.",
                mcpMetadata: McpMetadata("search", "Call search_files with { \"query\": \"DeviceCamera\", \"path\": \"ColorVision\" }; pass its next_cursor unchanged for another page."),
                presentation: FileSearchPresentation);
        public static CopilotSharedCapabilityDefinition GrepText { get; } =
            Shared(
                "workspace.grep-text",
                CopilotSharedAgentToolNames.GrepText,
                "grep_text",
                GrepTextInputSchema,
                _ => new CopilotGrepTextTool(),
                dispatcher => (arguments, scope, token) => Task.FromResult(dispatcher.GrepText(arguments, scope, token)),
                executionRoute: CopilotSharedCapabilityExecutionRoute.WorkspaceAuthorizationAdapter,
                agentDescription: "Search one stable bounded page of workspace text matches, optionally limited to one workspace file or directory, with an opaque continuation cursor when more matches remain. A completed empty search is successful evidence, not a tool failure; inspect scan_complete before concluding that text is absent.",
                mcpDescription: "Search one stable bounded page of text matches under allowed ColorVision workspace roots using a literal case-insensitive query. The optional path may identify one file or directory. Required argument: query. Optional: path, cursor.",
                mcpMetadata: McpMetadata("search", "Call grep_text with { \"query\": \"FlowEngineManager\", \"path\": \"ColorVision/Copilot\" }; pass its next_cursor unchanged for another page."),
                presentation: FileSearchPresentation);
        public static CopilotSharedCapabilityDefinition ReadAllowedFile { get; } =
            SurfaceSpecific(
                "workspace.read-file",
                CopilotSharedAgentToolNames.ReadLocalFile,
                "read_allowed_file",
                ReadAllowedFileAgentInputSchema,
                ReadAllowedFileMcpInputSchema,
                "Agent calls may omit path to batch-read preselected files; external MCP calls require one explicit file path.",
                _ => new CopilotReadLocalFileTool(),
                dispatcher => (arguments, scope, token) => dispatcher.ReadAllowedFileAsync(arguments, scope, token),
                executionRoute: CopilotSharedCapabilityExecutionRoute.WorkspaceAuthorizationAdapter,
                agentDescription: "Read bounded local text allowed for the current round, prefix every returned source line with its authoritative one-based L<number>: coordinate, and report a safe line-and-column continuation cursor when content is truncated. When multiple exact files are preselected, omit path and line range to batch-read one task-focused evidence window from every file in one call. Otherwise, for known files or symbols, use GrepText on each exact file first and request focused line ranges; an unbounded read intentionally returns only the first bounded segment.",
                mcpDescription: "Read a text file only if it is under an allowed ColorVision workspace root. Required argument: path. Optional: start_line, start_column, end_line.",
                mcpMetadata: McpMetadata("file", "Call read_allowed_file with { \"path\": \"README.md\", \"start_line\": 1, \"start_column\": 1, \"end_line\": 40 }."),
                presentation: FileReadPresentation);
        public static CopilotSharedCapabilityDefinition ListAllowedDirectory { get; } =
            Shared(
                "workspace.list-directory",
                CopilotSharedAgentToolNames.ListDirectory,
                "list_allowed_directory",
                ListAllowedDirectoryInputSchema,
                _ => new CopilotListDirectoryTool(),
                dispatcher => (arguments, scope, token) => Task.FromResult(dispatcher.ListAllowedDirectory(arguments, scope, token)),
                executionRoute: CopilotSharedCapabilityExecutionRoute.WorkspaceAuthorizationAdapter,
                agentDescription: "List one stable, bounded page of files and subdirectories from an allowed local directory, with an opaque continuation cursor when more entries remain.",
                mcpDescription: "List one stable bounded directory page only if it is under an allowed ColorVision workspace root. Optional arguments: path, cursor.",
                mcpMetadata: McpMetadata("file", "Call list_allowed_directory with { \"path\": \"Engine\" }; pass its next_cursor unchanged to request another page."),
                presentation: DirectoryPresentation);
        public static CopilotSharedCapabilityDefinition RecentLog { get; } =
            Shared(
                "diagnostics.recent-log",
                CopilotSharedAgentToolNames.GetRecentLog,
                "get_recent_log",
                RecentLogInputSchema,
                _ => new CopilotGetRecentLogTool(),
                dispatcher => (arguments, _, token) => dispatcher.GetRecentLogAsync(arguments, token),
                executionRoute: CopilotSharedCapabilityExecutionRoute.SurfaceCapabilityAdapter,
                agentDescription: "Read recent ColorVision application logs for failure or exception diagnosis. Do not use this tool for Windows version, port, process, service, or other machine-state inspection.",
                mcpDescription: "Read recent ColorVision application log lines. Optional arguments: query, max_lines.",
                mcpMetadata: McpMetadata("search", "Call get_recent_log with { \"query\": \"error\", \"max_lines\": 200 }."),
                presentation: LogReadPresentation);
        public static CopilotSharedCapabilityDefinition SavedTemplateContext { get; } =
            Shared(
                "template.saved-context",
                CopilotSharedAgentToolNames.InspectSavedTemplate,
                "get_saved_template_context",
                SavedTemplateContextInputSchema,
                applicationCapabilities => new CopilotInspectSavedTemplateTool(applicationCapabilities),
                _ => (arguments, _, _) => Task.FromResult(CopilotMcpToolDispatcher.GetSavedTemplateContext(arguments)),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read the exact saved template attached with @ as a bounded, redacted, read-only in-memory snapshot. Use the template_code and template_name from that reference before describing its values. This never queries the database, modifies, or saves a template.",
                mcpDescription: "Return a bounded redacted read-only snapshot of one already loaded saved ColorVision template. Required arguments: template_code, template_name.",
                mcpMetadata: McpMetadata("context", "Call get_saved_template_context with { \"template_code\": \"SFR\", \"template_name\": \"Default\" } after the user references a saved template."),
                presentation: SavedTemplatePresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition TemplateTypeContext { get; } =
            Shared(
                "template.type-context",
                CopilotSharedAgentToolNames.InspectTemplateType,
                "get_template_type_context",
                TemplateTypeContextInputSchema,
                applicationCapabilities => new CopilotInspectTemplateTypeTool(applicationCapabilities),
                _ => (arguments, _, _) => Task.FromResult(CopilotMcpToolDispatcher.GetTemplateTypeContext(arguments)),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Inspect the template type attached with @ as bounded read-only metadata: identity, loaded saved names, and browsable parameter field schema without values. Use its exact template_code. This never queries the database, reads template values, modifies, or saves a template.",
                mcpDescription: "Return bounded read-only metadata for one already loaded ColorVision template type, including saved names and parameter field schema but never parameter values. Required argument: template_code.",
                mcpMetadata: McpMetadata("context", "Call get_template_type_context with { \"template_code\": \"SFR\" } after the user references a template type."),
                presentation: TemplateTypePresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowGraph { get; } =
            Shared(
                "flow.graph",
                CopilotSharedAgentToolNames.InspectFlowGraph,
                "get_flow_graph",
                FlowGraphInputSchema,
                applicationCapabilities => new CopilotInspectFlowGraphTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.GetFlowGraphAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Inspect the active ColorVision flow as a structured graph with a revision, stable node ids, exact runtime type keys, ports, and edges. Use this instead of reading the binary .stn file.",
                mcpDescription: "Return the active ColorVision flow as a bounded structured graph with a revision, stable node ids, runtime type keys, ports, and edges. Use this instead of reading the binary .stn file.",
                mcpMetadata: McpMetadata("context", "Call get_flow_graph with { \"max_nodes\": 80 } before planning a flow edit."),
                presentation: FlowGraphPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowIncident { get; } =
            Shared(
                "flow.incident",
                CopilotSharedAgentToolNames.InspectFlowIncident,
                "inspect_flow_incident",
                FlowIncidentInputSchema,
                applicationCapabilities => new CopilotInspectFlowIncidentTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectFlowIncidentAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Inspect one exact recorded ColorVision flow incident and its associated run, node attempt, and bounded execution events. Use only an incident_id supplied by the user, current flow-incidents context, or ListFlowIncidents; never guess an id. This read-only tool never acknowledges, resolves, runs, or changes a flow.",
                mcpDescription: "Return a bounded read-only inspection of one exact recorded ColorVision flow incident, associated run and node attempt, and execution events. Required argument: incident_id from the user, current flow-incidents context, or list_flow_incidents; never guess an id. Optional: max_events (1-100, default 40).",
                mcpMetadata: McpMetadata("context", "Call inspect_flow_incident with the exact incident_id supplied by the user, current flow-incidents context, or list_flow_incidents; optionally set max_events up to 100."),
                presentation: FlowIncidentPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowIncidents { get; } =
            Shared(
                "flow.incidents",
                CopilotSharedAgentToolNames.ListFlowIncidents,
                "list_flow_incidents",
                FlowIncidentsInputSchema,
                applicationCapabilities => new CopilotListFlowIncidentsTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.ListFlowIncidentsAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "List one bounded read-only page of recorded ColorVision flow incidents, optionally filtered by state. The default active filter returns unresolved incidents: all states except the exact standard Resolved state. Use a returned incident_id with InspectFlowIncident to inspect exact associated evidence. Pass next_cursor unchanged for the same filters. The list alone establishes neither a fault root cause nor a snapshot of the entire incident history. This never acknowledges, resolves, runs, or changes a flow.",
                mcpDescription: "Return one bounded read-only page of recorded ColorVision flow incidents. Optional: state (active by default: unresolved incidents, all states except the exact standard Resolved state), max_results (1-100, default 20), cursor. Call inspect_flow_incident with a returned incident_id for exact details. Pass next_cursor unchanged for the same filters; a page is not a fault root cause or a snapshot of the entire incident history.",
                mcpMetadata: McpMetadata("context", "Call list_flow_incidents with optional state and max_results; pass its next_cursor unchanged for the same filters. Use a returned incident_id with inspect_flow_incident for details."),
                presentation: FlowIncidentsPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition RecordedFlowTemplate { get; } =
            Shared(
                "flow.recorded-template",
                CopilotSharedAgentToolNames.InspectRecordedFlowTemplate,
                "inspect_recorded_flow_template",
                RecordedFlowTemplateInputSchema,
                applicationCapabilities => new CopilotInspectRecordedFlowTemplateTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectRecordedFlowTemplateAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Inspect the exact template snapshot retained by a recorded ColorVision run. Use run_record_id from the user or InspectFlowIncident. Returns bounded saved node identities, property names, and connections only when saved port layout resolves them; never loads node types or returns property values. Optional node_id focuses a saved node and direct neighbors. Retained template data can omit unsaved canvas changes and does not establish the actual executed graph. Current InspectFlowGraph and its Revision are separate evidence.",
                mcpDescription: "Read the exact retained template linked by run_record_id, without loading nodes or an editor. Optional node_id is an exact GUID; max_nodes is 1-200 (default 80). Returns saved identities/property names and resolvable saved connections, never property values or raw bytes. Retained data may omit unsaved canvas changes and must not be treated as proof of the actual executed graph.",
                mcpMetadata: McpMetadata("context", "Use run_record_id from inspect_flow_incident or the user. Call inspect_recorded_flow_template; optionally focus an exact saved node_id. Compare provenance before using current editor evidence."),
                presentation: RecordedFlowTemplatePresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowRunHistory { get; } =
            Shared(
                "flow.run-history",
                CopilotSharedAgentToolNames.InspectFlowRunHistory,
                "inspect_flow_run_history",
                FlowRunHistoryInputSchema,
                applicationCapabilities => new CopilotInspectFlowRunHistoryTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectFlowRunHistoryAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Inspect one exact recorded ColorVision run and a bounded window of lower run ids in descending id order, with states, durations, and optional exact-node attempts. Confirmed flow identity requires the complete stored FlowKey to match. Rows where both FlowKeys are absent and a positive TemplateId matches are labeled legacy references, never confirmed same-flow statistics. Descriptive success samples require matching recorded content hashes and known successful final outcomes; null outcomes are unknown. This is not full history, a success rate, or evidence of the actual executed canvas. It never reads snapshot BLOBs, SNs, operators, or payloads, or controls a flow.",
                mcpDescription: "Return complete JSON for one exact run and bounded previous run ids, optionally including bounded attempts of one exact node. Required: run_record_id. Optional: node_id unchanged, max_runs (1-50, default 10). Complete stored FlowKey matching confirms identity; same positive TemplateId with both FlowKeys absent is legacy reference only. Descriptive success samples use matching recorded hashes and known successful final outcomes; null outcomes are unknown. This is not full history, a success rate, or the actual executed canvas. No snapshot BLOBs, SNs, operators, payloads, or flow control.",
                mcpMetadata: McpMetadata("context", "Call inspect_flow_run_history with the exact run_record_id returned by inspect_flow_incident; optionally pass an exact unchanged node_id and max_runs up to 50."),
                presentation: FlowRunHistoryPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowNodeCatalog { get; } =
            Shared(
                "flow.node-catalog",
                CopilotSharedAgentToolNames.SearchFlowNodeCatalog,
                "get_flow_node_catalog",
                FlowNodeCatalogInputSchema,
                applicationCapabilities => new CopilotSearchFlowNodeCatalogTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.GetFlowNodeCatalogAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Search the node types loaded by the active Flow editor. Returns exact type keys and writable property schemas. Search first and never guess which camera node the user means.",
                mcpDescription: "Search the node types loaded by the active Flow editor. Returns exact runtime type keys, categories, default device codes, and writable property schemas; do not guess a camera node type.",
                mcpMetadata: McpMetadata("context", "Call get_flow_node_catalog with { \"query\": \"相机\", \"max_results\": 30 }."),
                presentation: FlowNodeSearchPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition PreviewFlowPatch { get; } =
            Shared(
                "flow.preview-patch",
                CopilotSharedAgentToolNames.PreviewFlowPatch,
                "preview_flow_patch",
                FlowPatchInputSchema,
                applicationCapabilities => new CopilotPreviewFlowPatchTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.PreviewFlowPatchAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Validate exactly one add_node, set_property, or connect operation against the active Flow graph revision. Use exact ids, port ids, and type keys from the read tools. For supported camera nodes, explicitly set inherit_camera_defaults=true to preview current acquisition settings and obtain cameraDefaultsFingerprint for apply. This never edits, saves, or runs the flow.",
                mcpDescription: "Validate one bounded Flow graph change without editing: add_node, set_property, or connect. Use exact ids/type keys from the Flow graph and node catalog. Optional inherit_camera_defaults=true previews effective camera settings and their cameraDefaultsFingerprint.",
                mcpMetadata: McpMetadata("context", "Call preview_flow_patch with one exact operation and the current graph revision."),
                presentation: FlowPreviewPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition ApplyFlowPatch { get; } =
            Shared(
                "flow.apply-patch",
                CopilotSharedAgentToolNames.ApplyFlowPatch,
                "apply_flow_patch",
                FlowPatchInputSchema,
                applicationCapabilities => new CopilotApplyFlowPatchTool(applicationCapabilities),
                dispatcher => (arguments, scope, token) => dispatcher.ApplyFlowPatchAsync(arguments, scope, token),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                CopilotToolIdempotency.NonIdempotent,
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Apply one previously previewed add_node, set_property, or connect operation to the active Flow editor. Rechecks the revision, requires explicit approval, and never saves or runs the flow. When inheriting camera settings, also pass the preview's exact camera_defaults_fingerprint; changed settings are rejected before adding the node.",
                mcpDescription: "Apply one previously previewed add_node, set_property, or connect change after explicit approval. Rechecks the graph revision and, for inherit_camera_defaults=true, the required camera_defaults_fingerprint. Never saves or runs the flow.",
                approvalMetadata: ManualApproval(
                    "修改不会自动保存或运行流程；如需恢复，必须在编辑器中手动撤销。"),
                mcpMetadata: McpMetadata(
                    "app-control",
                    "Call apply_flow_patch with the exact operation and values used by preview_flow_patch, then wait for approval.",
                    destructiveHint: true),
                presentation: FlowApplyPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition ExecuteMenu { get; } =
            SurfaceSpecific(
                "application.execute-menu",
                CopilotSharedAgentToolNames.ExecuteMenu,
                "execute_menu",
                ExecuteMenuAgentInputSchema,
                ExecuteMenuMcpInputSchema,
                "The Agent surface exposes only the approval-bound execution path; external MCP also exposes dry_run resolution.",
                applicationCapabilities => new CopilotExecuteMenuTool(applicationCapabilities),
                dispatcher => (arguments, scope, token) => dispatcher.ExecuteMenuAsync(arguments, scope, token),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                CopilotToolIdempotency.Unknown,
                agentDescription: "Execute a generic main-menu command by exact menu selector, name, or path after explicit approval. For an attached @ menu reference, copy its ExecuteMenu query value exactly into input.query. Prefer dedicated tools such as SetTheme, ConvertBatchImages, or OpenBatchImageProcessing when available; never use this generic fallback for batch image conversion or processing.",
                mcpDescription: "Execute a visible main-window menu command by menu name or path. Required argument: query.",
                approvalMetadata: Approval(
                    CopilotApprovalReversibility.Unknown,
                    "所选命令未声明自动撤销能力；请在批准前核对影响。"),
                mcpMetadata: McpMetadata(
                    "app-control",
                    "Call execute_menu with { \"query\": \"View > Copilot\", \"dry_run\": true } first.",
                    destructiveHint: true),
                presentation: MenuPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition CreateFlow { get; } =
            Shared(
                "application.create-flow",
                CopilotSharedAgentToolNames.CreateFlow,
                "create_flow",
                CreateFlowInputSchema,
                applicationCapabilities => new CopilotCreateFlowTool(applicationCapabilities),
                dispatcher => (arguments, scope, token) => dispatcher.CreateFlowAsync(arguments, scope, token),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                CopilotToolIdempotency.NonIdempotent,
                agentDescription: "Create a new empty ColorVision flow after explicit user approval. Put the optional requested flow name in input.name; omit it to generate a timestamped name. This tool stages the action and never opens the flow-template manager.",
                mcpDescription: "Create a new empty ColorVision flow after explicit user approval. Optional argument: name; a timestamped name is generated when omitted.",
                approvalMetadata: ManualApproval(
                    "新建流程不会自动删除；如需恢复，必须手动关闭或移除。"),
                mcpMetadata: McpMetadata("app-control", "Call create_flow with { \"name\": \"CalibrationFlow\" }, then wait for approval in ColorVision."),
                presentation: FlowCreationPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition PreviewTemplatePatch { get; } =
            Shared(
                "template.preview-patch",
                CopilotSharedAgentToolNames.TemplatePatch,
                "preview_template_patch",
                PreviewTemplatePatchInputSchema,
                applicationCapabilities => new CopilotTemplatePatchTool(applicationCapabilities),
                dispatcher => (arguments, _, _) => Task.FromResult(dispatcher.PreviewTemplatePatch(arguments)),
                idempotency: CopilotToolIdempotency.Unknown,
                agentDescription: "Preview guarded changes to the active template JSON with template_identifier and proposed_changes. This function never applies or saves the template; use ApplyTemplatePatch for an existing preview.",
                mcpDescription: "Preview a proposed template JSON patch without saving it. Required arguments: template_identifier, proposed_changes. Optional: current_json.",
                mcpMetadata: McpMetadata("context", "Call preview_template_patch with { \"template_identifier\": \"Default\", \"proposed_changes\": { \"Exposure\": 12 } }."),
                presentation: TemplatePreviewPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition ApplyTemplatePatch { get; } =
            Shared(
                "template.apply-patch",
                CopilotSharedAgentToolNames.ApplyTemplatePatch,
                "apply_template_patch",
                ApplyTemplatePatchInputSchema,
                applicationCapabilities => new CopilotApplyTemplatePatchTool(applicationCapabilities),
                dispatcher => (arguments, scope, token) => dispatcher.ApplyTemplatePatchAsync(arguments, scope, token),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                CopilotToolIdempotency.NonIdempotent,
                agentDescription: "Apply an existing guarded template preview after explicit approval using input.preview_id. The change affects only the active editor and does not save the template.",
                mcpDescription: "Create a user-confirmed action that applies a prior preview_template_patch result to the active template JSON editor. Required argument: preview_id.",
                approvalMetadata: ManualApproval(
                    "修改只应用到当前编辑器；保存前可通过重新加载模板手动恢复。"),
                mcpMetadata: McpMetadata(
                    "app-control",
                    "Call preview_template_patch first, then apply_template_patch with the returned preview_id.",
                    destructiveHint: true),
                presentation: TemplateApplyPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition SetTheme { get; } =
            Shared(
                "application.set-theme",
                CopilotSharedAgentToolNames.SetTheme,
                "set_theme",
                SetThemeInputSchema,
                applicationCapabilities => new CopilotSetThemeTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.SetThemeAsync(arguments, token),
                CopilotSharedCapabilitySafetyClass.LowRiskWrite,
                CopilotToolIdempotency.Idempotent,
                agentDescription: "Switch the application theme requested by the user. input.theme is a target such as system, dark, light, pink, or cyan.",
                mcpDescription: "Set the ColorVision UI theme. Required argument: theme. Allowed values include system, light, dark, pink, cyan.",
                mcpMetadata: McpMetadata(
                    "app-control",
                    "Call set_theme with { \"theme\": \"dark\" }.",
                    destructiveHint: true),
                presentation: SettingsPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition SetLanguage { get; } =
            Shared(
                "application.set-language",
                CopilotSharedAgentToolNames.SetLanguage,
                "set_language",
                SetLanguageInputSchema,
                applicationCapabilities => new CopilotSetLanguageTool(applicationCapabilities),
                dispatcher => (arguments, scope, token) => dispatcher.SetLanguageAsync(arguments, scope, token),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite,
                CopilotToolIdempotency.Idempotent,
                agentDescription: "Switch the UI language requested by the user. input.language accepts a language or culture name such as English, Chinese, en-US, or zh-Hans. The change may ask for confirmation and restart the application.",
                mcpDescription: "Set the ColorVision UI language. Required argument: language. This may trigger the app's existing restart confirmation flow.",
                approvalMetadata: ManualApproval(
                    "可在设置中再次切换语言，但本操作没有自动回滚步骤。"),
                mcpMetadata: McpMetadata(
                    "app-control",
                    "Call set_language with { \"language\": \"en-US\" } and expect user confirmation.",
                    destructiveHint: true),
                presentation: SettingsPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);

        public static IReadOnlyList<CopilotSharedCapabilityDefinition> All { get; } =
            Array.AsReadOnly(new[]
            {
                SearchDocs,
                SearchFiles,
                GrepText,
                ReadAllowedFile,
                ListAllowedDirectory,
                RecentLog,
                SavedTemplateContext,
                TemplateTypeContext,
                FlowGraph,
                FlowIncident,
                FlowIncidents,
                FlowRunHistory,
                RecordedFlowTemplate,
                FlowNodeCatalog,
                PreviewFlowPatch,
                ApplyFlowPatch,
                ExecuteMenu,
                CreateFlow,
                PreviewTemplatePatch,
                ApplyTemplatePatch,
                SetTheme,
                SetLanguage,
            });

        private static IReadOnlyDictionary<string, CopilotSharedCapabilityDefinition> ByAgentToolName { get; } =
            All.ToDictionary(
                definition => definition.AgentToolName,
                StringComparer.OrdinalIgnoreCase);

        private static IReadOnlyDictionary<string, CopilotSharedCapabilityDefinition> ByMcpToolName { get; } =
            All.ToDictionary(
                definition => definition.McpToolName,
                StringComparer.OrdinalIgnoreCase);

        public static bool TryResolveAgentTool(
            string? agentToolName,
            out CopilotSharedCapabilityDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(agentToolName)
                && ByAgentToolName.TryGetValue(agentToolName.Trim(), out var resolved))
            {
                definition = resolved;
                return true;
            }

            definition = null!;
            return false;
        }

        public static bool TryResolveMcpTool(
            string? mcpToolName,
            out CopilotSharedCapabilityDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(mcpToolName)
                && ByMcpToolName.TryGetValue(mcpToolName.Trim(), out var resolved))
            {
                definition = resolved;
                return true;
            }

            definition = null!;
            return false;
        }

        public static CopilotToolApprovalPresentation ApplyApprovalMetadata(
            string? agentToolName,
            CopilotToolApprovalPresentation presentation)
        {
            ArgumentNullException.ThrowIfNull(presentation);
            if (!TryResolveAgentTool(agentToolName, out var definition)
                || !definition.ApprovalMetadata.HasPresentation)
            {
                return presentation;
            }

            return presentation with
            {
                Reversibility = definition.ApprovalMetadata.Reversibility,
                ReversibilitySummary = definition.ApprovalMetadata.ReversibilitySummary,
            };
        }

        public static void ValidateAgentSurface(IEnumerable<ICopilotTool> tools)
        {
            var toolsByName = (tools ?? Array.Empty<ICopilotTool>())
                .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            ValidateSurface(
                "Agent",
                All.Where(definition => !toolsByName.ContainsKey(definition.AgentToolName))
                    .Select(definition => definition.AgentToolName));
            ValidateSchemaBindings(
                "Agent",
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.AgentToolName, out var tool)
                        && !ReferenceEquals(tool.InputSchema, definition.AgentInputSchema))
                    .Select(definition => definition.AgentToolName));
            ValidateExecutionPolicyBindings(
                "Agent",
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.AgentToolName, out var tool)
                        && !definition.MatchesAgentPolicy(tool.Capability))
                    .Select(definition => definition.AgentToolName));
            ValidateDescriptionBindings(
                "Agent",
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.AgentToolName, out var tool)
                        && !string.Equals(
                            tool.Description,
                            definition.AgentDescription,
                            StringComparison.Ordinal))
                    .Select(definition => definition.AgentToolName));
        }

        public static void ValidateMcpSurface(IEnumerable<CopilotMcpToolDescriptor> tools)
        {
            var toolsByName = (tools ?? Array.Empty<CopilotMcpToolDescriptor>())
                .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            ValidateSurface(
                "MCP",
                All.Where(definition => !toolsByName.ContainsKey(definition.McpToolName))
                    .Select(definition => definition.McpToolName));
            ValidateSchemaBindings(
                "MCP",
                All.Where(definition =>
                    {
                        if (!toolsByName.TryGetValue(definition.McpToolName, out var tool)
                            || tool.InputSchema is not JsonElement schema)
                        {
                            return true;
                        }
                        return !JsonElement.DeepEquals(
                            schema,
                            definition.McpInputSchema.JsonSchema);
                    })
                    .Select(definition => definition.McpToolName));
            ValidateExecutionPolicyBindings(
                "MCP",
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.McpToolName, out var tool)
                        && !string.Equals(
                            tool.RiskLevel,
                            definition.McpRiskLevel,
                            StringComparison.Ordinal))
                    .Select(definition => definition.McpToolName));
            ValidateDescriptionBindings(
                "MCP",
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.McpToolName, out var tool)
                        && !string.Equals(
                            tool.Description,
                            definition.McpDescription,
                            StringComparison.Ordinal))
                    .Select(definition => definition.McpToolName));
            ValidateMcpMetadataBindings(
                All.Where(definition =>
                        toolsByName.TryGetValue(definition.McpToolName, out var tool)
                        && (!string.Equals(
                                tool.Category,
                                definition.McpMetadata.Category,
                                StringComparison.Ordinal)
                            || !string.Equals(
                                tool.UsageExample,
                                definition.McpMetadata.UsageHint,
                                StringComparison.Ordinal)
                            || !HasBooleanAnnotation(
                                tool,
                                "readOnlyHint",
                                definition.AgentCapability.Access == CopilotToolAccess.ReadOnly)
                            || !HasBooleanAnnotation(
                                tool,
                                "idempotentHint",
                                definition.AgentCapability.Idempotency
                                    == CopilotToolIdempotency.Idempotent)
                            || !HasBooleanAnnotation(
                                tool,
                                "destructiveHint",
                                definition.McpMetadata.DestructiveHint)
                            || !HasBooleanAnnotation(
                                tool,
                                "openWorldHint",
                                definition.McpMetadata.OpenWorldHint)
                            || tool.EffectiveExecutionTimeout
                                != definition.AgentCapability.EffectiveExecutionTimeout))
                    .Select(definition => definition.McpToolName));
        }

        public static CopilotSharedCapabilityDefinition ResolveBinding(
            string agentToolName,
            string mcpToolName)
        {
            var definition = All.SingleOrDefault(definition =>
                    string.Equals(definition.AgentToolName, agentToolName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(definition.McpToolName, mcpToolName, StringComparison.OrdinalIgnoreCase));
            if (definition != null)
                return definition;

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

        private static void ValidateSchemaBindings(
            string surface,
            IEnumerable<string> mismatchedNames)
        {
            var mismatched = mismatchedNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (mismatched.Length == 0)
                return;

            throw new InvalidOperationException(
                $"The {surface} tool surface has shared input schema drift: {string.Join(", ", mismatched)}.");
        }

        private static void ValidateExecutionPolicyBindings(
            string surface,
            IEnumerable<string> mismatchedNames)
        {
            var mismatched = mismatchedNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (mismatched.Length == 0)
                return;

            throw new InvalidOperationException(
                $"The {surface} tool surface has shared execution policy drift: {string.Join(", ", mismatched)}.");
        }

        private static void ValidateDescriptionBindings(
            string surface,
            IEnumerable<string> mismatchedNames)
        {
            var mismatched = mismatchedNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (mismatched.Length == 0)
                return;

            throw new InvalidOperationException(
                $"The {surface} tool surface has shared description drift: {string.Join(", ", mismatched)}.");
        }

        private static void ValidateMcpMetadataBindings(IEnumerable<string> mismatchedNames)
        {
            var mismatched = mismatchedNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (mismatched.Length == 0)
                return;

            throw new InvalidOperationException(
                $"The MCP tool surface has shared descriptor metadata drift: {string.Join(", ", mismatched)}.");
        }

        private static bool HasBooleanAnnotation(
            CopilotMcpToolDescriptor descriptor,
            string name,
            bool expected) =>
            descriptor.Annotations != null
            && descriptor.Annotations.TryGetValue(name, out var value)
            && value is bool actual
            && actual == expected;

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

        private static CopilotToolInputSchema CreateReadAllowedFileSchema(bool requirePath)
        {
            return CreateSchema(
                new Dictionary<string, object?>
                {
                    ["path"] = new { type = "string", description = "Absolute path, or a path relative to an allowed root. Agent calls may omit it only when preselected files are available." },
                    ["start_line"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "1-based start line." },
                    ["start_column"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "1-based character column within start_line. Use the exact continuation cursor returned by a truncated read." },
                    ["end_line"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "1-based end line." },
                },
                requirePath ? ["path"] : Array.Empty<string>());
        }

        private static CopilotSharedCapabilityDefinition Shared(
            string id,
            string agentToolName,
            string mcpToolName,
            CopilotToolInputSchema inputSchema,
            Func<ICopilotApplicationCapabilityInvoker, ICopilotTool> agentToolFactory,
            Func<CopilotMcpToolDispatcher, CopilotScopedMcpToolHandler> mcpHandlerFactory,
            CopilotSharedCapabilitySafetyClass safetyClass = CopilotSharedCapabilitySafetyClass.ReadOnly,
            CopilotToolIdempotency idempotency = CopilotToolIdempotency.Idempotent,
            TimeSpan? executionTimeout = null,
            CopilotToolAuditArgumentMode auditArgumentMode = CopilotToolAuditArgumentMode.RedactedSummary,
            CopilotToolEvidenceMode evidenceMode = CopilotToolEvidenceMode.Summary,
            string agentDescription = "",
            string mcpDescription = "",
            CopilotSharedCapabilityApprovalMetadata? approvalMetadata = null,
            CopilotSharedCapabilityMcpMetadata? mcpMetadata = null,
            CopilotSharedCapabilityPresentation? presentation = null,
            CopilotSharedCapabilityExecutionRoute executionRoute =
                CopilotSharedCapabilityExecutionRoute.Unspecified)
        {
            approvalMetadata ??= CopilotSharedCapabilityApprovalMetadata.None;
            ValidateDefinitionMetadata(
                id,
                agentDescription,
                mcpDescription,
                approvalMetadata,
                mcpMetadata,
                presentation,
                executionRoute,
                agentToolFactory,
                mcpHandlerFactory);
            return new CopilotSharedCapabilityDefinition(
                id,
                agentToolName,
                mcpToolName,
                inputSchema,
                inputSchema,
                CreateAgentCapability(
                    safetyClass,
                    idempotency,
                    executionTimeout,
                    auditArgumentMode,
                    evidenceMode),
                agentDescription,
                mcpDescription,
                approvalMetadata,
                mcpMetadata!,
                presentation!,
                executionRoute,
                agentToolFactory,
                mcpHandlerFactory);
        }

        private static CopilotSharedCapabilityDefinition SurfaceSpecific(
            string id,
            string agentToolName,
            string mcpToolName,
            CopilotToolInputSchema agentInputSchema,
            CopilotToolInputSchema mcpInputSchema,
            string difference,
            Func<ICopilotApplicationCapabilityInvoker, ICopilotTool> agentToolFactory,
            Func<CopilotMcpToolDispatcher, CopilotScopedMcpToolHandler> mcpHandlerFactory,
            CopilotSharedCapabilitySafetyClass safetyClass = CopilotSharedCapabilitySafetyClass.ReadOnly,
            CopilotToolIdempotency idempotency = CopilotToolIdempotency.Idempotent,
            TimeSpan? executionTimeout = null,
            CopilotToolAuditArgumentMode auditArgumentMode = CopilotToolAuditArgumentMode.RedactedSummary,
            CopilotToolEvidenceMode evidenceMode = CopilotToolEvidenceMode.Summary,
            string agentDescription = "",
            string mcpDescription = "",
            CopilotSharedCapabilityApprovalMetadata? approvalMetadata = null,
            CopilotSharedCapabilityMcpMetadata? mcpMetadata = null,
            CopilotSharedCapabilityPresentation? presentation = null,
            CopilotSharedCapabilityExecutionRoute executionRoute =
                CopilotSharedCapabilityExecutionRoute.Unspecified)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(difference);
            approvalMetadata ??= CopilotSharedCapabilityApprovalMetadata.None;
            ValidateDefinitionMetadata(
                id,
                agentDescription,
                mcpDescription,
                approvalMetadata,
                mcpMetadata,
                presentation,
                executionRoute,
                agentToolFactory,
                mcpHandlerFactory);
            return new CopilotSharedCapabilityDefinition(
                id,
                agentToolName,
                mcpToolName,
                agentInputSchema,
                mcpInputSchema,
                CreateAgentCapability(
                    safetyClass,
                    idempotency,
                    executionTimeout,
                    auditArgumentMode,
                    evidenceMode),
                agentDescription,
                mcpDescription,
                approvalMetadata,
                mcpMetadata!,
                presentation!,
                executionRoute,
                agentToolFactory,
                mcpHandlerFactory,
                difference);
        }

        private static void ValidateDefinitionMetadata(
            string id,
            string agentDescription,
            string mcpDescription,
            CopilotSharedCapabilityApprovalMetadata approvalMetadata,
            CopilotSharedCapabilityMcpMetadata? mcpMetadata,
            CopilotSharedCapabilityPresentation? presentation,
            CopilotSharedCapabilityExecutionRoute executionRoute,
            Func<ICopilotApplicationCapabilityInvoker, ICopilotTool> agentToolFactory,
            Func<CopilotMcpToolDispatcher, CopilotScopedMcpToolHandler> mcpHandlerFactory)
        {
            if (string.IsNullOrWhiteSpace(agentDescription)
                || string.IsNullOrWhiteSpace(mcpDescription))
            {
                throw new ArgumentException(
                    $"Shared capability '{id}' must declare both Agent and MCP descriptions.");
            }
            if (!approvalMetadata.IsValid)
            {
                throw new ArgumentException(
                    $"Shared capability '{id}' must declare valid approval metadata.");
            }
            if (mcpMetadata?.IsValid != true)
            {
                throw new ArgumentException(
                    $"Shared capability '{id}' must declare valid MCP descriptor metadata.");
            }
            if (presentation?.IsValid != true)
            {
                throw new ArgumentException(
                    $"Shared capability '{id}' must declare valid Agent trace presentation metadata.");
            }
            if (!Enum.IsDefined(executionRoute)
                || executionRoute == CopilotSharedCapabilityExecutionRoute.Unspecified)
                throw new ArgumentOutOfRangeException(nameof(executionRoute));
            ArgumentNullException.ThrowIfNull(agentToolFactory);
            ArgumentNullException.ThrowIfNull(mcpHandlerFactory);
        }

        private static CopilotSharedCapabilityPresentation Presentation(
            string traceCategory,
            string runningLabel,
            string completedLabel,
            string successSummary,
            bool isSearch = false) =>
            new(traceCategory, runningLabel, completedLabel, successSummary, isSearch);

        private static CopilotSharedCapabilityMcpMetadata McpMetadata(
            string category,
            string usageHint,
            bool destructiveHint = false,
            bool openWorldHint = false) =>
            new(category, usageHint, destructiveHint, openWorldHint);

        private static CopilotSharedCapabilityApprovalMetadata ManualApproval(
            string reversibilitySummary) =>
            Approval(CopilotApprovalReversibility.ManualOnly, reversibilitySummary);

        private static CopilotSharedCapabilityApprovalMetadata Approval(
            CopilotApprovalReversibility reversibility,
            string reversibilitySummary) =>
            new(reversibility, reversibilitySummary);

        private static CopilotToolCapabilityDescriptor CreateAgentCapability(
            CopilotSharedCapabilitySafetyClass safetyClass,
            CopilotToolIdempotency idempotency,
            TimeSpan? executionTimeout,
            CopilotToolAuditArgumentMode auditArgumentMode,
            CopilotToolEvidenceMode evidenceMode)
        {
            var (access, riskLevel, approvalMode) = safetyClass switch
            {
                CopilotSharedCapabilitySafetyClass.ReadOnly =>
                    (CopilotToolAccess.ReadOnly, CopilotToolRiskLevel.Low, CopilotToolApprovalMode.Never),
                CopilotSharedCapabilitySafetyClass.LowRiskWrite =>
                    (CopilotToolAccess.Write, CopilotToolRiskLevel.Low, CopilotToolApprovalMode.Never),
                CopilotSharedCapabilitySafetyClass.ApprovalRequiredWrite =>
                    (CopilotToolAccess.Write, CopilotToolRiskLevel.High, CopilotToolApprovalMode.Always),
                _ => throw new ArgumentOutOfRangeException(nameof(safetyClass)),
            };
            return new CopilotToolCapabilityDescriptor
            {
                Access = access,
                RiskLevel = riskLevel,
                ApprovalMode = approvalMode,
                Idempotency = idempotency,
                ConcurrencyMode = access == CopilotToolAccess.ReadOnly
                    && idempotency == CopilotToolIdempotency.Idempotent
                        ? CopilotToolConcurrencyMode.SharedRead
                        : CopilotToolConcurrencyMode.Exclusive,
                ExecutionTimeout = executionTimeout
                    ?? CopilotToolCapabilityDescriptor.DefaultExecutionTimeout,
                AuditArgumentMode = auditArgumentMode,
                EvidenceMode = access == CopilotToolAccess.ReadOnly
                    ? evidenceMode
                    : CopilotToolEvidenceMode.None,
                AllowsTemporaryFullAccess = false,
            };
        }
    }
}
