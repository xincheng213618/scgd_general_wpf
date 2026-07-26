using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Copilot.Mcp
{
    internal sealed class CopilotMcpClientSession
    {
        public string SessionId { get; init; } = string.Empty;

        public string CallerIdentity { get; init; } = string.Empty;

        public string NetworkSource { get; init; } = string.Empty;

        public CopilotExecutionScope ExecutionScope { get; init; } = CopilotExecutionScope.Empty;

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset LastSeenAtUtc { get; set; }
    }

    internal sealed class CopilotMcpClientSessionStore
    {
        internal const int MaximumSessions = 256;
        internal static readonly TimeSpan IdleLifetime = TimeSpan.FromMinutes(30);

        private readonly object _syncRoot = new();
        private readonly Dictionary<string, CopilotMcpClientSession> _sessions = new(StringComparer.Ordinal);
        private readonly Func<DateTimeOffset> _utcNow;

        public CopilotMcpClientSessionStore(Func<DateTimeOffset>? utcNow = null)
        {
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public bool TryCreate(
            string? networkSource,
            out CopilotMcpClientSession? session)
        {
            var normalizedSource = NormalizeNetworkSource(networkSource);
            var now = _utcNow();
            Span<byte> sessionBytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(sessionBytes);
            var sessionId = Convert.ToHexString(sessionBytes).ToLowerInvariant();
            var callerIdentity = CreateCallerIdentity(sessionId, normalizedSource);
            var candidate = new CopilotMcpClientSession
            {
                SessionId = sessionId,
                CallerIdentity = callerIdentity,
                NetworkSource = normalizedSource,
                ExecutionScope = CopilotExecutionScope.ForExternalMcpSession(sessionId, callerIdentity),
                CreatedAtUtc = now,
                LastSeenAtUtc = now,
            };

            lock (_syncRoot)
            {
                PruneExpiredNoLock(now);
                if (_sessions.Count >= MaximumSessions)
                {
                    session = null;
                    return false;
                }
                _sessions.Add(candidate.SessionId, candidate);
            }

            session = candidate;
            return true;
        }

        public bool TryResolve(string? sessionId, string? networkSource, out CopilotMcpClientSession? session)
        {
            session = null;
            var normalizedSessionId = (sessionId ?? string.Empty).Trim();
            if (normalizedSessionId.Length != 64 || normalizedSessionId.Any(character => !Uri.IsHexDigit(character)))
                return false;

            var normalizedSource = NormalizeNetworkSource(networkSource);
            var now = _utcNow();
            lock (_syncRoot)
            {
                PruneExpiredNoLock(now);
                if (!_sessions.TryGetValue(normalizedSessionId, out var candidate)
                    || !string.Equals(candidate.NetworkSource, normalizedSource, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                candidate.LastSeenAtUtc = now;
                session = candidate;
                return true;
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
                _sessions.Clear();
        }

        internal int Count
        {
            get
            {
                lock (_syncRoot)
                    return _sessions.Count;
            }
        }

        private void PruneExpiredNoLock(DateTimeOffset now)
        {
            var expiredIds = _sessions.Values
                .Where(item => now - item.LastSeenAtUtc >= IdleLifetime)
                .Select(item => item.SessionId)
                .ToArray();
            foreach (var expiredId in expiredIds)
                _sessions.Remove(expiredId);
        }

        private static string CreateCallerIdentity(string sessionId, string networkSource)
        {
            var bytes = Encoding.UTF8.GetBytes(sessionId + "\n" + networkSource);
            var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            return "mcp-session://" + digest;
        }

        private static string NormalizeNetworkSource(string? networkSource)
        {
            var normalized = (networkSource ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return "tcp://local";
            return normalized.Length <= 256 ? normalized : normalized[..256];
        }
    }
}
