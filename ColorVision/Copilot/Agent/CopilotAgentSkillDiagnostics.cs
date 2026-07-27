using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public static class CopilotAgentSkillDiagnostics
    {
        public static string FormatSummary(CopilotAgentSkillUsageSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.RecordedRuns == 0)
                return "尚无 Agent Skill 使用记录。";

            var loadedCount = snapshot.Entries.Count(entry => entry.LoadedRuns > 0);
            return $"共跟踪 {snapshot.Entries.Count} 个 Skill、{snapshot.RecordedRuns} 次运行；{loadedCount} 个曾被加载；{snapshot.HistoricalExplicitOnlySkills.Count} 个低使用率 Skill 仅限显式调用。";
        }

        public static string FormatEntries(CopilotAgentSkillUsageSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Entries.Count == 0)
                return "启用 Agent Skill 并运行 Copilot 后，将在此收集有界的本地使用证据。";

            return string.Join(Environment.NewLine, snapshot.Entries.Select(FormatEntry));
        }

        public static string FormatReport(
            CopilotAgentSkillUsageSnapshot snapshot,
            int metadataCharacterBudget,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder();
            builder.AppendLine("/skills · Agent Skill 使用快照");
            builder.AppendLine(FormatSummary(snapshot));
            builder.Append("元数据预算：")
                .Append(metadataCharacterBudget.ToString("N0"))
                .Append(" 字符（上下文 ")
                .Append(CopilotAgentSkills.SkillMetadataContextPercent)
                .Append("%，硬上限 ")
                .Append(CopilotAgentSkills.MaxAdvertisedSkillCharacters.ToString("N0"))
                .AppendLine("）。")
                .AppendLine("低使用率 Skill 不会被删除；连续多次选中但未加载后仅限显式调用。使用 $skill-name 或 /skill-name 点名并实际加载后，可恢复隐式匹配资格。")
                .Append("手动覆盖：")
                .AppendLine(FormatOverrides(overrides))
                .AppendLine()
                .Append(FormatEntries(snapshot));
            return builder.ToString();
        }

        public static string FormatOverrides(IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides)
        {
            if (overrides == null || overrides.Count == 0)
                return "无（所有 Skill 均为自动）。";

            var visibleOverrides = overrides
                .Where(item => item.Value != CopilotAgentSkillOverrideState.Auto)
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}={FormatState(item.Value)}")
                .ToArray();
            return visibleOverrides.Length == 0
                ? "无（所有 Skill 均为自动）。"
                : string.Join(", ", visibleOverrides);
        }

        private static string FormatState(CopilotAgentSkillOverrideState state)
        {
            return state switch
            {
                CopilotAgentSkillOverrideState.NameOnly => "仅名称",
                CopilotAgentSkillOverrideState.UserInvocableOnly => "仅显式调用",
                CopilotAgentSkillOverrideState.Off => "关闭",
                _ => "自动",
            };
        }

        private static string FormatEntry(CopilotAgentSkillUsageEntry entry)
        {
            var builder = new StringBuilder();
            builder.Append(entry.Name)
                .Append("：已加载 ")
                .Append(entry.LoadedRuns)
                .Append('/')
                .Append(entry.SelectedRuns)
                .Append(" 次选中运行（")
                .Append(entry.LoadRate.ToString("P0"))
                .Append("）；连续未加载 ")
                .Append(entry.ConsecutiveSelectedWithoutLoad)
                .Append('/')
                .Append(CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold)
                .Append("；最近选中 ")
                .Append(entry.LastSelectedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (entry.ConsecutiveSelectedWithoutLoad >= CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold)
                builder.Append(" · 当前仅限显式调用，点名并加载后可恢复");
            return builder.ToString();
        }
    }
}
