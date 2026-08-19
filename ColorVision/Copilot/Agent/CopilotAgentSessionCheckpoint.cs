using System;
using System.Buffers.Binary;
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
        EnvironmentSnapshotMissing,
        EnvironmentDrift,
        HookSurfaceSnapshotMissing,
        HookSurfaceDrift,
        ProjectInstructionSnapshotMissing,
        ProjectInstructionDrift,
        UncertainToolOutcome,
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
            or CopilotAgentCheckpointCompatibilityKind.ToolSurfaceDrift
            or CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift
            or CopilotAgentCheckpointCompatibilityKind.HookSurfaceSnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift
            or CopilotAgentCheckpointCompatibilityKind.ProjectInstructionSnapshotMissing
            or CopilotAgentCheckpointCompatibilityKind.ProjectInstructionDrift
            or CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome;
    }

    public sealed class CopilotAgentSessionCheckpoint
    {
        public const int MaxSerializedSessionCharacters = 4_000_000;
        internal const string CompressedSerializedSessionPrefix = "cv-gzip-v1:";
        private const int MinimumSerializedSessionCompressionCharacters = 1_024;
        public const int MaxCheckpointCapabilities = 2_048;
        public const int MaxAvailableToolNames = 2_048;
        public const int MaxAvailableToolNameLength = 256;
        public const int MaxConversationMemoryMessages = 16;
        public const int MaxConversationMemoryContentLength = 8_000;
        public const int MaxConversationMemoryCharacters = 64_000;
        public const int MaxTaskIntentTextLength = 8_000;
        public const int CurrentToolSurfaceVersion = 1;
        public const int CurrentEnvironmentVersion = CopilotAgentEnvironmentContext.CurrentVersion;
        public const int CurrentHookSurfaceVersion = 1;
        public const int CurrentProjectInstructionSurfaceVersion = 2;

        private string _serializedSessionJson = string.Empty;
        private string _serializedSessionPayload = string.Empty;
        private bool _isDetachedSnapshot;

        public string ProfileKey { get; init; } = string.Empty;

        [Newtonsoft.Json.JsonIgnore]
        public string SerializedSessionJson
        {
            get => _serializedSessionJson;
            init
            {
                _serializedSessionJson = value ?? string.Empty;
                _serializedSessionPayload = CopilotPersistedTextCodec.Encode(
                    _serializedSessionJson,
                    CompressedSerializedSessionPrefix,
                    MinimumSerializedSessionCompressionCharacters,
                    MaxSerializedSessionCharacters);
            }
        }

        [Newtonsoft.Json.JsonProperty(nameof(SerializedSessionJson))]
        private string SerializedSessionPayload
        {
            get => _serializedSessionPayload;
            init
            {
                var payload = value ?? string.Empty;
                _serializedSessionJson = CopilotPersistedTextCodec.Decode(
                    payload,
                    CompressedSerializedSessionPrefix,
                    MaxSerializedSessionCharacters);
                _serializedSessionPayload = CopilotPersistedTextCodec.RetainOrEncode(
                    payload,
                    _serializedSessionJson,
                    CompressedSerializedSessionPrefix,
                    MinimumSerializedSessionCompressionCharacters,
                    MaxSerializedSessionCharacters);
            }
        }

        public long CapabilityCatalogRevision { get; init; }

        public IReadOnlyList<CopilotAgentCheckpointCapability> Capabilities { get; init; } = Array.Empty<CopilotAgentCheckpointCapability>();

        public int ToolSurfaceVersion { get; init; }

        public IReadOnlyList<string> AvailableToolNames { get; init; } = Array.Empty<string>();

        public int EnvironmentVersion { get; init; }

        public string EnvironmentFingerprint { get; init; } = string.Empty;

        public int HookSurfaceVersion { get; init; }

        public string HookSurfaceFingerprint { get; init; } = string.Empty;

        public int ProjectInstructionSurfaceVersion { get; init; }

        public string ProjectInstructionSurfaceFingerprint { get; init; } = string.Empty;

        public IReadOnlyList<CopilotAgentEvidenceArtifact> EvidenceArtifacts { get; init; } = Array.Empty<CopilotAgentEvidenceArtifact>();

        public IReadOnlyList<CopilotRequestMessage> ConversationMemory { get; init; } = Array.Empty<CopilotRequestMessage>();

        public string TaskIntentText { get; init; } = string.Empty;

        public CopilotAgentTaskEventJournalSnapshot TaskEventJournal { get; init; } = new();

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public CopilotAgentSessionCheckpoint()
        {
        }

        internal CopilotAgentSessionCheckpoint(CopilotAgentSessionCheckpoint source)
        {
            ArgumentNullException.ThrowIfNull(source);
            _serializedSessionJson = source._serializedSessionJson;
            _serializedSessionPayload = source._serializedSessionPayload;
        }

        public bool IsUsableFor(CopilotProfileConfig profile)
        {
            return EvaluateFor(
                profile,
                CopilotCapabilityCatalog.Shared.GetSnapshot(),
                hookSurfaceSnapshot: CopilotToolExecutor.GetSharedHookSurfaceSnapshot()).CanResume;
        }

        public bool IsUsableFor(CopilotProfileConfig profile, CopilotCapabilityCatalogSnapshot capabilitySnapshot)
        {
            return EvaluateFor(
                profile,
                capabilitySnapshot,
                hookSurfaceSnapshot: CopilotToolExecutor.GetSharedHookSurfaceSnapshot()).CanResume;
        }

        public CopilotAgentCheckpointCompatibility EvaluateFor(
            CopilotProfileConfig profile,
            CopilotCapabilityCatalogSnapshot capabilitySnapshot,
            IReadOnlyCollection<string>? availableToolNames = null,
            CopilotAgentEnvironmentContext? environmentContext = null,
            CopilotToolExecutionHookRegistrySnapshot? hookSurfaceSnapshot = null,
            bool requireEnvironmentContextMatch = false,
            IReadOnlyList<CopilotProjectInstructionDocument>? projectInstructions = null,
            string? configuredDeveloperInstructions = null)
        {
            ArgumentNullException.ThrowIfNull(capabilitySnapshot);
            if (profile == null || !IsStructurallyValid())
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.Invalid, capabilitySnapshot);
            if (!string.Equals(ProfileKey, CreateProfileKey(profile), StringComparison.Ordinal))
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ProfileChanged, capabilitySnapshot);
            if (LatestRunHasUncertainToolOutcome())
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome, capabilitySnapshot);
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

            if (environmentContext != null)
            {
                if (!environmentContext.IsStructurallyValid()
                    || EnvironmentVersion != CurrentEnvironmentVersion
                    || string.IsNullOrWhiteSpace(EnvironmentFingerprint))
                {
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing, capabilitySnapshot);
                }
                if (!string.Equals(EnvironmentFingerprint, environmentContext.Fingerprint, StringComparison.OrdinalIgnoreCase))
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift, capabilitySnapshot);
            }
            else if (requireEnvironmentContextMatch
                && (EnvironmentVersion != 0 || !string.IsNullOrEmpty(EnvironmentFingerprint)))
            {
                return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift, capabilitySnapshot);
            }

            if (hookSurfaceSnapshot != null)
            {
                if (HookSurfaceVersion != CurrentHookSurfaceVersion
                    || !IsSha256(HookSurfaceFingerprint)
                    || !hookSurfaceSnapshot.IsStructurallyValid())
                {
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.HookSurfaceSnapshotMissing, capabilitySnapshot);
                }
                if (!string.Equals(HookSurfaceFingerprint, hookSurfaceSnapshot.Fingerprint, StringComparison.OrdinalIgnoreCase))
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift, capabilitySnapshot);
            }

            if (projectInstructions != null)
            {
                if (!TryCreateProjectInstructionSurfaceFingerprint(
                        projectInstructions,
                        configuredDeveloperInstructions,
                        out var currentProjectInstructionFingerprint)
                    || ProjectInstructionSurfaceVersion != CurrentProjectInstructionSurfaceVersion
                    || !IsSha256(ProjectInstructionSurfaceFingerprint))
                {
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionSnapshotMissing, capabilitySnapshot);
                }
                if (!string.Equals(ProjectInstructionSurfaceFingerprint, currentProjectInstructionFingerprint, StringComparison.OrdinalIgnoreCase))
                    return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.ProjectInstructionDrift, capabilitySnapshot);
            }

            return CreateCompatibility(CopilotAgentCheckpointCompatibilityKind.Compatible, capabilitySnapshot);
        }

        private bool LatestRunHasUncertainToolOutcome()
        {
            var latestRun = TaskEventJournal.Events.LastOrDefault(item =>
                item.Type == CopilotAgentTaskEventType.RunStarted);
            if (latestRun == null)
                return false;

            var runEvents = TaskEventJournal.Events
                .Where(item => string.Equals(item.RunId, latestRun.RunId, StringComparison.Ordinal))
                .ToArray();
            if (runEvents.Any(item => item.Type == CopilotAgentTaskEventType.ToolCompleted
                && string.Equals(
                    CopilotToolFailureCode.Normalize(item.FailureCode),
                    CopilotToolFailureCode.OutcomeUnknown,
                    StringComparison.Ordinal)))
            {
                return true;
            }

            return runEvents
                .Where(item => item.Type == CopilotAgentTaskEventType.ToolStarted)
                .Any(start => !runEvents.Any(item =>
                    item.Sequence > start.Sequence
                    && ((item.Type == CopilotAgentTaskEventType.ToolCompleted
                            && string.Equals(item.SubjectId, start.SubjectId, StringComparison.Ordinal))
                        || ((item.Type is CopilotAgentTaskEventType.ApprovalRequested
                                or CopilotAgentTaskEventType.ApprovalDenied)
                            && (string.Equals(item.SubjectId, start.SubjectId, StringComparison.Ordinal)
                                || item.RelatedIds.Contains(start.SubjectId, StringComparer.Ordinal))))));
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
                || EnvironmentVersion is < 0 or > CurrentEnvironmentVersion
                || (EnvironmentVersion == 0 && !string.IsNullOrEmpty(EnvironmentFingerprint))
                || (EnvironmentVersion == CurrentEnvironmentVersion && !IsSha256(EnvironmentFingerprint))
                || HookSurfaceVersion is < 0 or > CurrentHookSurfaceVersion
                || (HookSurfaceVersion == 0 && !string.IsNullOrEmpty(HookSurfaceFingerprint))
                || (HookSurfaceVersion == CurrentHookSurfaceVersion && !IsSha256(HookSurfaceFingerprint))
                || ProjectInstructionSurfaceVersion is < 0 or > CurrentProjectInstructionSurfaceVersion
                || (ProjectInstructionSurfaceVersion == 0 && !string.IsNullOrEmpty(ProjectInstructionSurfaceFingerprint))
                || (ProjectInstructionSurfaceVersion == CurrentProjectInstructionSurfaceVersion && !IsSha256(ProjectInstructionSurfaceFingerprint))
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
                || (EvidenceArtifacts?.Any(artifact => artifact?.IsStructurallyValid() != true) ?? false)
                || ConversationMemory == null
                || ConversationMemory.Count > MaxConversationMemoryMessages
                || ConversationMemory.Sum(message => message.Content?.Length ?? 0) > MaxConversationMemoryCharacters
                || ConversationMemory.Any(message => message.Role is not ("user" or "assistant")
                    || string.IsNullOrWhiteSpace(message.Content)
                    || !string.Equals(message.Content, message.Content.Trim(), StringComparison.Ordinal)
                    || message.Content.Length > MaxConversationMemoryContentLength
                    || message.Content.Contains('\0')
                    || (message.IsSteering && message.Role != "user"))
                || TaskIntentText == null
                || TaskIntentText.Length > MaxTaskIntentTextLength
                || TaskIntentText.Contains('\0')
                || (TaskIntentText.Length > 0 && !string.Equals(TaskIntentText, TaskIntentText.Trim(), StringComparison.Ordinal))
                || TaskEventJournal?.IsStructurallyValid() != true)
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

        internal static bool TryCreateSnapshot(
            CopilotAgentSessionCheckpoint? source,
            out CopilotAgentSessionCheckpoint snapshot)
        {
            snapshot = null!;
            if (source == null)
                return false;

            if (source._isDetachedSnapshot)
            {
                if (!source.IsStructurallyValid())
                    return false;
                snapshot = source;
                return true;
            }

            try
            {
                if (!CopilotAgentTaskEventJournal.TryCreateSnapshot(
                        source.TaskEventJournal,
                        out var taskEventJournal))
                {
                    return false;
                }

                var candidate = new CopilotAgentSessionCheckpoint(source)
                {
                    ProfileKey = source.ProfileKey,
                    CapabilityCatalogRevision = source.CapabilityCatalogRevision,
                    Capabilities = Array.AsReadOnly(source.Capabilities
                        .Take(MaxCheckpointCapabilities + 1)
                        .Select(capability => new CopilotAgentCheckpointCapability
                        {
                            Id = capability.Id,
                            Revision = capability.Revision,
                            Fingerprint = capability.Fingerprint,
                        })
                        .ToArray()),
                    ToolSurfaceVersion = source.ToolSurfaceVersion,
                    AvailableToolNames = Array.AsReadOnly(source.AvailableToolNames
                        .Take(MaxAvailableToolNames + 1)
                        .ToArray()),
                    EnvironmentVersion = source.EnvironmentVersion,
                    EnvironmentFingerprint = source.EnvironmentFingerprint,
                    HookSurfaceVersion = source.HookSurfaceVersion,
                    HookSurfaceFingerprint = source.HookSurfaceFingerprint,
                    ProjectInstructionSurfaceVersion = source.ProjectInstructionSurfaceVersion,
                    ProjectInstructionSurfaceFingerprint = source.ProjectInstructionSurfaceFingerprint,
                    EvidenceArtifacts = Array.AsReadOnly(source.EvidenceArtifacts
                        .Take(CopilotAgentEvidenceArtifact.MaxArtifacts + 1)
                        .ToArray()),
                    ConversationMemory = Array.AsReadOnly(source.ConversationMemory
                        .Take(MaxConversationMemoryMessages + 1)
                        .ToArray()),
                    TaskIntentText = source.TaskIntentText,
                    TaskEventJournal = taskEventJournal,
                    UpdatedAtUtc = source.UpdatedAtUtc,
                };
                if (!candidate.IsStructurallyValid())
                    return false;

                candidate._isDetachedSnapshot = true;
                snapshot = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool AreEquivalent(
            CopilotAgentSessionCheckpoint? left,
            CopilotAgentSessionCheckpoint? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left?.IsStructurallyValid() != true || right?.IsStructurallyValid() != true)
                return false;

            return string.Equals(left.ProfileKey, right.ProfileKey, StringComparison.Ordinal)
                && string.Equals(left.SerializedSessionJson, right.SerializedSessionJson, StringComparison.Ordinal)
                && left.CapabilityCatalogRevision == right.CapabilityCatalogRevision
                && SequenceEqual(left.Capabilities, right.Capabilities, AreCapabilitiesEquivalent)
                && left.ToolSurfaceVersion == right.ToolSurfaceVersion
                && left.AvailableToolNames.SequenceEqual(right.AvailableToolNames, StringComparer.Ordinal)
                && left.EnvironmentVersion == right.EnvironmentVersion
                && string.Equals(left.EnvironmentFingerprint, right.EnvironmentFingerprint, StringComparison.Ordinal)
                && left.HookSurfaceVersion == right.HookSurfaceVersion
                && string.Equals(left.HookSurfaceFingerprint, right.HookSurfaceFingerprint, StringComparison.Ordinal)
                && left.ProjectInstructionSurfaceVersion == right.ProjectInstructionSurfaceVersion
                && string.Equals(
                    left.ProjectInstructionSurfaceFingerprint,
                    right.ProjectInstructionSurfaceFingerprint,
                    StringComparison.Ordinal)
                && SequenceEqual(left.EvidenceArtifacts, right.EvidenceArtifacts, AreEvidenceArtifactsEquivalent)
                && left.ConversationMemory.SequenceEqual(right.ConversationMemory)
                && string.Equals(left.TaskIntentText, right.TaskIntentText, StringComparison.Ordinal)
                && CopilotAgentTaskEventJournal.AreEquivalent(left.TaskEventJournal, right.TaskEventJournal)
                && left.UpdatedAtUtc == right.UpdatedAtUtc;
        }

        private static bool AreCapabilitiesEquivalent(
            CopilotAgentCheckpointCapability left,
            CopilotAgentCheckpointCapability right)
        {
            return string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                && left.Revision == right.Revision
                && string.Equals(left.Fingerprint, right.Fingerprint, StringComparison.Ordinal);
        }

        private static bool AreEvidenceArtifactsEquivalent(
            CopilotAgentEvidenceArtifact left,
            CopilotAgentEvidenceArtifact right)
        {
            return string.Equals(left.Id, right.Id, StringComparison.Ordinal)
                && string.Equals(left.CapabilityId, right.CapabilityId, StringComparison.Ordinal)
                && string.Equals(left.CapabilityFingerprint, right.CapabilityFingerprint, StringComparison.Ordinal)
                && string.Equals(left.ToolName, right.ToolName, StringComparison.Ordinal)
                && string.Equals(left.SourceCallKey, right.SourceCallKey, StringComparison.Ordinal)
                && string.Equals(left.ResourceKey, right.ResourceKey, StringComparison.Ordinal)
                && string.Equals(left.Summary, right.Summary, StringComparison.Ordinal)
                && string.Equals(left.ContentExcerpt, right.ContentExcerpt, StringComparison.Ordinal)
                && string.Equals(left.ContentFingerprint, right.ContentFingerprint, StringComparison.Ordinal)
                && left.CapturedAtUtc == right.CapturedAtUtc;
        }

        private static bool SequenceEqual<T>(
            IReadOnlyList<T> left,
            IReadOnlyList<T> right,
            Func<T, T, bool> comparer)
        {
            return left.Count == right.Count
                && left.Zip(right, comparer).All(equivalent => equivalent);
        }

        public static CopilotAgentSessionCheckpoint? Create(
            CopilotProfileConfig profile,
            string serializedSessionJson,
            CopilotCapabilityCatalogSnapshot? capabilitySnapshot = null,
            IReadOnlyList<CopilotAgentEvidenceArtifact>? evidenceArtifacts = null,
            CopilotAgentTaskEventJournalSnapshot? taskEventJournal = null,
            IReadOnlyCollection<string>? availableToolNames = null,
            IReadOnlyList<CopilotRequestMessage>? conversationMemory = null,
            CopilotAgentEnvironmentContext? environmentContext = null,
            string? taskIntentText = null,
            CopilotToolExecutionHookRegistrySnapshot? hookSurfaceSnapshot = null,
            IReadOnlyList<CopilotProjectInstructionDocument>? projectInstructions = null,
            string? configuredDeveloperInstructions = null)
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
            if (!CopilotAgentTaskEventJournal.TryCreateSnapshot(
                    taskEventJournal,
                    out var persistedTaskEventJournal))
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
            var persistedTaskIntentText = NormalizeTaskIntentText(taskIntentText);
            if (environmentContext?.IsStructurallyValid() == false)
                return null;
            if (hookSurfaceSnapshot?.IsStructurallyValid() == false)
                return null;
            var projectInstructionSurfaceVersion = 0;
            var projectInstructionSurfaceFingerprint = string.Empty;
            if (projectInstructions != null)
            {
                if (!TryCreateProjectInstructionSurfaceFingerprint(
                        projectInstructions,
                        configuredDeveloperInstructions,
                        out projectInstructionSurfaceFingerprint))
                    return null;
                projectInstructionSurfaceVersion = CurrentProjectInstructionSurfaceVersion;
            }

            var checkpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = CreateProfileKey(profile),
                SerializedSessionJson = json,
                CapabilityCatalogRevision = capabilitySnapshot.Revision,
                Capabilities = Array.AsReadOnly(capabilitySnapshot.Capabilities
                    .Select(capability => new CopilotAgentCheckpointCapability
                    {
                        Id = capability.Id,
                        Revision = capability.Revision,
                        Fingerprint = capability.Fingerprint,
                    })
                    .ToArray()),
                ToolSurfaceVersion = availableToolNames == null ? 0 : CurrentToolSurfaceVersion,
                AvailableToolNames = Array.AsReadOnly(persistedToolNames),
                EnvironmentVersion = environmentContext == null ? 0 : CurrentEnvironmentVersion,
                EnvironmentFingerprint = environmentContext?.Fingerprint ?? string.Empty,
                HookSurfaceVersion = hookSurfaceSnapshot == null ? 0 : CurrentHookSurfaceVersion,
                HookSurfaceFingerprint = hookSurfaceSnapshot?.Fingerprint ?? string.Empty,
                ProjectInstructionSurfaceVersion = projectInstructionSurfaceVersion,
                ProjectInstructionSurfaceFingerprint = projectInstructionSurfaceFingerprint,
                EvidenceArtifacts = Array.AsReadOnly(persistedEvidence),
                ConversationMemory = Array.AsReadOnly(persistedConversationMemory),
                TaskIntentText = persistedTaskIntentText,
                TaskEventJournal = persistedTaskEventJournal,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            if (!checkpoint.IsStructurallyValid())
                return null;
            checkpoint._isDetachedSnapshot = true;
            return checkpoint;
        }

        internal CopilotAgentSessionCheckpoint? CopyWithTaskEventJournal(CopilotAgentTaskEventJournalSnapshot taskEventJournal)
        {
            return CopyWithOutcome(taskEventJournal, ConversationMemory);
        }

        internal CopilotAgentSessionCheckpoint? CopyWithTaskEventJournalForNormalization(
            CopilotAgentTaskEventJournalSnapshot taskEventJournal)
        {
            return CopyWithOutcomeCore(taskEventJournal, ConversationMemory, UpdatedAtUtc);
        }

        internal CopilotAgentSessionCheckpoint? CopyWithOutcome(
            CopilotAgentTaskEventJournalSnapshot taskEventJournal,
            IReadOnlyList<CopilotRequestMessage> conversationMemory)
        {
            return CopyWithOutcomeCore(
                taskEventJournal,
                conversationMemory,
                DateTimeOffset.UtcNow);
        }

        private CopilotAgentSessionCheckpoint? CopyWithOutcomeCore(
            CopilotAgentTaskEventJournalSnapshot taskEventJournal,
            IReadOnlyList<CopilotRequestMessage> conversationMemory,
            DateTimeOffset updatedAtUtc)
        {
            ArgumentNullException.ThrowIfNull(taskEventJournal);
            ArgumentNullException.ThrowIfNull(conversationMemory);
            if (!CopilotAgentTaskEventJournal.TryCreateSnapshot(
                    taskEventJournal,
                    out var taskEventJournalSnapshot))
            {
                return null;
            }
            var checkpoint = new CopilotAgentSessionCheckpoint(this)
            {
                ProfileKey = ProfileKey,
                CapabilityCatalogRevision = CapabilityCatalogRevision,
                Capabilities = Array.AsReadOnly(
                    (Capabilities ?? Array.Empty<CopilotAgentCheckpointCapability>()).ToArray()),
                ToolSurfaceVersion = ToolSurfaceVersion,
                AvailableToolNames = Array.AsReadOnly(
                    (AvailableToolNames ?? Array.Empty<string>()).ToArray()),
                EnvironmentVersion = EnvironmentVersion,
                EnvironmentFingerprint = EnvironmentFingerprint,
                HookSurfaceVersion = HookSurfaceVersion,
                HookSurfaceFingerprint = HookSurfaceFingerprint,
                ProjectInstructionSurfaceVersion = ProjectInstructionSurfaceVersion,
                ProjectInstructionSurfaceFingerprint = ProjectInstructionSurfaceFingerprint,
                EvidenceArtifacts = Array.AsReadOnly(
                    (EvidenceArtifacts ?? Array.Empty<CopilotAgentEvidenceArtifact>()).ToArray()),
                ConversationMemory = Array.AsReadOnly(conversationMemory.ToArray()),
                TaskIntentText = TaskIntentText,
                TaskEventJournal = taskEventJournalSnapshot,
                UpdatedAtUtc = updatedAtUtc,
            };
            if (!checkpoint.IsStructurallyValid())
                return null;
            checkpoint._isDetachedSnapshot = true;
            return checkpoint;
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

        private static bool TryCreateProjectInstructionSurfaceFingerprint(
            IReadOnlyList<CopilotProjectInstructionDocument> projectInstructions,
            string? configuredDeveloperInstructions,
            out string fingerprint)
        {
            fingerprint = string.Empty;
            if (projectInstructions.Count > CopilotAgentProjectInstructions.MaxDocuments
                || projectInstructions.Any(document => document?.IsStructurallyValid() != true))
            {
                return false;
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Span<byte> version = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(version, CurrentProjectInstructionSurfaceVersion);
            hash.AppendData(version);
            var effectiveDeveloperInstructions = (configuredDeveloperInstructions ?? string.Empty).Trim();
            if (effectiveDeveloperInstructions.Length > CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                || effectiveDeveloperInstructions.Contains('\0'))
            {
                return false;
            }
            AppendFingerprintValue(hash, effectiveDeveloperInstructions);
            foreach (var document in projectInstructions)
            {
                AppendFingerprintValue(hash, document.Path);
                AppendFingerprintValue(hash, document.Content);
                hash.AppendData([document.IsTruncated ? (byte)1 : (byte)0]);
            }

            fingerprint = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            return true;
        }

        private static void AppendFingerprintValue(IncrementalHash hash, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
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
            var transportVersion = CopilotOpenAiRequestPolicy
                .GetAgentSessionTransportVersion(profile);
            if (transportVersion.Length > 0)
                value += "|" + transportVersion;
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        }

        private static bool IsSha256(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

        private static string NormalizeTaskIntentText(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (normalized.Length <= MaxTaskIntentTextLength)
                return normalized;

            const string suffix = "\n...<task intent truncated>";
            return normalized[..(MaxTaskIntentTextLength - suffix.Length)] + suffix;
        }
    }
}
