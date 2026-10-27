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
        public const string ReadAlgorithmHelp = "ReadAlgorithmHelp";
        public const string SearchFiles = "SearchFiles";
        public const string GrepText = "GrepText";
        public const string ReadLocalFile = "ReadLocalFile";
        public const string ListDirectory = "ListDirectory";
        public const string GetRecentLog = "GetRecentLog";
        public const string InspectSavedTemplate = "InspectSavedTemplate";
        public const string InspectTemplateType = "InspectTemplateType";
        public const string InspectFlowGraph = "InspectFlowGraph";
        public const string InspectDeviceServices = "InspectDeviceServices";
        public const string InspectLoadedBatchResults = "InspectLoadedBatchResults";
        public const string ValidateFlowGraph = "ValidateFlowGraph";
        public const string InspectFlowIncident = "InspectFlowIncident";
        public const string ListFlowIncidents = "ListFlowIncidents";
        public const string ListFlowRuns = "ListFlowRuns";
        public const string InspectRecordedFlowTemplate = "InspectRecordedFlowTemplate";
        public const string InspectFlowRunHistory = "InspectFlowRunHistory";
        public const string InspectFlowRunEvents = "InspectFlowRunEvents";
        public const string InspectFlowRunAttempts = "InspectFlowRunAttempts";
        public const string CompareRecordedFlowTemplates = "CompareRecordedFlowTemplates";
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
        private static CopilotToolInputSchema AlgorithmHelpInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["topic_id"] = new { type = "string", minLength = 1, maxLength = 128, pattern = "^[a-z0-9]+(?:[.-][a-z0-9]+)*$", description = "Exact algorithmHelpTopicId returned by SearchFlowNodeCatalog. Never infer a topic id from a node title or type name." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Optional opaque next_cursor from the preceding page for the same topic. Pass it unchanged without trimming; omit it to start reading." },
                ["max_characters"] = new { type = "integer", minimum = 256, maximum = 12000, @default = 6000, description = "Maximum Markdown source characters in one page. Defaults to 6000." },
            }, "topic_id");
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
                ["node_id"] = new { type = "string", minLength = 1, maxLength = 256, description = "Optional exact stable node instance id or node id to focus. In the edges section, returns edges with either endpoint matching this node, including cross-page connections." },
                ["include_properties"] = new { type = "boolean", description = "Include redacted node property values. Defaults to false. Required true for the properties detail stream; ports remain readable without it." },
                ["max_nodes"] = new { type = "integer", minimum = 1, maximum = 200, @default = 80, description = "Maximum node rows in one nodes page. Defaults to 80 and may change between pages." },
                ["section"] = new { type = "string", @enum = new[] { "nodes", "edges", "ports", "properties" }, @default = "nodes", description = "Independent stream to read. nodes provides bounded overviews with internal edges; edges pages all matching edges; ports and properties page complete detail rows. properties requires include_properties=true. Defaults to nodes." },
                ["max_edges"] = new { type = "integer", minimum = 1, maximum = 200, @default = 80, description = "Maximum edge rows in an edges page or internal edges attached to a nodes page. Defaults to 80 and may change between pages. Attached-edge omissions are reported separately in coverage.edges_truncated." },
                ["max_items"] = new { type = "integer", minimum = 1, maximum = 200, @default = 80, description = "Maximum detail rows in a ports or properties page. Defaults to 80 and may change between pages. Node overview details are separately bounded; use the corresponding detail stream to read all rows." },
                ["expected_revision"] = new { type = "string", minLength = 64, maxLength = 64, pattern = "^[0-9a-fA-F]{64}$", description = "Optional exact revision returned by a prior graph page, passed unchanged without trimming. Supply it together with expected_source_instance_id on the first page of each independent stream to bind evidence to the previously inspected graph and host." },
                ["expected_source_instance_id"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Optional exact sourceInstanceId returned by a prior graph page, passed unchanged without trimming. Supply it with expected_revision on the first page of each independent stream; identical revisions can belong to different active editor instances." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Optional opaque next_cursor from the preceding page for the same revision, source instance, section, node_id and include_properties. Pass unchanged without trimming. Page limits may change; changing a bound filter requires restarting." },
            });
        private static CopilotToolInputSchema FlowGraphValidationInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["expected_revision"] = new { type = "string", minLength = 64, maxLength = 64, pattern = "^[0-9a-fA-F]{64}$", description = "Exact current revision returned by InspectFlowGraph/get_flow_graph. Pass it unchanged without trimming; this must identify the active graph being checked." },
            }, "expected_revision");
        private static CopilotToolInputSchema DeviceServicesInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["device_ref"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Optional exact member-object reference from a preceding device directory. Focus requires expected_source_instance_id and cannot be combined with cursor. Never infer it from a name, code, resource id, current selection, or a previous host." },
                ["expected_source_instance_id"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Host instance identifier returned by the directory. Required with device_ref; optional for list requests to pin the current owner. Pass without trimming." },
                ["max_devices"] = new { type = "integer", minimum = 1, maximum = 100, @default = 40, description = "Maximum list rows per page, default 40. May change between pages. Focus returns at most one row." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Opaque next_cursor from a directory page; pass unchanged without trimming. Cannot be combined with device_ref. Host or member-object inventory changes require restarting." },
            });
        private static CopilotToolInputSchema LoadedBatchResultsInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["source_ref"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Optional exact registered source reference from the directory or the complete Loaded batch result source pair in automatic measurement context. Always requires expected_source_instance_id. Omit for the directory of loaded batch-detail sources; never substitute a batch id, page name, or current selection." },
                ["expected_source_instance_id"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Registry owner identifier from the directory's source_instance_id or the complete automatic measurement context pair. Required whenever source_ref is supplied; optional to pin a directory owner. Pass without trimming. Snapshot references are revalidated at call time." },
                ["result_ref"] = new { type = "string", minLength = 32, maxLength = 32, pattern = "^[0-9a-fA-F]{32}$", description = "Optional exact loaded row-object reference from a source page. Requires source_ref and its owner; cannot be combined with cursor. Not a database id, result code, template name, or selected-row alias." },
                ["max_items"] = new { type = "integer", minimum = 1, maximum = 100, @default = 40, description = "Maximum directory or result rows per page, default 40. May change between pages. An exact result focus returns at most one row." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Opaque next_cursor from the preceding directory or source-results page. Pass unchanged without trimming for the same owner, mode and source. Not allowed with result_ref. Registration, batch, or row-object membership changes require restarting." },
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
        private static CopilotToolInputSchema FlowRunsInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["flow_key"] = new { type = "string", minLength = 1, maxLength = 256, description = "Optional complete recorded FlowKey from the user or a preceding run list. Matches ordinally after Unicode whitespace trimming; no name, prefix, template id, or legacy fallback. Omit to list all retained runs." },
                ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, @default = 20, description = "Maximum retained runs in this page; may change between pages. Defaults to 20." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Opaque next_cursor from the preceding page. Pass unchanged for the same canonical flow_key filter; never invent, trim, or edit it." },
            });
        private static CopilotToolInputSchema RecordedFlowTemplateInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact retained run id supplied by the user, InspectFlowIncident, or ListFlowRuns. Never infer it from the active template or incident id." },
                ["node_id"] = new { type = "string", minLength = 36, maxLength = 36, pattern = "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", description = "Optional exact nonempty node GUID in D format from recorded evidence. Focuses this node and its saved direct neighbors, without fuzzy name matching." },
                ["max_nodes"] = new { type = "integer", minimum = 1, maximum = 200, @default = 80, description = "Maximum saved nodes in the returned prefix or focused neighborhood. Defaults to 80." },
            }, "run_record_id");
        private static CopilotToolInputSchema FlowRunHistoryInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact positive retained run id supplied by the user, InspectFlowIncident, or ListFlowRuns. Never infer it from the current canvas or an incident id." },
                ["node_id"] = new { type = "string", minLength = 1, maxLength = 128, description = "Optional exact recorded node id. Pass it unchanged; arbitrary nonempty node identifiers are supported without GUID or fuzzy-name assumptions." },
                ["max_runs"] = new { type = "integer", minimum = 1, maximum = 50, @default = 10, description = "Maximum previous run rows to inspect, ordered by run id descending below the target run id. Defaults to 10." },
            },
            "run_record_id");
        private static CopilotToolInputSchema FlowRunEventsInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact retained run id supplied by ListFlowRuns, InspectFlowIncident, InspectFlowRunHistory, or the user. Never infer it from an incident id, template name, or current canvas." },
                ["max_events"] = new { type = "integer", minimum = 1, maximum = 100, @default = 40, description = "Maximum recorded events in one page. Defaults to 40. The first page selects the latest window; continuation selects earlier events." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Optional opaque next_cursor from the preceding page for the same run. Pass it unchanged without trimming; omit it to read the latest window." },
            }, "run_record_id");
        private static CopilotToolInputSchema FlowRunAttemptsInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact retained run id from ListFlowRuns, incident/history/event evidence, or the user. Never infer it from a template name, incident id, or current canvas." },
                ["node_id"] = new { type = "string", minLength = 1, maxLength = 128, description = "Optional exact recorded node id, including arbitrary non-GUID identifiers. Pass unchanged without trimming or fuzzy name matching; omit to read attempts of every recorded node in this run." },
                ["max_attempts"] = new { type = "integer", minimum = 1, maximum = 100, @default = 40, description = "Maximum attempts in this page. Defaults to 40; may change between pages. Starts with the latest attempt-id window, then continues to earlier ids." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Opaque next_cursor from the preceding page for the same run and exact node filter. Pass unchanged without trimming; omit for the latest window." },
            }, "run_record_id");
        private static CopilotToolInputSchema RecordedFlowTemplateComparisonInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["baseline_run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact baseline run id from ListFlowRuns, read history, or the user. Do not infer it from a template name or current canvas." },
                ["current_run_record_id"] = new { type = "integer", minimum = 1, maximum = int.MaxValue, description = "Exact current run id from ListFlowRuns, recorded evidence, or the user. Both retained templates must have compatible recorded flow identity." },
                ["max_changes"] = new { type = "integer", minimum = 1, maximum = 200, @default = 50, description = "Maximum total detail rows across node changes, added edges, and removed edges. Defaults to 50 and may change between pages. Whole bounded-template counts and global flags are returned separately." },
                ["cursor"] = new { type = "string", minLength = 1, maxLength = 4096, description = "Optional opaque next_cursor from the preceding comparison page. Pass it unchanged without trimming. Omit it to start a comparison." },
            }, "baseline_run_record_id", "current_run_record_id");
        private static CopilotToolInputSchema FlowNodeCatalogInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["query"] = new { type = "string", description = "Optional title, category, runtime type, node type, or device-code search text such as 相机 or camera." },
                ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, description = "Maximum matching node types to return. Defaults to 30." },
            });
        private static CopilotToolInputSchema FlowPatchInputSchema { get; } = CreateSchema(
            new Dictionary<string, object?>
            {
                ["operation"] = new { type = "string", @enum = new[] { "add_node", "set_property", "connect", "disconnect" }, description = "Exactly one bounded Flow graph operation. disconnect removes only the exact existing source-node/output-port/target-node/input-port connection." },
                ["expected_revision"] = new { type = "string", description = "Current graph revision from InspectFlowGraph. disconnect requires the exact 64-character hexadecimal revision." },
                ["type_key"] = new { type = "string", description = "add_node: exact type key from SearchFlowNodeCatalog." },
                ["left"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas X coordinate." },
                ["top"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas Y coordinate." },
                ["inherit_camera_defaults"] = new { type = "boolean", description = "add_node only: explicitly copy current camera acquisition settings for a catalog type with canInheritCameraDefaults. Defaults to false. Preview returns the effective settings and cameraDefaultsFingerprint." },
                ["camera_defaults_fingerprint"] = new { type = "string", description = "add_node with inherit_camera_defaults=true: copy the exact cameraDefaultsFingerprint from the preview into apply. Required when applying; changed camera settings require a new preview." },
                ["node_id"] = new { type = "string", description = "set_property: stable node instance id." },
                ["property_name"] = new { type = "string", description = "set_property: exact writable propertyName from the node catalog." },
                ["value"] = new { type = "string", description = "set_property: new value accepted by the existing STNodeProperty descriptor; an empty string is valid." },
                ["source_node_id"] = new { type = "string", description = "connect/disconnect: stable source node instance id. disconnect requires a nonempty GUID in D format from graph evidence, never a legacy node alias." },
                ["source_port_id"] = new { type = "string", description = "connect/disconnect: exact source output port id such as out:0." },
                ["target_node_id"] = new { type = "string", description = "connect/disconnect: stable target node instance id. disconnect requires a nonempty GUID in D format from graph evidence, never a legacy node alias." },
                ["target_port_id"] = new { type = "string", description = "connect/disconnect: exact target input port id such as in:0." },
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
        private static CopilotSharedCapabilityPresentation AlgorithmHelpPresentation { get; } =
            Presentation("file-read", "正在读取算法帮助", "读取了算法帮助", "已获得算法帮助正文。");
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
        private static CopilotSharedCapabilityPresentation FlowGraphValidationPresentation { get; } =
            Presentation("application", "正在校验流程图", "校验了流程图", "已获得宿主流程图校验结果。");
        private static CopilotSharedCapabilityPresentation DeviceServicesPresentation { get; } =
            Presentation("application", "正在读取设备服务", "读取了设备服务", "已获得已加载设备服务状态。");
        private static CopilotSharedCapabilityPresentation LoadedBatchResultsPresentation { get; } =
            Presentation("application", "正在读取已加载批次结果", "读取了已加载批次结果", "已获得已注册批次详情中的结果记录。");
        private static CopilotSharedCapabilityPresentation FlowIncidentPresentation { get; } =
            Presentation("application", "正在检查流程事故", "检查了流程事故", "流程事故检查完成。");
        private static CopilotSharedCapabilityPresentation FlowIncidentsPresentation { get; } =
            Presentation("application", "正在查询流程事故", "查询了流程事故", "已获得流程事故列表。", isSearch: true);
        private static CopilotSharedCapabilityPresentation FlowRunsPresentation { get; } =
            Presentation("application", "正在查询流程运行", "查询了流程运行", "已获得留存流程运行列表。", isSearch: true);
        private static CopilotSharedCapabilityPresentation FlowRunHistoryPresentation { get; } =
            Presentation("application", "正在检查历史流程运行", "检查了历史流程运行", "历史流程运行检查完成。");
        private static CopilotSharedCapabilityPresentation FlowRunEventsPresentation { get; } =
            Presentation("application", "正在读取流程运行事件", "读取了流程运行事件", "已获得流程运行事件。");
        private static CopilotSharedCapabilityPresentation FlowRunAttemptsPresentation { get; } =
            Presentation("application", "正在读取流程节点尝试", "读取了流程节点尝试", "已获得流程节点尝试记录。");
        private static CopilotSharedCapabilityPresentation RecordedFlowTemplateComparisonPresentation { get; } =
            Presentation("application", "正在比较留存流程模板", "比较了留存流程模板", "留存流程模板比较完成。");
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
        public static CopilotSharedCapabilityDefinition AlgorithmHelp { get; } =
            Shared(
                "docs.algorithm-help",
                CopilotSharedAgentToolNames.ReadAlgorithmHelp,
                "read_algorithm_help",
                AlgorithmHelpInputSchema,
                applicationCapabilities => new CopilotReadAlgorithmHelpTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.ReadAlgorithmHelpAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read the algorithm principles and parameter documentation delivered with this installation using the exact algorithmHelpTopicId returned by SearchFlowNodeCatalog. Returns bounded pages of the complete Markdown source as JSON, without executing diagrams or components or accessing the network. This documentation does not establish the current runtime configuration, measurement results, or acceptance. When has_more is true, pass next_cursor unchanged as cursor for the same topic; document changes invalidate continuation.",
                mcpDescription: "Read installed algorithm help Markdown using the exact algorithmHelpTopicId from get_flow_node_catalog. Required: topic_id. Optional: cursor unchanged for the same topic, max_characters (256-12000, default 6000). Returns complete JSON containing a bounded source page; follow next_cursor when has_more is true. Document changes invalidate continuation. No diagrams or components are executed and no network is used. Documentation is not evidence of current configuration, measurement, or acceptance.",
                mcpMetadata: McpMetadata("context", "Call get_flow_node_catalog first and copy its exact algorithmHelpTopicId to topic_id in read_algorithm_help. Follow has_more and next_cursor for the same topic to read the remaining Markdown source."),
                presentation: AlgorithmHelpPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
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
                agentDescription: "Inspect the active ColorVision flow as paged structured evidence with revision, sourceInstanceId, stable node ids, exact type keys, ports, and edges. The default nodes stream sorts InstanceId ordinally and returns bounded node overviews with detail counts and omission flags. Its attached internal edges are separately bounded by max_edges/coverage.edges_truncated; has_more covers only the selected stream. Independently read section=edges for all connections, section=ports for all port rows, or section=properties with include_properties=true for all parameter rows. Detail text remains bounded and reports truncation. Edges sort by source node/port then target node/port; detail streams contain no node overviews. An exact node_id focuses a node's rows or any incident edge, including cross-page connections. Complete structural graph evidence requires finishing nodes and edges; use ports/properties for omitted overview detail rows. On the first page of each additional stream, pass the prior revision and sourceInstanceId unchanged as expected_revision and expected_source_instance_id. Identical revisions can belong to different hosts. Follow next_cursor unchanged for the same section, focus and include_properties; source, revision or stream-key changes require restarting. max_nodes, max_edges and max_items (detail pages) may change. Inspect coverage and per-row text limits before claiming completeness. Use this instead of reading binary .stn files.",
                mcpDescription: "Return one bounded current-graph page as complete JSON. Optional section=nodes (default), edges, ports, or properties; max_nodes/max_edges/max_items (1-200, default 80), exact node_id, include_properties, expected_revision, expected_source_instance_id, opaque cursor. properties requires include_properties=true; ports does not. Nodes provide bounded overviews with detail counts/omission flags and internal edges capped by max_edges; has_more is independent of omitted attached details. edges independently pages all source/port/target/port-ordered connections; ports/properties page detail rows with max_items and no nodes. Parameter text remains bounded and marked. node_id focuses node details or either edge endpoint, including cross-page edges. Finish nodes+edges for structural graph evidence; use detail streams for omitted port/parameter rows. Bind every additional stream's first page using prior revision and sourceInstanceId unchanged as expected_revision and expected_source_instance_id. Follow next_cursor unchanged for the same revision, source instance, focus, include_properties and section; limits may change. Restart when bindings or stream keys change and inspect coverage.",
                mcpMetadata: McpMetadata("context", "Start get_flow_graph with section=nodes and follow has_more/next_cursor. Read section=edges independently for complete connections. Use section=ports or section=properties with include_properties=true for omitted overview details, limited by max_items. For each additional stream copy revision/sourceInstanceId into expected_revision/expected_source_instance_id; keep cursor unchanged and inspect row/text coverage."),
                presentation: FlowGraphPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowGraphValidation { get; } =
            Shared(
                "flow.validate",
                CopilotSharedAgentToolNames.ValidateFlowGraph,
                "validate_flow_graph",
                FlowGraphValidationInputSchema,
                applicationCapabilities => new CopilotValidateFlowGraphTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.ValidateFlowGraphAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Run the host's static validation of the entire active Flow graph using the exact expected_revision from InspectFlowGraph. Pass the revision unchanged. Returns complete JSON; valid=false is a completed validation result, not a tool failure. Missing editor, revision mismatch, or inability to check is a tool failure. This does not validate historical graphs, device readiness, every execution branch, algorithm parameter compliance, measurements, or acceptance; it never edits, saves, or runs the flow.",
                mcpDescription: "Return the host's static validation of the entire active Flow graph as complete JSON. Required: expected_revision unchanged from get_flow_graph. A successful call may report valid=false with a diagnostic. No active editor, a stale revision, or a failed check is a tool failure. This checks neither historical graphs nor device readiness, every branch, algorithm parameter compliance, measurements, or customer acceptance. No editing, saving, or execution occurs.",
                mcpMetadata: McpMetadata("context", "Call get_flow_graph first, then pass its exact revision unchanged as expected_revision to validate_flow_graph. Inspect valid and diagnostic in a successful result; invalid business graphs do not imply a failed tool call."),
                presentation: FlowGraphValidationPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition DeviceServices { get; } =
            Shared(
                "device.services",
                CopilotSharedAgentToolNames.InspectDeviceServices,
                "inspect_device_services",
                DeviceServicesInputSchema,
                applicationCapabilities => new CopilotInspectDeviceServicesTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectDeviceServicesAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read a directory of already-loaded device services, or focus an exact device_ref with its expected_source_instance_id from a preceding directory. No fuzzy name/code/resource-id or selected-device fallback is allowed. List pages use next_cursor unchanged; member-object or host changes require restarting, while cached state and metadata may change live. Returns complete JSON with metadata and supported built-in cached status only; unsupported plugins expose metadata with unknown state. Cached status is not an online/healthy, heartbeat-freshness, initialization, readiness, or measurement guarantee. Never creates a service/UI, reads configuration/credentials, polls a device, initializes, reconnects, or controls hardware.",
                mcpDescription: "Inspect already-loaded device-service metadata and supported built-in cached state as complete JSON. Omit device_ref for a directory page; max_devices is 1-100 (default 40), cursor is an unchanged opaque continuation bound to host/member inventory. To focus, pass the exact 32-hex device_ref plus expected_source_instance_id from the directory and omit cursor. List requests may also pin expected_source_instance_id. References identify member objects in one host, not names, codes, resource ids, or persistent identities. Unknown/unsupported/read-failed states remain explicit. No live polling, health inference, configuration, initialization, reconnect, control commands, or UI creation.",
                mcpMetadata: McpMetadata("context", "Call inspect_device_services without a focus to discover current references. Continue next_cursor unchanged for more directory rows, or pass one exact device_ref with the returned source_instance_id as expected_source_instance_id to inspect that member; never combine a focus with cursor."),
                presentation: DeviceServicesPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition LoadedBatchResults { get; } =
            Shared(
                "measurement.loaded-batch-results",
                CopilotSharedAgentToolNames.InspectLoadedBatchResults,
                "inspect_loaded_batch_results",
                LoadedBatchResultsInputSchema,
                applicationCapabilities => new CopilotInspectLoadedBatchResultsTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectLoadedBatchResultsAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read registered, already-loaded measurement batch details without a new database query. When automatic measurement context provides a complete Loaded batch result source JSON pair, pass its source_ref and expected_source_instance_id directly; otherwise discover the source directory first. Snapshot references are revalidated at call time; stale references require discovery, never a batch-id or current-selection fallback. Page image and algorithm master records, then optionally focus a returned result_ref without cursor; an automatic context's internal selected result id is not result_ref. Use opaque next_cursor unchanged for the same mode/source; page size may change. Sources and row objects are distinct from database ids, names, UI selection, and flow run ids. Raw nullable result codes do not establish customer PASS/FAIL, negative/unknown durations stay explicit, and recorded dates are not relabeled UTC. Never opens a page/file, probes file existence, parses result payloads, invokes result handlers, exports, or controls a device.",
                mcpDescription: "Read complete JSON for loaded batch-detail source directory, exact source result pages, or exact row focus. Use a complete Loaded batch result source JSON pair from automatic measurement context directly, or discover source_ref and source_instance_id from the directory; pass the latter as expected_source_instance_id. Optional result_ref requires that source and forbids cursor; internal selected result ids are not result_ref. Snapshot references are revalidated at call time; stale references require discovery without batch-id or current-selection fallback. max_items is 1-100 (default 40); cursor is unchanged opaque continuation for the same registry/mode/source and member inventory. Closing/unloading pages, replacing batches or row objects requires restarting. Only image/algorithm master metadata is read, including raw nullable result code, recorded duration and date kind. This is not SQL history, customer judgment, parsed measurement values, file availability, or current selection. No SN, path, message, payload, handler execution, UI navigation, or database query.",
                mcpMetadata: McpMetadata("context", "Use the complete Loaded batch result source JSON pair in automatic measurement context directly. Otherwise call inspect_loaded_batch_results without a source to discover registered loaded details, then pass one source_ref plus source_instance_id as expected_source_instance_id. References identify the captured source and are revalidated on calls; stale references require discovery, never inference from batch ids or selection. A returned result_ref may focus a row; internal selected result ids cannot. Follow next_cursor unchanged within its mode and exact source."),
                presentation: LoadedBatchResultsPresentation,
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
        public static CopilotSharedCapabilityDefinition FlowRuns { get; } =
            Shared(
                "flow.runs",
                CopilotSharedAgentToolNames.ListFlowRuns,
                "list_flow_runs",
                FlowRunsInputSchema,
                applicationCapabilities => new CopilotListFlowRunsTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.ListFlowRunsAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Discover retained local flow run ids even when no incident exists. Defaults to all retained runs, including legacy identity and unknown statuses, in run_record_id DESC order. Optional complete flow_key uses an ordinal Unicode-trimmed exact match. Use a returned run_record_id with InspectFlowRunHistory, InspectFlowRunEvents, or InspectRecordedFlowTemplate. Follow next_cursor unchanged for the same filter; pages query live data, not a frozen history or population success rate. This read-only tool never reads SNs, operators, payloads, or snapshot BLOBs, or controls a flow.",
                mcpDescription: "Return a bounded JSON page of retained local flow runs, including runs without incidents. Optional flow_key (1-256 characters) matches the full Unicode-trimmed recorded key ordinally; omitted means all retained runs including legacy/unknown records. max_results is 1-100 (default 20). Continue with opaque next_cursor unchanged for the same canonical filter; page size may change. Rows are run_record_id DESC, not execution start order. Returned exact ids feed inspect_flow_run_history, inspect_flow_run_events, and inspect_recorded_flow_template. This is live retained metadata, not all executions, a success rate, or an actual executed canvas; no private payload or flow control.",
                mcpMetadata: McpMetadata("context", "Start with list_flow_runs when no exact run id or incident exists. Use optional complete flow_key from recorded evidence, inspect returned run ids, and continue next_cursor unchanged for the same filter."),
                presentation: FlowRunsPresentation,
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
                agentDescription: "Inspect the exact template snapshot retained by a recorded ColorVision run. Use run_record_id from the user, InspectFlowIncident, or ListFlowRuns. Returns bounded saved node identities, property names, and connections only when saved port layout resolves them; never loads node types or returns property values. Optional node_id focuses a saved node and direct neighbors. Retained template data can omit unsaved canvas changes and does not establish the actual executed graph. Current InspectFlowGraph and its Revision are separate evidence.",
                mcpDescription: "Read the exact retained template linked by run_record_id, without loading nodes or an editor. Optional node_id is an exact GUID; max_nodes is 1-200 (default 80). Returns saved identities/property names and resolvable saved connections, never property values or raw bytes. Retained data may omit unsaved canvas changes and must not be treated as proof of the actual executed graph.",
                mcpMetadata: McpMetadata("context", "Use run_record_id from list_flow_runs, inspect_flow_incident, or the user. Call inspect_recorded_flow_template; optionally focus an exact saved node_id. Compare provenance before using current editor evidence."),
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
                mcpMetadata: McpMetadata("context", "Call inspect_flow_run_history with the exact run_record_id returned by list_flow_runs or inspect_flow_incident; optionally pass an exact unchanged node_id and max_runs up to 50."),
                presentation: FlowRunHistoryPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowRunEvents { get; } =
            Shared(
                "flow.run-events",
                CopilotSharedAgentToolNames.InspectFlowRunEvents,
                "inspect_flow_run_events",
                FlowRunEventsInputSchema,
                applicationCapabilities => new CopilotInspectFlowRunEventsTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectFlowRunEventsAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read bounded recorded execution events for an exact run_record_id from ListFlowRuns, InspectFlowIncident, InspectFlowRunHistory, or the user. Start with the latest event window, then follow next_cursor unchanged as cursor for the same run to read earlier windows. Each page is ordered by sequence and event id ascending. Returns complete JSON without payloads. Missing events do not establish success, measurement results, or customer judgment. This never controls devices, reruns a flow, or writes the journal.",
                mcpDescription: "Read exact retained run events as complete JSON. Required: run_record_id from list_flow_runs, incident/history evidence, or the user. Optional: max_events (1-100, default 40), opaque cursor unchanged for the same run. The first page is the latest window; has_more and next_cursor continue toward earlier events. Each page is ordered by sequence and event id ascending. No payloads are read; absent events do not establish success, measurements, or customer judgment. No device control, rerun, or database write occurs.",
                mcpMetadata: McpMetadata("context", "Pass an exact run_record_id from list_flow_runs, inspect_flow_incident, or inspect_flow_run_history to inspect_flow_run_events. Read events in each page's ascending sequence/id order; use next_cursor unchanged for the same run to retrieve earlier windows."),
                presentation: FlowRunEventsPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition FlowRunAttempts { get; } =
            Shared(
                "flow.run-attempts",
                CopilotSharedAgentToolNames.InspectFlowRunAttempts,
                "inspect_flow_run_attempts",
                FlowRunAttemptsInputSchema,
                applicationCapabilities => new CopilotInspectFlowRunAttemptsTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.InspectFlowRunAttemptsAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Read retained node attempts for an exact recorded run from ListFlowRuns, incidents, history, events, or the user. Optional exact node_id is passed unchanged and need not be a GUID; omit it for all nodes. Reads latest attempt-id windows, delivers each page by attempt_id ASC, then follows next_cursor unchanged toward earlier ids for the same run and node filter. Returns complete JSON with recorded outcomes, errors, and duration only when stored timing is valid. Repeated attempts are not deduplicated. Missing/unknown outcomes or errors do not establish run continuation, ContinueOnFail, a root cause, or customer judgment. No invocation ids, legacy ids, private payload, device control, or reruns.",
                mcpDescription: "Read one bounded JSON page of exact-run node attempts, including earlier detail omitted by incident/history overviews. Required run_record_id is a positive Int32 from recorded evidence or the user. Optional node_id (1-128 characters) matches exactly without trimming or GUID assumptions; omitted means all nodes. max_attempts is 1-100 (default 40). Pages select the latest attempt_id DESC window and deliver ASC; next_cursor unchanged reads an older window for the same run/node, with page size allowed to change. Null/unknown outcomes and invalid/missing durations remain unknown. Live retained pages do not freeze complete execution history or establish execution policy or root cause. No private payload or flow control.",
                mcpMetadata: McpMetadata("context", "Use an exact run id from list_flow_runs, incident/history/event evidence, or the user. Read inspect_flow_run_attempts, optionally with the exact unchanged node_id; follow next_cursor unchanged for the same scope to inspect older attempts and recorded errors."),
                presentation: FlowRunAttemptsPresentation,
                executionRoute: CopilotSharedCapabilityExecutionRoute.ApplicationCapabilityRuntime);
        public static CopilotSharedCapabilityDefinition RecordedFlowTemplateComparison { get; } =
            Shared(
                "flow.recorded-template-compare",
                CopilotSharedAgentToolNames.CompareRecordedFlowTemplates,
                "compare_recorded_flow_templates",
                RecordedFlowTemplateComparisonInputSchema,
                applicationCapabilities => new CopilotCompareRecordedFlowTemplatesTool(applicationCapabilities),
                dispatcher => (arguments, _, token) => dispatcher.CompareRecordedFlowTemplatesAsync(arguments, token),
                executionTimeout: TimeSpan.FromSeconds(15),
                auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
                agentDescription: "Compare the exact retained templates of baseline_run_record_id and current_run_record_id from read history or the user. Reads both references in one local journal snapshot and validates stored integrity. Reports bounded added/removed/modified nodes, changed saved field names, resolvable connection differences, and saved order/view flags without loading nodes or returning values or field fingerprints. Compatible recorded flow identity is required; legacy identity is explicitly weaker. Counts cover complete bounded templates, while max_changes limits detail rows and may change between pages. Follow next_cursor as the opaque cursor unchanged without trimming to continue details. Changed run, snapshot, SHA or source evidence requires a new comparison. Saved field names remain bounded independently of row pagination; finishing pages does not prove every field name was read. Retained differences are not proof of the actual executed canvas or a failure cause.",
                mcpDescription: "Compare exact retained templates of two recorded runs from one read-only journal snapshot. Required: baseline_run_record_id and current_run_record_id. Optional max_changes (1-200, default 50) limits combined node/edge detail rows and may change between pages; full bounded-template counts remain separate. Continue has_more results using next_cursor as the opaque cursor unchanged without trimming. Run, snapshot, SHA or source evidence changes require a new comparison. Saved field names remain independently bounded even after all detail rows are read. Returns saved field names and resolvable structural differences, never field values, field hashes, or raw bytes. Unknown legacy connections are not guessed, and retained templates do not establish actual execution or fault causality.",
                mcpMetadata: McpMetadata("context", "Read run history first, then pass explicit baseline_run_record_id and current_run_record_id to compare_recorded_flow_templates. Inspect counts, flags and coverage; when has_more is true, pass next_cursor unchanged as cursor to continue detail rows."),
                presentation: RecordedFlowTemplateComparisonPresentation,
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
                agentDescription: "Search node types loaded by the active Flow editor. Returns exact type keys, property schemas, descriptor types, declared enum member names, Flags declarations and standard boolean string candidates. Keep member spelling unchanged. These are declaration metadata, not a complete list of values accepted by custom descriptors or setters; verify a proposed value with PreviewFlowPatch. Empty candidates do not mean read-only. Search first and never guess which camera node the user means.",
                mcpDescription: "Search node types loaded by the active Flow editor. Returns runtime type keys, categories, device codes and property metadata including descriptorType, declaredEnumNames, isFlagsEnum and booleanStringCandidates. Preserve exact enum spelling; candidates are not a complete allowed-value list. Check isWritable and verify the proposed value with preview_flow_patch, especially for custom descriptors. Do not guess a camera node type.",
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
                agentDescription: "Validate exactly one add_node, set_property, connect, or disconnect operation against the active Flow graph revision. disconnect previews removal of one exact existing source-node/output-port/target-node/input-port connection; both node ids must be nonempty GUIDs in D format and expected_revision must be 64 hex characters from graph evidence. Use exact ids, port ids, and type keys from the read tools. For supported camera additions only, explicitly set inherit_camera_defaults=true to preview current acquisition settings and obtain cameraDefaultsFingerprint for apply. This never edits, saves, or runs the flow.",
                mcpDescription: "Validate one bounded Flow graph change without editing: add_node, set_property, connect, or disconnect. disconnect targets one exact existing connection using nonempty source/target node GUIDs in D format, exact output/input port ids, and the current 64-hex expected_revision. Use graph/catalog evidence. Camera defaults arguments apply only to add_node. No graph is changed, saved, or run.",
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
                agentDescription: "Apply one previously previewed add_node, set_property, connect, or disconnect operation to the active Flow editor after explicit approval. disconnect removes only the reviewed exact source-node/output-port/target-node/input-port connection, with nonempty GUID D instance ids and a 64-hex expected_revision. Rechecks the revision and existing connection and never saves or runs the flow. When inheriting camera settings for add_node, pass the preview's exact camera_defaults_fingerprint; changed settings are rejected before adding the node.",
                mcpDescription: "Apply one previously previewed add_node, set_property, connect, or disconnect change after explicit approval. disconnect removes only the exact reviewed connection using GUID D instance ids, output/input port ids and current 64-hex expected_revision. Rechecks graph state. Camera defaults arguments apply only to add_node. Never saves or runs the flow.",
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
                AlgorithmHelp,
                SearchFiles,
                GrepText,
                ReadAllowedFile,
                ListAllowedDirectory,
                RecentLog,
                SavedTemplateContext,
                TemplateTypeContext,
                FlowGraph,
                FlowGraphValidation,
                DeviceServices,
                LoadedBatchResults,
                FlowIncident,
                FlowIncidents,
                FlowRuns,
                FlowRunHistory,
                FlowRunEvents,
                FlowRunAttempts,
                RecordedFlowTemplateComparison,
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
