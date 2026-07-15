using ColorVision.Copilot;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ColorVision.UI.Tests;

public class CopilotFlowGraphToolTests
{
    [Fact]
    public void RegistryExposesFourBoundedFlowAuthoringToolsWithoutLegacyActionTools()
    {
        var tools = CopilotToolRegistry.CreateDefault().FindTools(new CopilotAgentRequest
        {
            UserText = "在流程里添加一个相机节点并连接到算法节点",
            Mode = CopilotAgentMode.Auto,
        });

        Assert.Contains(tools, tool => tool.Name == "InspectFlowGraph");
        Assert.Contains(tools, tool => tool.Name == "SearchFlowNodeCatalog");
        Assert.Contains(tools, tool => tool.Name == "PreviewFlowPatch");
        var apply = Assert.Single(tools, tool => tool.Name == "ApplyFlowPatch");
        Assert.DoesNotContain(tools, tool => tool.Name is "PreviewAddFlowNode" or "AddFlowNode");
        Assert.Equal(CopilotToolAccess.Write, apply.Capability.Access);
        Assert.Equal(CopilotToolApprovalMode.Always, apply.Capability.ApprovalMode);
        Assert.Equal(CopilotToolEvidenceMode.None, apply.Capability.EvidenceMode);
    }

    [Fact]
    public void ActiveFlowContextKeepsReadToolsButRequiresCurrentMutationIntentForWriteTools()
    {
        var registry = CopilotToolRegistry.CreateDefault();
        var inspectTools = registry.FindTools(new CopilotAgentRequest
        {
            UserText = "这个节点是什么？",
            Mode = CopilotAgentMode.Auto,
            ContextItems = [new CopilotContextItem { Id = "surface:flow", Title = "Flow context · Default" }],
        });

        Assert.Contains(inspectTools, tool => tool.Name == "InspectFlowGraph");
        Assert.Contains(inspectTools, tool => tool.Name == "SearchFlowNodeCatalog");
        Assert.DoesNotContain(inspectTools, tool => tool.Name is "PreviewFlowPatch" or "ApplyFlowPatch");

        var mutateTools = registry.FindTools(new CopilotAgentRequest
        {
            UserText = "添加一个相机节点",
            Mode = CopilotAgentMode.Auto,
            ContextItems = [new CopilotContextItem { Id = "surface:flow", Title = "Flow context · Default" }],
        });
        Assert.Contains(mutateTools, tool => tool.Name == "PreviewFlowPatch");
        Assert.Contains(mutateTools, tool => tool.Name == "ApplyFlowPatch");

        var adviceTools = registry.FindTools(new CopilotAgentRequest
        {
            UserText = "如何添加一个相机节点？",
            Mode = CopilotAgentMode.Auto,
            ContextItems = [new CopilotContextItem { Id = "surface:flow", Title = "Flow context · Default" }],
        });
        Assert.Contains(adviceTools, tool => tool.Name == "InspectFlowGraph");
        Assert.DoesNotContain(adviceTools, tool => tool.Name is "PreviewFlowPatch" or "ApplyFlowPatch");
    }

    [Fact]
    public void FlowPatchSchemaUsesOneOperationEnumAndRevisionInsteadOfSeparateTools()
    {
        var preview = new CopilotPreviewFlowPatchTool();
        var apply = new CopilotApplyFlowPatchTool();
        var previewSchema = preview.InputSchema.JsonSchema;
        var applySchema = apply.InputSchema.JsonSchema;

        Assert.Equal(previewSchema.GetRawText(), applySchema.GetRawText());
        var required = previewSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(new[] { "operation", "expected_revision" }, required);
        var operations = previewSchema.GetProperty("properties").GetProperty("operation").GetProperty("enum")
            .EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Equal(new[] { "add_node", "set_property", "connect" }, operations);
        Assert.False(previewSchema.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void FlowPatchSchemaRejectsUnknownOperationAndOutOfRangePosition()
    {
        var schema = new CopilotPreviewFlowPatchTool().InputSchema;

        Assert.False(schema.TryBind(new Dictionary<string, object?>
        {
            ["operation"] = "delete_node",
            ["expected_revision"] = "revision-1",
        }, out _, out var operationError));
        Assert.Contains("operation", operationError, StringComparison.OrdinalIgnoreCase);

        Assert.False(schema.TryBind(new Dictionary<string, object?>
        {
            ["operation"] = "add_node",
            ["expected_revision"] = "revision-1",
            ["type_key"] = "FlowEngineLib.dll|FlowEngineLib.CVCameraNode",
            ["left"] = 100001,
            ["top"] = 0,
        }, out _, out var positionError));
        Assert.Contains("left", positionError, StringComparison.OrdinalIgnoreCase);
    }
}
