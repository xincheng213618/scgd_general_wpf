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

        public string Message { get; init; } = string.Empty;
    }

    public static class CopilotMcpClientHealthRegistry
    {
        private const int MaximumSnapshots = 64;
        private const int MaximumMessageLength = 240;
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<string, CopilotMcpClientHealthSnapshot> Snapshots = new(StringComparer.OrdinalIgnoreCase);

        public static void RecordConnected(CopilotMcpClientServerConfig server, int discoveredToolCount, int exposedToolCount)
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
                Message = exposed == discovered
                    ? $"Connected · {exposed} tool(s) exposed."
                    : $"Connected · {exposed}/{discovered} tool(s) exposed by policy.",
            });
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
                while (Snapshots.Count > MaximumSnapshots)
                {
                    var oldest = Snapshots.MinBy(entry => entry.Value.CheckedAtUtc);
                    Snapshots.Remove(oldest.Key);
                }
            }
        }

        private static string BuildKey(CopilotMcpClientServerConfig server) => server.Name.Trim() + "|" + server.Endpoint.Trim();
    }
}
