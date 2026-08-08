using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotProjectInstructionCommandAction
    {
        List,
        Open,
        Invalid,
    }

    internal sealed record CopilotProjectInstructionCommandRequest(
        CopilotProjectInstructionCommandAction Action,
        int Position);

    internal sealed record CopilotProjectInstructionSnapshot(
        string WorkspacePath,
        string ActiveDocumentPath,
        string GlobalInstructionRootPath,
        CopilotProjectInstructionDiscoveryOptions DiscoveryOptions,
        IReadOnlyList<CopilotProjectInstructionDocument> Documents);

    internal static class CopilotProjectInstructionDiagnostics
    {
        internal const string Usage =
            "用法：/memory [open N]。不带参数时预览基于当前目标、会被工作区型 Agent 请求加载的个人与项目指令；open N 在内置编辑器中打开第 N 个文件。";

        public static CopilotProjectInstructionCommandRequest ParseCommand(string? arguments)
        {
            var parts = (arguments ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0
                || (parts.Length == 1
                    && string.Equals(parts[0], "list", StringComparison.OrdinalIgnoreCase)))
            {
                return new CopilotProjectInstructionCommandRequest(
                    CopilotProjectInstructionCommandAction.List,
                    0);
            }

            if (parts.Length == 2
                && string.Equals(parts[0], "open", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var position)
                && position > 0)
            {
                return new CopilotProjectInstructionCommandRequest(
                    CopilotProjectInstructionCommandAction.Open,
                    position);
            }

            return new CopilotProjectInstructionCommandRequest(
                CopilotProjectInstructionCommandAction.Invalid,
                0);
        }

        public static IReadOnlyList<CopilotProjectInstructionDocument> GetEffectiveDocuments(
            IEnumerable<CopilotProjectInstructionDocument>? documents)
        {
            return (documents ?? Array.Empty<CopilotProjectInstructionDocument>())
                .Where(document => document?.IsStructurallyValid() == true)
                .Take(CopilotAgentProjectInstructions.MaxDocuments)
                .ToArray();
        }

        public static CopilotProjectInstructionDocument? FindByPosition(
            IEnumerable<CopilotProjectInstructionDocument>? documents,
            int position)
        {
            if (position <= 0)
                return null;

            return GetEffectiveDocuments(documents).ElementAtOrDefault(position - 1);
        }

        public static string Format(
            CopilotProjectInstructionSnapshot? snapshot,
            bool hasActiveAgentRun)
        {
            var documents = GetEffectiveDocuments(snapshot?.Documents);
            var builder = new StringBuilder()
                .Append("Copilot 个人与项目指令 · ")
                .AppendLine(documents.Count.ToString("N0", CultureInfo.CurrentCulture));
            if (documents.Count == 0)
            {
                builder.AppendLine()
                    .AppendLine("当前 Codex Home、受信项目根和目标文件没有发现会被工作区型 Agent 请求加载的指令。")
                    .AppendLine("使用 /init 可在项目根创建 AGENTS.md；现有文件不会被覆盖。");
                AppendDiscoveryOptions(builder, snapshot?.DiscoveryOptions, snapshot?.WorkspacePath);
                builder
                    .Append("这里展示的是个人与工作区指令，不是自动生成的跨会话记忆；/memory 不会写入文件。");
                return builder.ToString();
            }

            builder.AppendLine()
                .AppendLine("以下是基于当前 Codex Home、活动文档与文件附件的注入预览：个人指令在前，项目指令由宽到窄，后列的局部规则只在自身作用域内覆盖前列规则。")
                .AppendLine("只有需要本地工作区证据的 Agent 请求才注入这些文件；下一条提示词中的显式本地路径也可能改变路径规则匹配。");
            AppendTarget(builder, snapshot);
            foreach (var document in documents.Select((value, index) => (Document: value, Position: index + 1)))
            {
                builder.Append('#')
                    .Append(document.Position.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" · ")
                    .Append(Path.GetFileName(document.Document.Path))
                    .Append(" · ")
                    .Append(GetSourceLabel(document.Document.Path, snapshot?.GlobalInstructionRootPath))
                    .Append(" · ")
                    .Append(document.Document.Content.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符");
                if (document.Document.IsTruncated)
                    builder.Append(" · 已截断");
                builder.AppendLine()
                    .Append("  ")
                    .AppendLine(FormatPath(document.Document.Path, snapshot?.WorkspacePath));
            }

            builder.AppendLine()
                .AppendLine("选择规则：Codex Home 先选首个非空 AGENTS.override.md/AGENTS.md；项目同目录优先 AGENTS.override.md、AGENTS.md，再尝试配置备用名与 CLAUDE.md 兼容回退；.claude/rules 为附加规则，CLAUDE.local.md 为私有局部覆盖。")
                .AppendLine("使用 /memory open N 打开文件。报告不包含指令正文，也不会自动修改任何指令。");
            AppendDiscoveryOptions(builder, snapshot?.DiscoveryOptions, snapshot?.WorkspacePath);
            if (hasActiveAgentRun)
                builder.AppendLine("当前运行中的任务已固定请求启动时的指令快照；现在编辑只影响后续请求。");
            builder.Append("这里展示的是个人与工作区指令，不是自动生成的跨会话记忆；Codex Home 不会因此成为通用文件或写入权限根。");
            return builder.ToString();
        }

        private static void AppendDiscoveryOptions(
            StringBuilder builder,
            CopilotProjectInstructionDiscoveryOptions? options,
            string? workspacePath)
        {
            var effective = options ?? CopilotProjectInstructionDiscoveryConfig.CreateDefault();
            builder.Append("发现预算：")
                .Append(effective.MaximumBytes.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" UTF-8 字节 · ")
                .AppendLine(effective.UsesCodexConfig
                    ? effective.ConfigSourceLabel + " 请求快照"
                    : "ColorVision 默认");
            if (effective.FallbackFileNames.Count > 0)
            {
                builder.Append("配置备用名：")
                    .AppendLine(string.Join("、", effective.FallbackFileNames));
            }
            if (effective.ProjectTrustLabel.Length > 0)
            {
                builder.Append("项目配置信任：")
                    .AppendLine(effective.ProjectTrustLabel);
            }
            if (effective.HasDeveloperInstructionsOverride)
            {
                builder.Append("Codex developer_instructions：")
                    .Append(effective.DeveloperInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.DeveloperInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.DeveloperInstructionsSourceLabel)
                    .AppendLine(effective.DeveloperInstructions.Length == 0
                        ? " 请求快照；显式清空）"
                        : " 请求快照；独立开发者指令）");
            }
            if (effective.HasPersonalityOverride)
            {
                builder.Append("Codex personality：")
                    .Append(CopilotResponsePersonalitySelection.GetDisplayName(effective.ConfiguredPersonality))
                    .Append('（')
                    .Append(CopilotResponsePersonalitySelection.GetCommandToken(effective.ConfiguredPersonality))
                    .Append("） · 来源 ")
                    .Append(effective.PersonalitySourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.PersonalitySourceLabel)
                    .AppendLine(" 请求快照；会话显式选择优先");
            }
            if (effective.HasWebSearchModeOverride)
            {
                builder.Append("Codex web_search：")
                    .Append(CopilotCodexWebSearchModeSelection.GetConfigToken(
                        effective.ConfiguredWebSearchMode))
                    .Append(" · 来源 ")
                    .Append(effective.WebSearchModeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.WebSearchModeSourceLabel)
                    .Append(" 请求快照；")
                    .AppendLine(CopilotCodexWebSearchModeSelection.GetEffectiveLabel(
                        effective.ConfiguredWebSearchMode));
            }
            if (effective.HasModelContextWindowOverride)
            {
                builder.Append("Codex model_context_window：")
                    .Append(effective.ConfiguredModelContextWindowTokens.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · 来源 ")
                    .Append(effective.ModelContextWindowSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelContextWindowSourceLabel)
                    .AppendLine(" 请求快照；覆盖应用默认上下文窗口");
            }
            if (effective.HasModelAutoCompactTokenLimitOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit：")
                    .Append(effective.ConfiguredModelAutoCompactTokenLimit.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · 来源 ")
                    .Append(effective.ModelAutoCompactTokenLimitSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelAutoCompactTokenLimitSourceLabel)
                    .AppendLine(" 请求快照；覆盖应用百分比阈值");
            }
            if (effective.HasModelAutoCompactTokenLimitScopeOverride)
            {
                builder.Append("Codex model_auto_compact_token_limit_scope：")
                    .Append(CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(
                        effective.EffectiveModelAutoCompactTokenLimitScope))
                    .Append(" · 来源 ")
                    .Append(effective.ModelAutoCompactTokenLimitScopeSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelAutoCompactTokenLimitScopeSourceLabel)
                    .AppendLine(" 请求快照");
            }
            if (effective.HasModelInstructionsFileOverride)
            {
                builder.Append("Codex model_instructions_file：")
                    .Append(effective.ModelInstructions.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.ModelInstructionsSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.ModelInstructionsSourceLabel)
                    .AppendLine(effective.HasEffectiveModelInstructions
                        ? " 请求快照；替换会话内置主体，宿主安全规则强制保留）"
                        : " 请求快照；文件为空或未安全加载，使用 Profile/内置主体）");
                if (effective.ModelInstructionsSourceFilePath.Length > 0)
                {
                    builder.Append("模型指令文件：")
                        .AppendLine(FormatPath(effective.ModelInstructionsSourceFilePath, workspacePath));
                }
            }
            if (effective.HasCompactPromptOverride)
            {
                builder.Append("Codex compact_prompt：")
                    .Append(effective.CompactPrompt.Length.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 字符（")
                    .Append(effective.CompactPromptSourceLabel.Length == 0
                        ? "Codex config.toml"
                        : effective.CompactPromptSourceLabel)
                    .AppendLine(effective.CompactPrompt.Length == 0
                        ? " 请求快照；未产生非空覆盖，使用内置主体）"
                        : " 请求快照；终态完整性后缀由宿主强制保留）");
                if (effective.CompactPromptSourceFilePath.Length > 0)
                {
                    builder.Append("压缩提示文件：")
                        .AppendLine(FormatPath(effective.CompactPromptSourceFilePath, workspacePath));
                }
            }
            builder.Append("项目根标记：");
            if (effective.ProjectRootMarkers.Count == 0)
            {
                builder.AppendLine(effective.HasProjectRootMarkersOverride
                    ? "[]（Codex Home 请求快照；不向上搜索）"
                    : "[]（默认；不向上搜索）");
            }
            else
            {
                builder.Append(string.Join("、", effective.ProjectRootMarkers))
                    .AppendLine(effective.HasProjectRootMarkersOverride
                        ? "（Codex Home 请求快照）"
                        : "（默认）");
            }
            if (effective.AppliedProjectConfigFilePaths.Count > 0)
            {
                builder.Append("项目配置层：")
                    .Append(effective.AppliedProjectConfigFilePaths.Count.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 个（项目根 → 工作目录，后者优先）");
                foreach (var configPath in effective.AppliedProjectConfigFilePaths)
                    builder.Append("  - ").AppendLine(FormatPath(configPath, workspacePath));
            }
        }

        private static void AppendTarget(
            StringBuilder builder,
            CopilotProjectInstructionSnapshot? snapshot)
        {
            var workspacePath = (snapshot?.WorkspacePath ?? string.Empty).Trim();
            if (workspacePath.Length > 0)
                builder.Append("项目根：").AppendLine(workspacePath);

            var activeDocumentPath = (snapshot?.ActiveDocumentPath ?? string.Empty).Trim();
            if (activeDocumentPath.Length > 0)
            {
                builder.Append("活动目标：")
                    .AppendLine(FormatPath(activeDocumentPath, workspacePath));
            }
        }

        private static string GetSourceLabel(string? path, string? globalInstructionRootPath)
        {
            var normalized = (path ?? string.Empty).Replace('/', '\\');
            if (!string.IsNullOrWhiteSpace(globalInstructionRootPath)
                && CopilotWorkspaceSearchSupport.IsPathWithinRoots(path, [globalInstructionRootPath]))
            {
                return "Codex 全局指令";
            }
            var fileName = Path.GetFileName(normalized);
            if (string.Equals(fileName, "AGENTS.override.md", StringComparison.OrdinalIgnoreCase))
                return "共享覆盖";
            if (string.Equals(fileName, "AGENTS.md", StringComparison.OrdinalIgnoreCase))
                return "共享指令";
            if (string.Equals(fileName, "CLAUDE.local.md", StringComparison.OrdinalIgnoreCase))
                return "私有局部覆盖";
            if (normalized.Contains(@"\.claude\rules\", StringComparison.OrdinalIgnoreCase))
                return "Claude 路径规则";
            if (string.Equals(fileName, "CLAUDE.md", StringComparison.OrdinalIgnoreCase))
                return "Claude 兼容指令";
            return "项目指令";
        }

        private static string FormatPath(string? path, string? workspacePath)
        {
            var normalizedPath = (path ?? string.Empty).Trim();
            if (normalizedPath.Length == 0)
                return "（路径不可用）";

            try
            {
                var fullPath = Path.GetFullPath(normalizedPath);
                var normalizedWorkspace = string.IsNullOrWhiteSpace(workspacePath)
                    ? string.Empty
                    : Path.GetFullPath(workspacePath);
                if (normalizedWorkspace.Length > 0
                    && CopilotWorkspaceSearchSupport.IsPathWithinRoots(fullPath, [normalizedWorkspace]))
                {
                    return Path.GetRelativePath(normalizedWorkspace, fullPath);
                }
                return fullPath;
            }
            catch
            {
                return normalizedPath;
            }
        }
    }
}
