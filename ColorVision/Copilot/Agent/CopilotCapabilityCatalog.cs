using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace ColorVision.Copilot
{
    public enum CopilotCapabilitySourceKind
    {
        BuiltIn,
        ExternalMcp,
        Plugin,
    }

    public interface ICopilotCapabilityCatalogIdentity
    {
        string CatalogCapabilityKey { get; }
    }

    public interface ICopilotCapabilityCatalogVersionIdentity
    {
        string CatalogVersionFingerprint { get; }
    }

    public sealed class CopilotCapabilityCatalogEntry
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public CopilotCapabilitySourceKind SourceKind { get; init; }

        public string SourceId { get; init; } = string.Empty;

        public string SourceName { get; init; } = string.Empty;

        public long Revision { get; init; }

        public string Fingerprint { get; init; } = string.Empty;

        public CopilotToolAccess Access { get; init; }

        public CopilotToolRiskLevel RiskLevel { get; init; }

        public CopilotToolApprovalMode ApprovalMode { get; init; }

        public CopilotToolIdempotency Idempotency { get; init; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; init; }

        public long ExecutionTimeoutMs { get; init; }

        public CopilotToolAuditArgumentMode AuditArgumentMode { get; init; }

        public CopilotToolEvidenceMode EvidenceMode { get; init; }

        public string InputSchemaFingerprint { get; init; } = string.Empty;
    }

    public sealed class CopilotCapabilityCatalogSnapshot
    {
        public long Revision { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public int SourceCount { get; init; }

        public IReadOnlyList<CopilotCapabilityCatalogEntry> Capabilities { get; init; } = Array.Empty<CopilotCapabilityCatalogEntry>();
    }

    public sealed class CopilotCapabilityCatalogChangedEventArgs : EventArgs
    {
        public long PreviousRevision { get; init; }

        public long Revision { get; init; }

        public int CapabilityCount { get; init; }
    }

    public sealed class CopilotCapabilityCatalog
    {
        public const int MaximumCapabilitiesPerSource = 256;
        public const int MaximumCapabilities = 2_048;
        public const int MaximumKnownCapabilities = MaximumCapabilities;
        public const int MaximumToolNameLength = 256;
        public const int MaximumSourceNameInputLength = 1_024;
        public const int MaximumToolDescriptionInputLength = 4_096;
        public const int MaximumCapabilityKeyLength = 512;
        public const int MaximumInputSchemaCharacters = 128 * 1_024;
        public const int MaximumVersionFingerprintLength = 4_096;

        private const int MaximumSources = 64;
        private const int MaximumSourceIdLength = 96;
        private const int MaximumSourceNameLength = 120;
        private const int MaximumDescriptionLength = 800;
        private static readonly Lazy<CopilotCapabilityCatalog> SharedCatalog = new(CreateSharedCatalog, LazyThreadSafetyMode.ExecutionAndPublication);
        private readonly Dictionary<string, SourceState> _sources = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, KnownCapability> _knownCapabilities = new(StringComparer.OrdinalIgnoreCase);
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly object _syncRoot = new();
        private long _revision;
        private DateTimeOffset _updatedAtUtc;

        public CopilotCapabilityCatalog(Func<DateTimeOffset>? utcNow = null)
        {
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        }

        public static CopilotCapabilityCatalog Shared => SharedCatalog.Value;

        public event EventHandler<CopilotCapabilityCatalogChangedEventArgs>? Changed;

        public CopilotCapabilityCatalogSnapshot PublishSource(
            CopilotCapabilitySourceKind sourceKind,
            string sourceId,
            string sourceName,
            IEnumerable<ICopilotTool> tools)
        {
            if (!Enum.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            var normalizedSourceId = NormalizeSourceId(sourceId);
            var rawSourceName = sourceName ?? string.Empty;
            if (rawSourceName.Length > MaximumSourceNameInputLength)
            {
                throw new ArgumentException(
                    $"A capability source name cannot exceed {MaximumSourceNameInputLength} input characters.",
                    nameof(sourceName));
            }
            var normalizedSourceName = Sanitize(rawSourceName, MaximumSourceNameLength);
            if (string.IsNullOrWhiteSpace(normalizedSourceName))
                throw new ArgumentException("A capability source must have a non-empty display name.", nameof(sourceName));

            var sourceTools = (tools ?? throw new ArgumentNullException(nameof(tools)))
                .Take(MaximumCapabilitiesPerSource + 1)
                .ToArray();
            if (sourceTools.Length > MaximumCapabilitiesPerSource)
            {
                throw new ArgumentException(
                    $"A capability source may publish at most {MaximumCapabilitiesPerSource} capabilities.",
                    nameof(tools));
            }

            var candidates = sourceTools
                .Select(tool => CreateCandidate(sourceKind, normalizedSourceId, normalizedSourceName, tool))
                .OrderBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var duplicateId = candidates.GroupBy(candidate => candidate.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (!string.IsNullOrWhiteSpace(duplicateId))
                throw new ArgumentException($"Capability source '{normalizedSourceId}' produced duplicate id '{duplicateId}'.", nameof(tools));

            CopilotCapabilityCatalogChangedEventArgs? change = null;
            CopilotCapabilityCatalogSnapshot snapshot;
            lock (_syncRoot)
            {
                _sources.TryGetValue(normalizedSourceId, out var previousSource);
                if (previousSource == null && candidates.Length == 0)
                    return CreateSnapshotLocked();
                if (SourceMatches(previousSource, sourceKind, normalizedSourceName, candidates))
                    return CreateSnapshotLocked();
                if (previousSource == null && candidates.Length > 0 && _sources.Count >= MaximumSources)
                    throw new InvalidOperationException($"The capability catalog reached its {MaximumSources}-source limit.");
                var prospectiveCapabilityCount = _sources.Values.Sum(source => source.Capabilities.Count)
                    - (previousSource?.Capabilities.Count ?? 0)
                    + candidates.Length;
                if (prospectiveCapabilityCount > MaximumCapabilities)
                {
                    throw new InvalidOperationException(
                        $"The capability catalog reached its {MaximumCapabilities}-capability limit.");
                }

                var previousRevision = _revision;
                if (candidates.Length == 0)
                {
                    _sources.Remove(normalizedSourceId);
                }
                else
                {
                    var records = candidates.ToDictionary(candidate => candidate.Id, candidate => PublishCandidate(candidate), StringComparer.OrdinalIgnoreCase);
                    _sources[normalizedSourceId] = new SourceState(sourceKind, normalizedSourceName, records);
                }
                TrimKnownCapabilitiesLocked();

                _revision++;
                _updatedAtUtc = _utcNow();
                snapshot = CreateSnapshotLocked();
                change = new CopilotCapabilityCatalogChangedEventArgs
                {
                    PreviousRevision = previousRevision,
                    Revision = _revision,
                    CapabilityCount = snapshot.Capabilities.Count,
                };
            }

            PublishChanged(change);
            return snapshot;
        }

        public CopilotCapabilityCatalogSnapshot RetainSources(CopilotCapabilitySourceKind sourceKind, IEnumerable<string> sourceIds)
        {
            if (!Enum.IsDefined(sourceKind))
                throw new ArgumentOutOfRangeException(nameof(sourceKind));
            var retained = new HashSet<string>((sourceIds ?? throw new ArgumentNullException(nameof(sourceIds))).Select(NormalizeSourceId), StringComparer.OrdinalIgnoreCase);
            CopilotCapabilityCatalogChangedEventArgs? change = null;
            CopilotCapabilityCatalogSnapshot snapshot;
            lock (_syncRoot)
            {
                var removed = _sources
                    .Where(pair => pair.Value.SourceKind == sourceKind && !retained.Contains(pair.Key))
                    .Select(pair => pair.Key)
                    .ToArray();
                if (removed.Length == 0)
                    return CreateSnapshotLocked();

                var previousRevision = _revision;
                foreach (var sourceId in removed)
                    _sources.Remove(sourceId);
                TrimKnownCapabilitiesLocked();
                _revision++;
                _updatedAtUtc = _utcNow();
                snapshot = CreateSnapshotLocked();
                change = new CopilotCapabilityCatalogChangedEventArgs
                {
                    PreviousRevision = previousRevision,
                    Revision = _revision,
                    CapabilityCount = snapshot.Capabilities.Count,
                };
            }

            PublishChanged(change);
            return snapshot;
        }

        public CopilotCapabilityCatalogSnapshot GetSnapshot()
        {
            lock (_syncRoot)
                return CreateSnapshotLocked();
        }

        public CopilotCapabilityCatalogSnapshot PublishExternalMcp(CopilotMcpClientServerConfig server, IEnumerable<ICopilotTool> tools)
        {
            ArgumentNullException.ThrowIfNull(server);
            return PublishSource(CopilotCapabilitySourceKind.ExternalMcp, BuildExternalMcpSourceId(server), server.Name, tools);
        }

        public CopilotCapabilityCatalogSnapshot RetainExternalMcpServers(IEnumerable<CopilotMcpClientServerConfig> servers)
        {
            var sourceIds = (servers ?? throw new ArgumentNullException(nameof(servers)))
                .Where(server => server?.Enabled == true)
                .Select(BuildExternalMcpSourceId);
            return RetainSources(CopilotCapabilitySourceKind.ExternalMcp, sourceIds);
        }

        public static string BuildExternalMcpSourceId(CopilotMcpClientServerConfig server)
        {
            ArgumentNullException.ThrowIfNull(server);
            var endpoint = server.Endpoint?.Trim() ?? string.Empty;
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
                endpoint = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
            var connectionIdentity = endpoint.ToUpperInvariant() + "\n" + (server.BearerTokenEnvironmentVariable?.Trim().ToUpperInvariant() ?? string.Empty);
            var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(connectionIdentity));
            return "mcp:" + Convert.ToHexString(fingerprint.AsSpan(0, 6)).ToLowerInvariant();
        }

        private static CopilotCapabilityCatalog CreateSharedCatalog()
        {
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "builtin", "ColorVision", CopilotToolRegistry.CreateBuiltInCatalogTools());
            return catalog;
        }

        private CapabilityRecord PublishCandidate(Candidate candidate)
        {
            var revision = 1L;
            if (_knownCapabilities.TryGetValue(candidate.Id, out var known))
                revision = string.Equals(known.Signature, candidate.Signature, StringComparison.Ordinal) ? known.Revision : known.Revision + 1;

            var entry = new CopilotCapabilityCatalogEntry
            {
                Id = candidate.Id,
                Name = candidate.Name,
                Description = candidate.Description,
                SourceKind = candidate.SourceKind,
                SourceId = candidate.SourceId,
                SourceName = candidate.SourceName,
                Revision = revision,
                Fingerprint = candidate.Signature.ToLowerInvariant(),
                Access = candidate.Capability.Access,
                RiskLevel = candidate.Capability.RiskLevel,
                ApprovalMode = candidate.Capability.ApprovalMode,
                Idempotency = candidate.Capability.Idempotency,
                ConcurrencyMode = candidate.Capability.EffectiveConcurrencyMode,
                ExecutionTimeoutMs = Math.Max(1, (long)candidate.Capability.EffectiveExecutionTimeout.TotalMilliseconds),
                AuditArgumentMode = candidate.Capability.AuditArgumentMode,
                EvidenceMode = candidate.Capability.EvidenceMode,
                InputSchemaFingerprint = candidate.SchemaFingerprint,
            };
            _knownCapabilities[candidate.Id] = new KnownCapability(candidate.Signature, revision, _revision + 1);
            return new CapabilityRecord(entry, candidate.Signature);
        }

        private static Candidate CreateCandidate(
            CopilotCapabilitySourceKind sourceKind,
            string sourceId,
            string sourceName,
            ICopilotTool tool)
        {
            ArgumentNullException.ThrowIfNull(tool);
            var rawName = tool.Name ?? string.Empty;
            if (rawName.Length == 0 || rawName.Length > MaximumToolNameLength || string.IsNullOrWhiteSpace(rawName))
            {
                throw new ArgumentException(
                    $"A catalog capability name must contain 1-{MaximumToolNameLength} characters.",
                    nameof(tool));
            }
            var name = rawName.Trim();
            if (name.Any(char.IsControl))
                throw new ArgumentException("A catalog capability name cannot contain control characters.", nameof(tool));
            var rawDescription = tool.Description ?? string.Empty;
            if (rawDescription.Length > MaximumToolDescriptionInputLength)
            {
                throw new ArgumentException(
                    $"Catalog capability '{name}' has a description longer than {MaximumToolDescriptionInputLength} characters.",
                    nameof(tool));
            }

            var capability = tool.Capability ?? throw new ArgumentException($"Capability '{name}' has no descriptor.", nameof(tool));
            capability.Validate(name);
            var capabilityKey = tool is ICopilotCapabilityCatalogIdentity identity
                ? identity.CatalogCapabilityKey
                : name;
            if ((capabilityKey?.Length ?? 0) > MaximumCapabilityKeyLength)
            {
                throw new ArgumentException(
                    $"Catalog capability '{name}' has an identity key longer than {MaximumCapabilityKeyLength} characters.",
                    nameof(tool));
            }
            var id = sourceId + ":" + NormalizeCapabilityKey(capabilityKey);
            var description = Sanitize(rawDescription, MaximumDescriptionLength);
            var schema = GetSchemaText(tool, name);
            var schemaFingerprint = CreateHash(schema)[..16].ToLowerInvariant();
            var rawVersionFingerprint = tool is ICopilotCapabilityCatalogVersionIdentity versionIdentity
                ? versionIdentity.CatalogVersionFingerprint ?? string.Empty
                : string.Empty;
            if (rawVersionFingerprint.Length > MaximumVersionFingerprintLength)
            {
                throw new ArgumentException(
                    $"Catalog capability '{name}' has a version fingerprint longer than {MaximumVersionFingerprintLength} characters.",
                    nameof(tool));
            }
            var versionFingerprint = rawVersionFingerprint.Trim();
            var signatureText = string.Join("\n", new[]
            {
                id,
                name,
                description,
                sourceKind.ToString(),
                sourceName,
                ((int)capability.Access).ToString(),
                ((int)capability.RiskLevel).ToString(),
                ((int)capability.ApprovalMode).ToString(),
                ((int)capability.Idempotency).ToString(),
                ((int)capability.EffectiveConcurrencyMode).ToString(),
                capability.EffectiveExecutionTimeout.Ticks.ToString(),
                ((int)capability.AuditArgumentMode).ToString(),
                ((int)capability.EvidenceMode).ToString(),
                schema,
                versionFingerprint,
            });
            return new Candidate(
                id,
                name,
                description,
                sourceKind,
                sourceId,
                sourceName,
                capability,
                schemaFingerprint,
                CreateHash(signatureText));
        }

        private static bool SourceMatches(SourceState? previous, CopilotCapabilitySourceKind sourceKind, string sourceName, IReadOnlyList<Candidate> candidates)
        {
            if (previous == null
                || previous.SourceKind != sourceKind
                || !string.Equals(previous.SourceName, sourceName, StringComparison.Ordinal)
                || previous.Capabilities.Count != candidates.Count)
            {
                return false;
            }

            return candidates.All(candidate => previous.Capabilities.TryGetValue(candidate.Id, out var existing)
                && string.Equals(existing.Signature, candidate.Signature, StringComparison.Ordinal));
        }

        private CopilotCapabilityCatalogSnapshot CreateSnapshotLocked()
        {
            var entries = _sources.Values
                .SelectMany(source => source.Capabilities.Values)
                .Select(record => record.Entry)
                .OrderBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new CopilotCapabilityCatalogSnapshot
            {
                Revision = _revision,
                UpdatedAtUtc = _updatedAtUtc,
                SourceCount = _sources.Count,
                Capabilities = entries,
            };
        }

        private void TrimKnownCapabilitiesLocked()
        {
            var activeCapabilityIds = _sources.Values
                .SelectMany(source => source.Capabilities.Keys)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            while (_knownCapabilities.Count > MaximumKnownCapabilities)
            {
                var oldestRetired = _knownCapabilities
                    .Where(pair => !activeCapabilityIds.Contains(pair.Key))
                    .OrderBy(pair => pair.Value.LastSeenSequence)
                    .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (string.IsNullOrEmpty(oldestRetired.Key))
                    throw new InvalidOperationException("The capability history limit cannot be satisfied without removing an active capability.");
                _knownCapabilities.Remove(oldestRetired.Key);
            }
        }

        private void PublishChanged(CopilotCapabilityCatalogChangedEventArgs? change)
        {
            if (change == null || Changed is not { } handlers)
                return;
            foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<CopilotCapabilityCatalogChangedEventArgs>>())
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

        private static string NormalizeSourceId(string sourceId)
        {
            var raw = sourceId ?? string.Empty;
            if (raw.Length > MaximumSourceIdLength)
                throw new ArgumentException($"A capability source id must contain 1-{MaximumSourceIdLength} characters.", nameof(sourceId));
            var normalized = raw.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || normalized.Length > MaximumSourceIdLength)
                throw new ArgumentException($"A capability source id must contain 1-{MaximumSourceIdLength} characters.", nameof(sourceId));
            if (normalized.Any(character => !(character is >= 'a' and <= 'z' or >= '0' and <= '9' or ':' or '.' or '_' or '-')))
                throw new ArgumentException("A capability source id may contain only ASCII letters, digits, ':', '.', '_' and '-'.", nameof(sourceId));
            return normalized;
        }

        private static string NormalizeCapabilityKey(string capabilityKey)
        {
            var source = capabilityKey?.Trim() ?? string.Empty;
            var builder = new StringBuilder(source.Length);
            var previousSeparator = false;
            foreach (var character in source)
            {
                var normalized = character is >= 'A' and <= 'Z' ? char.ToLowerInvariant(character) : character;
                var allowed = normalized is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '.' or '-';
                if (allowed)
                {
                    builder.Append(normalized);
                    previousSeparator = false;
                }
                else if (!previousSeparator)
                {
                    builder.Append('-');
                    previousSeparator = true;
                }
            }

            var key = builder.ToString().Trim('-', '.', '_');
            if (key.Length == 0)
                key = "capability-" + CreateHash(source)[..12].ToLowerInvariant();
            return key.Length <= 96 ? key : key[..83] + "-" + CreateHash(source)[..12].ToLowerInvariant();
        }

        private static string GetSchemaText(ICopilotTool tool, string toolName)
        {
            string schema;
            try
            {
                var inputSchema = tool.InputSchema
                    ?? throw new InvalidOperationException("The tool returned a null input schema.");
                schema = inputSchema.JsonSchema.GetRawText();
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Catalog capability '{toolName}' must expose a readable input schema.",
                    nameof(tool),
                    ex);
            }
            if (schema.Length > MaximumInputSchemaCharacters)
            {
                throw new ArgumentException(
                    $"Catalog capability '{toolName}' has an input schema longer than {MaximumInputSchemaCharacters} characters.",
                    nameof(tool));
            }
            return schema;
        }

        private static string CreateHash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));

        private static string Sanitize(string? value, int maximumLength)
        {
            var text = (value ?? string.Empty).Trim();
            var truncated = text.Length > maximumLength;
            var length = Math.Min(text.Length, maximumLength);
            var builder = new StringBuilder(length + (truncated ? 3 : 0));
            for (var index = 0; index < length; index++)
                builder.Append(char.IsControl(text[index]) ? ' ' : text[index]);
            if (truncated)
                builder.Append("...");
            return builder.ToString();
        }

        private sealed record Candidate(
            string Id,
            string Name,
            string Description,
            CopilotCapabilitySourceKind SourceKind,
            string SourceId,
            string SourceName,
            CopilotToolCapabilityDescriptor Capability,
            string SchemaFingerprint,
            string Signature);

        private sealed record CapabilityRecord(CopilotCapabilityCatalogEntry Entry, string Signature);

        private sealed record KnownCapability(string Signature, long Revision, long LastSeenSequence);

        private sealed record SourceState(
            CopilotCapabilitySourceKind SourceKind,
            string SourceName,
            IReadOnlyDictionary<string, CapabilityRecord> Capabilities);
    }
}
