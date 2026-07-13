using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotMcpClientHealthState
    {
        Unknown,
        Connected,
        Unavailable,
    }

    public sealed class CopilotMcpClientHealthSnapshot
    {
        public string ServerName { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public CopilotMcpClientHealthState State { get; init; }

        public DateTimeOffset CheckedAtUtc { get; init; }

        public int DiscoveredToolCount { get; init; }

        public int ExposedToolCount { get; init; }

        public int FilteredToolCount { get; init; }

        public bool UsedCachedDiscovery { get; init; }

        public long CapabilityRevision { get; init; }

        public bool CapabilitiesChanged { get; init; }

        public bool ToolListChangeNotificationsEnabled { get; init; }

        public bool CacheInvalidated { get; init; }

        public string Message { get; init; } = string.Empty;
    }

    public static class CopilotMcpClientHealthRegistry
    {
        private const int MaximumSnapshots = 64;
        private const int MaximumMessageLength = 240;
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, CopilotMcpClientHealthSnapshot> Snapshots = new(StringComparer.OrdinalIgnoreCase);

        public static void RecordConnected(
            CopilotMcpClientServerConfig server,
            int discoveredToolCount,
            int exposedToolCount,
            bool usedCachedDiscovery = false,
            long capabilityRevision = 1,
            bool capabilitiesChanged = false,
            bool toolListChangeNotificationsEnabled = false)
        {
            ArgumentNullException.ThrowIfNull(server);
            var discovered = Math.Max(0, discoveredToolCount);
            var exposed = Math.Clamp(exposedToolCount, 0, discovered);
            Store(server, new CopilotMcpClientHealthSnapshot
            {
                ServerName = server.Name,
                Endpoint = server.Endpoint,
                State = CopilotMcpClientHealthState.Connected,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                DiscoveredToolCount = discovered,
                ExposedToolCount = exposed,
                FilteredToolCount = Math.Max(0, discovered - exposed),
                UsedCachedDiscovery = usedCachedDiscovery,
                CapabilityRevision = Math.Max(1, capabilityRevision),
                CapabilitiesChanged = capabilitiesChanged,
                ToolListChangeNotificationsEnabled = toolListChangeNotificationsEnabled,
                CacheInvalidated = false,
                Message = exposed == discovered
                    ? $"Connected · {exposed} tool(s) exposed · {(usedCachedDiscovery ? "cached" : "live")} discovery."
                    : $"Connected · {exposed}/{discovered} tool(s) exposed by policy · {(usedCachedDiscovery ? "cached" : "live")} discovery.",
            });
        }

        public static void RecordToolListChanged(CopilotMcpClientServerConfig server, bool cacheInvalidated)
        {
            ArgumentNullException.ThrowIfNull(server);
            lock (SyncRoot)
            {
                Snapshots.TryGetValue(BuildKey(server), out var previous);
                Snapshots[BuildKey(server)] = new CopilotMcpClientHealthSnapshot
                {
                    ServerName = server.Name,
                    Endpoint = server.Endpoint,
                    State = CopilotMcpClientHealthState.Connected,
                    CheckedAtUtc = DateTimeOffset.UtcNow,
                    DiscoveredToolCount = previous?.DiscoveredToolCount ?? 0,
                    ExposedToolCount = previous?.ExposedToolCount ?? 0,
                    FilteredToolCount = previous?.FilteredToolCount ?? 0,
                    UsedCachedDiscovery = previous?.UsedCachedDiscovery ?? false,
                    CapabilityRevision = Math.Max(1, previous?.CapabilityRevision ?? 1),
                    CapabilitiesChanged = true,
                    ToolListChangeNotificationsEnabled = true,
                    CacheInvalidated = cacheInvalidated,
                    Message = cacheInvalidated
                        ? "Tool list changed · cached discovery invalidated."
                        : "Tool list changed · live discovery required.",
                };
                TrimSnapshots();
            }
        }

        public static void RecordUnavailable(CopilotMcpClientServerConfig server, string? message)
        {
            ArgumentNullException.ThrowIfNull(server);
            var sanitized = CopilotMcpAuditLogger.RedactText(message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (sanitized.Length > MaximumMessageLength)
                sanitized = sanitized[..MaximumMessageLength] + "...";
            Store(server, new CopilotMcpClientHealthSnapshot
            {
                ServerName = server.Name,
                Endpoint = server.Endpoint,
                State = CopilotMcpClientHealthState.Unavailable,
                CheckedAtUtc = DateTimeOffset.UtcNow,
                Message = string.IsNullOrWhiteSpace(sanitized) ? "Connection unavailable." : sanitized,
            });
        }

        public static bool TryGetSnapshot(CopilotMcpClientServerConfig server, out CopilotMcpClientHealthSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(server);
            lock (SyncRoot)
                return Snapshots.TryGetValue(BuildKey(server), out snapshot!);
        }

        private static void Store(CopilotMcpClientServerConfig server, CopilotMcpClientHealthSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                Snapshots[BuildKey(server)] = snapshot;
                TrimSnapshots();
            }
        }

        private static void TrimSnapshots()
        {
            while (Snapshots.Count > MaximumSnapshots)
            {
                var oldest = Snapshots.MinBy(entry => entry.Value.CheckedAtUtc);
                Snapshots.Remove(oldest.Key);
            }
        }

        private static string BuildKey(CopilotMcpClientServerConfig server) => server.Name.Trim() + "|" + server.Endpoint.Trim();
    }
}
