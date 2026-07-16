#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpClientTests
{
    [Fact]
    public async Task DiscoveryPropagatesCallerCancellationFromHangingServer()
    {
        var providerType = typeof(CopilotAgentRequest).Assembly.GetType(
            "ColorVision.Copilot.CopilotMcpToolProvider",
            throwOnError: true)!;
        var provider = Assert.IsAssignableFrom<ICopilotExternalToolProvider>(Activator.CreateInstance(providerType, nonPublic: true));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            using var cancellation = new CancellationTokenSource();
            var discovery = provider.DiscoverAsync(new CopilotAgentRequest
            {
                ExternalMcpServers =
                [
                    new CopilotMcpClientServerConfig
                    {
                        Name = "hanging-test",
                        Endpoint = $"http://127.0.0.1:{endpoint.Port}/mcp",
                        ConnectionTimeoutSeconds = 30,
                    },
                ],
            }, cancellation.Token);
            using var acceptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = await listener.AcceptTcpClientAsync(acceptTimeout.Token);

            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => discovery.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            listener.Stop();
        }
    }

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

    [Fact]
    public void ConfigurationText_ParsesExactToolAllowlistAndPerToolPolicies()
    {
        var text = "local | http://127.0.0.1:3001/mcp | LOCAL_MCP_TOKEN | approval | get_server_status=read-only,apply_template_patch=approval";

        Assert.True(CopilotMcpClientConfigurationText.TryParse(text, out var servers, out var error), error);
        var server = Assert.Single(servers);
        Assert.Equal(2, server.ToolRules.Count);
        Assert.True(server.TryResolveToolAccessPolicy("get_server_status", out var statusPolicy));
        Assert.Equal(CopilotMcpClientAccessPolicy.ReadOnly, statusPolicy);
        Assert.True(server.TryResolveToolAccessPolicy("apply_template_patch", out var patchPolicy));
        Assert.Equal(CopilotMcpClientAccessPolicy.RequireApproval, patchPolicy);
        Assert.False(server.TryResolveToolAccessPolicy("search_files", out _));
        Assert.Equal(text, CopilotMcpClientConfigurationText.Format(servers));
    }

    [Fact]
    public void ConfigurationText_WithoutToolRulesExposesAllToolsUsingServerPolicy()
    {
        Assert.True(CopilotMcpClientConfigurationText.TryParse(
            "local | http://127.0.0.1:3001/mcp | | read-only | *",
            out var servers,
            out var error), error);

        var server = Assert.Single(servers);
        Assert.Empty(server.ToolRules);
        Assert.True(server.TryResolveToolAccessPolicy("any_remote_tool", out var policy));
        Assert.Equal(CopilotMcpClientAccessPolicy.ReadOnly, policy);
    }

    [Theory]
    [InlineData("remote | http://mcp.example.test/mcp | | approval", "plain HTTP")]
    [InlineData("embedded | https://user:password@mcp.example.test/mcp | | approval", "embedded credentials")]
    [InlineData("bad name! | https://mcp.example.test/mcp | | approval", "invalid server name")]
    [InlineData("remote | https://mcp.example.test/mcp | TOKEN | annotations", "access policy")]
    [InlineData("remote | https://mcp.example.test/mcp | TOKEN | approval | bad tool=read-only", "invalid MCP tool name")]
    [InlineData("remote | https://mcp.example.test/mcp | TOKEN | approval | status=read-only,status=approval", "duplicates MCP tool rule")]
    [InlineData("remote | https://mcp.example.test/mcp | TOKEN | approval | status=annotations", "tool 'status' policy")]
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

    [Fact]
    public void CapabilityPolicy_MapsExternalMcpTrustWithoutDuplicatedFlags()
    {
        var readOnly = CopilotMcpClientCapabilityPolicy.Create(CopilotMcpClientAccessPolicy.ReadOnly, TimeSpan.FromSeconds(12));
        var protectedWrite = CopilotMcpClientCapabilityPolicy.Create(CopilotMcpClientAccessPolicy.RequireApproval, TimeSpan.FromSeconds(18));

        Assert.Equal(CopilotToolAccess.ReadOnly, readOnly.Access);
        Assert.Equal(CopilotToolApprovalMode.Never, readOnly.ApprovalMode);
        Assert.Equal(CopilotToolIdempotency.Idempotent, readOnly.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, readOnly.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, readOnly.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(12), readOnly.EffectiveExecutionTimeout);

        Assert.Equal(CopilotToolAccess.Write, protectedWrite.Access);
        Assert.Equal(CopilotToolRiskLevel.High, protectedWrite.RiskLevel);
        Assert.Equal(CopilotToolApprovalMode.Always, protectedWrite.ApprovalMode);
        Assert.Equal(CopilotToolIdempotency.NonIdempotent, protectedWrite.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.Exclusive, protectedWrite.EffectiveConcurrencyMode);
        Assert.Equal(CopilotToolAuditArgumentMode.NamesOnly, protectedWrite.AuditArgumentMode);
        Assert.Equal(TimeSpan.FromSeconds(18), protectedWrite.EffectiveExecutionTimeout);
    }

    [Fact]
    public void ApprovalPresentation_ShowsMcpIdentityAndRedactedArgumentValues()
    {
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["target"] = "flow-42",
                ["options"] = new Dictionary<string, object?>
                {
                    ["retry"] = 2,
                    ["api_token"] = "secret-value",
                    ["pwd"] = "multi word password",
                },
            },
        };

        var presentation = CopilotMcpClientApprovalPresentation.Create("production", "apply_flow", input);

        Assert.Contains("production/apply_flow", presentation.Title, StringComparison.Ordinal);
        Assert.Contains("External MCP server 'production'", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("\"target\":\"flow-42\"", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("\"retry\":2", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("\"api_token\":\"<redacted>\"", presentation.Description, StringComparison.Ordinal);
        Assert.Contains("\"pwd\":\"<redacted>\"", presentation.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", presentation.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("multi word password", presentation.Description, StringComparison.Ordinal);
    }
}
