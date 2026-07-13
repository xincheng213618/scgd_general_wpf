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

        public bool CanResume => Kind == CopilotAgentCheckpointCompatibilityKind.Compatible;

        public bool RequiresReplan => Kind is CopilotAgentCheckpointCompatibilityKind.CapabilitySnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.CapabilityDrift;
    }

    public sealed class CopilotAgentSessionCheckpoint
    {
        public const int MaxSerializedSessionCharacters = 4_000_000;
        public const int MaxCheckpointCapabilities = 2_048;

        public string ProfileKey { get; init; } = string.Empty;

        public string SerializedSessionJson { get; init; } = string.Empty;

        public long CapabilityCatalogRevision { get; init; }

        public IReadOnlyList<CopilotAgentCheckpointCapability> Capabilities { get; init; } = Array.Empty<CopilotAgentCheckpointCapability>();

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
            CopilotCapabilityCatalogSnapshot capabilitySnapshot)
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

            return removed.Count == 0 && changed.Count == 0
                ? CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.Compatible, capabilitySnapshot)
                : CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.CapabilityDrift, capabilitySnapshot, removed, changed);
        }

        public bool IsStructurallyValid()
        {
            if (string.IsNullOrWhiteSpace(ProfileKey)
                || string.IsNullOrWhiteSpace(SerializedSessionJson)
                || SerializedSessionJson.Length > MaxSerializedSessionCharacters
                || CapabilityCatalogRevision < 0
                || Capabilities?.Count > MaxCheckpointCapabilities
                || (Capabilities?.Any(capability => capability == null
                    || string.IsNullOrWhiteSpace(capability.Id)
                    || capability.Id.Length > 200
                    || capability.Revision <= 0
                    || string.IsNullOrWhiteSpace(capability.Fingerprint)
                    || capability.Fingerprint.Length != 64
                    || capability.Fingerprint.Any(character => !Uri.IsHexDigit(character))) ?? false)
                || (Capabilities?.Select(capability => capability.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Capabilities?.Count))
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
            CopilotCapabilityCatalogSnapshot? capabilitySnapshot = null)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var json = serializedSessionJson?.Trim() ?? string.Empty;
            if (json.Length == 0 || json.Length > MaxSerializedSessionCharacters)
                return null;
            capabilitySnapshot ??= CopilotCapabilityCatalog.Shared.GetSnapshot();
            if (capabilitySnapshot.Capabilities.Count == 0 || capabilitySnapshot.Capabilities.Count > MaxCheckpointCapabilities)
                return null;

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
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            return checkpoint.IsStructurallyValid() ? checkpoint : null;
        }

        private CopilotAgentCheckpointCompatibility CreateCompatibility(
            CopilotAgentCheckpointCompatibilityKind kind,
            CopilotCapabilityCatalogSnapshot currentSnapshot,
            IReadOnlyList<string>? removed = null,
            IReadOnlyList<string>? changed = null)
        {
            return new CopilotAgentCheckpointCompatibility
            {
                Kind = kind,
                PreviousCatalogRevision = CapabilityCatalogRevision,
                CurrentCatalogRevision = currentSnapshot.Revision,
                RemovedCapabilityIds = removed ?? Array.Empty<string>(),
                ChangedCapabilityIds = changed ?? Array.Empty<string>(),
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
