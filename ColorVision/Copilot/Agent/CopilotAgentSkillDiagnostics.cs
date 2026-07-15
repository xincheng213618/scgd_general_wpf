using System;
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
                return "Agent Skills have no recorded runs yet.";

            var loadedCount = snapshot.Entries.Count(entry => entry.LoadedRuns > 0);
            return $"{snapshot.Entries.Count} tracked across {snapshot.RecordedRuns} run(s); {loadedCount} loaded; {snapshot.HistoricalExplicitOnlySkills.Count} low-use explicit-only.";
        }

        public static string FormatEntries(CopilotAgentSkillUsageSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Entries.Count == 0)
                return "Run Copilot with Agent Skills enabled to collect bounded usage evidence.";

            return string.Join(Environment.NewLine, snapshot.Entries.Select(FormatEntry));
        }

        public static string FormatReport(CopilotAgentSkillUsageSnapshot snapshot, int metadataCharacterBudget)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder();
            builder.AppendLine("/skills · Agent Skill 使用快照");
            builder.AppendLine(FormatSummary(snapshot));
            builder.Append("Metadata budget: ")
                .Append(metadataCharacterBudget.ToString("N0"))
                .Append(" characters (")
                .Append(CopilotAgentSkills.SkillMetadataContextPercent)
                .Append("% context, ")
                .Append(CopilotAgentSkills.MaxAdvertisedSkillCharacters.ToString("N0"))
                .AppendLine(" hard cap).")
                .AppendLine("Low-use skills remain installed and become explicit-only after consecutive misses; a direct load restores implicit eligibility.")
                .AppendLine()
                .Append(FormatEntries(snapshot));
            return builder.ToString();
        }

        private static string FormatEntry(CopilotAgentSkillUsageEntry entry)
        {
            var builder = new StringBuilder();
            builder.Append(entry.Name)
                .Append(": loaded ")
                .Append(entry.LoadedRuns)
                .Append('/')
                .Append(entry.SelectedRuns)
                .Append(" selected run(s) (")
                .Append(entry.LoadRate.ToString("P0"))
                .Append("); consecutive misses ")
                .Append(entry.ConsecutiveSelectedWithoutLoad)
                .Append('/')
                .Append(CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold)
                .Append("; last selected ")
                .Append(entry.LastSelectedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (entry.ConsecutiveSelectedWithoutLoad >= CopilotAgentSkillUsageStore.LowUseConsecutiveMissThreshold)
                builder.Append(" · explicit-only until directly requested and loaded");
            return builder.ToString();
        }
    }
}
