using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpClientConfigurationTests
{
    [Fact]
    public void EnsureInitializedRemovesNullExternalMcpServersAndToolRules()
    {
        var config = JsonConvert.DeserializeObject<CopilotConfig>(
            """
            {
              "ExternalMcpServers": [
                null,
                {
                  "Name": "docs",
                  "Endpoint": "https://example.test/mcp",
                  "ToolRules": [
                    null,
                    { "ToolName": "search", "AccessPolicy": 1 }
                  ]
                }
              ]
            }
            """)!;

        Assert.True(config.EnsureInitialized());

        var server = Assert.Single(config.ExternalMcpServers);
        var rule = Assert.Single(server.ToolRules);
        Assert.Equal("search", rule.ToolName);
        Assert.Equal(CopilotMcpClientAccessPolicy.ReadOnly, rule.AccessPolicy);
    }

    [Fact]
    public void CloneSkipsNullToolRulesAtRequestSnapshotBoundary()
    {
        var server = JsonConvert.DeserializeObject<CopilotMcpClientServerConfig>(
            """
            {
              "Name": "docs",
              "Endpoint": "https://example.test/mcp",
              "ToolRules": [
                null,
                { "ToolName": "search", "AccessPolicy": 1 }
              ]
            }
            """)!;

        var snapshot = server.Clone();

        var rule = Assert.Single(snapshot.ToolRules);
        Assert.Equal("search", rule.ToolName);
        Assert.NotSame(server.ToolRules[1], rule);
    }
}
