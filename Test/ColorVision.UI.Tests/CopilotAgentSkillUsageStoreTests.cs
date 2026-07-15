#pragma warning disable CA1707
using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSkillUsageStoreTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "ColorVision-SkillUsage-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void RecordRun_PersistsSelectedAndLoadedSkillCounts()
    {
        var store = new CopilotAgentSkillUsageStore(_tempRoot);
        var firstTimestamp = new DateTimeOffset(2026, 7, 15, 1, 0, 0, TimeSpan.Zero);

        store.RecordRun(["database-operations", "flow-diagnostics"], ["flow-diagnostics", "not-selected"], firstTimestamp);
        store.RecordRun(["flow-diagnostics"], [], firstTimestamp.AddMinutes(1));

        var snapshot = new CopilotAgentSkillUsageStore(_tempRoot).GetSnapshot();
        Assert.Equal(2, snapshot.RecordedRuns);
        Assert.Equal(2, snapshot.Entries.Count);
        var flow = Assert.Single(snapshot.Entries, entry => entry.Name == "flow-diagnostics");
        Assert.Equal(2, flow.SelectedRuns);
        Assert.Equal(1, flow.LoadedRuns);
        Assert.Equal(0.5, flow.LoadRate);
        Assert.Equal(1, flow.ConsecutiveSelectedWithoutLoad);
        var database = Assert.Single(snapshot.Entries, entry => entry.Name == "database-operations");
        Assert.Equal(1, database.SelectedRuns);
        Assert.Equal(0, database.LoadedRuns);
        Assert.Equal(1, database.ConsecutiveSelectedWithoutLoad);
    }

    [Fact]
    public void Snapshot_DemotesConsecutiveMissesAndARealLoadRestoresImplicitEligibility()
    {
        var store = new CopilotAgentSkillUsageStore(_tempRoot);
        var timestamp = new DateTimeOffset(2026, 7, 15, 1, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold; index++)
        {
            store.RecordRun(
                ["never-loaded", "sometimes-loaded"],
                index == 0 ? ["sometimes-loaded"] : [],
                timestamp.AddMinutes(index));
        }

        var snapshot = store.GetSnapshot();
        var candidate = Assert.Single(snapshot.HistoricalExplicitOnlySkills);
        Assert.Equal("never-loaded", candidate.Name);
        Assert.DoesNotContain(snapshot.HistoricalExplicitOnlySkills, entry => entry.Name == "sometimes-loaded");

        snapshot = store.RecordRun(["sometimes-loaded"], [], timestamp.AddHours(1));
        Assert.Contains(snapshot.HistoricalExplicitOnlySkills, entry => entry.Name == "sometimes-loaded");

        snapshot = store.RecordRun(["sometimes-loaded"], ["sometimes-loaded"], timestamp.AddHours(2));
        var restored = Assert.Single(snapshot.Entries, entry => entry.Name == "sometimes-loaded");
        Assert.Equal(0, restored.ConsecutiveSelectedWithoutLoad);
        Assert.DoesNotContain(snapshot.HistoricalExplicitOnlySkills, entry => entry.Name == "sometimes-loaded");
    }

    [Fact]
    public void Snapshot_MigratesNeverLoadedVersionOneHistoryIntoConsecutiveMisses()
    {
        Directory.CreateDirectory(_tempRoot);
        File.WriteAllText(Path.Combine(_tempRoot, "skill-usage.json"), """
            {
              "SchemaVersion": 1,
              "RecordedRuns": 20,
              "Entries": [
                {
                  "Name": "legacy-unused",
                  "SelectedRuns": 20,
                  "LoadedRuns": 0,
                  "FirstSelectedAtUtc": "2026-07-15T01:00:00+00:00",
                  "LastSelectedAtUtc": "2026-07-15T02:00:00+00:00"
                },
                {
                  "Name": "legacy-used",
                  "SelectedRuns": 20,
                  "LoadedRuns": 1,
                  "FirstSelectedAtUtc": "2026-07-15T01:00:00+00:00",
                  "LastSelectedAtUtc": "2026-07-15T02:00:00+00:00"
                }
              ]
            }
            """);

        var snapshot = new CopilotAgentSkillUsageStore(_tempRoot).GetSnapshot();

        Assert.Equal(20, Assert.Single(snapshot.Entries, entry => entry.Name == "legacy-unused").ConsecutiveSelectedWithoutLoad);
        Assert.Equal(0, Assert.Single(snapshot.Entries, entry => entry.Name == "legacy-used").ConsecutiveSelectedWithoutLoad);
        Assert.Contains(snapshot.HistoricalExplicitOnlySkills, entry => entry.Name == "legacy-unused");
        Assert.DoesNotContain(snapshot.HistoricalExplicitOnlySkills, entry => entry.Name == "legacy-used");
    }

    [Fact]
    public void Snapshot_RecoversFromCorruptOrOversizedState()
    {
        Directory.CreateDirectory(_tempRoot);
        var store = new CopilotAgentSkillUsageStore(_tempRoot);
        File.WriteAllText(store.StateFilePath, "{not-json");
        Assert.Empty(store.GetSnapshot().Entries);

        var oversizedDirectory = Path.Combine(_tempRoot, "oversized");
        Directory.CreateDirectory(oversizedDirectory);
        var oversizedStore = new CopilotAgentSkillUsageStore(oversizedDirectory);
        File.WriteAllText(oversizedStore.StateFilePath, new string('x', 1_048_577));
        Assert.Empty(oversizedStore.GetSnapshot().Entries);
    }

    [Fact]
    public void RecordRun_BoundsTrackedSkillEntries()
    {
        var store = new CopilotAgentSkillUsageStore(_tempRoot);
        var selected = Enumerable.Range(0, 140).Select(index => $"skill-{index:000}");

        var snapshot = store.RecordRun(selected, [], DateTimeOffset.UtcNow);

        Assert.Equal(128, snapshot.Entries.Count);
    }

    [Fact]
    public void SkillDiagnostics_ExplainsUsageAndReversibleExplicitOnlyState()
    {
        var timestamp = new DateTimeOffset(2026, 7, 15, 1, 2, 3, TimeSpan.Zero);
        var entry = new CopilotAgentSkillUsageEntry
        {
            Name = "legacy-workflow",
            SelectedRuns = 24,
            LoadedRuns = 1,
            ConsecutiveSelectedWithoutLoad = CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold,
            LastSelectedAtUtc = timestamp,
        };
        var snapshot = new CopilotAgentSkillUsageSnapshot
        {
            RecordedRuns = 24,
            Entries = [entry],
            HistoricalExplicitOnlySkills = [entry],
        };

        var report = CopilotAgentSkillDiagnostics.FormatReport(snapshot, 8_000, new Dictionary<string, CopilotAgentSkillOverrideState>
        {
            ["legacy-workflow"] = CopilotAgentSkillOverrideState.UserInvocableOnly,
            ["verbose-reference"] = CopilotAgentSkillOverrideState.NameOnly,
        });

        Assert.Contains("1 tracked across 24 run(s); 1 loaded; 1 low-use explicit-only", report, StringComparison.Ordinal);
        Assert.Contains("Metadata budget: 8,000 characters", report, StringComparison.Ordinal);
        Assert.Contains("loaded 1/24", report, StringComparison.Ordinal);
        Assert.Contains("consecutive misses 20/20", report, StringComparison.Ordinal);
        Assert.Contains("remain installed", report, StringComparison.Ordinal);
        Assert.Contains("direct load restores", report, StringComparison.Ordinal);
        Assert.Contains("legacy-workflow=explicit-only", report, StringComparison.Ordinal);
        Assert.Contains("verbose-reference=name-only", report, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
