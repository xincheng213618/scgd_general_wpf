using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentSkillDiagnosticsTests
{
    [Fact]
    public void ReportUsesChineseLabelsAndPreservesSkillNames()
    {
        var entry = new CopilotAgentSkillUsageEntry
        {
            Name = "review-code",
            SelectedRuns = 20,
            LoadedRuns = 5,
            ConsecutiveSelectedWithoutLoad = CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold,
            LastSelectedAtUtc = new DateTimeOffset(2026, 7, 26, 10, 0, 0, TimeSpan.Zero),
        };
        var snapshot = new CopilotAgentSkillUsageSnapshot
        {
            RecordedRuns = 20,
            Entries = [entry],
            HistoricalExplicitOnlySkills = [entry],
        };

        var report = CopilotAgentSkillDiagnostics.FormatReport(
            snapshot,
            metadataCharacterBudget: 4096,
            new Dictionary<string, CopilotAgentSkillOverrideState>
            {
                ["review-code"] = CopilotAgentSkillOverrideState.UserInvocableOnly,
            });

        Assert.Contains("共跟踪 1 个 Skill、20 次运行", report, StringComparison.Ordinal);
        Assert.Contains("元数据预算：", report, StringComparison.Ordinal);
        Assert.Contains("手动覆盖：review-code=仅显式调用", report, StringComparison.Ordinal);
        Assert.Contains("review-code：已加载 5/20 次选中运行", report, StringComparison.Ordinal);
        Assert.Contains("当前仅限显式调用，点名并加载后可恢复", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Metadata budget", report, StringComparison.Ordinal);
        Assert.DoesNotContain("selected run(s)", report, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySnapshotExplainsHowUsageEvidenceAppears()
    {
        var snapshot = new CopilotAgentSkillUsageSnapshot();

        Assert.Equal("尚无 Agent Skill 使用记录。", CopilotAgentSkillDiagnostics.FormatSummary(snapshot));
        Assert.Contains("有界的本地使用证据", CopilotAgentSkillDiagnostics.FormatEntries(snapshot), StringComparison.Ordinal);
    }
}
