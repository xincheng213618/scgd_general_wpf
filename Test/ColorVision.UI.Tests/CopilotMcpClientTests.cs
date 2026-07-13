#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpClientTests
{
    [Fact]
    public void ConfigurationText_ParsesSafeHttpEndpointsAndAccessPolicies()
    {
        var text = "local | http://127.0.0.1:3001/mcp | LOCAL_MCP_TOKEN | read-only\nremote | https://mcp.example.test/mcp | REMOTE_MCP_TOKEN | approval";

        var success = CopilotMcpClientConfigurationText.TryParse(text, out var servers, out var error);

        Assert.True(success, error);
        Assert.Equal(2, servers.Count);
        Assert.Equal(CopilotMcpClientAccessPolicy.ReadOnly, servers[0].AccessPolicy);
        Assert.Equal(CopilotMcpClientAccessPolicy.RequireApproval, servers[1].AccessPolicy);
        Assert.Equal(text, CopilotMcpClientConfigurationText.Format(servers).Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("remote | http://mcp.example.test/mcp | | approval", "plain HTTP")]
    [InlineData("embedded | https://user:password@mcp.example.test/mcp | | approval", "embedded credentials")]
    [InlineData("bad name! | https://mcp.example.test/mcp | | approval", "invalid server name")]
    [InlineData("remote | https://mcp.example.test/mcp | TOKEN | annotations", "access policy")]
    public void ConfigurationText_RejectsUnsafeOrAmbiguousEntries(string text, string expectedError)
    {
        Assert.False(CopilotMcpClientConfigurationText.TryParse(text, out var servers, out var error));
        Assert.Empty(servers);
        Assert.Contains(expectedError, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArbitraryToolSchema_BindsNestedArgumentsAndRedactsAuditSecrets()
    {
        var schema = CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                message = new { type = "string" },
                options = new { type = "object" },
                api_token = new { type = "string" },
            },
            required = new[] { "message" },
            additionalProperties = false,
        }));
        var arguments = new Dictionary<string, object?>
        {
            ["message"] = "hello",
            ["options"] = new Dictionary<string, object?> { ["count"] = 2 },
            ["api_token"] = "secret-value",
        };

        Assert.True(schema.TryBind(arguments, out var input, out var error), error);
        Assert.Equal("hello", input.Arguments["message"]);
        var summary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(input);
        Assert.Contains("options=", summary, StringComparison.Ordinal);
        Assert.Contains("api_token=<redacted>", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void ArbitraryToolSchema_RejectsMissingAndUnknownArguments()
    {
        var schema = CopilotToolInputSchema.FromJsonSchema(JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { message = new { type = "string" } },
            required = new[] { "message" },
            additionalProperties = false,
        }));

        Assert.False(schema.TryBind(new Dictionary<string, object?>(), out _, out var missingError));
        Assert.Contains("Required argument 'message'", missingError, StringComparison.Ordinal);
        Assert.False(schema.TryBind(new Dictionary<string, object?> { ["message"] = "ok", ["extra"] = 1 }, out _, out var unknownError));
        Assert.Contains("Unknown argument 'extra'", unknownError, StringComparison.Ordinal);
    }
}
