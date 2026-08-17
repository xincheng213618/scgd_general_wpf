using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSharedCapabilityCatalogTests
{
    [Fact]
    public void SharedCatalogMapsEveryDeclaredCapabilityToBothSurfaces()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools();
        var mcpTools = new CopilotMcpToolDispatcher().ListTools();

        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.AgentToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            CopilotSharedCapabilityCatalog.All.Count,
            CopilotSharedCapabilityCatalog.All.Select(definition => definition.McpToolName)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var definition in CopilotSharedCapabilityCatalog.All)
        {
            Assert.Contains(agentTools, tool => string.Equals(
                tool.Name,
                definition.AgentToolName,
                StringComparison.OrdinalIgnoreCase));
            Assert.Contains(mcpTools, tool => string.Equals(
                tool.Name,
                definition.McpToolName,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void SharedStructuredInputsUseOneSchemaOnAgentAndMcpSurfaces()
    {
        var agentTools = CopilotToolRegistry.CreateCoreDefaultTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        var mcpTools = new CopilotMcpToolDispatcher().ListTools()
            .ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
        var sharedDefinitions = CopilotSharedCapabilityCatalog.All
            .Where(definition => definition.SharedInputSchema != null)
            .ToArray();
        Assert.Equal(9, sharedDefinitions.Length);

        foreach (var definition in sharedDefinitions)
        {
            var schema = definition.SharedInputSchema!;
            Assert.Same(schema, agentTools[definition.AgentToolName].InputSchema);
            var mcpSchema = Assert.IsType<JsonElement>(mcpTools[definition.McpToolName].InputSchema);
            Assert.Equal(schema.JsonSchema.GetRawText(), mcpSchema.GetRawText());
        }
    }
}
