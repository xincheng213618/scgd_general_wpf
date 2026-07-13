using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal enum CopilotMcpDiscoveryCacheUpdateKind
    {
        Added,
        Unchanged,
        Changed,
    }

    internal sealed record CopilotMcpToolDiscoverySnapshot(
        IReadOnlyList<Tool> Tools,
        int DiscoveredToolCount,
        DateTimeOffset DiscoveredAtUtc,
        DateTimeOffset ExpiresAtUtc,
        long Revision);

    internal sealed class CopilotMcpCapabilitiesChangedEventArgs : EventArgs
    {
        public string ServerName { get; init; } = string.Empty;

        public string Endpoint { get; init; } = string.Empty;

        public long PreviousRevision { get; init; }

        public long Revision { get; init; }

        public int ToolCount { get; init; }
    }

    internal sealed class CopilotMcpToolDiscoveryCache
    {
        private const int DefaultMaximumEntries = 32;
        private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(5);
        private readonly Dictionary<CacheKey, CacheEntry> _entries = new();
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly TimeSpan _timeToLive;
        private readonly int _maximumEntries;
        private readonly object _syncRoot = new();

        public CopilotMcpToolDiscoveryCache(
            TimeSpan? timeToLive = null,
            int maximumEntries = DefaultMaximumEntries,
            Func<DateTimeOffset>? utcNow = null)
        {
            _timeToLive = timeToLive ?? DefaultTimeToLive;
            if (_timeToLive <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeToLive));
            _maximumEntries = Math.Clamp(maximumEntries, 1, 128);
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public static CopilotMcpToolDiscoveryCache Shared { get; } = new();

        public event EventHandler<CopilotMcpCapabilitiesChangedEventArgs>? CapabilitiesChanged;

        public bool TryGet(CopilotMcpClientServerConfig server, string bearerToken, out CopilotMcpToolDiscoverySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(server);
            var key = BuildKey(server, bearerToken);
            lock (_syncRoot)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    snapshot = null!;
                    return false;
                }

                if (entry.Invalidated || entry.ExpiresAtUtc <= _utcNow())
                {
                    snapshot = null!;
                    return false;
                }

                snapshot = entry.ToSnapshot();
                return true;
            }
        }

        public CopilotMcpDiscoveryCacheUpdateKind Store(
            CopilotMcpClientServerConfig server,
            string bearerToken,
            IReadOnlyList<Tool> tools,
            int discoveredToolCount,
            out CopilotMcpToolDiscoverySnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(tools);
            var definitions = tools.Where(tool => tool != null).ToArray();
            var discovered = Math.Max(definitions.Length, discoveredToolCount);
            var signature = CreateSignature(definitions, discovered);
            var key = BuildKey(server, bearerToken);
            var now = _utcNow();
            CopilotMcpCapabilitiesChangedEventArgs? change = null;
            CopilotMcpDiscoveryCacheUpdateKind updateKind;

            lock (_syncRoot)
            {
                if (_entries.TryGetValue(key, out var previous))
                {
                    var changed = !string.Equals(previous.Signature, signature, StringComparison.Ordinal);
                    var revision = changed ? previous.Revision + 1 : previous.Revision;
                    var entry = new CacheEntry(definitions, discovered, signature, now, now + _timeToLive, revision, false);
                    _entries[key] = entry;
                    snapshot = entry.ToSnapshot();
                    updateKind = changed ? CopilotMcpDiscoveryCacheUpdateKind.Changed : CopilotMcpDiscoveryCacheUpdateKind.Unchanged;
                    if (changed)
                    {
                        change = new CopilotMcpCapabilitiesChangedEventArgs
                        {
                            ServerName = server.Name,
                            Endpoint = server.Endpoint,
                            PreviousRevision = previous.Revision,
                            Revision = revision,
                            ToolCount = definitions.Length,
                        };
                    }
                }
                else
                {
                    var entry = new CacheEntry(definitions, discovered, signature, now, now + _timeToLive, 1, false);
                    _entries[key] = entry;
                    snapshot = entry.ToSnapshot();
                    updateKind = CopilotMcpDiscoveryCacheUpdateKind.Added;
                }

                TrimToMaximumEntries();
            }

            if (change != null && CapabilitiesChanged is { } handlers)
            {
                foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<CopilotMcpCapabilitiesChangedEventArgs>>())
                {
                    try
                    {
                        handler(this, change);
                    }
                    catch
                    {
                    }
                }
            }
            return updateKind;
        }

        public int Invalidate(CopilotMcpClientServerConfig server)
        {
            ArgumentNullException.ThrowIfNull(server);
            var invalidatedCount = 0;
            lock (_syncRoot)
            {
                foreach (var key in _entries.Keys
                    .Where(key => string.Equals(key.ServerName, server.Name, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(key.Endpoint, server.Endpoint, StringComparison.OrdinalIgnoreCase))
                    .ToArray())
                {
                    var entry = _entries[key];
                    if (entry.Invalidated)
                        continue;
                    _entries[key] = entry with { Invalidated = true };
                    invalidatedCount++;
                }
            }
            return invalidatedCount;
        }

        private void TrimToMaximumEntries()
        {
            while (_entries.Count > _maximumEntries)
            {
                var oldest = _entries.MinBy(entry => entry.Value.DiscoveredAtUtc);
                _entries.Remove(oldest.Key);
            }
        }

        private static CacheKey BuildKey(CopilotMcpClientServerConfig server, string bearerToken)
        {
            var tokenFingerprint = string.IsNullOrEmpty(bearerToken)
                ? string.Empty
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bearerToken)));
            return new CacheKey(server.Name.Trim(), server.Endpoint.Trim(), tokenFingerprint);
        }

        private static string CreateSignature(IReadOnlyList<Tool> tools, int discoveredToolCount)
        {
            var builder = new StringBuilder().Append(discoveredToolCount).Append('\n');
            foreach (var tool in tools.OrderBy(tool => tool.Name, StringComparer.Ordinal))
                builder.Append(JsonSerializer.Serialize(tool)).Append('\n');
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
        }

        private readonly record struct CacheKey(string ServerName, string Endpoint, string TokenFingerprint);

        private sealed record CacheEntry(
            IReadOnlyList<Tool> Tools,
            int DiscoveredToolCount,
            string Signature,
            DateTimeOffset DiscoveredAtUtc,
            DateTimeOffset ExpiresAtUtc,
            long Revision,
            bool Invalidated)
        {
            public CopilotMcpToolDiscoverySnapshot ToSnapshot() => new(Tools, DiscoveredToolCount, DiscoveredAtUtc, ExpiresAtUtc, Revision);
        }
    }

    public static class CopilotMcpClientDiscoveryRegistry
    {
        public static bool NotifyToolListChanged(CopilotMcpClientServerConfig server)
            => NotifyToolListChanged(server, CopilotMcpToolDiscoveryCache.Shared);

        internal static bool NotifyToolListChanged(CopilotMcpClientServerConfig server, CopilotMcpToolDiscoveryCache discoveryCache)
        {
            ArgumentNullException.ThrowIfNull(server);
            ArgumentNullException.ThrowIfNull(discoveryCache);
            var invalidated = discoveryCache.Invalidate(server) > 0;
            CopilotMcpClientHealthRegistry.RecordToolListChanged(server, invalidated);
            return invalidated;
        }
    }
}
