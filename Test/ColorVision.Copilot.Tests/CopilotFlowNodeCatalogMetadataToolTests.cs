using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowNodeCatalogMetadataToolTests
{
    [Fact]
    public async Task AgentAndMcpPublishIdenticalPropertyDeclarationMetadataAndExistingFields()
    {
        var calls = 0;
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot(),
            FlowNodeCatalogProvider = (query, maximum, _) =>
            {
                calls++;
                Assert.Equal("条件分支", query);
                Assert.Equal(3, maximum);
                return Task.FromResult<CopilotFlowNodeCatalogSnapshot?>(new()
                {
                    Query = query!, TotalMatches = 1,
                    NodeTypes = [new()
                    {
                        TypeKey = "FlowEngineLib.dll|ConditionBranchNode", RuntimeType = "FlowEngineLib.Logical.ConditionBranchNode",
                        Properties = [new()
                        {
                            PropertyName = "ExpectedStatus", DisplayName = "期望状态", Description = "Recorded declaration.",
                            DataType = "StatusTypeEnum", IsWritable = true, DescriptorType = "ST.Library.UI.NodeEditor.STNodePropertyDescriptor",
                            DeclaredEnumNames = ["Runing", "Completed"],
                        }, new()
                        {
                            PropertyName = "Enabled", DataType = "System.Boolean", IsWritable = true,
                            DescriptorType = "ST.Library.UI.NodeEditor.STNodePropertyDescriptor", BooleanStringCandidates = ["True", "False"],
                        }, new()
                        {
                            PropertyName = "Options", DataType = "Example.Flags", DescriptorType = "Example.CustomDescriptor",
                            IsFlagsEnum = true, DeclaredEnumNames = ["None", "First", "Alias"],
                        }],
                    }],
                });
            },
        });
        var input = new CopilotAgentToolInput { Arguments = new Dictionary<string, object?> { ["query"] = "条件分支", ["max_results"] = 3 } };
        var request = new CopilotAgentRequest { Mode = CopilotAgentMode.Diagnose, UserText = "Inspect the Flow node catalog before setting a property.", TaskId = "catalog-metadata", ConversationId = "catalog-metadata" };
        var agent = await new CopilotSearchFlowNodeCatalogTool(dispatcher).ExecuteAsync(request, input, CancellationToken.None);
        var mcp = await dispatcher.CallAsync("get_flow_node_catalog", CopilotFlowReadToolBase.ToJsonArguments(input), CancellationToken.None);
        Assert.True(agent.Success, agent.ErrorMessage);
        Assert.True(mcp.Success, mcp.Text);
        Assert.Equal(2, calls);
        Assert.Equal(mcp.Text, agent.Content);
        using var json = JsonDocument.Parse(agent.Content);
        var root = json.RootElement;
        Assert.Equal("colorvision.flow-node-catalog.v1", root.GetProperty("format").GetString());
        var node = Assert.Single(root.GetProperty("nodeTypes").EnumerateArray());
        Assert.Equal("FlowEngineLib.dll|ConditionBranchNode", node.GetProperty("typeKey").GetString());
        var properties = node.GetProperty("properties").EnumerateArray().ToArray();
        Assert.Equal("期望状态", properties[0].GetProperty("displayName").GetString());
        Assert.True(properties[0].GetProperty("isWritable").GetBoolean());
        Assert.Equal(new[] { "Runing", "Completed" }, Strings(properties[0], "declaredEnumNames"));
        Assert.False(properties[0].GetProperty("isFlagsEnum").GetBoolean());
        Assert.Empty(Strings(properties[0], "booleanStringCandidates"));
        Assert.Equal(new[] { "True", "False" }, Strings(properties[1], "booleanStringCandidates"));
        Assert.True(properties[2].GetProperty("isFlagsEnum").GetBoolean());
        Assert.Equal("Example.CustomDescriptor", properties[2].GetProperty("descriptorType").GetString());
        Assert.Equal(new[] { "None", "First", "Alias" }, Strings(properties[2], "declaredEnumNames"));
        Assert.Contains("not a complete allowed-value list", root.GetProperty("guidance").GetString(), StringComparison.Ordinal);
    }

    private static string?[] Strings(JsonElement property, string key) => property.GetProperty(key).EnumerateArray().Select(value => value.GetString()).ToArray();
}
