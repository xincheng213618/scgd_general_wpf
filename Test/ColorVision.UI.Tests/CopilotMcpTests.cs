using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpTests : IDisposable
{
    private const string Token = "test-token";
    private readonly string _tempRoot;

    public CopilotMcpTests()
    {
        CopilotMcpAuditLogger.ClearForTests();
        _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVisionMcpTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task DisabledServer_ReturnsServiceUnavailable()
    {
        var handler = CreateHandler(enabled: false);

        var response = await handler.HandleAsync(CreateJsonRpcRequest("ping", authorized: true), CancellationToken.None);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains("disabled", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingBearerToken_ReturnsUnauthorized()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync(CreateJsonRpcRequest("ping", authorized: false), CancellationToken.None);

        Assert.Equal(401, response.StatusCode);
        Assert.True(response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Fact]
    public async Task ToolsList_ReturnsColorVisionTools()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync(CreateJsonRpcRequest("tools/list", authorized: true), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("get_workspace_context", response.Body, StringComparison.Ordinal);
        Assert.Contains("read_allowed_file", response.Body, StringComparison.Ordinal);
        Assert.Contains("set_theme", response.Body, StringComparison.Ordinal);
        Assert.Contains("get_server_status", response.Body, StringComparison.Ordinal);
        Assert.Contains("get_audit_log", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResourcesListAndRead_ReturnStableWorkspaceResource()
    {
        var handler = CreateHandler();

        var listResponse = await handler.HandleAsync(CreateJsonRpcRequest("resources/list", authorized: true), CancellationToken.None);
        Assert.Equal(200, listResponse.StatusCode);
        Assert.Contains("colorvision://workspace/current", listResponse.Body, StringComparison.Ordinal);

        var readResponse = await ReadResourceAsync(handler, "colorvision://workspace/current");
        var text = ReadResourceText(readResponse);

        Assert.Contains("ColorVision workspace context", text, StringComparison.Ordinal);
        Assert.Contains(_tempRoot, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetServerStatus_ReturnsAuthenticatedRuntimeState()
    {
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "get_server_status", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("Authentication: passed", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("MCP enabled: True", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Caller/source: unit-test", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchFiles_UsesAllowedWorkspaceRoot()
    {
        var filePath = Path.Combine(_tempRoot, "DeviceCamera.cs");
        await File.WriteAllTextAsync(filePath, "public sealed class DeviceCamera { }");
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "search_files", new { query = "DeviceCamera" });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("DeviceCamera.cs", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GrepText_UsesSharedTextSearchCapability()
    {
        var filePath = Path.Combine(_tempRoot, "FlowNode.cs");
        await File.WriteAllTextAsync(filePath, "public string NeedleSymbol { get; set; } = string.Empty;");
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "grep_text", new { query = "NeedleSymbol" });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("NeedleSymbol", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("FlowNode.cs", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAllowedFile_ReadsFileUnderAllowedRoot()
    {
        var filePath = Path.Combine(_tempRoot, "settings.json");
        await File.WriteAllLinesAsync(filePath, new[]
        {
            "{",
            "  \"theme\": \"dark\"",
            "}",
        });
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "read_allowed_file", new { path = filePath, start_line = 2, end_line = 2 });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("theme", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("{", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAllowedFile_RejectsPathOutsideAllowedRoot()
    {
        var outsidePath = Path.Combine(Path.GetTempPath(), "ColorVisionMcpOutside_" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outsidePath, "outside");
        var handler = CreateHandler();

        try
        {
            var response = await CallToolAsync(handler, "read_allowed_file", new { path = outsidePath });
            var toolResult = ReadToolResult(response);

            Assert.True(toolResult.IsError);
            Assert.Contains("outside the allowed ColorVision workspace roots", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task AuditLog_RecordsToolNameTimestampSuccessAndFailure()
    {
        var filePath = Path.Combine(_tempRoot, "DeviceCamera.cs");
        await File.WriteAllTextAsync(filePath, "public sealed class DeviceCamera { }");
        var outsidePath = Path.Combine(Path.GetTempPath(), "ColorVisionMcpOutside_" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(outsidePath, "outside");
        var handler = CreateHandler();

        try
        {
            await CallToolAsync(handler, "search_files", new { query = "DeviceCamera" });
            await CallToolAsync(handler, "read_allowed_file", new { path = outsidePath });

            var response = await CallToolAsync(handler, "get_audit_log", new { max_entries = 10 });
            var toolResult = ReadToolResult(response);

            Assert.False(toolResult.IsError);
            Assert.Contains("Timestamp UTC", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Tool: search_files", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Tool: read_allowed_file", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Success: True", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Success: False", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Caller/source: unit-test", toolResult.Text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task LowRiskActionTools_RouteThroughEnvironmentHandlers()
    {
        var openedPanel = string.Empty;
        var executedMenu = string.Empty;
        var handler = CreateHandler(
            openPanelHandler: (panel, _) =>
            {
                openedPanel = panel;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("opened " + panel));
            },
            executeMenuHandler: (query, _) =>
            {
                executedMenu = query;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("executed " + query));
            });

        var panelResult = ReadToolResult(await CallToolAsync(handler, "open_panel", new { panel = "copilot" }));
        var menuResult = ReadToolResult(await CallToolAsync(handler, "execute_menu", new { query = "View/Copilot" }));

        Assert.False(panelResult.IsError);
        Assert.False(menuResult.IsError);
        Assert.Equal("copilot", openedPanel);
        Assert.Equal("View/Copilot", executedMenu);
    }

    private CopilotMcpRequestHandler CreateHandler(
        bool enabled = true,
        Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? openPanelHandler = null,
        Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? executeMenuHandler = null)
    {
        var environment = new CopilotMcpToolEnvironment
        {
            RuntimeSettingsProvider = () => new CopilotMcpRuntimeSettings
            {
                Enabled = enabled,
                BearerToken = Token,
                Port = 38473,
            },
            ServerRunningProvider = () => enabled,
            ServerStatusMessageProvider = () => enabled ? "ColorVision MCP test server is running." : "ColorVision MCP test server is disabled.",
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = _tempRoot,
                SearchRootPaths = new[] { _tempRoot },
            },
            OpenPanelHandler = openPanelHandler,
            ExecuteMenuHandler = executeMenuHandler,
        };

        var dispatcher = new CopilotMcpToolDispatcher(environment);
        return new CopilotMcpRequestHandler(() => new CopilotMcpRuntimeSettings
        {
            Enabled = enabled,
            BearerToken = Token,
            Port = 38473,
        }, dispatcher);
    }

    private static async Task<CopilotMcpHttpResponse> CallToolAsync(CopilotMcpRequestHandler handler, string toolName, object arguments)
    {
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments,
            },
        });

        return await handler.HandleAsync(CreatePostRequest(body, authorized: true), CancellationToken.None);
    }

    private static async Task<CopilotMcpHttpResponse> ReadResourceAsync(CopilotMcpRequestHandler handler, string uri)
    {
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "resources/read",
            @params = new
            {
                uri,
            },
        });

        return await handler.HandleAsync(CreatePostRequest(body, authorized: true), CancellationToken.None);
    }

    private static CopilotMcpHttpRequest CreateJsonRpcRequest(string method, bool authorized)
    {
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
        });
        return CreatePostRequest(body, authorized);
    }

    private static CopilotMcpHttpRequest CreatePostRequest(string body, bool authorized)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (authorized)
            headers["Authorization"] = "Bearer " + Token;

        return new CopilotMcpHttpRequest
        {
            Method = "POST",
            Path = "/mcp",
            Headers = headers,
            Body = body,
            CallerSource = "unit-test",
        };
    }

    private static (bool IsError, string Text) ReadToolResult(CopilotMcpHttpResponse response)
    {
        Assert.Equal(200, response.StatusCode);

        using var document = JsonDocument.Parse(response.Body);
        var result = document.RootElement.GetProperty("result");
        var isError = result.TryGetProperty("isError", out var isErrorElement) && isErrorElement.GetBoolean();
        var text = result
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
        return (isError, text);
    }

    private static string ReadResourceText(CopilotMcpHttpResponse response)
    {
        Assert.Equal(200, response.StatusCode);

        using var document = JsonDocument.Parse(response.Body);
        return document.RootElement
            .GetProperty("result")
            .GetProperty("contents")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
    }
}