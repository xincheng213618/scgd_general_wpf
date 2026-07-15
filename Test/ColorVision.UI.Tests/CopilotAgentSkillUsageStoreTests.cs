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
        var database = Assert.Single(snapshot.Entries, entry => entry.Name == "database-operations");
        Assert.Equal(1, database.SelectedRuns);
        Assert.Equal(0, database.LoadedRuns);
    }

    [Fact]
    public void Snapshot_MakesOnlyRepeatedlySelectedNeverLoadedSkillsExplicitOnly()
    {
        var store = new CopilotAgentSkillUsageStore(_tempRoot);
        var timestamp = new DateTimeOffset(2026, 7, 15, 1, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < CopilotAgentSkillUsageStore.LowUseMinimumSelectedRuns; index++)
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

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
