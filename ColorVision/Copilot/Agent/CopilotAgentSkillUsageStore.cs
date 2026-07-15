using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentSkillUsageEntry
    {
        public string Name { get; set; } = string.Empty;

        public int SelectedRuns { get; set; }

        public int LoadedRuns { get; set; }

        public DateTimeOffset FirstSelectedAtUtc { get; set; }

        public DateTimeOffset LastSelectedAtUtc { get; set; }

        public DateTimeOffset? LastLoadedAtUtc { get; set; }

        public double LoadRate => SelectedRuns <= 0 ? 0 : (double)LoadedRuns / SelectedRuns;
    }

    public sealed class CopilotAgentSkillUsageSnapshot
    {
        public long RecordedRuns { get; init; }

        public DateTimeOffset? UpdatedAtUtc { get; init; }

        public IReadOnlyList<CopilotAgentSkillUsageEntry> Entries { get; init; } = Array.Empty<CopilotAgentSkillUsageEntry>();

        public IReadOnlyList<CopilotAgentSkillUsageEntry> HistoricalExplicitOnlySkills { get; init; } = Array.Empty<CopilotAgentSkillUsageEntry>();
    }

    public sealed class CopilotAgentSkillUsageStore
    {
        public const int LowUseMinimumSelectedRuns = 20;

        private const int CurrentSchemaVersion = 1;
        private const int MaxSkillNameLength = 64;
        private const int MaxEntries = 128;
        private const int MaxFileBytes = 1_048_576;
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
        };
        private static readonly Lazy<CopilotAgentSkillUsageStore> SharedStore = new(() => new CopilotAgentSkillUsageStore(
            Path.Combine(Environments.DirLocalAppData, "Copilot", "State")));
        private readonly object _sync = new();
        private UsageState? _state;

        public CopilotAgentSkillUsageStore(string stateDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(stateDirectoryPath))
                throw new ArgumentException("A state directory is required.", nameof(stateDirectoryPath));

            StateDirectoryPath = Path.GetFullPath(stateDirectoryPath);
            StateFilePath = Path.Combine(StateDirectoryPath, "skill-usage.json");
        }

        public static CopilotAgentSkillUsageStore Shared => SharedStore.Value;

        public string StateDirectoryPath { get; }

        public string StateFilePath { get; }

        public CopilotAgentSkillUsageSnapshot RecordRun(
            IEnumerable<string>? selectedSkillNames,
            IEnumerable<string>? loadedSkillNames,
            DateTimeOffset recordedAtUtc)
        {
            var selectedNames = NormalizeNames(selectedSkillNames);
            var loadedNames = NormalizeNames(loadedSkillNames)
                .Where(selectedNames.Contains)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var timestamp = recordedAtUtc.ToUniversalTime();
            lock (_sync)
            {
                var state = GetStateLocked();
                state.RecordedRuns = state.RecordedRuns == long.MaxValue ? long.MaxValue : Math.Max(0, state.RecordedRuns) + 1;
                state.UpdatedAtUtc = timestamp;
                foreach (var name in selectedNames)
                {
                    var entry = state.Entries.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (entry == null)
                    {
                        entry = new CopilotAgentSkillUsageEntry
                        {
                            Name = name,
                            FirstSelectedAtUtc = timestamp,
                        };
                        state.Entries.Add(entry);
                    }

                    entry.SelectedRuns = Increment(entry.SelectedRuns);
                    entry.LastSelectedAtUtc = timestamp;
                    if (loadedNames.Contains(name))
                    {
                        entry.LoadedRuns = Increment(entry.LoadedRuns);
                        entry.LastLoadedAtUtc = timestamp;
                    }
                }

                state.Entries = state.Entries
                    .OrderByDescending(entry => entry.LastSelectedAtUtc)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxEntries)
                    .ToList();
                SaveLocked(state);
                return CreateSnapshot(state);
            }
        }

        public CopilotAgentSkillUsageSnapshot GetSnapshot()
        {
            lock (_sync)
                return CreateSnapshot(GetStateLocked());
        }

        private UsageState GetStateLocked()
        {
            if (_state != null)
                return _state;

            _state = LoadState();
            return _state;
        }

        private UsageState LoadState()
        {
            try
            {
                var file = new FileInfo(StateFilePath);
                if (!file.Exists || file.Length <= 0 || file.Length > MaxFileBytes)
                    return new UsageState();

                var state = JsonSerializer.Deserialize<UsageState>(File.ReadAllText(StateFilePath), SerializerOptions) ?? new UsageState();
                state.SchemaVersion = CurrentSchemaVersion;
                state.RecordedRuns = Math.Max(0, state.RecordedRuns);
                state.Entries = NormalizeEntries(state.Entries);
                return state;
            }
            catch
            {
                return new UsageState();
            }
        }

        private void SaveLocked(UsageState state)
        {
            Directory.CreateDirectory(StateDirectoryPath);
            state.SchemaVersion = CurrentSchemaVersion;
            var tempFilePath = StateFilePath + ".tmp";
            try
            {
                var json = JsonSerializer.Serialize(state, SerializerOptions);
                File.WriteAllText(tempFilePath, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tempFilePath, StateFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
        }

        private static CopilotAgentSkillUsageSnapshot CreateSnapshot(UsageState state)
        {
            var entries = state.Entries
                .Select(CloneEntry)
                .OrderByDescending(entry => entry.LastSelectedAtUtc)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new CopilotAgentSkillUsageSnapshot
            {
                RecordedRuns = state.RecordedRuns,
                UpdatedAtUtc = state.UpdatedAtUtc == default ? null : state.UpdatedAtUtc,
                Entries = entries,
                HistoricalExplicitOnlySkills = entries
                    .Where(entry => entry.SelectedRuns >= LowUseMinimumSelectedRuns && entry.LoadedRuns == 0)
                    .OrderByDescending(entry => entry.SelectedRuns)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
            };
        }

        private static List<CopilotAgentSkillUsageEntry> NormalizeEntries(IEnumerable<CopilotAgentSkillUsageEntry>? entries)
        {
            return (entries ?? Array.Empty<CopilotAgentSkillUsageEntry>())
                .Where(entry => entry != null)
                .Select(entry => new { Entry = entry, Name = NormalizeName(entry.Name) })
                .Where(item => item.Name != null)
                .GroupBy(item => item.Name!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(item => item.Entry.LastSelectedAtUtc).First().Entry)
                .Select(CloneEntry)
                .Take(MaxEntries)
                .ToList();
        }

        private static HashSet<string> NormalizeNames(IEnumerable<string>? names)
        {
            return (names ?? Array.Empty<string>())
                .Select(NormalizeName)
                .Where(name => name != null)
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static string? NormalizeName(string? value)
        {
            var name = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (name.Length == 0 || name.Length > MaxSkillNameLength)
                return null;
            return name.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '-') ? name : null;
        }

        private static CopilotAgentSkillUsageEntry CloneEntry(CopilotAgentSkillUsageEntry entry)
        {
            return new CopilotAgentSkillUsageEntry
            {
                Name = NormalizeName(entry.Name) ?? string.Empty,
                SelectedRuns = Math.Max(0, entry.SelectedRuns),
                LoadedRuns = Math.Clamp(entry.LoadedRuns, 0, Math.Max(0, entry.SelectedRuns)),
                FirstSelectedAtUtc = entry.FirstSelectedAtUtc,
                LastSelectedAtUtc = entry.LastSelectedAtUtc,
                LastLoadedAtUtc = entry.LastLoadedAtUtc,
            };
        }

        private static int Increment(int value) => value == int.MaxValue ? int.MaxValue : Math.Max(0, value) + 1;

        private sealed class UsageState
        {
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;

            public long RecordedRuns { get; set; }

            public DateTimeOffset UpdatedAtUtc { get; set; }

            public List<CopilotAgentSkillUsageEntry> Entries { get; set; } = [];
        }
    }
}
