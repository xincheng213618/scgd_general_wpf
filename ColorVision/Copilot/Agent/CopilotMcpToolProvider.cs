using ColorVision.Copilot.Mcp;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        internal static readonly TimeSpan DefaultDisposalTimeout = TimeSpan.FromSeconds(1);
        internal static readonly TimeSpan DefaultResourceDisposalTimeout = TimeSpan.FromMilliseconds(500);

        private readonly object _disposeLock = new();
        private readonly IReadOnlyList<IAsyncDisposable> _resources;
        private readonly TimeSpan _disposalTimeout;
        private readonly TimeSpan _resourceDisposalTimeout;
        private Task? _disposeTask;

        public CopilotExternalToolLease(
            IReadOnlyList<ICopilotTool>? tools = null,
            IReadOnlyList<string>? diagnostics = null,
            IReadOnlyList<IAsyncDisposable>? resources = null)
            : this(
                tools,
                diagnostics,
                resources,
                DefaultDisposalTimeout,
                DefaultResourceDisposalTimeout)
        {
        }

        internal CopilotExternalToolLease(
            IReadOnlyList<ICopilotTool>? tools,
            IReadOnlyList<string>? diagnostics,
            IReadOnlyList<IAsyncDisposable>? resources,
            TimeSpan disposalTimeout,
            TimeSpan resourceDisposalTimeout)
        {
            Tools = Array.AsReadOnly((tools ?? Array.Empty<ICopilotTool>()).ToArray());
            Diagnostics = Array.AsReadOnly((diagnostics ?? Array.Empty<string>()).ToArray());
            _resources = resources?.Where(resource => resource != null).ToArray()
                ?? Array.Empty<IAsyncDisposable>();
            _disposalTimeout = ValidateTimeout(disposalTimeout, nameof(disposalTimeout));
            _resourceDisposalTimeout = ValidateTimeout(resourceDisposalTimeout, nameof(resourceDisposalTimeout));
        }

        public IReadOnlyList<ICopilotTool> Tools { get; }

        public IReadOnlyList<string> Diagnostics { get; }

        public ValueTask DisposeAsync()
        {
            Task disposeTask;
            lock (_disposeLock)
            {
                disposeTask = _disposeTask ??= DisposeResourcesAsync(
                    _resources,
                    _disposalTimeout,
                    _resourceDisposalTimeout);
            }
            return new ValueTask(disposeTask);
        }

        internal static async Task DisposeResourcesAsync(
            IReadOnlyList<IAsyncDisposable> resources,
            TimeSpan disposalTimeout,
            TimeSpan resourceDisposalTimeout)
        {
            ArgumentNullException.ThrowIfNull(resources);
            disposalTimeout = ValidateTimeout(disposalTimeout, nameof(disposalTimeout));
            resourceDisposalTimeout = ValidateTimeout(resourceDisposalTimeout, nameof(resourceDisposalTimeout));
            var stopwatch = Stopwatch.StartNew();
            foreach (var resource in resources.Reverse())
            {
                Task disposeTask;
                try
                {
                    disposeTask = Task.Run(
                        async () => await resource.DisposeAsync().ConfigureAwait(false),
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    TraceDisposalFailure(resource, ex);
                    continue;
                }

                var remaining = disposalTimeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    CopilotCancellationBoundary.ObserveLateFault(disposeTask);
                    continue;
                }

                var waitTimeout = remaining < resourceDisposalTimeout
                    ? remaining
                    : resourceDisposalTimeout;
                try
                {
                    await disposeTask.WaitAsync(waitTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    CopilotCancellationBoundary.ObserveLateFault(disposeTask);
                    Trace.TraceWarning(
                        "Copilot external resource disposal exceeded {0}; detaching late cleanup for {1}.",
                        waitTimeout,
                        resource.GetType().Name);
                }
                catch (Exception ex)
                {
                    TraceDisposalFailure(resource, ex);
                }
            }
        }

        private static TimeSpan ValidateTimeout(TimeSpan timeout, string parameterName)
        {
            if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(parameterName, "Disposal timeout must be finite and positive.");
            return timeout;
        }

        private static void TraceDisposalFailure(IAsyncDisposable resource, Exception exception)
        {
            Trace.TraceWarning(
                "Copilot external resource disposal failed for {0}: {1}",
                resource.GetType().Name,
                CopilotUserFacingErrorFormatter.Sanitize(exception.Message));
        }
    }

    internal sealed class CopilotMcpToolProvider : ICopilotExternalToolProvider
    {
        private const int MaximumToolsPerServer = 32;
        private const int MaximumToolsPerRequest = 64;
        private static readonly TimeSpan DiscoveryCleanupTimeout = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan DiscoveryResourceCleanupTimeout = TimeSpan.FromMilliseconds(500);
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
                if (cancellationToken.IsCancellationRequested)
                {
                    await DisposeDiscoveryResourcesAsync(clients).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (tools.Count >= MaximumToolsPerRequest)
                    break;
                McpClient? client = null;
                HttpClientTransport? transport = null;
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
                    var transportOptions = new HttpClientTransportOptions
                    {
                        Name = server.Name,
                        Endpoint = new Uri(server.Endpoint),
                        TransportMode = HttpTransportMode.StreamableHttp,
                        ConnectionTimeout = TimeSpan.FromSeconds(server.ConnectionTimeoutSeconds),
                        AdditionalHeaders = headers,
                    };
                    var httpClient = CopilotMcpHttpTransport.CreateClient(Timeout.InfiniteTimeSpan);
                    try
                    {
                        transport = new HttpClientTransport(transportOptions, httpClient, loggerFactory: null, ownsHttpClient: true);
                    }
                    catch
                    {
                        httpClient.Dispose();
                        throw;
                    }

                    using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    connectionTimeout.CancelAfter(TimeSpan.FromSeconds(server.ConnectionTimeoutSeconds));
                    var clientCreationTask = McpClient.CreateAsync(
                        transport,
                        cancellationToken: connectionTimeout.Token);
                    try
                    {
                        client = await clientCreationTask
                            .WaitAsync(connectionTimeout.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (connectionTimeout.IsCancellationRequested)
                    {
                        _ = DisposeLateClientAsync(clientCreationTask);
                        throw;
                    }
                    transport = null;
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
                    var compatibleAdapters = SelectRuntimeCompatibleTools(
                        adapters,
                        out var incompatibleSchemaCount);
                    if (incompatibleSchemaCount > 0)
                    {
                        diagnostics.Add(
                            $"MCP client {server.Name} skipped {incompatibleSchemaCount} tool definition(s) whose input schemas are not executable by the shared runtime.");
                    }

                    // Publish first: catalog registration is the commit gate for the
                    // same definitions exposed to this turn. A rejected source must not
                    // leave callable adapters backed by a client that the finally block
                    // is about to dispose.
                    CopilotCapabilityCatalog.Shared.PublishExternalMcp(server, compatibleAdapters);
                    tools.AddRange(compatibleAdapters);
                    CopilotMcpClientHealthRegistry.RecordConnected(
                        server,
                        discoveredToolCount,
                        compatibleAdapters.Length,
                        usedCachedDiscovery,
                        capabilityRevision,
                        cacheUpdate == CopilotMcpDiscoveryCacheUpdateKind.Changed,
                        toolListChangeNotificationsEnabled);
                    Volatile.Write(ref discoveryReady, 1);
                    if (Interlocked.Exchange(ref toolListChangeNotificationPending, 0) == 1)
                        CopilotMcpClientDiscoveryRegistry.NotifyToolListChanged(server, _discoveryCache);
                    if (compatibleAdapters.Length > 0)
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
                    diagnostics.Add(compatibleAdapters.Length == discoveredToolCount
                        ? $"MCP client connected to {server.Name} · {compatibleAdapters.Length} tool(s) exposed from {discoverySource}."
                        : $"MCP client connected to {server.Name} · {compatibleAdapters.Length}/{discoveredToolCount} tool(s) exposed from {discoverySource} by policy, runtime contract, and request limits.");
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
                    await DisposeDiscoveryResourcesAsync(clients).ConfigureAwait(false);
                    clients.Clear();
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
                    var failedServerResources = new List<IAsyncDisposable>(2);
                    if (client != null)
                        failedServerResources.Add(client);
                    else if (transport != null)
                        failedServerResources.Add(transport);
                    if (toolListChangedRegistration != null)
                        failedServerResources.Add(toolListChangedRegistration);
                    await DisposeDiscoveryResourcesAsync(failedServerResources).ConfigureAwait(false);
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                await DisposeDiscoveryResourcesAsync(clients).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
            }
            return new CopilotExternalToolLease(tools, diagnostics, clients);
        }

        internal static ICopilotTool[] SelectRuntimeCompatibleTools(
            IEnumerable<ICopilotTool>? tools,
            out int rejectedCount)
        {
            var candidates = (tools ?? Array.Empty<ICopilotTool>()).ToArray();
            var compatible = candidates
                .Where(tool => tool != null
                    && CopilotToolInputContractValidator.TryValidateSchema(
                        tool.InputSchema?.JsonSchema,
                        out _,
                        requireClosedObjects: false))
                .ToArray();
            rejectedCount = candidates.Length - compatible.Length;
            return compatible;
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

        private static Task DisposeDiscoveryResourcesAsync(IReadOnlyList<IAsyncDisposable> resources)
            => CopilotExternalToolLease.DisposeResourcesAsync(
                resources,
                DiscoveryCleanupTimeout,
                DiscoveryResourceCleanupTimeout);

        private static async Task DisposeLateClientAsync(Task<McpClient> clientCreationTask)
        {
            try
            {
                var lateClient = await clientCreationTask.ConfigureAwait(false);
                await DisposeDiscoveryResourcesAsync([lateClient]).ConfigureAwait(false);
            }
            catch
            {
            }
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
        public static CopilotToolCapabilityDescriptor Create(
            CopilotMcpClientAccessPolicy accessPolicy,
            TimeSpan executionTimeout,
            ToolAnnotations? annotations = null)
        {
            var effectiveAccessPolicy = annotations?.DestructiveHint == true
                ? CopilotMcpClientAccessPolicy.RequireApproval
                : accessPolicy;
            return effectiveAccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly
                ? CopilotToolCapabilityDescriptor.ReadOnly(executionTimeout, CopilotToolAuditArgumentMode.NamesOnly)
                : CopilotToolCapabilityDescriptor.ProtectedWrite(
                    CopilotToolIdempotency.NonIdempotent,
                    executionTimeout,
                    CopilotToolAuditArgumentMode.NamesOnly,
                    approvalPromptCategory: CopilotApprovalPromptCategory.McpElicitations);
        }
    }
}
