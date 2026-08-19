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
        public CopilotInspectFlowGraphTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotInspectFlowGraphTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(CopilotSharedCapabilityCatalog.FlowGraph.AgentToolName, CopilotSharedCapabilityCatalog.FlowGraph.McpToolName, capabilityInvoker)
        {
        }
    }

    public sealed class CopilotSearchFlowNodeCatalogTool : CopilotFlowReadToolBase
    {
        public CopilotSearchFlowNodeCatalogTool()
            : this(CopilotApplicationCapabilityInvokerFactory.CreateDefault())
        {
        }

        public CopilotSearchFlowNodeCatalogTool(ICopilotApplicationCapabilityInvoker capabilityInvoker)
            : base(CopilotSharedCapabilityCatalog.FlowNodeCatalog.AgentToolName, CopilotSharedCapabilityCatalog.FlowNodeCatalog.McpToolName, capabilityInvoker)
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
            : base(CopilotSharedCapabilityCatalog.PreviewFlowPatch.AgentToolName, CopilotSharedCapabilityCatalog.PreviewFlowPatch.McpToolName, capabilityInvoker)
        {
        }

        public override bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowMutation(request);
    }

    public sealed class CopilotApplyFlowPatchTool : ICopilotFrameworkApprovedTool, ICopilotAgentDrivenTool, ICopilotFrameworkApprovalPresentation, ICopilotApplicationCapabilityClient
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

        public string Name => CopilotSharedCapabilityCatalog.ApplyFlowPatch.AgentToolName;

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description => CopilotSharedCapabilityCatalog.ApplyFlowPatch.AgentDescription;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.ApplyFlowPatch.AgentInputSchema;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.ApplyFlowPatch.AgentCapability;

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowMutation(request);

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: false, cancellationToken);
        }

        Task<CopilotToolResult> ICopilotFrameworkApprovedTool.ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return ExecuteCoreAsync(request, toolInput, frameworkApprovalGranted: true, cancellationToken);
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

        private async Task<CopilotToolResult> ExecuteCoreAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, bool frameworkApprovalGranted, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var arguments = CopilotFlowReadToolBase.ToJsonArguments(toolInput);
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                CopilotSharedCapabilityCatalog.ApplyFlowPatch.McpToolName,
                arguments,
                request,
                frameworkApprovalGranted,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                "Flow patch applied.",
                "Flow patch failed.",
                "Flow patch is waiting for explicit approval.");
        }

        private static object? GetArgument(IReadOnlyDictionary<string, object?> arguments, string name)
        {
            return arguments.TryGetValue(name, out var value) ? value : null;
        }
    }

    public abstract class CopilotFlowReadToolBase : ICopilotAgentDrivenTool, ICopilotApplicationCapabilityClient
    {
        private readonly ICopilotApplicationCapabilityInvoker _capabilityInvoker;
        private readonly string _mcpToolName;

        protected CopilotFlowReadToolBase(
            string name,
            string mcpToolName,
            ICopilotApplicationCapabilityInvoker capabilityInvoker)
        {
            var definition = CopilotSharedCapabilityCatalog.ResolveBinding(name, mcpToolName);
            Name = name;
            _mcpToolName = mcpToolName;
            Capability = definition.AgentCapability;
            Description = definition.AgentDescription;
            InputSchema = definition.AgentInputSchema;
            _capabilityInvoker = capabilityInvoker ?? throw new ArgumentNullException(nameof(capabilityInvoker));
        }

        public string Name { get; }

        public ICopilotApplicationCapabilityInvoker ApplicationCapabilityInvoker => _capabilityInvoker;

        public string Description { get; }

        public CopilotToolInputSchema InputSchema { get; }

        public CopilotToolCapabilityDescriptor Capability { get; }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public virtual bool IsAvailable(CopilotAgentRequest request) => CopilotToolIntentPolicy.NeedsFlowGraph(request);

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var result = await CopilotApplicationCapabilityInvocation.InvokeAsync(
                _capabilityInvoker,
                _mcpToolName,
                ToJsonArguments(toolInput),
                request,
                frameworkApprovalGranted: false,
                cancellationToken);
            return CopilotApplicationCapabilityInvocation.ToToolResult(
                result,
                Name,
                $"{Name} completed.",
                $"{Name} failed.");
        }

        public static Dictionary<string, JsonElement> ToJsonArguments(CopilotAgentToolInput? toolInput)
        {
            return (toolInput?.Arguments ?? new Dictionary<string, object?>())
                .ToDictionary(pair => pair.Key, pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.OrdinalIgnoreCase);
        }
    }
}
