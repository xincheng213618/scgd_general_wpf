using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot
{
    public enum CopilotToolExecutionHookMode
    {
        Sync,
    }

    internal sealed record CopilotToolExecutionHookBinding(
        string SourceId,
        ICopilotToolExecutionHook Hook);

    internal sealed record CopilotToolExecutionHookRegistrationDefinition(
        string SourceId,
        ICopilotToolExecutionHook Hook,
        string ToolNamePattern = "*",
        int Order = 0,
        string ConfigurationFingerprint = "");

    public sealed class CopilotToolExecutionHookRegistryEntry
    {
        public string SourceId { get; init; } = string.Empty;

        public string ToolNamePattern { get; init; } = "*";

        public int Order { get; init; }

        public string HookType { get; init; } = string.Empty;

        public CopilotToolExecutionHookMode ExecutionMode { get; init; } =
            CopilotToolExecutionHookMode.Sync;

        public string DefinitionFingerprint { get; init; } = string.Empty;

        public bool IsStructurallyValid()
        {
            return !string.IsNullOrWhiteSpace(SourceId)
                && string.Equals(SourceId, SourceId.Trim(), StringComparison.Ordinal)
                && SourceId.Length <= CopilotToolExecutionHookRegistry.MaxSourceIdLength
                && !SourceId.Any(char.IsControl)
                && !string.IsNullOrWhiteSpace(ToolNamePattern)
                && string.Equals(ToolNamePattern, ToolNamePattern.Trim(), StringComparison.Ordinal)
                && ToolNamePattern.Length <= CopilotToolExecutionHookRegistry.MaxToolNamePatternLength
                && !ToolNamePattern.Any(char.IsControl)
                && !string.IsNullOrWhiteSpace(HookType)
                && string.Equals(HookType, HookType.Trim(), StringComparison.Ordinal)
                && HookType.Length <= 1_024
                && !HookType.Any(char.IsControl)
                && ExecutionMode == CopilotToolExecutionHookMode.Sync
                && IsSha256(DefinitionFingerprint);
        }

        private static bool IsSha256(string value) =>
            value?.Length == 64 && value.All(Uri.IsHexDigit);
    }

    public sealed class CopilotToolExecutionHookRegistrySnapshot
    {
        public long Revision { get; init; }

        public string Fingerprint { get; init; } = string.Empty;

        public IReadOnlyList<CopilotToolExecutionHookRegistryEntry> Entries { get; init; } =
            Array.Empty<CopilotToolExecutionHookRegistryEntry>();

        public bool IsStructurallyValid()
        {
            return Revision >= 0
                && Entries != null
                && Entries.Count <= CopilotToolExecutionHookRegistry.MaxRegistrations + 1
                && Entries.All(entry => entry?.IsStructurallyValid() == true)
                && Entries.Select(entry => entry.SourceId).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Entries.Count
                && Fingerprint?.Length == 64
                && Fingerprint.All(Uri.IsHexDigit)
                && string.Equals(
                    Fingerprint,
                    CopilotToolExecutionHookRegistry.ComputeFingerprint(Entries),
                    StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Holds trusted in-process tool lifecycle hooks. The executor resolves one
    /// immutable hook list per invocation so registration changes never split
    /// the before/after lifecycle of an in-flight tool call.
    /// </summary>
    public sealed class CopilotToolExecutionHookRegistry
    {
        public const int MaxRegistrations = 128;
        public const int MaxSourceIdLength = 160;
        public const int MaxToolNamePatternLength = 512;

        private static readonly Lazy<CopilotToolExecutionHookRegistry> SharedRegistry = new(
            () => new CopilotToolExecutionHookRegistry(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly object _syncRoot = new();
        private readonly Dictionary<long, Registration> _registrations = new();
        private long _nextRegistrationId;
        private long _revision;

        public static CopilotToolExecutionHookRegistry Shared => SharedRegistry.Value;

        /// <summary>
        /// Registers a trusted in-process hook. When hook behavior depends on
        /// mutable configuration, supply its SHA-256 digest so persisted Agent
        /// checkpoints can detect that authorization surface changing.
        /// </summary>
        public IDisposable Register(
            string sourceId,
            ICopilotToolExecutionHook hook,
            string toolNamePattern = "*",
            int order = 0,
            string configurationFingerprint = "")
        {
            ArgumentNullException.ThrowIfNull(hook);
            return RegisterBatch(
            [
                new CopilotToolExecutionHookRegistrationDefinition(
                    sourceId,
                    hook,
                    toolNamePattern,
                    order,
                    configurationFingerprint),
            ]);
        }

        internal IDisposable RegisterBatch(
            IEnumerable<CopilotToolExecutionHookRegistrationDefinition> definitions)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            var candidates = definitions.Select(CreateCandidate).ToArray();
            if (candidates.Length == 0)
                throw new ArgumentException("At least one Copilot tool hook registration is required.", nameof(definitions));
            var duplicateSource = candidates
                .GroupBy(candidate => candidate.Entry.SourceId, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1)
                ?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateSource))
            {
                throw new InvalidOperationException(
                    $"A Copilot tool hook registration batch contains duplicate source '{duplicateSource}'.");
            }

            long[] registrationIds;
            lock (_syncRoot)
            {
                if (_registrations.Count + candidates.Length > MaxRegistrations)
                    throw new InvalidOperationException($"Copilot tool hook registration is limited to {MaxRegistrations} active entries.");
                var conflictingSource = candidates
                    .Select(candidate => candidate.Entry.SourceId)
                    .FirstOrDefault(sourceId => _registrations.Values.Any(item =>
                        string.Equals(item.SourceId, sourceId, StringComparison.OrdinalIgnoreCase)));
                if (!string.IsNullOrWhiteSpace(conflictingSource))
                {
                    throw new InvalidOperationException(
                        $"A Copilot tool hook is already registered for source '{conflictingSource}'.");
                }

                registrationIds = new long[candidates.Length];
                for (var index = 0; index < candidates.Length; index++)
                {
                    var candidate = candidates[index];
                    var registrationId = ++_nextRegistrationId;
                    registrationIds[index] = registrationId;
                    _registrations.Add(registrationId, new Registration(
                        registrationId,
                        candidate.Entry.SourceId,
                        candidate.Entry.ToolNamePattern,
                        candidate.Entry.Order,
                        candidate.Hook,
                        candidate.Entry.DefinitionFingerprint,
                        candidate.Matcher));
                }
                _revision++;
            }

            return new RegistrationLease(this, registrationIds);
        }

        public CopilotToolExecutionHookRegistrySnapshot GetSnapshot()
        {
            lock (_syncRoot)
            {
                return CreateSnapshot(
                    _revision,
                    Order(_registrations.Values)
                        .Select(item => new CopilotToolExecutionHookRegistryEntry
                        {
                            SourceId = item.SourceId,
                            ToolNamePattern = item.ToolNamePattern,
                            Order = item.Order,
                            HookType = item.Hook.GetType().FullName ?? item.Hook.GetType().Name,
                            DefinitionFingerprint = item.DefinitionFingerprint,
                        }));
            }
        }

        internal static CopilotToolExecutionHookRegistryEntry CreateSnapshotEntry(
            string sourceId,
            string toolNamePattern,
            int order,
            ICopilotToolExecutionHook hook,
            string configurationFingerprint = "")
        {
            ArgumentNullException.ThrowIfNull(hook);
            return new CopilotToolExecutionHookRegistryEntry
            {
                SourceId = NormalizeSourceId(sourceId),
                ToolNamePattern = NormalizePattern(toolNamePattern),
                Order = order,
                HookType = hook.GetType().FullName ?? hook.GetType().Name,
                ExecutionMode = CopilotToolExecutionHookMode.Sync,
                DefinitionFingerprint = CreateDefinitionFingerprint(hook, configurationFingerprint),
            };
        }

        internal static CopilotToolExecutionHookRegistrySnapshot CreateSnapshot(
            long revision,
            IEnumerable<CopilotToolExecutionHookRegistryEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var materialized = entries.ToArray();
            return new CopilotToolExecutionHookRegistrySnapshot
            {
                Revision = Math.Max(0, revision),
                Fingerprint = ComputeFingerprint(materialized),
                Entries = materialized,
            };
        }

        internal static string ComputeFingerprint(
            IEnumerable<CopilotToolExecutionHookRegistryEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(entries);
            var stableData = JsonSerializer.Serialize(entries.Select(entry => new
            {
                entry.SourceId,
                entry.ToolNamePattern,
                entry.Order,
                entry.HookType,
                ExecutionMode = entry.ExecutionMode.ToString().ToLowerInvariant(),
                entry.DefinitionFingerprint,
            }));
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(stableData))).ToLowerInvariant();
        }

        internal IReadOnlyList<CopilotToolExecutionHookBinding> Resolve(string toolName)
        {
            var normalizedToolName = toolName?.Trim() ?? string.Empty;
            lock (_syncRoot)
            {
                return Order(_registrations.Values)
                    .Where(item => item.Matcher.IsMatch(normalizedToolName))
                    .Select(item => new CopilotToolExecutionHookBinding(item.SourceId, item.Hook))
                    .ToArray();
            }
        }

        private static IEnumerable<Registration> Order(IEnumerable<Registration> registrations)
        {
            return registrations
                .OrderBy(item => item.Order)
                .ThenBy(item => item.RegistrationId);
        }

        private static string NormalizeSourceId(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("Copilot tool hook source id cannot be empty.", nameof(sourceId));
            var normalized = sourceId.Trim();
            if (normalized.Length > MaxSourceIdLength || normalized.Any(char.IsControl))
                throw new ArgumentException($"Copilot tool hook source id must be at most {MaxSourceIdLength} visible characters.", nameof(sourceId));
            return normalized;
        }

        private static string NormalizePattern(string? toolNamePattern)
        {
            var normalized = string.IsNullOrWhiteSpace(toolNamePattern) ? "*" : toolNamePattern.Trim();
            if (normalized.Length > MaxToolNamePatternLength || normalized.Any(char.IsControl))
            {
                throw new ArgumentException(
                    $"Copilot tool hook matcher must be at most {MaxToolNamePatternLength} visible characters.",
                    nameof(toolNamePattern));
            }
            return normalized;
        }

        private static Regex CreateMatcher(string toolNamePattern)
        {
            var expression = toolNamePattern == "*" ? ".*" : toolNamePattern;
            try
            {
                return new Regex(
                    expression,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                throw new ArgumentException(
                    "Copilot tool hook matcher must be a valid non-backtracking regular expression.",
                    nameof(toolNamePattern),
                    ex);
            }
        }

        private static RegistrationCandidate CreateCandidate(
            CopilotToolExecutionHookRegistrationDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            ArgumentNullException.ThrowIfNull(definition.Hook);
            var entry = CreateSnapshotEntry(
                definition.SourceId,
                definition.ToolNamePattern,
                definition.Order,
                definition.Hook,
                definition.ConfigurationFingerprint);
            if (entry.SourceId.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The 'builtin:' Copilot tool hook source prefix is reserved.", nameof(definition));
            return new RegistrationCandidate(
                entry,
                definition.Hook,
                CreateMatcher(entry.ToolNamePattern));
        }

        private static string CreateDefinitionFingerprint(
            ICopilotToolExecutionHook hook,
            string? configurationFingerprint)
        {
            var normalizedConfigurationFingerprint = NormalizeConfigurationFingerprint(configurationFingerprint);
            var hookType = hook.GetType();
            var assemblyName = hookType.Assembly.GetName();
            var identity = string.Join("|", new[]
            {
                assemblyName.Name ?? string.Empty,
                assemblyName.Version?.ToString() ?? string.Empty,
                hookType.Module.ModuleVersionId.ToString("N"),
                hookType.FullName ?? hookType.Name,
                normalizedConfigurationFingerprint,
            });
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }

        private static string NormalizeConfigurationFingerprint(string? configurationFingerprint)
        {
            var normalized = configurationFingerprint?.Trim() ?? string.Empty;
            if (normalized.Length == 0)
                return string.Empty;
            if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ArgumentException(
                    "Copilot tool hook configuration fingerprint must be a SHA-256 value.",
                    nameof(configurationFingerprint));
            }
            return normalized.ToLowerInvariant();
        }

        private void Unregister(IReadOnlyList<long> registrationIds)
        {
            lock (_syncRoot)
            {
                var changed = false;
                foreach (var registrationId in registrationIds)
                    changed |= _registrations.Remove(registrationId);
                if (changed)
                    _revision++;
            }
        }

        private sealed record RegistrationCandidate(
            CopilotToolExecutionHookRegistryEntry Entry,
            ICopilotToolExecutionHook Hook,
            Regex Matcher);

        private sealed record Registration(
            long RegistrationId,
            string SourceId,
            string ToolNamePattern,
            int Order,
            ICopilotToolExecutionHook Hook,
            string DefinitionFingerprint,
            Regex Matcher);

        private sealed class RegistrationLease : IDisposable
        {
            private CopilotToolExecutionHookRegistry? _owner;
            private readonly long[] _registrationIds;

            public RegistrationLease(CopilotToolExecutionHookRegistry owner, long[] registrationIds)
            {
                _owner = owner;
                _registrationIds = registrationIds;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _owner, null)?.Unregister(_registrationIds);
            }
        }
    }
}
