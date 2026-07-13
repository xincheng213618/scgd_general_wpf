#pragma warning disable CA1707
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotCapabilityCatalogTests
{
    [Fact]
    public void PublishSource_PreservesStableIdsAndRevisionsUntilMetadataChanges()
    {
        var now = new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
        var catalog = new CopilotCapabilityCatalog(() => now);
        var changeCount = 0;
        catalog.Changed += (_, _) => changeCount++;

        var first = catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:test-suite",
            "Test suite",
            [new CatalogTool("Inspect", "First description", "inspect")]);
        var unchanged = catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:test-suite",
            "Test suite",
            [new CatalogTool("Inspect", "First description", "inspect")]);
        var changed = catalog.PublishSource(
            CopilotCapabilitySourceKind.Plugin,
            "plugin:test-suite",
            "Test suite",
            [new CatalogTool("Inspect", "Changed description", "inspect")]);

        var firstEntry = Assert.Single(first.Capabilities);
        Assert.Equal("plugin:test-suite:inspect", firstEntry.Id);
        Assert.Equal(1, first.Revision);
        Assert.Equal(1, firstEntry.Revision);
        Assert.Equal(first.Revision, unchanged.Revision);
        Assert.Equal(firstEntry.Revision, Assert.Single(unchanged.Capabilities).Revision);
        Assert.Equal(2, changed.Revision);
        Assert.Equal(2, Assert.Single(changed.Capabilities).Revision);
        Assert.Equal(2, changeCount);
        Assert.Equal(now, changed.UpdatedAtUtc);

        var removed = catalog.RetainSources(CopilotCapabilitySourceKind.Plugin, Array.Empty<string>());

        Assert.Equal(3, removed.Revision);
        Assert.Empty(removed.Capabilities);
        Assert.Equal(3, changeCount);
    }

    [Fact]
    public void ExternalMcpSourceId_UsesStableConnectionFingerprintWithoutLeakingConfiguration()
    {
        var first = new CopilotMcpClientServerConfig
        {
            Name = "lab-a",
            Endpoint = "https://mcp.example.test/private/path?secret=one",
            BearerTokenEnvironmentVariable = "LAB_A_TOKEN",
        };
        var renamed = new CopilotMcpClientServerConfig
        {
            Name = "lab-b",
            Endpoint = "https://MCP.EXAMPLE.TEST/private/path/?secret=two",
            BearerTokenEnvironmentVariable = "LAB_A_TOKEN",
        };
        var differentCredential = renamed.Clone();
        differentCredential.BearerTokenEnvironmentVariable = "LAB_B_TOKEN";

        var firstId = CopilotCapabilityCatalog.BuildExternalMcpSourceId(first);
        var renamedId = CopilotCapabilityCatalog.BuildExternalMcpSourceId(renamed);
        var differentCredentialId = CopilotCapabilityCatalog.BuildExternalMcpSourceId(differentCredential);

        Assert.Equal(firstId, renamedId);
        Assert.NotEqual(firstId, differentCredentialId);
        Assert.StartsWith("mcp:", firstId, StringComparison.Ordinal);
        Assert.DoesNotContain("example", firstId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", firstId, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", firstId, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CatalogTool(string name, string description, string catalogKey) : ICopilotTool, ICopilotCapabilityCatalogIdentity
    {
        public string Name { get; } = name;

        public string Description { get; } = description;

        public string CatalogCapabilityKey { get; } = catalogKey;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult { ToolName = Name, Success = true, Summary = "Completed." });
        }
    }
}
