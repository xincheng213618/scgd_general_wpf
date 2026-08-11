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
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides = null,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? availableSkills = null,
            bool catalogReloaded = false,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? pathOverrides = null,
            IReadOnlyList<CopilotMcpClientServerConfig>? externalMcpServers = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var builder = new StringBuilder();
            builder.AppendLine("/skills · Agent Skill 目录与使用快照");
            AppendCatalog(
                builder,
                availableSkills,
                overrides,
                pathOverrides,
                catalogReloaded,
                externalMcpServers);
            builder.AppendLine()
                .AppendLine("本地使用证据")
                .AppendLine(FormatSummary(snapshot));
            builder.Append("元数据预算：")
                .Append(metadataCharacterBudget.ToString("N0"))
                .Append(" 字符（上下文 ")
                .Append(CopilotAgentSkills.SkillMetadataContextPercent)
                .Append("%，硬上限 ")
                .Append(CopilotAgentSkills.MaxAdvertisedSkillCharacters.ToString("N0"))
                .AppendLine("）。")
                .AppendLine("低使用率 Skill 不会被删除；连续多次选中但未加载后仅限显式调用。使用 $skill-name 或 /skill-name 点名并实际加载后，可恢复隐式匹配资格。")
                .Append("手动覆盖：")
                .AppendLine(FormatOverrides(overrides, pathOverrides))
                .AppendLine()
                .Append(FormatEntries(snapshot));
            return builder.ToString();
        }

        private static void AppendCatalog(
            StringBuilder builder,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? availableSkills,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? pathOverrides,
            bool catalogReloaded,
            IReadOnlyList<CopilotMcpClientServerConfig>? externalMcpServers)
        {
            var items = availableSkills ?? Array.Empty<CopilotAgentSkillCatalogItem>();
            var enabledCount = items.Count(item => CopilotAgentSkillOverrideConfig.ResolveState(
                item.Name,
                item.SkillFilePath,
                overrides,
                pathOverrides) != CopilotAgentSkillOverrideState.Off);
            builder.Append("当前可调用：")
                .Append(enabledCount)
                .Append('/').Append(items.Count)
                .Append(" 个 Skill 路径；")
                .AppendLine(catalogReloaded
                    ? "已强制从磁盘重扫目录。"
                    : "本地 SKILL.md 与界面元数据变更会自动使目录缓存失效。")
                .AppendLine("正在运行的 Agent 保留启动时的 Skill 快照；新目录从下一次请求开始生效。");
            if (items.Count == 0)
            {
                builder.AppendLine("当前可信项目与内置目录中未发现有效 Skill。");
                return;
            }

            var orderedItems = items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            for (var index = 0; index < orderedItems.Length; index++)
            {
                var item = orderedItems[index];
                var state = CopilotAgentSkillOverrideConfig.ResolveState(
                    item.Name,
                    item.SkillFilePath,
                    overrides,
                    pathOverrides);
                builder.Append("- ")
                    .Append(index + 1)
                    .Append(". $")
                    .Append(item.Name)
                    .Append(FormatSource(item.SourceKind))
                    .Append(FormatDisplayName(item.DisplayName))
                    .Append(item.EffectiveDescription)
                    .Append(" [")
                    .Append(FormatState(state))
                    .AppendLine("]");
                builder.Append("  路径：").AppendLine(item.SkillFilePath);
                if (item.Dependencies.Count > 0)
                {
                    builder.Append("  依赖：")
                        .AppendLine(string.Join("；", item.Dependencies.Select(dependency =>
                            FormatDependency(dependency, externalMcpServers))));
                }
            }
        }

        private static string FormatDisplayName(string displayName) =>
            string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName + " · ";

        private static string FormatDependency(
            CopilotAgentSkillDependency dependency,
            IReadOnlyList<CopilotMcpClientServerConfig>? externalMcpServers)
        {
            var description = string.IsNullOrWhiteSpace(dependency.Description)
                ? string.Empty
                : $"（{dependency.Description}）";
            if (!string.Equals(dependency.Type, "mcp", StringComparison.OrdinalIgnoreCase))
                return $"{dependency.Type}:{dependency.Value}{description}";

            var transport = CopilotAgentSkillMcpDependencyPolicy.ResolveTransport(dependency);
            var status = CopilotAgentSkillMcpDependencyPolicy.Evaluate(dependency, externalMcpServers);
            var statusLabel = status switch
            {
                CopilotAgentSkillMcpDependencyStatus.Installed => "已配置",
                CopilotAgentSkillMcpDependencyStatus.ConfiguredDisabled => "已配置但已禁用",
                CopilotAgentSkillMcpDependencyStatus.Installable => "可安全配置",
                CopilotAgentSkillMcpDependencyStatus.MissingInstallMetadata => "缺少 URL",
                CopilotAgentSkillMcpDependencyStatus.UnsupportedTransport => "当前传输不支持",
                CopilotAgentSkillMcpDependencyStatus.InvalidConfiguration => "配置无效",
                _ => "未识别",
            };
            return $"{dependency.Type}:{dependency.Value}{description} [{transport} · {statusLabel}]";
        }

        private static string FormatSource(CopilotAgentSkillSourceKind sourceKind)
        {
            return sourceKind switch
            {
                CopilotAgentSkillSourceKind.User => " [用户] — ",
                CopilotAgentSkillSourceKind.BuiltIn => " [内置] — ",
                _ => " [项目] — ",
            };
        }

        public static string FormatOverrides(
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? pathOverrides = null)
        {
            if ((overrides == null || overrides.Count == 0)
                && (pathOverrides == null || pathOverrides.Count == 0))
                return "无（所有 Skill 均为自动）。";

            var visibleOverrides = (overrides ?? new Dictionary<string, CopilotAgentSkillOverrideState>())
                .Where(item => item.Value != CopilotAgentSkillOverrideState.Auto)
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}={FormatState(item.Value)}")
                .Concat((pathOverrides ?? new Dictionary<string, CopilotAgentSkillOverrideState>())
                    .Where(item => item.Value != CopilotAgentSkillOverrideState.Auto)
                    .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(item => $"{item.Key}={FormatState(item.Value)}"))
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
                CopilotAgentSkillOverrideState.On => "启用",
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
