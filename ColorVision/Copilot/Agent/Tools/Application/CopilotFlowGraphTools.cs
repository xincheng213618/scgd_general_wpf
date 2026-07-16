using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotInspectFlowGraphTool : CopilotFlowReadToolBase
    {
        private static readonly CopilotToolInputSchema Schema = CreateSchema(new Dictionary<string, object?>
        {
            ["node_id"] = new { type = "string", description = "Optional stable node instance id or node id to focus." },
            ["include_properties"] = new { type = "boolean", description = "Include redacted node property values. Defaults to false." },
            ["max_nodes"] = new { type = "integer", minimum = 1, maximum = 200, description = "Maximum nodes to return. Defaults to 80." },
        });

        public CopilotInspectFlowGraphTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectFlowGraphTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base("InspectFlowGraph", "get_flow_graph", "Inspect the active ColorVision flow as a structured graph with a revision, stable node ids, exact runtime type keys, ports, and edges. Use this instead of reading the binary .stn file.", Schema, capabilityInvoker)
        {
        }
    }

    public sealed class CopilotSearchFlowNodeCatalogTool : CopilotFlowReadToolBase
    {
        private static readonly CopilotToolInputSchema Schema = CreateSchema(new Dictionary<string, object?>
        {
            ["query"] = new { type = "string", description = "Optional title, category, runtime type, node type, or device-code search text such as 相机 or camera." },
            ["max_results"] = new { type = "integer", minimum = 1, maximum = 100, description = "Maximum matching node types to return. Defaults to 30." },
        });

        public CopilotSearchFlowNodeCatalogTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotSearchFlowNodeCatalogTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base("SearchFlowNodeCatalog", "get_flow_node_catalog", "Search the node types loaded by the active Flow editor. Returns exact type keys and writable property schemas. Search first and never guess which camera node the user means.", Schema, capabilityInvoker)
        {
        }
    }

    public sealed class CopilotPreviewFlowPatchTool : CopilotFlowReadToolBase
    {
        public CopilotPreviewFlowPatchTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotPreviewFlowPatchTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base("PreviewFlowPatch", "preview_flow_patch", "Validate exactly one add_node, set_property, or connect operation against the active Flow graph revision. Use exact ids, port ids, and type keys from the read tools. This never edits, saves, or runs the flow.", CopilotFlowPatchSchema.Value, capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowMutation(request);
    }

    public sealed class CopilotApplyFlowPatchTool : ICopilotFrameworkApprovedTool, ICopilotAgentDrivenTool, ICopilotFrameworkApprovalPresentation
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;

        public CopilotApplyFlowPatchTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotApplyFlowPatchTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name => "ApplyFlowPatch";

        public string Description => "Apply one previously previewed add_node, set_property, or connect operation to the active Flow editor. Rechecks the revision, requires explicit approval, and never saves or runs the flow.";

        public CopilotToolInputSchema InputSchema => CopilotFlowPatchSchema.Value;

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
            ExecutionTimeout = TimeSpan.FromSeconds(30),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
            EvidenceMode = CopilotToolEvidenceMode.None,
        };

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowMutation(request);

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgent, cancellationToken);
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, CopilotApplicationCapabilityCaller.InAppAgentFrameworkApproved, cancellationToken);
        }

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
        {
            var arguments = toolInput?.Arguments ?? new Dictionary<string, object?>();
            arguments.TryGetValue("operation", out var operation);
            var detail = operation?.ToString() switch
            {
                "add_node" => $"类型：{GetArgument(arguments, "type_key")}\n位置：({GetArgument(arguments, "left")}, {GetArgument(arguments, "top")})",
                "set_property" => $"节点：{GetArgument(arguments, "node_id")}\n属性：{GetArgument(arguments, "property_name")}\n新值：{CopilotMcpAuditLogger.RedactArgument(GetArgument(arguments, "property_name")?.ToString() ?? string.Empty, GetArgument(arguments, "value")?.ToString())}",
                "connect" => $"连接：{GetArgument(arguments, "source_node_id")}/{GetArgument(arguments, "source_port_id")} → {GetArgument(arguments, "target_node_id")}/{GetArgument(arguments, "target_port_id")}",
                _ => "未知操作",
            };
            return new CopilotToolApprovalPresentation("应用 Flow 图修改", $"操作：{operation}\n{detail}\n不会自动保存或运行流程。");
        }

        private async Task<CopilotToolResult> ExecuteCoreAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CopilotApplicationCapabilityCaller caller, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var arguments = CopilotFlowReadToolBase.ToJsonArguments(toolInput);
            var result = await _capabilityInvoker.InvokeAsync("apply_flow_patch", arguments, caller, cancellationToken);
            var waitingForApproval = result.IsApprovalRequired;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success || waitingForApproval,
                Summary = waitingForApproval ? "Flow patch is waiting for explicit approval." : result.Success ? "Flow patch applied." : "Flow patch failed.",
                Content = result.Content,
                ErrorMessage = result.Success || waitingForApproval ? string.Empty : result.Content,
                Approval = result.Approval,
            };
        }

        private static object? GetArgument(IReadOnlyDictionary<string, object?> arguments, string name)
        {
            return arguments.TryGetValue(name, out var value) ? value : null;
        }
    }

    internal static class CopilotFlowPatchSchema
    {
        public static CopilotToolInputSchema Value { get; } = CopilotFlowReadToolBase.CreateSchema(new Dictionary<string, object?>
        {
            ["operation"] = new { type = "string", @enum = new[] { "add_node", "set_property", "connect" }, description = "Exactly one bounded Flow graph operation." },
            ["expected_revision"] = new { type = "string", description = "Current graph revision from InspectFlowGraph." },
            ["type_key"] = new { type = "string", description = "add_node: exact type key from SearchFlowNodeCatalog." },
            ["left"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas X coordinate." },
            ["top"] = new { type = "integer", minimum = -100000, maximum = 100000, description = "add_node: canvas Y coordinate." },
            ["node_id"] = new { type = "string", description = "set_property: stable node instance id." },
            ["property_name"] = new { type = "string", description = "set_property: exact writable propertyName from the node catalog." },
            ["value"] = new { type = "string", description = "set_property: new value accepted by the existing STNodeProperty descriptor; an empty string is valid." },
            ["source_node_id"] = new { type = "string", description = "connect: stable source node instance id." },
            ["source_port_id"] = new { type = "string", description = "connect: source output port id such as out:0." },
            ["target_node_id"] = new { type = "string", description = "connect: stable target node instance id." },
            ["target_port_id"] = new { type = "string", description = "connect: target input port id such as in:0." },
        }, "operation", "expected_revision");
    }

    public abstract class CopilotFlowReadToolBase : ICopilotAgentDrivenTool
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;
        private readonly string _mcpToolName;

        protected CopilotFlowReadToolBase(
            string name,
            string mcpToolName,
            string description,
            CopilotToolInputSchema inputSchema,
            ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            Name = name;
            _mcpToolName = mcpToolName;
            Description = description;
            InputSchema = inputSchema;
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name { get; }

        public string Description { get; }

        public CopilotToolInputSchema InputSchema { get; }

        public CopilotToolCapabilityDescriptor Capability { get; } = CopilotToolCapabilityDescriptor.ReadOnly(
            executionTimeout: TimeSpan.FromSeconds(15),
            auditArgumentMode: CopilotToolAuditArgumentMode.NamesOnly,
            evidenceMode: CopilotToolEvidenceMode.Summary);

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public virtual bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowGraph(request);

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var result = await _capabilityInvoker.InvokeAsync(
                _mcpToolName,
                ToJsonArguments(toolInput),
                CopilotApplicationCapabilityCaller.InAppAgent,
                cancellationToken);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = result.Success ? $"{Name} completed." : $"{Name} failed.",
                Content = result.Content,
                ErrorMessage = result.Success ? string.Empty : result.Content,
            };
        }

        public static CopilotToolInputSchema CreateSchema(IReadOnlyDictionary<string, object?> properties, params string[] required)
        {
            return CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false,
            }));
        }

        public static Dictionary<string, JsonElement> ToJsonArguments(CopilotAgentToolInput? toolInput)
        {
            return (toolInput?.Arguments ?? new Dictionary<string, object?>())
                .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.OrdinalIgnoreCase);
        }
    }
}
