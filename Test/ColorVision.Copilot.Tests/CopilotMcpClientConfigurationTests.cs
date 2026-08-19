using ColorVision.Copilot;
using ModelContextProtocol.Protocol;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ColorVision.Copilot.Tests;

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

    [Fact]
    public void DiscoveryCacheDetachesMutableToolDefinitionsAndReturnedSnapshots()
    {
        var now = new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);
        var cache = new CopilotMcpToolDiscoveryCache(TimeSpan.FromMinutes(5), utcNow: () => now);
        var server = new CopilotMcpClientServerConfig
        {
            Name = "docs",
            Endpoint = "https://example.test/mcp",
        };
        var source = new Tool
        {
            Name = "search",
            Title = "Search",
            Description = "Search documents",
            InputSchema = ParseElement("""{"type":"object","properties":{"query":{"type":"string"}}}"""),
            OutputSchema = ParseElement("""{"type":"object","properties":{"result":{"type":"string"}}}"""),
            Annotations = new ToolAnnotations
            {
                Title = "Search annotation",
                ReadOnlyHint = true,
                DestructiveHint = false,
                IdempotentHint = true,
                OpenWorldHint = false,
            },
            Icons =
            [
                new Icon
                {
                    Source = "https://example.test/search.svg",
                    MimeType = "image/svg+xml",
                    Sizes = ["any"],
                    Theme = "light",
                },
            ],
            Meta = new JsonObject { ["origin"] = "server" },
        };

        var updateKind = cache.Store(server, "token", [source], 1, out var storedSnapshot);

        Assert.Equal(CopilotMcpDiscoveryCacheUpdateKind.Added, updateKind);
        var storedTools = Assert.IsAssignableFrom<IList<Tool>>(storedSnapshot.Tools);
        Assert.True(storedTools.IsReadOnly);
        var replacement = new Tool { Name = "replacement", InputSchema = ParseElement("""{"type":"object"}""") };
        Assert.Throws<NotSupportedException>(() => storedTools[0] = replacement);

        source.Name = "source-mutated";
        source.InputSchema = ParseElement("""{"type":"object","additionalProperties":false}""");
        source.OutputSchema = ParseElement("""{"type":"string"}""");
        source.Annotations!.ReadOnlyHint = false;
        source.Icons![0].Source = "https://example.test/mutated.svg";
        source.Icons[0].Sizes![0] = "64x64";
        source.Meta!["origin"] = "source-mutated";
        storedSnapshot.Tools[0].Description = "snapshot-mutated";
        storedSnapshot.Tools[0].Annotations!.Title = "snapshot-mutated";
        storedSnapshot.Tools[0].Icons![0].Theme = "dark";
        storedSnapshot.Tools[0].Meta!["origin"] = "snapshot-mutated";

        Assert.True(cache.TryGet(server, "token", out var cachedSnapshot));
        var cachedTool = Assert.Single(cachedSnapshot.Tools);
        Assert.Equal(1, cachedSnapshot.Revision);
        Assert.Equal("search", cachedTool.Name);
        Assert.Equal("Search documents", cachedTool.Description);
        Assert.Equal("object", cachedTool.InputSchema.GetProperty("type").GetString());
        Assert.Equal("object", cachedTool.OutputSchema!.Value.GetProperty("type").GetString());
        Assert.True(cachedTool.Annotations!.ReadOnlyHint);
        Assert.Equal("Search annotation", cachedTool.Annotations.Title);
        Assert.Equal("https://example.test/search.svg", cachedTool.Icons![0].Source);
        Assert.Equal("any", cachedTool.Icons[0].Sizes![0]);
        Assert.Equal("light", cachedTool.Icons[0].Theme);
        Assert.Equal("server", cachedTool.Meta!["origin"]!.GetValue<string>());
        Assert.NotSame(source, cachedTool);
        Assert.NotSame(storedSnapshot.Tools[0], cachedTool);
        Assert.NotSame(source.Annotations, cachedTool.Annotations);
        Assert.NotSame(source.Icons, cachedTool.Icons);
        Assert.NotSame(source.Meta, cachedTool.Meta);

        cachedTool.Name = "cached-snapshot-mutated";
        cachedTool.Annotations.Title = "cached-snapshot-mutated";
        var cachedSizes = Assert.IsAssignableFrom<IList<string>>(cachedTool.Icons[0].Sizes);
        Assert.True(cachedSizes.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => cachedSizes[0] = "128x128");
        cachedTool.Icons[0].Theme = "cached-snapshot-mutated";
        cachedTool.Meta["origin"] = "cached-snapshot-mutated";

        Assert.True(cache.TryGet(server, "token", out var nextSnapshot));
        var nextTool = Assert.Single(nextSnapshot.Tools);
        Assert.Equal("search", nextTool.Name);
        Assert.Equal("Search annotation", nextTool.Annotations!.Title);
        Assert.Equal("any", nextTool.Icons![0].Sizes![0]);
        Assert.Equal("light", nextTool.Icons[0].Theme);
        Assert.Equal("server", nextTool.Meta!["origin"]!.GetValue<string>());
        Assert.NotSame(cachedTool, nextTool);
    }

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
