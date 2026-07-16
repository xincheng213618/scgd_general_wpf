using ColorVision.Copilot.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
        private readonly CopilotMcpToolDiscoveryCache _discoveryCache;

        public CopilotMcpToolProvider()
            : this(CopilotMcpToolDiscoveryCache.Shared)
        {
        }

        internal CopilotMcpToolProvider(CopilotMcpToolDiscoveryCache discoveryCache)
        {
            _discoveryCache = discoveryCache ?? throw new ArgumentNullException(nameof(discoveryCache));
        }

        public async Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var tools = new List<ICopilotTool>();
            var diagnostics = new List<string>();
            var clients = new List<IAsyncDisposable>();
            var enabledServers = request.ExternalMcpServers.Where(server => server?.Enabled == true).Take(8).ToArray();
            CopilotCapabilityCatalog.Shared.RetainExternalMcpServers(enabledServers);
            foreach (var server in enabledServers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (tools.Count >= MaximumToolsPerRequest)
                    break;
                McpClient? client = null;
                IAsyncDisposable? toolListChangedRegistration = null;
                var toolListChangeNotificationPending = 0;
                var discoveryReady = 0;
                var toolListChangeNotificationsEnabled = false;
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
                    if (client.ServerCapabilities.Tools?.ListChanged == true)
                    {
                        try
                        {
                            var serverSnapshot = server.Clone();
                            toolListChangedRegistration = client.RegisterNotificationHandler(
                                NotificationMethods.ToolListChangedNotification,
                                (_, _) =>
                                {
                                    Interlocked.Exchange(ref toolListChangeNotificationPending, 1);
                                    if (Volatile.Read(ref discoveryReady) == 1
                                        && Interlocked.Exchange(ref toolListChangeNotificationPending, 0) == 1)
                                    {
                                        CopilotMcpClientDiscoveryRegistry.NotifyToolListChanged(serverSnapshot, _discoveryCache);
                                    }
                                    return ValueTask.CompletedTask;
                                });
                            toolListChangeNotificationsEnabled = true;
                        }
                        catch (Exception ex)
                        {
                            diagnostics.Add($"MCP client {server.Name} could not watch tool-list changes · {CopilotMcpAuditLogger.RedactText(ex.Message)}");
                        }
                    }
                    CopilotMcpToolDiscoverySnapshot cachedDiscovery = null!;
                    var usedCachedDiscovery = !request.ForceExternalMcpToolRefresh
                        && _discoveryCache.TryGet(server, token, out cachedDiscovery);
                    McpClientTool[] remoteTools;
                    int discoveredToolCount;
                    CopilotMcpDiscoveryCacheUpdateKind? cacheUpdate = null;
                    long capabilityRevision;
                    if (usedCachedDiscovery)
                    {
                        remoteTools = cachedDiscovery.Tools.Select(tool => new McpClientTool(client, tool)).ToArray();
                        discoveredToolCount = cachedDiscovery.DiscoveredToolCount;
                        capabilityRevision = cachedDiscovery.Revision;
                    }
                    else
                    {
                        var discovery = await CopilotMcpToolDiscoveryPaginator.DiscoverAsync(
                            (requestParams, token) => client.ListToolsAsync(requestParams, token),
                            cancellationToken: connectionTimeout.Token);
                        discoveredToolCount = discovery.DiscoveredToolCount;
                        remoteTools = discovery.Tools.Select(tool => new McpClientTool(client, tool)).ToArray();
                        cacheUpdate = _discoveryCache.Store(
                            server,
                            token,
                            remoteTools.Select(tool => tool.ProtocolTool).ToArray(),
                            discoveredToolCount,
                            out var refreshedDiscovery);
                        capabilityRevision = refreshedDiscovery.Revision;
                        if (discovery.Truncated)
                        {
                            diagnostics.Add(
                                $"MCP client {server.Name} stopped live discovery after {discovery.PageCount} page(s) and "
                                + $"{remoteTools.Length} cached tool definition(s) within the safety limits.");
                        }
                        if (discovery.DuplicateToolCount > 0)
                        {
                            diagnostics.Add(
                                $"MCP client {server.Name} skipped {discovery.DuplicateToolCount} duplicate tool definition(s).");
                        }
                        if (discovery.RejectedToolCount > 0)
                        {
                            diagnostics.Add(
                                $"MCP client {server.Name} skipped {discovery.RejectedToolCount} invalid or oversized tool definition(s).");
                        }
                    }
                    var remaining = MaximumToolsPerRequest - tools.Count;
                    var allowedTools = remoteTools
                        .Select(tool => server.TryResolveToolAccessPolicy(tool.Name, out var accessPolicy)
                            ? new AllowedMcpTool(tool, accessPolicy)
                            : null)
                        .OfType<AllowedMcpTool>()
                        .Take(Math.Min(MaximumToolsPerServer, remaining))
                        .ToArray();
                    var adapters = allowedTools
                        .Select(allowedTool => new CopilotMcpToolAdapter(server, allowedTool.Tool, allowedTool.AccessPolicy))
                        .ToArray();
                    tools.AddRange(adapters);
                    CopilotCapabilityCatalog.Shared.PublishExternalMcp(server, adapters);
                    CopilotMcpClientHealthRegistry.RecordConnected(
                        server,
                        discoveredToolCount,
                        allowedTools.Length,
                        usedCachedDiscovery,
                        capabilityRevision,
                        cacheUpdate == CopilotMcpDiscoveryCacheUpdateKind.Changed,
                        toolListChangeNotificationsEnabled);
                    Volatile.Write(ref discoveryReady, 1);
                    if (Interlocked.Exchange(ref toolListChangeNotificationPending, 0) == 1)
                        CopilotMcpClientDiscoveryRegistry.NotifyToolListChanged(server, _discoveryCache);
                    if (allowedTools.Length > 0)
                    {
                        clients.Add(client);
                        client = null;
                        if (toolListChangedRegistration != null)
                        {
                            clients.Add(toolListChangedRegistration);
                            toolListChangedRegistration = null;
                        }
                    }
                    var discoverySource = usedCachedDiscovery ? "cached discovery" : "live discovery";
                    diagnostics.Add(allowedTools.Length == discoveredToolCount
                        ? $"MCP client connected to {server.Name} · {allowedTools.Length} tool(s) exposed from {discoverySource}."
                        : $"MCP client connected to {server.Name} · {allowedTools.Length}/{discoveredToolCount} tool(s) exposed from {discoverySource} by policy and request limits.");
                    if (discoveredToolCount > remoteTools.Length)
                        diagnostics.Add($"MCP client {server.Name} cached the first {remoteTools.Length}/{discoveredToolCount} tool definition(s) within the safety limit.");
                    if (cacheUpdate == CopilotMcpDiscoveryCacheUpdateKind.Changed)
                        diagnostics.Add($"MCP client {server.Name} capability set changed · revision {capabilityRevision}.");
                    if (tools.Count >= MaximumToolsPerRequest)
                    {
                        diagnostics.Add($"MCP client discovery reached the {MaximumToolsPerRequest}-tool request limit.");
                        break;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
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
                    if (toolListChangedRegistration != null)
                    {
                        try
                        {
                            await toolListChangedRegistration.DisposeAsync();
                        }
                        catch
                        {
                        }
                    }
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

    internal sealed record CopilotMcpToolDiscoveryBatch(
        IReadOnlyList<Tool> Tools,
        int DiscoveredToolCount,
        int PageCount,
        int DuplicateToolCount,
        int RejectedToolCount,
        bool Truncated);

    internal static class CopilotMcpToolDiscoveryPaginator
    {
        public const int MaximumToolDefinitions = 512;
        public const int MaximumPages = 32;
        public const int MaximumRemoteToolNameLength = 128;
        public const int MaximumSerializedToolDefinitionBytes = 128 * 1024;
        public const int MaximumTotalToolDefinitionBytes = 2 * 1024 * 1024;

        public static async Task<CopilotMcpToolDiscoveryBatch> DiscoverAsync(
            Func<ListToolsRequestParams, CancellationToken, ValueTask<ListToolsResult>> listPageAsync,
            int maximumToolDefinitions = MaximumToolDefinitions,
            int maximumPages = MaximumPages,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(listPageAsync);
            if (maximumToolDefinitions < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumToolDefinitions));
            if (maximumPages < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumPages));

            var tools = new List<Tool>(Math.Min(maximumToolDefinitions, 64));
            var seenCursors = new HashSet<string>(StringComparer.Ordinal);
            var seenToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var discoveredToolCount = 0;
            var duplicateToolCount = 0;
            var rejectedToolCount = 0;
            var serializedToolDefinitionBytes = 0;
            var pageCount = 0;
            string? cursor = null;
            while (pageCount < maximumPages && tools.Count < maximumToolDefinitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = await listPageAsync(new ListToolsRequestParams { Cursor = cursor }, cancellationToken);
                pageCount++;
                var pageTools = page?.Tools ?? Array.Empty<Tool>();
                discoveredToolCount = discoveredToolCount > int.MaxValue - pageTools.Count
                    ? int.MaxValue
                    : discoveredToolCount + pageTools.Count;
                foreach (var tool in pageTools)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (tool == null)
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(tool.Name)
                        || tool.Name.Length > MaximumRemoteToolNameLength
                        || tool.Name.Any(char.IsControl))
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (seenToolNames.Contains(tool.Name))
                    {
                        duplicateToolCount++;
                        continue;
                    }
                    int definitionBytes;
                    try
                    {
                        definitionBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(tool));
                    }
                    catch
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (definitionBytes > MaximumSerializedToolDefinitionBytes)
                    {
                        rejectedToolCount++;
                        continue;
                    }
                    if (serializedToolDefinitionBytes > MaximumTotalToolDefinitionBytes - definitionBytes)
                    {
                        rejectedToolCount++;
                        return new CopilotMcpToolDiscoveryBatch(
                            tools,
                            discoveredToolCount,
                            pageCount,
                            duplicateToolCount,
                            rejectedToolCount,
                            Truncated: true);
                    }
                    if (tools.Count >= maximumToolDefinitions)
                    {
                        return new CopilotMcpToolDiscoveryBatch(
                            tools,
                            discoveredToolCount,
                            pageCount,
                            duplicateToolCount,
                            rejectedToolCount,
                            Truncated: true);
                    }
                    seenToolNames.Add(tool.Name);
                    tools.Add(tool);
                    serializedToolDefinitionBytes += definitionBytes;
                }

                var nextCursor = page?.NextCursor;
                if (string.IsNullOrWhiteSpace(nextCursor))
                {
                    return new CopilotMcpToolDiscoveryBatch(
                        tools,
                        discoveredToolCount,
                        pageCount,
                        duplicateToolCount,
                        rejectedToolCount,
                        Truncated: false);
                }

                if (tools.Count >= maximumToolDefinitions)
                    return new CopilotMcpToolDiscoveryBatch(tools, discoveredToolCount, pageCount, duplicateToolCount, rejectedToolCount, Truncated: true);
                if (!seenCursors.Add(nextCursor))
                    throw new InvalidOperationException("External MCP server repeated a tools/list pagination cursor.");
                cursor = nextCursor;
            }

            return new CopilotMcpToolDiscoveryBatch(tools, discoveredToolCount, pageCount, duplicateToolCount, rejectedToolCount, Truncated: true);
        }
    }

    internal static class CopilotMcpToolIdentity
    {
        private const int MaximumIdentityLength = 96;
        private const int HashSuffixLength = 12;
        private static readonly Regex InvalidCatalogKeyCharacters = new("[^A-Za-z0-9_.-]", RegexOptions.Compiled);
        private static readonly Regex InvalidLocalNameCharacters = new("[^A-Za-z0-9_]", RegexOptions.Compiled);

        public static string BuildLocalName(string serverName, string remoteToolName)
        {
            var normalizedServerName = InvalidLocalNameCharacters.Replace(serverName ?? string.Empty, "_");
            var normalizedToolName = InvalidLocalNameCharacters.Replace(remoteToolName ?? string.Empty, "_");
            var combined = "Mcp_" + normalizedServerName + "_" + normalizedToolName;
            var isLossless = string.Equals(serverName, normalizedServerName, StringComparison.Ordinal)
                && string.Equals(remoteToolName, normalizedToolName, StringComparison.Ordinal);
            return isLossless && combined.Length <= MaximumIdentityLength
                ? combined
                : AppendHashSuffix(combined, serverName + "\n" + remoteToolName, "_");
        }

        public static string BuildCatalogKey(string remoteToolName)
        {
            var source = remoteToolName ?? string.Empty;
            var normalized = InvalidCatalogKeyCharacters.Replace(source, "-").Trim('-', '.', '_');
            var isLossless = normalized.Length > 0
                && normalized.Length <= MaximumIdentityLength
                && string.Equals(source, normalized, StringComparison.Ordinal);
            return isLossless ? source : AppendHashSuffix(normalized.Length == 0 ? "tool" : normalized, source, "-");
        }

        private static string AppendHashSuffix(string prefix, string source, string separator)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source ?? string.Empty)))[..HashSuffixLength].ToLowerInvariant();
            var maximumPrefixLength = MaximumIdentityLength - separator.Length - hash.Length;
            var boundedPrefix = prefix.Length <= maximumPrefixLength ? prefix : prefix[..maximumPrefixLength];
            return boundedPrefix + separator + hash;
        }
    }

    internal sealed class CopilotMcpToolAdapter : ICopilotFrameworkApprovedTool, ICopilotFrameworkApprovalPresentation, ICopilotCapabilityCatalogIdentity
    {
        private const int MaximumResultLength = 65_536;
        private readonly CopilotMcpClientServerConfig _server;
        private readonly McpClientTool _remoteTool;

        public CopilotMcpToolAdapter(
            CopilotMcpClientServerConfig server,
            McpClientTool remoteTool,
            CopilotMcpClientAccessPolicy accessPolicy)
        {
            _server = server?.Clone() ?? throw new ArgumentNullException(nameof(server));
            _remoteTool = remoteTool ?? throw new ArgumentNullException(nameof(remoteTool));
            Name = CopilotMcpToolIdentity.BuildLocalName(_server.Name, remoteTool.Name);
            Description = BuildDescription(_server.Name, remoteTool.Description);
            InputSchema = CopilotToolInputSchema.FromJsonSchema(remoteTool.JsonSchema);
            Capability = CopilotMcpClientCapabilityPolicy.Create(accessPolicy, TimeSpan.FromSeconds(_server.ToolTimeoutSeconds));
        }

        public string Name { get; }

        public string Description { get; }

        public string CatalogCapabilityKey => CopilotMcpToolIdentity.BuildCatalogKey(_remoteTool.ProtocolTool.Name);

        public CopilotToolCapabilityDescriptor Capability { get; }

        public CopilotToolAccess Access => Capability.Access;

        public CopilotToolRiskLevel RiskLevel => Capability.RiskLevel;

        public CopilotToolApprovalMode ApprovalMode => Capability.ApprovalMode;

        public CopilotToolIdempotency Idempotency => Capability.Idempotency;

        public CopilotToolConcurrencyMode ConcurrencyMode => Capability.ConcurrencyMode;

        public CopilotToolInputSchema InputSchema { get; }

        public TimeSpan ExecutionTimeout => Capability.ExecutionTimeout;

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && !string.IsNullOrWhiteSpace(request.UserText)
                && CopilotToolIntentPolicy.CanExposeExternalTool(
                    request,
                    _remoteTool.ProtocolTool.Name,
                    _remoteTool.Description);
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

        public CopilotToolApprovalPresentation CreateApprovalPresentation(CopilotAgentToolInput toolInput)
            => CopilotMcpClientApprovalPresentation.Create(_server.Name, _remoteTool.ProtocolTool.Name, toolInput);

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

        private static string BuildDescription(string serverName, string? description)
        {
            var normalized = (description ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (normalized.Length > 800)
                normalized = normalized[..800] + "...";
            return $"External MCP tool from configured server '{serverName}'. {normalized}".TrimEnd();
        }
    }

    public static class CopilotMcpClientApprovalPresentation
    {
        public static CopilotToolApprovalPresentation Create(string serverName, string remoteToolName, CopilotAgentToolInput toolInput)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("MCP server name is required for approval.", nameof(serverName));
            if (string.IsNullOrWhiteSpace(remoteToolName))
                throw new ArgumentException("MCP tool name is required for approval.", nameof(remoteToolName));

            var argumentsSummary = CopilotToolApprovalArgumentFormatter.Create(toolInput);
            return new CopilotToolApprovalPresentation(
                $"Approve MCP action: {serverName}/{remoteToolName}",
                $"External MCP server '{serverName}' wants to run tool '{remoteToolName}'. Review the redacted argument values before approving: {argumentsSummary}");
        }
    }

    public static class CopilotMcpClientCapabilityPolicy
    {
        public static CopilotToolCapabilityDescriptor Create(CopilotMcpClientAccessPolicy accessPolicy, TimeSpan executionTimeout)
        {
            return accessPolicy == CopilotMcpClientAccessPolicy.ReadOnly
                ? CopilotToolCapabilityDescriptor.ReadOnly(executionTimeout, CopilotToolAuditArgumentMode.NamesOnly)
                : CopilotToolCapabilityDescriptor.ProtectedWrite(
                    CopilotToolIdempotency.NonIdempotent,
                    executionTimeout,
                    CopilotToolAuditArgumentMode.NamesOnly);
        }
    }
}
