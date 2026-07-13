using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public enum CopilotAgentCheckpointCompatibilityKind
    {
        Compatible,
        Invalid,
        ProfileChanged,
        CapabilitySnapshotMissing,
        CapabilityDrift,
        ToolSurfaceSnapshotMissing,
        ToolSurfaceDrift,
    }

    public sealed class CopilotAgentCheckpointCapability
    {
        public string Id { get; init; } = string.Empty;

        public long Revision { get; init; }

        public string Fingerprint { get; init; } = string.Empty;
    }

    public sealed class CopilotAgentCheckpointCompatibility
    {
        public CopilotAgentCheckpointCompatibilityKind Kind { get; init; }

        public long PreviousCatalogRevision { get; init; }

        public long CurrentCatalogRevision { get; init; }

        public IReadOnlyList<string> RemovedCapabilityIds { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ChangedCapabilityIds { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> RemovedToolNames { get; init; } = Array.Empty<string>();

        public bool CanResume => Kind == CopilotAgentCheckpointCompatibilityKind.Compatible;

        public bool RequiresReplan => Kind is CopilotAgentCheckpointCompatibilityKind.CapabilitySnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.CapabilityDrift
            or CopilotAgentCheckpointCompatibilityKind.ToolSurfaceSnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.ToolSurfaceDrift;
    }

    public sealed class CopilotAgentSessionCheckpoint
    {
        public const int MaxSerializedSessionCharacters = 4_000_000;
        public const int MaxCheckpointCapabilities = 2_048;
        public const int MaxAvailableToolNames = 2_048;
        public const int MaxAvailableToolNameLength = 256;
        public const int MaxConversationMemoryMessages = 16;
        public const int MaxConversationMemoryContentLength = 8_000;
        public const int MaxConversationMemoryCharacters = 64_000;
        public const int CurrentToolSurfaceVersion = 1;

        public string ProfileKey { get; init; } = string.Empty;

        public string SerializedSessionJson { get; init; } = string.Empty;

        public long CapabilityCatalogRevision { get; init; }

        public IReadOnlyList<CopilotAgentCheckpointCapability> Capabilities { get; init; } = Array.Empty<CopilotAgentCheckpointCapability>();

        public int ToolSurfaceVersion { get; init; }

        public IReadOnlyList<string> AvailableToolNames { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotAgentEvidenceArtifact> EvidenceArtifacts { get; init; } = Array.Empty<CopilotAgentEvidenceArtifact>();

        public IReadOnlyList<CopilotRequestMessage> ConversationMemory { get; init; } = Array.Empty<CopilotRequestMessage>();

        public CopilotAgentTaskEventJournalSnapshot TaskEventJournal { get; init; } = new();

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public bool IsUsableFor(CopilotProfileConfig profile)
        {
            return EvaluateFor(profile, CopilotCapabilityCatalog.Shared.GetSnapshot()).CanResume;
        }

        public bool IsUsableFor(CopilotProfileConfig profile, CopilotCapabilityCatalogSnapshot capabilitySnapshot)
        {
            return EvaluateFor(profile, capabilitySnapshot).CanResume;
        }

        public CopilotAgentCheckpointCompatibility EvaluateFor(
            CopilotProfileConfig profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            IReadOnlyCollection<string>? availableToolNames = null)
        {
            ArgumentNullException.ThrowIfNull(capabilitySnapshot);
            if (profile == null || !IsStructurallyValid())
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.Invalid, capabilitySnapshot);
            if (!string.Equals(ProfileKey, CreateProfileKey(profile), StringComparison.Ordinal))
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ProfileChanged, capabilitySnapshot);
            if (CapabilityCatalogRevision <= 0 || Capabilities == null || Capabilities.Count == 0)
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.CapabilitySnapshotMissing, capabilitySnapshot);

            var currentCapabilities = capabilitySnapshot.Capabilities.ToDictionary(capability => capability.Id, StringComparer.OrdinalIgnoreCase);
            var removed = new List<string>();
            var changed = new List<string>();
            foreach (var persisted in Capabilities)
            {
                if (!currentCapabilities.TryGetValue(persisted.Id, out var current))
                {
                    removed.Add(persisted.Id);
                }
                else if (!string.Equals(persisted.Fingerprint, current.Fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    changed.Add(persisted.Id);
                }
            }

            if (removed.Count > 0 || changed.Count > 0)
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.CapabilityDrift, capabilitySnapshot, removed, changed);

            if (availableToolNames != null)
            {
                if (ToolSurfaceVersion != CurrentToolSurfaceVersion)
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ToolSurfaceSnapshotMissing, capabilitySnapshot);

                var currentToolNames = availableToolNames
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Select(name => name.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removedToolNames = AvailableToolNames
                    .Where(name => !currentToolNames.Contains(name))
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (removedToolNames.Length > 0)
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ToolSurfaceDrift, capabilitySnapshot, removedTools: removedToolNames);
            }

            return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.Compatible, capabilitySnapshot);
        }

        public bool IsStructurallyValid()
        {
            if (string.IsNullOrWhiteSpace(ProfileKey)
                || string.IsNullOrWhiteSpace(SerializedSessionJson)
                || SerializedSessionJson.Length > MaxSerializedSessionCharacters
                || CapabilityCatalogRevision < 0
                || Capabilities == null
                || Capabilities?.Count > MaxCheckpointCapabilities
                || ToolSurfaceVersion is < 0 or > CurrentToolSurfaceVersion
                || AvailableToolNames == null
                || AvailableToolNames?.Count > MaxAvailableToolNames
                || (AvailableToolNames?.Any(name => string.IsNullOrWhiteSpace(name)
                    || !string.Equals(name, name.Trim(), StringComparison.Ordinal)
                    || name.Length > MaxAvailableToolNameLength
                    || name.Any(char.IsControl)) ?? false)
                || (AvailableToolNames?.Distinct(StringComparer.OrdinalIgnoreCase).Count() != AvailableToolNames?.Count)
                || (ToolSurfaceVersion == 0 && AvailableToolNames?.Count > 0)
                || (Capabilities?.Any(capability => capability == null
                    || string.IsNullOrWhiteSpace(capability.Id)
                    || capability.Id.Length > 200
                    || capability.Revision <= 0
                    || string.IsNullOrWhiteSpace(capability.Fingerprint)
                    || capability.Fingerprint.Length != 64
                    || capability.Fingerprint.Any(character => !Uri.IsHexDigit(character))) ?? false)
                || (Capabilities?.Select(capability => capability.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Capabilities?.Count)
                || EvidenceArtifacts == null
                || EvidenceArtifacts?.Count > CopilotAgentEvidenceArtifact.MaxArtifacts
                || ConversationMemory == null
                || ConversationMemory.Count > MaxConversationMemoryMessages
                || ConversationMemory.Sum(message => message.Content?.Length ?? 0) > MaxConversationMemoryCharacters
                || ConversationMemory.Any(message => message.Role is not ("user" or "assistant")
                    || string.IsNullOrWhiteSpace(message.Content)
                    || !string.Equals(message.Content, message.Content.Trim(), StringComparison.Ordinal)
                    || message.Content.Length > MaxConversationMemoryContentLength
                    || message.Content.Contains('\0'))
                || TaskEventJournal == null
                || TaskEventJournal?.Events?.Count > CopilotAgentTaskEventJournal.MaxEvents)
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(SerializedSessionJson);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static CopilotAgentSessionCheckpoint? Create(
            CopilotProfileConfig profile,
            string serializedSessionJson,
            CopilotCapabilityCatalogSnapshot? capabilitySnapshot = null,
            IReadOnlyList<CopilotAgentEvidenceArtifact>? evidenceArtifacts = null,
            CopilotAgentTaskEventJournalSnapshot? taskEventJournal = null,
            IReadOnlyCollection<string>? availableToolNames = null,
            IReadOnlyList<CopilotRequestMessage>? conversationMemory = null)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var json = serializedSessionJson?.Trim() ?? string.Empty;
            if (json.Length == 0 || json.Length > MaxSerializedSessionCharacters)
                return null;
            capabilitySnapshot ??= CopilotCapabilityCatalog.Shared.GetSnapshot();
            if (capabilitySnapshot.Capabilities.Count == 0 || capabilitySnapshot.Capabilities.Count > MaxCheckpointCapabilities)
                return null;
            var persistedEvidence = (evidenceArtifacts ?? Array.Empty<CopilotAgentEvidenceArtifact>()).ToArray();
            if (persistedEvidence.Length > CopilotAgentEvidenceArtifact.MaxArtifacts
                || persistedEvidence.Any(artifact => artifact?.IsStructurallyValid() != true))
            {
                return null;
            }
            taskEventJournal ??= new CopilotAgentTaskEventJournalSnapshot();
            if (!taskEventJournal.IsStructurallyValid())
                return null;
            var persistedToolNames = (availableToolNames ?? Array.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (persistedToolNames.Length > MaxAvailableToolNames
                || persistedToolNames.Any(name => name.Length > MaxAvailableToolNameLength || name.Any(char.IsControl)))
            {
                return null;
            }
            var persistedConversationMemory = (conversationMemory ?? Array.Empty<CopilotRequestMessage>()).ToArray();

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = CreateProfileKey(profile),
                SerializedSessionJson = json,
                CapabilityCatalogRevision = capabilitySnapshot.Revision,
                Capabilities = capabilitySnapshot.Capabilities
                    .Select(capability => new CopilotAgentCheckpointCapability
                    {
                        Id = capability.Id,
                        Revision = capability.Revision,
                        Fingerprint = capability.Fingerprint,
                    })
                    .ToArray(),
                ToolSurfaceVersion = availableToolNames == null ? 0 : CurrentToolSurfaceVersion,
                AvailableToolNames = persistedToolNames,
                EvidenceArtifacts = persistedEvidence,
                ConversationMemory = persistedConversationMemory,
                TaskEventJournal = taskEventJournal,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            return checkpoint.IsStructurallyValid() ? checkpoint : null;
        }

        private CopilotAgentCheckpointCompatibility CreateCompatibility(
            CopilotAgentCheckpointCompatibilityKind kind,
            CopilotCapabilityCatalogSnapshot currentSnapshot,
            IReadOnlyList<string>? removed = null,
            IReadOnlyList<string>? changed = null,
            IReadOnlyList<string>? removedTools = null)
        {
            return new CopilotAgentCheckpointCompatibility
            {
                Kind = kind,
                PreviousCatalogRevision = CapabilityCatalogRevision,
                CurrentCatalogRevision = currentSnapshot.Revision,
                RemovedCapabilityIds = removed ?? Array.Empty<string>(),
                ChangedCapabilityIds = changed ?? Array.Empty<string>(),
                RemovedToolNames = removedTools ?? Array.Empty<string>(),
            };
        }

        public static string CreateProfileKey(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var value = string.Join("|", new[]
            {
                profile.Id?.Trim() ?? string.Empty,
                profile.ProviderType.ToString(),
                profile.BaseUrl?.Trim().TrimEnd('/') ?? string.Empty,
                profile.Model?.Trim() ?? string.Empty,
                profile.EffectiveSystemPrompt,
            });
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        }
    }
}
