#pragma warning disable CA1707,CA1826,CA1861
using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotMcpTests : IDisposable
{
    private const string Token = "test-token";
    private readonly string _tempRoot;

    public CopilotMcpTests()
    {
        CopilotMcpAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
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
    public async Task AuthorizedPing_ReturnsOk()
    {
        var handler = CreateHandler();

        var response = await handler.HandleAsync(CreateJsonRpcRequest("ping", authorized: true), CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("result", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidBearerToken_ReturnsUnauthorizedWithoutRecordingToken()
    {
        const string invalidToken = "wrong-secret-token";
        var handler = CreateHandler();

        var response = await handler.HandleAsync(CreateJsonRpcRequest("ping", authorized: true, bearerToken: invalidToken), CancellationToken.None);
        var auditEntry = CopilotMcpAuditLogger.GetRecentEntries(5).LastOrDefault();

        Assert.Equal(401, response.StatusCode);
        Assert.DoesNotContain(invalidToken, response.Body, StringComparison.Ordinal);
        Assert.NotNull(auditEntry);
        Assert.Equal("authentication", auditEntry.ToolName);
        Assert.Contains("invalid bearer token", auditEntry.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(invalidToken, auditEntry.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(invalidToken, auditEntry.ArgumentSummary, StringComparison.Ordinal);
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
        Assert.Contains("get_agent_task_events", response.Body, StringComparison.Ordinal);
        Assert.Contains("open_panel", response.Body, StringComparison.Ordinal);
        Assert.Contains("create_flow", response.Body, StringComparison.Ordinal);
        Assert.Contains("search_docs", response.Body, StringComparison.Ordinal);
        Assert.Contains("confirm_action", response.Body, StringComparison.Ordinal);
        Assert.Contains("preview_template_patch", response.Body, StringComparison.Ordinal);
        Assert.Contains("apply_template_patch", response.Body, StringComparison.Ordinal);
        Assert.Contains("preview_flow_action", response.Body, StringComparison.Ordinal);
        Assert.Contains("get_diagnostic_bundle", response.Body, StringComparison.Ordinal);
        Assert.Contains("diagnose_flow_failure", response.Body, StringComparison.Ordinal);
        Assert.Contains("suggest_template_patch", response.Body, StringComparison.Ordinal);
        Assert.Contains("riskLevel", response.Body, StringComparison.Ordinal);
        Assert.Contains("category", response.Body, StringComparison.Ordinal);
        Assert.Contains("annotations", response.Body, StringComparison.Ordinal);
        Assert.Contains("usageExample", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetEnabledTools_ReturnsCategoriesRiskAndExamples()
    {
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "get_enabled_tools", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("## status", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("## context", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("## search", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("## file", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("## app-control", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("## audit", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("[read-only]", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("[low-risk-action]", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("[confirmation-required]", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Example:", toolResult.Text, StringComparison.Ordinal);
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
    public async Task GetRecentLog_ForwardsRequestCancellationToLogProvider()
    {
        var providerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = CreateHandler(recentLogProvider: async (_, _, _, _, cancellationToken) =>
        {
            providerStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new CopilotCapabilityResult { Success = true };
        });
        using var cancellation = new CancellationTokenSource();

        var callTask = CallToolAsync(handler, "get_recent_log", new { max_lines = 20 }, cancellation.Token);
        await providerStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => callTask);
    }

    [Fact]
    public async Task ResourcesExposeVersionedCopilotCapabilityCatalog()
    {
        var handler = CreateHandler();

        var listResponse = await handler.HandleAsync(CreateJsonRpcRequest("resources/list", authorized: true), CancellationToken.None);
        var readResponse = await ReadResourceAsync(handler, "colorvision://copilot/capabilities");
        var text = ReadResourceText(readResponse);

        Assert.Contains("colorvision://copilot/capabilities", listResponse.Body, StringComparison.Ordinal);
        using (var responseDocument = JsonDocument.Parse(readResponse.Body))
        {
            var mimeType = responseDocument.RootElement
                .GetProperty("result")
                .GetProperty("contents")[0]
                .GetProperty("mimeType")
                .GetString();
            Assert.Equal("application/json", mimeType);
        }

        using var catalogDocument = JsonDocument.Parse(text);
        var root = catalogDocument.RootElement;
        Assert.True(root.GetProperty("revision").GetInt64() >= 1);
        Assert.True(root.GetProperty("sourceCount").GetInt32() >= 1);
        var capabilities = root.GetProperty("capabilities").EnumerateArray().ToArray();
        Assert.True(capabilities.Length >= 10);
        var createFlow = Assert.Single(capabilities, capability => capability.GetProperty("id").GetString() == "builtin:createflow");
        Assert.Equal("builtIn", createFlow.GetProperty("sourceKind").GetString());
        Assert.Equal("always", createFlow.GetProperty("approvalMode").GetString());
        Assert.Equal("exclusive", createFlow.GetProperty("concurrencyMode").GetString());
        Assert.Equal("none", createFlow.GetProperty("evidenceMode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(createFlow.GetProperty("inputSchemaFingerprint").GetString()));
        var fetchUrl = Assert.Single(capabilities, capability => capability.GetProperty("id").GetString() == "builtin:fetchurl");
        Assert.Equal("redactedExcerpt", fetchUrl.GetProperty("evidenceMode").GetString());
        Assert.DoesNotContain("http://", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AgentTaskEvents_AreExplicitlyQueryableBoundedAndRedacted()
    {
        var journalBuilder = new CopilotAgentTaskEventJournalBuilder();
        journalBuilder.RecordRunStarted();
        journalBuilder.RecordSteering("api_key=secret-steering-value");
        journalBuilder.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
        {
            ToolName = "SearchDocs",
            Success = true,
            Summary = "Documentation search completed.",
        }, new CopilotToolExecutionInfo
        {
            CallId = "search-docs-call",
            ToolName = "SearchDocs",
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMilliseconds(-10),
            CompletedAtUtc = DateTimeOffset.UtcNow,
        }));
        journalBuilder.Observe(CopilotAgentEvent.Error("token=secret-runtime-value"));
        journalBuilder.RecordStop(CopilotAgentStopReason.Completed);
        var context = new CopilotAgentTaskEventJournalContext
        {
            ConversationId = "conversation-task-events",
            Journal = journalBuilder.Snapshot(),
            PublishedAtUtc = DateTimeOffset.UtcNow,
        };
        var handler = CreateHandler(taskEventJournalProvider: () => context);

        var listResponse = await handler.HandleAsync(CreateJsonRpcRequest("resources/list", authorized: true), CancellationToken.None);
        Assert.Contains("colorvision://copilot/task-events", listResponse.Body, StringComparison.Ordinal);
        var resourceResponse = await ReadResourceAsync(handler, "colorvision://copilot/task-events");
        var resourceText = ReadResourceText(resourceResponse);
        Assert.DoesNotContain("secret-steering-value", resourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-runtime-value", resourceText, StringComparison.Ordinal);
        using (var resourceDocument = JsonDocument.Parse(resourceText))
        {
            Assert.Equal("conversation-task-events", resourceDocument.RootElement.GetProperty("conversationId").GetString());
            Assert.Equal(CopilotAgentTaskEventJournalSnapshot.CurrentSchemaVersion, resourceDocument.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.True(resourceDocument.RootElement.GetProperty("events").GetArrayLength() >= 5);
        }

        var filteredResult = ReadToolResult(await CallToolAsync(handler, "get_agent_task_events", new
        {
            event_types = new[] { "toolCompleted" },
            tool = "SearchDocs",
            max_events = 1,
        }));
        Assert.False(filteredResult.IsError);
        using (var filteredDocument = JsonDocument.Parse(filteredResult.Text))
        {
            var events = filteredDocument.RootElement.GetProperty("events");
            Assert.Equal(1, events.GetArrayLength());
            Assert.Equal("toolCompleted", events[0].GetProperty("type").GetString());
            Assert.Equal("SearchDocs", events[0].GetProperty("toolName").GetString());
        }

        var firstPage = ReadToolResult(await CallToolAsync(handler, "get_agent_task_events", new { max_events = 2 }));
        using var firstPageDocument = JsonDocument.Parse(firstPage.Text);
        Assert.True(firstPageDocument.RootElement.GetProperty("hasMore").GetBoolean());
        var cursor = firstPageDocument.RootElement.GetProperty("nextBeforeSequence").GetInt64();
        var secondPage = ReadToolResult(await CallToolAsync(handler, "get_agent_task_events", new { before_sequence = cursor, max_events = 2 }));
        using var secondPageDocument = JsonDocument.Parse(secondPage.Text);
        var firstIds = firstPageDocument.RootElement.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("id").GetString()).ToHashSet();
        Assert.DoesNotContain(secondPageDocument.RootElement.GetProperty("events").EnumerateArray(), item => firstIds.Contains(item.GetProperty("id").GetString()));

        var invalidFilter = ReadToolResult(await CallToolAsync(handler, "get_agent_task_events", new { event_types = new[] { "notAnEvent" } }));
        Assert.True(invalidFilter.IsError);
        Assert.Contains("Unknown Agent task event type", invalidFilter.Text, StringComparison.Ordinal);

        var diagnosticBundle = ReadToolResult(await CallToolAsync(handler, "get_diagnostic_bundle", new { max_chars = 12000 }));
        Assert.DoesNotContain("task-event:", diagnosticBundle.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentTaskEvents_ReportUnavailableWithoutFallingBackToOtherDiagnostics()
    {
        var handler = CreateHandler(taskEventJournalProvider: () => null);

        var resourceResponse = await ReadResourceAsync(handler, "colorvision://copilot/task-events");
        Assert.Contains("No saved Agent task event journal", resourceResponse.Body, StringComparison.Ordinal);
        var toolResult = ReadToolResult(await CallToolAsync(handler, "get_agent_task_events", new { }));
        Assert.True(toolResult.IsError);
        Assert.Contains("No saved Agent task event journal", toolResult.Text, StringComparison.Ordinal);
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
        Assert.Contains("Pending actions:", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void McpTokenGeneration_ProducesHexSecret()
    {
        var token = CopilotConfig.GenerateMcpBearerToken();

        Assert.Equal(64, token.Length);
        Assert.All(token, ch => Assert.True(Uri.IsHexDigit(ch), $"Token contains a non-hex character: {ch}"));
    }

    [Fact]
    public void McpTokenEncryption_RoundTripsWithoutPlaintextStorage()
    {
        const string token = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        const string apiKey = "api-secret-for-test";
        var config = new CopilotConfig
        {
            McpBearerToken = token,
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            ApiKey = apiKey,
            BaseUrl = "https://example.invalid/v1",
            Model = "test-model",
        });

        config.Encryption();

        Assert.NotEqual(token, config.McpBearerToken);
        Assert.NotEqual(apiKey, config.Profiles[0].ApiKey);
        Assert.StartsWith("dpapi:v1:", config.McpBearerToken, StringComparison.Ordinal);
        Assert.StartsWith("dpapi:v1:", config.Profiles[0].ApiKey, StringComparison.Ordinal);
        Assert.DoesNotContain(token, config.McpBearerToken, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, config.Profiles[0].ApiKey, StringComparison.Ordinal);
        var persistedJson = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        Assert.DoesNotContain(token, persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, persistedJson, StringComparison.Ordinal);

        config.Decrypt();

        Assert.Equal(token, config.McpBearerToken);
        Assert.Equal(apiKey, config.Profiles[0].ApiKey);
    }

    [Fact]
    public void ConfigHandlerSaveWritesProtectedCredentialsAndRestoresLivePlaintext()
    {
        const string token = "handler-mcp-secret";
        const string apiKey = "handler-api-secret";
        var config = new CopilotConfig { McpBearerToken = token };
        config.Profiles.Add(new CopilotProfileConfig
        {
            ApiKey = apiKey,
            BaseUrl = "https://example.invalid/v1",
            Model = "test-model",
        });
        var configFilePath = Path.Combine(_tempRoot, "protected-config.json");
        var handler = new ColorVision.UI.ConfigHandler { IsAutoSave = false };
        handler.Configs[typeof(CopilotConfig)] = config;

        handler.SaveConfigs(configFilePath);

        var persistedJson = File.ReadAllText(configFilePath);
        Assert.DoesNotContain(token, persistedJson, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, persistedJson, StringComparison.Ordinal);
        Assert.Contains("dpapi:v1:", persistedJson, StringComparison.Ordinal);
        Assert.Equal(token, config.McpBearerToken);
        Assert.Equal(apiKey, config.Profiles[0].ApiKey);
    }

    [Fact]
    public void CredentialEncryption_DecryptsLegacyAesAndReencryptsWithCurrentUserProtection()
    {
        const string token = "legacy-mcp-token";
        const string apiKey = "legacy-api-secret";
        var config = new CopilotConfig
        {
            McpBearerToken = ColorVision.Common.Utilities.Cryptography.AESEncrypt(
                token,
                CopilotConfig.ConfigAESKey,
                CopilotConfig.ConfigAESVector),
        };
        config.Profiles.Add(new CopilotProfileConfig
        {
            ApiKey = ColorVision.Common.Utilities.Cryptography.AESEncrypt(
                apiKey,
                CopilotConfig.ConfigAESKey,
                CopilotConfig.ConfigAESVector),
            BaseUrl = "https://example.invalid/v1",
            Model = "test-model",
        });

        config.Decrypt();

        Assert.Equal(token, config.McpBearerToken);
        Assert.Equal(apiKey, config.Profiles[0].ApiKey);
        Assert.False(config.Profiles[0].CredentialNeedsReentry);

        config.Encryption();

        Assert.StartsWith("dpapi:v1:", config.McpBearerToken, StringComparison.Ordinal);
        Assert.StartsWith("dpapi:v1:", config.Profiles[0].ApiKey, StringComparison.Ordinal);

        config.Decrypt();
        Assert.Equal(token, config.McpBearerToken);
        Assert.Equal(apiKey, config.Profiles[0].ApiKey);
    }

    [Fact]
    public void CredentialDecryptionFailureIsIsolatedAndRequiresOnlyAffectedProfileReentry()
    {
        const string validApiKey = "valid-api-secret";
        var validConfig = new CopilotConfig { McpBearerToken = "valid-mcp-token" };
        validConfig.Profiles.Add(new CopilotProfileConfig
        {
            ApiKey = validApiKey,
            BaseUrl = "https://valid.example.invalid/v1",
            Model = "valid-model",
        });
        validConfig.Encryption();
        var protectedValidApiKey = validConfig.Profiles[0].ApiKey;

        var config = new CopilotConfig { McpBearerToken = "dpapi:v1:not-valid-base64" };
        config.Profiles.Add(new CopilotProfileConfig
        {
            Name = "Broken profile",
            ApiKey = "dpapi:v1:not-valid-base64",
            BaseUrl = "https://broken.example.invalid/v1",
            Model = "broken-model",
        });
        config.Profiles.Add(new CopilotProfileConfig
        {
            Name = "Valid profile",
            ApiKey = protectedValidApiKey,
            BaseUrl = "https://valid.example.invalid/v1",
            Model = "valid-model",
        });

        config.Decrypt();

        Assert.Equal(string.Empty, config.Profiles[0].ApiKey);
        Assert.True(config.Profiles[0].CredentialNeedsReentry);
        Assert.Contains("re-entry required", config.Profiles[0].ConfigurationStatusToolTip, StringComparison.Ordinal);
        Assert.Equal(validApiKey, config.Profiles[1].ApiKey);
        Assert.False(config.Profiles[1].CredentialNeedsReentry);
        Assert.Equal(string.Empty, config.McpBearerToken);

        Assert.True(config.EnsureInitialized());
        Assert.Equal(64, config.McpBearerToken.Length);
        Assert.All(config.McpBearerToken, character => Assert.True(Uri.IsHexDigit(character)));

        config.Profiles[0].ApiKey = "replacement-api-key";
        Assert.False(config.Profiles[0].CredentialNeedsReentry);
        Assert.True(config.Profiles[0].IsConfigured);
    }

    [Fact]
    public void SettingsSave_PersistsMcpValues()
    {
        var config = CopilotConfig.Instance;
        var originalEnabled = config.McpEnabled;
        var originalPort = config.McpPort;
        var originalToken = config.McpBearerToken;
        var originalProfiles = config.Profiles.Select(profile => profile.Clone()).ToArray();

        try
        {
            var viewModel = new CopilotSettingsViewModel
            {
                McpEnabled = false,
                McpPort = 38474,
                McpBearerToken = "settings-save-token",
            };

            Assert.True(viewModel.Save());
            Assert.False(config.McpEnabled);
            Assert.Equal(38474, config.McpPort);
            Assert.Equal("settings-save-token", config.McpBearerToken);
        }
        finally
        {
            config.McpEnabled = originalEnabled;
            config.McpPort = originalPort;
            config.McpBearerToken = originalToken;
            config.Profiles.Clear();
            foreach (var profile in originalProfiles)
                config.Profiles.Add(profile.Clone());

            ColorVision.UI.ConfigHandler.GetInstance().Save<CopilotConfig>();
            CopilotMcpServer.Instance.ApplyConfig();
        }
    }

    [Fact]
    public void SettingsViewModel_BuildsCodexMcpSetupSnippets()
    {
        var viewModel = new CopilotSettingsViewModel
        {
            McpPort = 38475,
            McpBearerToken = "settings-token",
        };

        Assert.Contains("[mcp_servers.colorvision]", viewModel.CodexMcpConfigSnippet, StringComparison.Ordinal);
        Assert.Contains("url = \"http://127.0.0.1:38475/mcp\"", viewModel.CodexMcpConfigSnippet, StringComparison.Ordinal);
        Assert.Contains("bearer_token_env_var = \"COLORVISION_MCP_TOKEN\"", viewModel.CodexMcpConfigSnippet, StringComparison.Ordinal);
        Assert.Equal(
            "[Environment]::SetEnvironmentVariable(\"COLORVISION_MCP_TOKEN\", \"settings-token\", \"User\")",
            viewModel.McpTokenEnvironmentCommandText);
    }

    [Fact]
    public async Task SettingsViewModelDisposeCancelsPendingMcpConnectionTest()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            using var viewModel = new CopilotSettingsViewModel
            {
                McpPort = endpoint.Port,
                McpBearerToken = "settings-lifetime-token",
            };
            var testTask = viewModel.TestMcpConnectionAsync();
            using var acceptTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var client = await listener.AcceptTcpClientAsync(acceptTimeout.Token);

            viewModel.Dispose();
            await testTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.False(viewModel.IsTestingMcpConnection);
            Assert.False(viewModel.CanTestMcpConnection);
        }
        finally
        {
            listener.Stop();
        }
    }

    [Fact]
    public async Task McpServerStopClosesPartialClientAndRestartsSamePort()
    {
        var server = CopilotMcpServer.Instance;
        var port = ReserveLoopbackPort();
        var settings = new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = Token,
        };
        try
        {
            server.ApplySettings(settings);
            Assert.True(server.IsRunning, server.LastStatusMessage);
            using var client = new TcpClient();
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(IPAddress.Loopback, port, connectTimeout.Token);
            var stream = client.GetStream();
            var partialRequest = Encoding.ASCII.GetBytes(
                "POST /mcp HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 100\r\n\r\n{");
            await stream.WriteAsync(partialRequest, connectTimeout.Token);

            server.Stop();

            Assert.True(await WaitForConnectionCloseAsync(stream), "Stopping the MCP server left a partial client connection open.");
            server.ApplySettings(settings);
            Assert.True(server.IsRunning, server.LastStatusMessage);
        }
        finally
        {
            server.Stop();
            server.ApplyConfig();
        }
    }

    [Fact]
    public async Task McpServerRejectsClientsBeyondConcurrencyLimit()
    {
        var server = CopilotMcpServer.Instance;
        var port = ReserveLoopbackPort();
        var settings = new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = Token,
        };
        var clients = new List<TcpClient>();
        try
        {
            server.ApplySettings(settings);
            Assert.True(server.IsRunning, server.LastStatusMessage);
            var partialRequest = Encoding.ASCII.GetBytes(
                "POST /mcp HTTP/1.1\r\nHost: 127.0.0.1\r\nContent-Length: 100\r\n\r\n{");
            for (var index = 0; index < CopilotMcpServer.MaximumConcurrentClients; index++)
            {
                var client = new TcpClient();
                clients.Add(client);
                using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await client.ConnectAsync(IPAddress.Loopback, port, connectTimeout.Token);
                await client.GetStream().WriteAsync(partialRequest, connectTimeout.Token);
            }

            Assert.True(
                await WaitForActiveClientCountAsync(server, CopilotMcpServer.MaximumConcurrentClients),
                "The MCP server did not account for all partial clients before the overflow connection was opened.");
            using var overflowClient = new TcpClient();
            using var overflowTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await overflowClient.ConnectAsync(IPAddress.Loopback, port, overflowTimeout.Token);

            Assert.True(
                await WaitForConnectionCloseAsync(overflowClient.GetStream()),
                "The MCP server kept a connection open after its concurrent client capacity was exhausted.");
        }
        finally
        {
            foreach (var client in clients)
                client.Dispose();
            server.Stop();
            await WaitForActiveClientCountAsync(server, 0);
            server.ApplyConfig();
        }
    }

    [Fact]
    public async Task McpServerEnforcesHeaderAndLengthBoundariesOverTcp()
    {
        var server = CopilotMcpServer.Instance;
        var port = ReserveLoopbackPort();
        var settings = new CopilotMcpRuntimeSettings
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = port,
            BearerToken = Token,
        };
        try
        {
            server.ApplySettings(settings);
            Assert.True(server.IsRunning, server.LastStatusMessage);

            var validResponse = await SendRawHttpRequestAsync(
                port,
                $"GET /mcp HTTP/1.1\r\nHost: 127.0.0.1\r\nAuthorization: Bearer {Token}\r\n\r\n");
            var oversizedHeaderResponse = await SendRawHttpRequestAsync(
                port,
                "GET /mcp HTTP/1.1\r\nX-Oversized: " + new string('x', 64 * 1024) + "\r\n\r\n",
                allowConnectionReset: true);
            var negativeLengthResponse = await SendRawHttpRequestAsync(
                port,
                $"POST /mcp HTTP/1.1\r\nAuthorization: Bearer {Token}\r\nContent-Length: -1\r\n\r\n");
            var ambiguousLengthResponse = await SendRawHttpRequestAsync(
                port,
                $"POST /mcp HTTP/1.1\r\nAuthorization: Bearer {Token}\r\nContent-Length: 0\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n");

            Assert.StartsWith("HTTP/1.1 200 OK", validResponse, StringComparison.Ordinal);
            Assert.True(
                oversizedHeaderResponse.Length == 0 || oversizedHeaderResponse.StartsWith("HTTP/1.1 400 Bad Request", StringComparison.Ordinal),
                $"Expected an immediate close or 400 response for an oversized header, but received: {oversizedHeaderResponse}");
            Assert.StartsWith("HTTP/1.1 400 Bad Request", negativeLengthResponse, StringComparison.Ordinal);
            Assert.StartsWith("HTTP/1.1 400 Bad Request", ambiguousLengthResponse, StringComparison.Ordinal);
        }
        finally
        {
            server.Stop();
            await WaitForActiveClientCountAsync(server, 0);
            server.ApplyConfig();
        }
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
    public async Task AuditLog_CanFilterByToolAndFailuresOnly()
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

            var response = await CallToolAsync(handler, "get_audit_log", new { max_entries = 10, tool = "read_allowed_file", failed_only = true });
            var toolResult = ReadToolResult(response);

            Assert.False(toolResult.IsError);
            Assert.Contains("Tool: read_allowed_file", toolResult.Text, StringComparison.Ordinal);
            Assert.Contains("Success: False", toolResult.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tool: search_files", toolResult.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Success: True", toolResult.Text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(outsidePath);
        }
    }

    [Fact]
    public async Task AuditLog_CanFilterByActionId()
    {
        var fixture = await CreatePendingMenuActionAsync();

        var response = await CallToolAsync(fixture.Handler, "get_audit_log", new { max_entries = 20, action_id = fixture.ActionId });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("Tool: action_created", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains($"Action id: {fixture.ActionId}", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Tool: execute_menu", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeEnvironmentSummary_DoesNotExposeSecrets()
    {
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "get_runtime_environment_summary", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("ColorVision version:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Process:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Config directory:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("MCP listener running:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Process start time:", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api_key", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFlowSummary_WhenNoActiveFlow_ReturnsStableMessage()
    {
        var handler = CreateHandler(flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(null));

        var response = await CallToolAsync(handler, "get_flow_summary", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("No active flow", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetFlowSummary_ReturnsSelectedNodesAndRecentRunSummary()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "InspectionFlow",
            TemplateName = "TemplateA",
            Status = "Ready",
            RecentRunMessage = "Last run completed",
            RecentFailureSummary = "No recent error",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Camera Node",
                    NodeName = "Camera1",
                    NodeType = "Camera",
                    NodeId = "node-1",
                    IsSelected = true,
                    Parameters = new[]
                    {
                        new CopilotContextProperty { Name = "Exposure", Value = "12" },
                    },
                },
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Analysis Node",
                    NodeName = "Algorithm1",
                    NodeType = "Algorithm",
                    NodeId = "node-2",
                },
            },
        };
        var handler = CreateHandler(flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot));

        var response = await CallToolAsync(handler, "get_flow_summary", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("Flow name: InspectionFlow", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Node count: 2", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Selected node count: 1", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Selected nodes: Camera Node", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Recent run message:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Recent failure summary: No recent error", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFlowGraph_ReturnsStructuredPortsEdgesRevisionAndRedactsProperties()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            Revision = "revision-123",
            FlowName = "InspectionFlow",
            TemplateId = "template-1",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    InstanceId = "instance-camera",
                    NodeId = "instance-camera",
                    TypeKey = "FlowEngineLib.dll|FlowEngineLib.CVCameraNode",
                    RuntimeType = "FlowEngineLib.CVCameraNode",
                    Title = "Camera",
                    Left = 10,
                    Top = 20,
                    OutputPorts = new[]
                    {
                        new CopilotFlowPortContextSnapshot { PortId = "out:0", Name = "Image", DataType = "CVImage", ConnectionCount = 1 },
                    },
                    Parameters = new[]
                    {
                        new CopilotContextProperty { Name = "ApiKey", Value = "secret-value" },
                    },
                },
                new CopilotFlowNodeContextSnapshot
                {
                    InstanceId = "instance-algorithm",
                    NodeId = "instance-algorithm",
                    TypeKey = "FlowEngineLib.dll|FlowEngineLib.AlgorithmNode",
                    RuntimeType = "FlowEngineLib.AlgorithmNode",
                    Title = "Algorithm",
                    InputPorts = new[]
                    {
                        new CopilotFlowPortContextSnapshot { PortId = "in:0", Name = "Image", DataType = "CVImage", ConnectionCount = 1 },
                    },
                },
            },
            Edges = new[]
            {
                new CopilotFlowEdgeContextSnapshot
                {
                    SourceNodeId = "instance-camera",
                    SourcePortId = "out:0",
                    TargetNodeId = "instance-algorithm",
                    TargetPortId = "in:0",
                    DataType = "CVImage",
                },
            },
        };
        var handler = CreateHandler(flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot));

        var response = await CallToolAsync(handler, "get_flow_graph", new { include_properties = true, max_nodes = 80 });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("colorvision.flow-graph.v1", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("revision-123", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("FlowEngineLib.CVCameraNode", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("instance-camera", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("out:0", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("instance-algorithm", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("redacted", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetFlowNodeCatalog_ReturnsExactCameraTypeAndWritablePropertySchema()
    {
        var catalog = new CopilotFlowNodeCatalogSnapshot
        {
            Query = "相机",
            TotalMatches = 2,
            NodeTypes = new[]
            {
                new CopilotFlowNodeTypeContextSnapshot
                {
                    TypeKey = "FlowEngineLib.dll|FlowEngineLib.CVCameraNode",
                    RuntimeType = "FlowEngineLib.CVCameraNode",
                    CategoryPath = "/02 相机",
                    Title = "ColorVision相机",
                    NodeType = "Camera",
                    DefaultDeviceCode = "DEV.Camera.Default",
                    Properties = new[]
                    {
                        new CopilotFlowNodePropertySchemaSnapshot
                        {
                            PropertyName = "ExpTime",
                            DisplayName = "曝光时间",
                            DataType = "System.Single",
                            IsWritable = true,
                        },
                    },
                },
            },
        };
        var handler = CreateHandler(flowNodeCatalogProvider: (_, _, _) => Task.FromResult<CopilotFlowNodeCatalogSnapshot?>(catalog));

        var response = await CallToolAsync(handler, "get_flow_node_catalog", new { query = "相机", max_results = 30 });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("colorvision.flow-node-catalog.v1", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("FlowEngineLib.dll|FlowEngineLib.CVCameraNode", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("DEV.Camera.Default", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("ExpTime", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Choose an exact typeKey", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewAndApplyFlowPatch_UseRevisionAndRequireApprovalBeforeMutation()
    {
        var applyCount = 0;
        var handler = CreateHandler(
            previewFlowPatchHandler: (request, _) => Task.FromResult(CopilotMcpToolCallResult.Ok($"preview operation={request.Operation}; type={request.TypeKey}; position={request.Left},{request.Top}; revision={request.ExpectedRevision}")),
            applyFlowPatchHandler: (_, _) =>
            {
                applyCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("applied"));
            });

        var preview = ReadToolResult(await CallToolAsync(handler, "preview_flow_patch", new
        {
            operation = "add_node",
            type_key = "FlowEngineLib.dll|FlowEngineLib.CVCameraNode",
            left = 120,
            top = 240,
            expected_revision = "revision-123",
        }));
        var apply = ReadToolResult(await CallToolAsync(handler, "apply_flow_patch", new
        {
            operation = "add_node",
            type_key = "FlowEngineLib.dll|FlowEngineLib.CVCameraNode",
            left = 120,
            top = 240,
            expected_revision = "revision-123",
        }));

        Assert.False(preview.IsError);
        Assert.Contains("revision-123", preview.Text, StringComparison.Ordinal);
        Assert.True(apply.IsError);
        Assert.Contains("must approve", apply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Does not save or run", apply.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, applyCount);
    }

    [Theory]
    [InlineData("set_property")]
    [InlineData("connect")]
    public async Task PreviewFlowPatch_PassesBoundedPropertyAndConnectionOperations(string operation)
    {
        CopilotFlowPatchRequest? captured = null;
        var handler = CreateHandler(previewFlowPatchHandler: (request, _) =>
        {
            captured = request;
            return Task.FromResult(CopilotMcpToolCallResult.Ok("previewed"));
        });
        object arguments = operation == "set_property"
            ? new { operation, expected_revision = "revision-1", node_id = "node-1", property_name = "Exposure", value = "12.5" }
            : new { operation, expected_revision = "revision-1", source_node_id = "node-1", source_port_id = "out:0", target_node_id = "node-2", target_port_id = "in:0" };

        var result = ReadToolResult(await CallToolAsync(handler, "preview_flow_patch", arguments));

        Assert.False(result.IsError);
        Assert.NotNull(captured);
        Assert.Equal(operation, captured.Operation);
        Assert.Equal("revision-1", captured.ExpectedRevision);
        if (operation == "set_property")
        {
            Assert.Equal("node-1", captured.NodeId);
            Assert.Equal("Exposure", captured.PropertyName);
            Assert.Equal("12.5", captured.Value);
        }
        else
        {
            Assert.Equal("out:0", captured.SourcePortId);
            Assert.Equal("in:0", captured.TargetPortId);
        }
    }

    [Fact]
    public async Task PreviewFlowPatch_RejectsIncompleteOperationBeforeHandler()
    {
        var handlerCalls = 0;
        var handler = CreateHandler(previewFlowPatchHandler: (_, _) =>
        {
            handlerCalls++;
            return Task.FromResult(CopilotMcpToolCallResult.Ok("unexpected"));
        });

        var result = ReadToolResult(await CallToolAsync(handler, "preview_flow_patch", new
        {
            operation = "connect",
            expected_revision = "revision-1",
            source_node_id = "node-1",
        }));

        Assert.True(result.IsError);
        Assert.Contains("source_port_id", result.Text, StringComparison.Ordinal);
        Assert.Equal(0, handlerCalls);
    }

    [Fact]
    public async Task GetActiveTemplateContext_ReturnsTemplateSummaryWithoutSecrets()
    {
        var liveContext = new CopilotLiveContext
        {
            SourceId = "template-json-editor:test",
            Title = "Template JSON editor - CameraTemplate",
            Summary = "JSON lines 8 · unchanged · valid",
            SnapshotItems = new[]
            {
                new CopilotContextItem
                {
                    Title = "CameraTemplate",
                    Summary = "valid template",
                    Content = "Surface: Template JSON editor\nTemplate name: CameraTemplate\nCurrent selection: CameraTemplate\nJSON validation: passed\nJSON line count: 8\n\nCurrent JSON:\n```json\n{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 12,\n  \"McpBearerToken\": \"secret-token-value\"\n}\n```",
                },
            },
        };
        var handler = CreateHandler(liveContextProvider: () => liveContext);

        var response = await CallToolAsync(handler, "get_active_template_context", new { });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("Template type: CameraRun", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Template name from JSON: CameraTemplate", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Key parameter summary:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Exposure=12", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-value", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewTemplatePatch_ReturnsDiffAndDoesNotSave()
    {
        const string currentJson = "{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 10\n}";
        var liveContext = new CopilotLiveContext
        {
            SourceId = "template-json-editor:test",
            Title = "Template JSON editor - CameraTemplate",
            SnapshotItems = new[]
            {
                new CopilotContextItem
                {
                    Title = "CameraTemplate",
                    Content = "Current JSON:\n```json\n" + currentJson + "\n```",
                },
            },
        };
        var handler = CreateHandler(liveContextProvider: () => liveContext);

        var response = await CallToolAsync(handler, "preview_template_patch", new
        {
            template_identifier = "CameraTemplate",
            proposed_changes = new
            {
                Exposure = 12,
                Name = "CameraTemplateV2",
            },
        });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Contains("Mode: preview only", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Would save: False", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Exposure", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("No template file was saved", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("\"Exposure\": 10", liveContext.SnapshotItems[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewTemplatePatch_RejectsSensitiveFields()
    {
        var handler = CreateHandler();

        var response = await CallToolAsync(handler, "preview_template_patch", new
        {
            template_identifier = "CameraTemplate",
            current_json = "{ \"Name\": \"CameraTemplate\" }",
            proposed_changes = new
            {
                ApiKey = "secret-value",
            },
        });
        var toolResult = ReadToolResult(response);
        var auditResult = ReadToolResult(await CallToolAsync(handler, "get_audit_log", new { max_entries = 10 }));

        Assert.True(toolResult.IsError);
        Assert.Contains("sensitive", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", toolResult.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", auditResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTemplatePatch_BeforeUserApproval_DoesNotSave()
    {
        var fixture = await CreatePendingTemplatePatchAsync();

        Assert.Equal(0, fixture.ApplyCount());
        Assert.Contains("\"Exposure\": 10", fixture.CurrentJson(), StringComparison.Ordinal);
        Assert.Equal(1, CopilotMcpConfirmationStore.Instance.PendingCount);
        Assert.False(string.IsNullOrWhiteSpace(fixture.ActionId));
        Assert.False(string.IsNullOrWhiteSpace(fixture.ArgumentsSummary));
    }

    [Fact]
    public async Task ApplyTemplatePatch_AfterUserApproval_AppliesPatchOnce()
    {
        var fixture = await CreatePendingTemplatePatchAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary,
        }));
        var duplicateResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.False(confirmResult.IsError);
        Assert.Equal(1, fixture.ApplyCount());
        Assert.Contains("\"Exposure\": 12", fixture.CurrentJson(), StringComparison.Ordinal);
        Assert.True(duplicateResult.IsError);
        Assert.Equal(1, fixture.ApplyCount());
    }

    [Fact]
    public async Task ApplyTemplatePatch_ArgumentsMismatch_DoesNotSave()
    {
        var fixture = await CreatePendingTemplatePatchAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary + "; changed=true",
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ApplyCount());
        Assert.Contains("\"Exposure\": 10", fixture.CurrentJson(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyTemplatePatch_RejectedAction_DoesNotSave()
    {
        var fixture = await CreatePendingTemplatePatchAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Reject(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ApplyCount());
    }

    [Fact]
    public async Task ApplyTemplatePatch_ExpiredAction_DoesNotSave()
    {
        CopilotMcpConfirmationStore.Instance.ActionLifetime = TimeSpan.FromMilliseconds(-1);
        var fixture = await CreatePendingTemplatePatchAsync();

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ApplyCount());
        Assert.Contains("expired", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyTemplatePatch_Conflict_DoesNotSave()
    {
        var fixture = await CreatePendingTemplatePatchAsync();
        fixture.SetCurrentJson("{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 11\n}");
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "apply_template_patch",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ApplyCount());
        Assert.Contains("changed after preview", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyTemplatePatch_RequiresPreviewAndRejectsSensitiveFields()
    {
        var handler = CreateHandler();

        var applyWithoutPreview = ReadToolResult(await CallToolAsync(handler, "apply_template_patch", new { preview_id = "missing" }));
        var sensitivePreview = ReadToolResult(await CallToolAsync(handler, "preview_template_patch", new
        {
            template_identifier = "CameraTemplate",
            current_json = "{ \"Name\": \"CameraTemplate\" }",
            proposed_changes = new
            {
                Authorization = "Bearer secret-value",
            },
        }));

        Assert.True(applyWithoutPreview.IsError);
        Assert.Contains("Call preview_template_patch first", applyWithoutPreview.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(sensitivePreview.IsError);
        Assert.Contains("sensitive", sensitivePreview.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", sensitivePreview.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDiagnosticBundle_RedactsSecretsAndHonorsMaxChars()
    {
        const int maxChars = 1200;
        var liveContext = new CopilotLiveContext
        {
            SourceId = "template-json-editor:test",
            Title = "Template JSON editor - SecretTemplate",
            SnapshotItems = new[]
            {
                new CopilotContextItem
                {
                    Title = "SecretTemplate",
                    Content = "Current JSON:\n```json\n{\n  \"Name\": \"SecretTemplate\",\n  \"ApiKey\": \"secret-value\",\n  \"Password\": \"password-value\"\n}\n```",
                },
            },
        };
        var handler = CreateHandler(
            liveContextProvider: () => liveContext,
            recentLogProvider: (_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult
            {
                Success = true,
                Summary = "Recent log",
                Content = "token=secret-token-value password=password-value apiKey=secret-value",
            }));

        var result = ReadToolResult(await CallToolAsync(handler, "get_diagnostic_bundle", new { max_chars = maxChars }));

        Assert.False(result.IsError);
        Assert.True(result.Text.Length <= maxChars, $"Diagnostic bundle exceeded max_chars: {result.Text.Length}");
        Assert.Contains("ColorVision MCP diagnostic bundle", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("password-value", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-value", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnoseFlowFailure_ReturnsReadOnlyDiagnosisAndRedactsSecrets()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "InspectionFlow",
            Status = "Failed",
            LastNodeSummary = "Camera Node",
            RecentRunMessage = "last run failed at Camera Node with timeout",
            RecentFailureSummary = "Camera Node timeout",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Camera Node",
                    NodeName = "Camera1",
                    NodeType = "Camera",
                    NodeId = "node-1",
                    Mark = "last error: timeout",
                    Parameters = new[] { new CopilotContextProperty { Name = "Exposure", Value = "12" } },
                },
            },
        };
        var currentJson = "{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 10,\n  \"TimeoutMs\": 1000,\n  \"ApiKey\": \"secret-value\"\n}";
        var handler = CreateHandler(
            flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot),
            liveContextProvider: () => CreateTemplateLiveContext(currentJson),
            recentLogProvider: (_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult
            {
                Success = true,
                Summary = "Recent log",
                Content = "Camera timeout token=secret-token-value",
            }));

        var result = ReadToolResult(await CallToolAsync(handler, "diagnose_flow_failure", new { node_name = "Camera", query = "timeout" }));

        Assert.False(result.IsError);
        Assert.Contains("ColorVision flow failure diagnosis", result.Text, StringComparison.Ordinal);
        Assert.Contains("Mode: read-only diagnosis", result.Text, StringComparison.Ordinal);
        Assert.Contains("Camera/acquisition evidence", result.Text, StringComparison.Ordinal);
        Assert.Contains("suggest_template_patch", result.Text, StringComparison.Ordinal);
        Assert.Contains("No flow was started", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token-value", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestTemplatePatch_ReturnsPreviewPayloadAndWarnings()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "InspectionFlow",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Camera Node",
                    NodeName = "Camera1",
                    NodeType = "Camera",
                    NodeId = "node-1",
                    Mark = "timeout",
                    Parameters = new[] { new CopilotContextProperty { Name = "Exposure", Value = "12" } },
                },
            },
        };
        var currentJson = "{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 10,\n  \"TimeoutMs\": 1000,\n  \"Offset\": 5\n}";
        var handler = CreateHandler(
            flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot),
            liveContextProvider: () => CreateTemplateLiveContext(currentJson));

        var result = ReadToolResult(await CallToolAsync(handler, "suggest_template_patch", new
        {
            template_identifier = "CameraTemplate",
            intent = "Camera timeout",
            node_name = "Camera",
            proposed_changes = new
            {
                TimeoutMs = "2000",
                Offset = (string?)null,
                NewField = 1,
            },
        }));

        Assert.False(result.IsError);
        Assert.Contains("ColorVision template patch suggestion", result.Text, StringComparison.Ordinal);
        Assert.Contains("Would apply: False", result.Text, StringComparison.Ordinal);
        Assert.Contains("Candidate Fields", result.Text, StringComparison.Ordinal);
        Assert.Contains("Call preview_template_patch", result.Text, StringComparison.Ordinal);
        Assert.Contains("Warning: TimeoutMs changes type", result.Text, StringComparison.Ordinal);
        Assert.Contains("Warning: Offset is set to null", result.Text, StringComparison.Ordinal);
        Assert.Contains("Warning: NewField is a new top-level key", result.Text, StringComparison.Ordinal);
        Assert.Contains("\"template_identifier\": \"CameraTemplate\"", result.Text, StringComparison.Ordinal);
        Assert.Contains("\"TimeoutMs\": \"2000\"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuggestTemplatePatch_RejectsSensitiveProposedChanges()
    {
        var handler = CreateHandler(liveContextProvider: () => CreateTemplateLiveContext("{ \"Name\": \"CameraTemplate\" }"));

        var result = ReadToolResult(await CallToolAsync(handler, "suggest_template_patch", new
        {
            template_identifier = "CameraTemplate",
            intent = "update API key",
            proposed_changes = new
            {
                ApiKey = "secret-value",
            },
        }));

        Assert.True(result.IsError);
        Assert.Contains("sensitive", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewFlowAction_InspectsNodeAndRefusesRun()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "InspectionFlow",
            RecentFailureSummary = "Node Camera Node failed last run",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Camera Node",
                    NodeName = "Camera1",
                    NodeType = "Camera",
                    NodeId = "node-1",
                    Mark = "last error: timeout",
                },
            },
        };
        var handler = CreateHandler(flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot));

        var inspectResult = ReadToolResult(await CallToolAsync(handler, "preview_flow_action", new { action = "inspect_node_errors", node_name = "Camera" }));
        var runResult = ReadToolResult(await CallToolAsync(handler, "preview_flow_action", new { action = "run_flow" }));

        Assert.False(inspectResult.IsError);
        Assert.Contains("Mode: preview only", inspectResult.Text, StringComparison.Ordinal);
        Assert.Contains("Matched node: Camera Node", inspectResult.Text, StringComparison.Ordinal);
        Assert.Contains("No flow was started", inspectResult.Text, StringComparison.Ordinal);
        Assert.True(runResult.IsError);
        Assert.Contains("risk_level: confirmation-required", runResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not_supported_current_stage", runResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreviewFlowAction_ExplainsNodeAndTracesFailure()
    {
        var flowSnapshot = new CopilotFlowContextSnapshot
        {
            FlowName = "InspectionFlow",
            LastNodeSummary = "Camera Node",
            RecentRunMessage = "last run failed at Camera Node with timeout",
            RecentFailureSummary = "Camera Node timeout",
            Nodes = new[]
            {
                new CopilotFlowNodeContextSnapshot
                {
                    Title = "Camera Node",
                    NodeName = "Camera1",
                    NodeType = "Camera",
                    NodeId = "node-1",
                    Mark = "last error: timeout",
                    Inputs = new[] { "trigger" },
                    Outputs = new[] { "image" },
                    Parameters = new[] { new CopilotContextProperty { Name = "Exposure", Value = "12" } },
                },
            },
        };
        var handler = CreateHandler(flowSnapshotProvider: _ => Task.FromResult<CopilotFlowContextSnapshot?>(flowSnapshot));

        var explainResult = ReadToolResult(await CallToolAsync(handler, "preview_flow_action", new { action = "explain_node", node_name = "Camera" }));
        var traceResult = ReadToolResult(await CallToolAsync(handler, "preview_flow_action", new { action = "trace_recent_failure", node_name = "Camera" }));

        Assert.False(explainResult.IsError);
        Assert.Contains("Node parameters:", explainResult.Text, StringComparison.Ordinal);
        Assert.Contains("Suggested next steps:", explainResult.Text, StringComparison.Ordinal);
        Assert.False(traceResult.IsError);
        Assert.Contains("Recent failure summary: Camera Node timeout", traceResult.Text, StringComparison.Ordinal);
        Assert.Contains("No flow was started", traceResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteMenu_DryRun_DoesNotExecute()
    {
        var executionCount = 0;
        var observedDryRun = false;
        var handler = CreateHandler(
            executeMenuHandler: (query, dryRun, _) =>
            {
                observedDryRun = dryRun;
                if (!dryRun)
                    executionCount++;

                return Task.FromResult(CopilotMcpToolCallResult.Ok($"query={query}; dry_run={dryRun}; risk=low-risk-action; execution_status={(dryRun ? "dry_run_only" : "scheduled")}"));
            });

        var response = await CallToolAsync(handler, "execute_menu", new { query = "View > Copilot", dry_run = true });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.True(observedDryRun);
        Assert.Equal(0, executionCount);
        Assert.Contains("dry_run=True", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dry_run_only", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteMenu_DefaultsToDryRunTrue()
    {
        var observedDryRun = false;
        var handler = CreateHandler(
            executeMenuHandler: (_, dryRun, _) =>
            {
                observedDryRun = dryRun;
                return Task.FromResult(CopilotMcpToolCallResult.Ok($"dry_run={dryRun}; would_execute=True; matched_menu_path=View > Copilot; display_name=Copilot; source_type=main-window-menu; risk_level=low-risk-action"));
            });

        var response = await CallToolAsync(handler, "execute_menu", new { query = "View > Copilot" });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.True(observedDryRun);
        Assert.Contains("matched_menu_path", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("display_name", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("source_type", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("risk_level", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("would_execute", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteMenu_DryRunFalse_CanExecuteLowRiskMenu()
    {
        var executionCount = 0;
        var handler = CreateHandler(
            executeMenuHandler: (_, dryRun, _) =>
            {
                if (!dryRun)
                    executionCount++;

                return Task.FromResult(CopilotMcpToolCallResult.Ok("dry_run=False; would_execute=True; matched_menu_path=View > Copilot; display_name=Copilot; source_type=main-window-menu; risk_level=low-risk-action; execution_status=scheduled"));
            });

        var response = await CallToolAsync(handler, "execute_menu", new { query = "View > Copilot", dry_run = false });
        var toolResult = ReadToolResult(response);

        Assert.False(toolResult.IsError);
        Assert.Equal(1, executionCount);
        Assert.Contains("risk_level=low-risk-action", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("execution_status=scheduled", toolResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteMenu_DryRunFalse_RejectsConfirmationRequiredMenu()
    {
        var fixture = await CreatePendingMenuActionAsync();

        Assert.Equal(0, fixture.ExecutionCount());
        Assert.Equal(1, CopilotMcpConfirmationStore.Instance.PendingCount);
        Assert.False(string.IsNullOrWhiteSpace(fixture.ActionId));
        Assert.False(string.IsNullOrWhiteSpace(fixture.ArgumentsSummary));
    }

    [Fact]
    public void ConfirmableActionPayload_ContainsConfirmActionArguments()
    {
        var action = CopilotMcpConfirmationStore.Instance.Create(
            "Confirm menu command",
            "Execute ColorVision menu command: Tools > Update",
            "confirmation-required",
            "execute_menu",
            "query=Tools > Update, dry_run=False",
            _ => Task.FromResult(CopilotMcpToolCallResult.Ok("ok")));

        using var document = JsonDocument.Parse(action.ConfirmActionPayloadJson);
        var root = document.RootElement;

        Assert.Equal(action.ActionId, root.GetProperty("action_id").GetString());
        Assert.Equal("execute_menu", root.GetProperty("tool_name").GetString());
        Assert.Equal("query=Tools > Update, dry_run=False", root.GetProperty("arguments_summary").GetString());
    }

    [Fact]
    public async Task ConfirmAction_BeforeUserApproval_ReturnsPending()
    {
        var fixture = await CreatePendingMenuActionAsync();

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ExecutionCount());
        Assert.Contains("waiting for user approval", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmAction_AfterUserApproval_ExecutesOnce()
    {
        var fixture = await CreatePendingMenuActionAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary,
        }));
        var duplicateResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.False(confirmResult.IsError);
        Assert.Equal(1, fixture.ExecutionCount());
        Assert.Contains("menu executed after approval", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(duplicateResult.IsError);
        Assert.Contains("already been executed", duplicateResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmAction_RejectedAction_DoesNotExecute()
    {
        var fixture = await CreatePendingMenuActionAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Reject(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ExecutionCount());
        Assert.Contains("rejected", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmAction_ExpiredAction_DoesNotExecute()
    {
        CopilotMcpConfirmationStore.Instance.ActionLifetime = TimeSpan.FromMilliseconds(-1);
        var fixture = await CreatePendingMenuActionAsync();

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary,
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ExecutionCount());
        Assert.Contains("expired", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmAction_ArgumentsMismatch_DoesNotExecute()
    {
        var fixture = await CreatePendingMenuActionAsync();
        Assert.True(CopilotMcpConfirmationStore.Instance.Approve(fixture.ActionId, out _));

        var confirmResult = ReadToolResult(await CallToolAsync(fixture.Handler, "confirm_action", new
        {
            action_id = fixture.ActionId,
            tool_name = "execute_menu",
            arguments_summary = fixture.ArgumentsSummary + "; changed=true",
        }));

        Assert.True(confirmResult.IsError);
        Assert.Equal(0, fixture.ExecutionCount());
        Assert.Contains("do not match", confirmResult.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteMenu_UnknownMenu_ReturnsCandidates()
    {
        var handler = CreateHandler(
            executeMenuHandler: (_, _, _) => Task.FromResult(CopilotMcpToolCallResult.Fail(
                "menu_not_found",
                "No menu match. Candidates: View > Copilot; View > Log; Settings > Options")));

        var response = await CallToolAsync(handler, "execute_menu", new { query = "does-not-exist", dry_run = true });
        var toolResult = ReadToolResult(response);

        Assert.True(toolResult.IsError);
        Assert.Contains("Candidates:", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("Copilot", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenPanel_UnsupportedAlias_ReturnsSupportedAliases()
    {
        var handler = CreateHandler(
            openPanelHandler: (_, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("should not be called")));

        var response = await CallToolAsync(handler, "open_panel", new { panel = "unknown-panel" });
        var toolResult = ReadToolResult(response);

        Assert.True(toolResult.IsError);
        Assert.Contains("Unsupported panel alias", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("copilot", toolResult.Text, StringComparison.Ordinal);
        Assert.Contains("device", toolResult.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LowRiskActionTools_RouteThroughEnvironmentHandlers()
    {
        var openedPanel = string.Empty;
        var executedMenu = string.Empty;
        var executedDryRun = true;
        var handler = CreateHandler(
            openPanelHandler: (panel, _) =>
            {
                openedPanel = panel;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("opened " + panel));
            },
            executeMenuHandler: (query, dryRun, _) =>
            {
                executedMenu = query;
                executedDryRun = dryRun;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("executed " + query));
            });

        var panelResult = ReadToolResult(await CallToolAsync(handler, "open_panel", new { panel = "copilot" }));
        var menuResult = ReadToolResult(await CallToolAsync(handler, "execute_menu", new { query = "View/Copilot", dry_run = false }));

        Assert.False(panelResult.IsError);
        Assert.False(menuResult.IsError);
        Assert.Equal("copilot", openedPanel);
        Assert.Equal("View/Copilot", executedMenu);
        Assert.False(executedDryRun);
    }

    private async Task<(CopilotMcpRequestHandler Handler, string ActionId, string ArgumentsSummary, Func<int> ExecutionCount)> CreatePendingMenuActionAsync()
    {
        var firstExecutionAttempt = true;
        var executionCount = 0;
        var handler = CreateHandler(
            executeMenuHandler: (_, dryRun, _) =>
            {
                if (!dryRun && firstExecutionAttempt)
                {
                    firstExecutionAttempt = false;
                    return Task.FromResult(CopilotMcpToolCallResult.Fail(
                        "confirmation_required",
                        "dry_run=False; would_execute=False; matched_menu_path=Tools > Update; display_name=Update; source_type=main-window-menu; risk_level=confirmation-required; execution_status=confirmation_required"));
                }

                if (!dryRun)
                    executionCount++;

                return Task.FromResult(CopilotMcpToolCallResult.Ok("menu executed after approval"));
            });

        var response = await CallToolAsync(handler, "execute_menu", new { query = "Tools > Update", dry_run = false });
        var toolResult = ReadToolResult(response);

        Assert.True(toolResult.IsError);
        Assert.Contains("confirmation_required", toolResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action_id:", toolResult.Text, StringComparison.Ordinal);

        return (handler, ExtractField(toolResult.Text, "action_id"), ExtractField(toolResult.Text, "arguments_summary"), () => executionCount);
    }

    private async Task<(
        CopilotMcpRequestHandler Handler,
        string PreviewId,
        string ActionId,
        string ArgumentsSummary,
        Func<string> CurrentJson,
        Action<string> SetCurrentJson,
        Func<int> ApplyCount)> CreatePendingTemplatePatchAsync()
    {
        var currentJson = "{\n  \"TemplateType\": \"CameraRun\",\n  \"Name\": \"CameraTemplate\",\n  \"Exposure\": 10\n}";
        var applyCount = 0;
        var handler = CreateHandler(
            liveContextProvider: () => CreateTemplateLiveContext(currentJson),
            applyTemplatePatchHandler: (request, _) =>
            {
                applyCount++;
                currentJson = request.PatchedJson;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("template patch applied after approval"));
            });

        var previewResult = ReadToolResult(await CallToolAsync(handler, "preview_template_patch", new
        {
            template_identifier = "CameraTemplate",
            proposed_changes = new
            {
                Exposure = 12,
            },
        }));
        Assert.False(previewResult.IsError);
        Assert.Contains("preview_id:", previewResult.Text, StringComparison.Ordinal);
        var previewId = ExtractField(previewResult.Text, "preview_id");

        var applyResult = ReadToolResult(await CallToolAsync(handler, "apply_template_patch", new { preview_id = previewId }));
        Assert.True(applyResult.IsError);
        Assert.Contains("confirmation_required", applyResult.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, applyCount);

        return (
            handler,
            previewId,
            ExtractField(applyResult.Text, "action_id"),
            ExtractField(applyResult.Text, "arguments_summary"),
            () => currentJson,
            value => currentJson = value,
            () => applyCount);
    }

    private static CopilotLiveContext CreateTemplateLiveContext(string currentJson)
    {
        return new CopilotLiveContext
        {
            SourceId = "template-json-editor:test",
            Title = "Template JSON editor - CameraTemplate",
            SnapshotItems = new[]
            {
                new CopilotContextItem
                {
                    Title = "CameraTemplate",
                    Content = "Surface: Template JSON editor\nTemplate name: CameraTemplate\nCurrent JSON:\n```json\n" + currentJson + "\n```",
                },
            },
        };
    }

    private static string ExtractField(string text, string fieldName)
    {
        var line = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault(item => item.TrimStart().StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase));

        Assert.False(string.IsNullOrWhiteSpace(line), $"Missing field {fieldName} in text: {text}");
        var separatorIndex = line.IndexOf(':');
        return line[(separatorIndex + 1)..].Trim();
    }

    private CopilotMcpRequestHandler CreateHandler(
        bool enabled = true,
        Func<string, CancellationToken, Task<CopilotMcpToolCallResult>>? openPanelHandler = null,
        Func<string, bool, CancellationToken, Task<CopilotMcpToolCallResult>>? executeMenuHandler = null,
        Func<CopilotLiveContext?>? liveContextProvider = null,
        Func<CopilotAgentTaskEventJournalContext?>? taskEventJournalProvider = null,
        Func<CancellationToken, Task<CopilotFlowContextSnapshot?>>? flowSnapshotProvider = null,
        Func<string?, int, CancellationToken, Task<CopilotFlowNodeCatalogSnapshot?>>? flowNodeCatalogProvider = null,
        Func<CopilotFlowPatchRequest, CancellationToken, Task<CopilotMcpToolCallResult>>? previewFlowPatchHandler = null,
        Func<CopilotFlowPatchRequest, CancellationToken, Task<CopilotMcpToolCallResult>>? applyFlowPatchHandler = null,
        Func<CopilotTemplatePatchApplyRequest, CancellationToken, Task<CopilotMcpToolCallResult>>? applyTemplatePatchHandler = null,
        Func<string?, CopilotRecentLogMode, int, int, CancellationToken, Task<CopilotCapabilityResult>>? recentLogProvider = null)
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
            LiveContextProvider = liveContextProvider ?? (() => null),
            TaskEventJournalProvider = taskEventJournalProvider ?? (() => null),
            FlowSnapshotProvider = flowSnapshotProvider ?? (_ => Task.FromResult<CopilotFlowContextSnapshot?>(null)),
            FlowNodeCatalogProvider = flowNodeCatalogProvider ?? ((_, _, _) => Task.FromResult<CopilotFlowNodeCatalogSnapshot?>(null)),
            PreviewFlowPatchHandler = previewFlowPatchHandler ?? ((_, _) => Task.FromResult(CopilotMcpToolCallResult.Fail("flow_unavailable", "No test Flow editor is available."))),
            ApplyFlowPatchHandler = applyFlowPatchHandler ?? ((_, _) => Task.FromResult(CopilotMcpToolCallResult.Fail("flow_unavailable", "No test Flow editor is available."))),
            ApplyTemplatePatchHandler = applyTemplatePatchHandler ?? ((_, _) => Task.FromResult(CopilotMcpToolCallResult.Fail("apply_template_patch_unavailable", "No test apply handler is available."))),
            RecentLogProvider = recentLogProvider ?? ((_, _, _, _, _) => Task.FromResult(new CopilotCapabilityResult
            {
                Success = false,
                Summary = "No test log is available.",
                ErrorMessage = "No test log is available.",
            })),
        };

        var dispatcher = new CopilotMcpToolDispatcher(environment);
        return new CopilotMcpRequestHandler(() => new CopilotMcpRuntimeSettings
        {
            Enabled = enabled,
            BearerToken = Token,
            Port = 38473,
        }, dispatcher);
    }

    private static async Task<CopilotMcpHttpResponse> CallToolAsync(
        CopilotMcpRequestHandler handler,
        string toolName,
        object arguments,
        CancellationToken cancellationToken = default)
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

        return await handler.HandleAsync(CreatePostRequest(body, authorized: true), cancellationToken);
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

    private static CopilotMcpHttpRequest CreateJsonRpcRequest(string method, bool authorized, string? bearerToken = null)
    {
        var body = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
        });
        return CreatePostRequest(body, authorized, bearerToken);
    }

    private static CopilotMcpHttpRequest CreatePostRequest(string body, bool authorized, string? bearerToken = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (authorized)
            headers["Authorization"] = "Bearer " + (bearerToken ?? Token);

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

    private static int ReserveLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<bool> WaitForConnectionCloseAsync(NetworkStream stream)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            var buffer = new byte[1];
            return await stream.ReadAsync(buffer, timeout.Token) == 0;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            return true;
        }
    }

    private static async Task<bool> WaitForActiveClientCountAsync(CopilotMcpServer server, int expectedCount)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            while (server.ActiveClientCount != expectedCount)
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            return true;
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            return false;
        }
    }

    private static async Task<string> SendRawHttpRequestAsync(int port, string request, bool allowConnectionReset = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new TcpClient();
        using var response = new MemoryStream();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            var stream = client.GetStream();
            var requestBytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(requestBytes, timeout.Token);
            client.Client.Shutdown(SocketShutdown.Send);

            var buffer = new byte[4096];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                    break;
                response.Write(buffer, 0, read);
            }
        }
        catch (IOException ex) when (allowConnectionReset && ex.InnerException is SocketException socketException &&
            socketException.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
        {
        }
        catch (SocketException ex) when (allowConnectionReset &&
            ex.SocketErrorCode is SocketError.ConnectionReset or SocketError.ConnectionAborted)
        {
        }

        return Encoding.UTF8.GetString(response.ToArray());
    }

    public void Dispose()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
        }
        CopilotMcpTemplatePatchPreviewStore.Instance.ClearForTests();
    }
}
