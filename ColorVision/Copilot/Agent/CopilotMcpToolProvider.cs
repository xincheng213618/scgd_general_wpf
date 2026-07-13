using ColorVision.Copilot.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public interface ICopilotExternalToolProvider
    {
        Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken);
    }

    public sealed class CopilotExternalToolLease : IAsyncDisposable
    {
        private readonly IReadOnlyList<IAsyncDisposable> _resources;

        public CopilotExternalToolLease(
            IReadOnlyList<ICopilotTool>? tools = null,
            IReadOnlyList<string>? diagnostics = null,
            IReadOnlyList<IAsyncDisposable>? resources = null)
        {
            Tools = tools ?? Array.Empty<ICopilotTool>();
            Diagnostics = diagnostics ?? Array.Empty<string>();
            _resources = resources ?? Array.Empty<IAsyncDisposable>();
        }

        public IReadOnlyList<ICopilotTool> Tools { get; }

        public IReadOnlyList<string> Diagnostics { get; }

        public async ValueTask DisposeAsync()
        {
            foreach (var resource in _resources.Reverse())
            {
                try
                {
                    await resource.DisposeAsync();
                }
                catch
                {
                }
            }
        }
    }

    internal sealed class CopilotMcpToolProvider : ICopilotExternalToolProvider
    {
        private const int MaximumToolsPerServer = 32;
        private const int MaximumToolsPerRequest = 64;

        public async Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var tools = new List<ICopilotTool>();
            var diagnostics = new List<string>();
            var clients = new List<IAsyncDisposable>();
            foreach (var server in request.ExternalMcpServers.Where(server => server?.Enabled == true).Take(8))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tools.Count >= MaximumToolsPerRequest)
                    break;
                McpClient? client = null;
                try
                {
                    var token = ResolveBearerToken(server);
                    var headers = string.IsNullOrWhiteSpace(token)
                        ? null
                        : new Dictionary<string, string> { ["Authorization"] = "Bearer " + token };
                    var transport = new HttpClientTransport(new HttpClientTransportOptions
                    {
                        Name = server.Name,
                        Endpoint = new Uri(server.Endpoint),
                        TransportMode = HttpTransportMode.StreamableHttp,
                        ConnectionTimeout = TimeSpan.FromSeconds(server.ConnectionTimeoutSeconds),
                        AdditionalHeaders = headers,
                    });

                    using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectionTimeout.CancelAfter(TimeSpan.FromSeconds(server.ConnectionTimeoutSeconds));
                    client = await McpClient.CreateAsync(transport, cancellationToken: connectionTimeout.Token);
                    var remoteTools = await client.ListToolsAsync(cancellationToken: connectionTimeout.Token);
                    var remaining = MaximumToolsPerRequest - tools.Count;
                    var allowedTools = remoteTools
                        .Select(tool => server.TryResolveToolAccessPolicy(tool.Name, out var accessPolicy)
                            ? new AllowedMcpTool(tool, accessPolicy)
                            : null)
                        .OfType<AllowedMcpTool>()
                        .Take(Math.Min(MaximumToolsPerServer, remaining))
                        .ToArray();
                    foreach (var allowedTool in allowedTools)
                        tools.Add(new CopilotMcpToolAdapter(server, allowedTool.Tool, allowedTool.AccessPolicy));
                    if (allowedTools.Length > 0)
                    {
                        clients.Add(client);
                        client = null;
                    }
                    CopilotMcpClientHealthRegistry.RecordConnected(server, remoteTools.Count, allowedTools.Length);
                    diagnostics.Add(allowedTools.Length == remoteTools.Count
                        ? $"MCP client connected to {server.Name} · {allowedTools.Length} tool(s) exposed."
                        : $"MCP client connected to {server.Name} · {allowedTools.Length}/{remoteTools.Count} tool(s) exposed by policy and request limits.");
                    if (tools.Count >= MaximumToolsPerRequest)
                    {
                        diagnostics.Add($"MCP client discovery reached the {MaximumToolsPerRequest}-tool request limit.");
                        break;
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    CopilotMcpClientHealthRegistry.RecordUnavailable(server, "Connection timed out.");
                    diagnostics.Add($"MCP client {server.Name} was unavailable · connection timed out.");
                }
                catch (Exception ex)
                {
                    var error = CopilotMcpAuditLogger.RedactText(ex.Message);
                    CopilotMcpClientHealthRegistry.RecordUnavailable(server, error);
                    diagnostics.Add($"MCP client {server.Name} was unavailable · {error}");
                }
                finally
                {
                    if (client != null)
                        await client.DisposeAsync();
                }
            }

            return new CopilotExternalToolLease(tools, diagnostics, clients);
        }

        private sealed record AllowedMcpTool(McpClientTool Tool, CopilotMcpClientAccessPolicy AccessPolicy);

        private static string ResolveBearerToken(CopilotMcpClientServerConfig server)
        {
            if (string.IsNullOrWhiteSpace(server.BearerTokenEnvironmentVariable))
                return string.Empty;

            var value = Environment.GetEnvironmentVariable(server.BearerTokenEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"token environment variable '{server.BearerTokenEnvironmentVariable}' is not set");
            return value.Trim();
        }
    }

    internal sealed class CopilotMcpToolAdapter : ICopilotFrameworkApprovedTool
    {
        private const int MaximumResultLength = 65_536;
        private static readonly Regex InvalidNameCharacters = new("[^A-Za-z0-9_]", RegexOptions.Compiled);
        private readonly CopilotMcpClientServerConfig _server;
        private readonly McpClientTool _remoteTool;
        private readonly CopilotMcpClientAccessPolicy _accessPolicy;

        public CopilotMcpToolAdapter(
            CopilotMcpClientServerConfig server,
            McpClientTool remoteTool,
            CopilotMcpClientAccessPolicy accessPolicy)
        {
            _server = server?.Clone() ?? throw new ArgumentNullException(nameof(server));
            _remoteTool = remoteTool ?? throw new ArgumentNullException(nameof(remoteTool));
            _accessPolicy = accessPolicy;
            Name = BuildToolName(_server.Name, remoteTool.Name);
            Description = BuildDescription(_server.Name, remoteTool.Description);
            InputSchema = CopilotToolInputSchema.FromJsonSchema(remoteTool.JsonSchema);
        }

        public string Name { get; }

        public string Description { get; }

        public CopilotToolAccess Access => _accessPolicy == CopilotMcpClientAccessPolicy.ReadOnly
            ? CopilotToolAccess.ReadOnly
            : CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => Access == CopilotToolAccess.ReadOnly
            ? CopilotToolRiskLevel.Low
            : CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => Access == CopilotToolAccess.ReadOnly
            ? CopilotToolApprovalMode.Never
            : CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => Access == CopilotToolAccess.ReadOnly
            ? CopilotToolIdempotency.Idempotent
            : CopilotToolIdempotency.NonIdempotent;

        public CopilotToolConcurrencyMode ConcurrencyMode => Access == CopilotToolAccess.ReadOnly
            ? CopilotToolConcurrencyMode.SharedRead
            : CopilotToolConcurrencyMode.Exclusive;

        public CopilotToolInputSchema InputSchema { get; }

        public TimeSpan ExecutionTimeout => TimeSpan.FromSeconds(_server.ToolTimeoutSeconds);

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && !string.IsNullOrWhiteSpace(request.UserText);
        }

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => $"mcp:{_server.Name}:{_remoteTool.ProtocolTool.Name}";

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            if (Access == CopilotToolAccess.ReadOnly)
                return InvokeRemoteAsync(toolInput, cancellationToken);

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"{Name} requires explicit approval.",
                ErrorMessage = "External MCP tools configured with the approval policy can run only after the exact call is approved.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
            => InvokeRemoteAsync(toolInput, cancellationToken);

        private async Task<CopilotToolResult> InvokeRemoteAsync(CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            var result = await _remoteTool.CallAsync(toolInput.Arguments, cancellationToken: cancellationToken);
            var content = BuildResultContent(result);
            var isError = result.IsError == true;
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = !isError,
                Summary = isError
                    ? $"External MCP tool {_server.Name}/{_remoteTool.ProtocolTool.Name} returned an error."
                    : $"External MCP tool {_server.Name}/{_remoteTool.ProtocolTool.Name} completed.",
                Content = isError ? string.Empty : content,
                ErrorMessage = isError ? content : string.Empty,
                FailureKind = isError ? CopilotToolFailureKind.Unspecified : CopilotToolFailureKind.None,
            };
        }

        private static string BuildResultContent(CallToolResult result)
        {
            var parts = result.Content
                .OfType<TextContentBlock>()
                .Select(block => block.Text?.Trim())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Cast<string>()
                .ToList();
            if (result.StructuredContent.HasValue)
                parts.Add(result.StructuredContent.Value.GetRawText());
            if (parts.Count == 0 && result.Content.Count > 0)
                parts.Add($"MCP returned {result.Content.Count} non-text content block(s).");

            var content = string.Join(Environment.NewLine + Environment.NewLine, parts).Trim();
            if (content.Length > MaximumResultLength)
                content = content[..MaximumResultLength] + "...";
            return content;
        }

        private static string BuildToolName(string serverName, string toolName)
        {
            var combined = "Mcp_" + InvalidNameCharacters.Replace(serverName, "_") + "_" + InvalidNameCharacters.Replace(toolName, "_");
            return combined.Length <= 96 ? combined : combined[..96];
        }

        private static string BuildDescription(string serverName, string? description)
        {
            var normalized = (description ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (normalized.Length > 800)
                normalized = normalized[..800] + "...";
            return $"External MCP tool from configured server '{serverName}'. {normalized}".TrimEnd();
        }
    }
}
