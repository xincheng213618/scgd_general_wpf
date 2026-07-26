using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed class CopilotPermissionDiagnosticSnapshot
    {
        public CopilotAgentMode Mode { get; init; }

        public CopilotAgentAccessMode AccessMode { get; init; } = CopilotAgentAccessMode.ConfirmProtectedActions;

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> TrustedProjectRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableRootPaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> WritableFilePaths { get; init; } = Array.Empty<string>();

        public long CapabilityCatalogRevision { get; init; }

        public IReadOnlyList<CopilotCapabilityCatalogEntry> Capabilities { get; init; } = Array.Empty<CopilotCapabilityCatalogEntry>();

        public IReadOnlyList<CopilotMcpClientServerConfig> ExternalMcpServers { get; init; } = Array.Empty<CopilotMcpClientServerConfig>();

        public int PendingApprovals { get; init; }
    }

    public static class CopilotPermissionDiagnostics
    {
        private const int MaximumDisplayedPaths = 8;
        private const int MaximumDisplayedCapabilities = 16;
        private const int MaximumDisplayedMcpServers = 8;
        private const int MaximumInlineTextCharacters = 220;

        public static string Format(CopilotPermissionDiagnosticSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            var capabilities = (snapshot.Capabilities ?? Array.Empty<CopilotCapabilityCatalogEntry>())
                .Where(capability => capability != null)
                .ToArray();
            var externalMcpServers = (snapshot.ExternalMcpServers ?? Array.Empty<CopilotMcpClientServerConfig>())
                .Where(server => server?.Enabled == true)
                .ToArray();
            var readOnlyCount = capabilities.Count(capability => capability.Access == CopilotToolAccess.ReadOnly);
            var writeCount = capabilities.Count(capability => capability.Access == CopilotToolAccess.Write);
            var neverApprovalCount = capabilities.Count(capability => capability.ApprovalMode == CopilotToolApprovalMode.Never);
            var conditionalApproval = capabilities
                .Where(capability => capability.ApprovalMode == CopilotToolApprovalMode.Conditional)
                .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var alwaysApproval = capabilities
                .Where(capability => capability.ApprovalMode == CopilotToolApprovalMode.Always)
                .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var unapprovedWrites = capabilities
                .Where(capability => capability.Access == CopilotToolAccess.Write
                    && capability.ApprovalMode == CopilotToolApprovalMode.Never)
                .OrderBy(capability => capability.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var builder = new StringBuilder();
            builder.Append("当前模式：").Append(snapshot.Mode);
            if (snapshot.Mode == CopilotAgentMode.Chat)
                builder.AppendLine("（不启动 Agent 工具循环）");
            else if (snapshot.Mode is CopilotAgentMode.Review or CopilotAgentMode.Diagnose)
                builder.AppendLine("（运行时只暴露只读工具）");
            else
                builder.AppendLine("（工具仍按请求意图、运行时可用性和本地范围过滤）");
            builder.Append("访问模式：")
                .AppendLine(snapshot.AccessMode == CopilotAgentAccessMode.FullAccess
                    ? "临时自动批准（仅对当前任务中明确支持且写入范围位于当前工作区的结构化操作生效；其他受保护操作仍逐次确认）"
                    : "按需确认（受保护 Agent 操作逐次确认）");

            builder.AppendLine();
            builder.AppendLine("本地范围：");
            AppendPaths(builder, "搜索根", snapshot.SearchRootPaths);
            AppendPaths(builder, "受信项目根（项目指令与 Skill）", snapshot.TrustedProjectRootPaths);
            AppendPaths(builder, "可写根", snapshot.WritableRootPaths);
            AppendPaths(builder, "可写文件", snapshot.WritableFilePaths);

            builder.AppendLine();
            builder.Append("能力目录：revision ")
                .Append(Math.Max(0, snapshot.CapabilityCatalogRevision).ToString("N0", CultureInfo.InvariantCulture))
                .Append('，')
                .Append(capabilities.Length.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" 个能力（只读 ")
                .Append(readOnlyCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append("，写入 ")
                .Append(writeCount.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine("）");
            builder.Append("审批策略：无需审批 ")
                .Append(neverApprovalCount.ToString("N0", CultureInfo.InvariantCulture))
                .Append("，条件审批 ")
                .Append(conditionalApproval.Length.ToString("N0", CultureInfo.InvariantCulture))
                .Append("，每次审批 ")
                .Append(alwaysApproval.Length.ToString("N0", CultureInfo.InvariantCulture))
                .Append("；当前待处理 ")
                .Append(Math.Max(0, snapshot.PendingApprovals).ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine();
            AppendCapabilities(builder, "每次审批", alwaysApproval);
            AppendCapabilities(builder, "条件审批", conditionalApproval);
            if (unapprovedWrites.Length > 0)
                AppendCapabilities(builder, "警告：无审批写入", unapprovedWrites);

            builder.AppendLine();
            builder.Append("外部 MCP：")
                .Append(externalMcpServers.Length.ToString("N0", CultureInfo.InvariantCulture))
                .AppendLine(" 个已启用服务；这里只显示配置策略和最近发布到能力目录的元数据，不主动联网刷新");
            foreach (var server in externalMcpServers.Take(MaximumDisplayedMcpServers))
            {
                var rules = server.ToolRules
                    .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.ToolName))
                    .ToArray();
                builder.Append("  - ")
                    .Append(FormatInlineText(server.Name, "unnamed"));
                if (rules.Length == 0)
                {
                    builder.Append(" · 默认 ")
                        .Append(server.AccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly ? "只读" : "每次审批");
                }
                else
                {
                    builder.Append(" · 白名单 ")
                        .Append(rules.Length.ToString("N0", CultureInfo.InvariantCulture))
                        .Append("（只读 ")
                        .Append(rules.Count(rule => rule.AccessPolicy == CopilotMcpClientAccessPolicy.ReadOnly).ToString("N0", CultureInfo.InvariantCulture))
                        .Append("，每次审批 ")
                        .Append(rules.Count(rule => rule.AccessPolicy == CopilotMcpClientAccessPolicy.RequireApproval).ToString("N0", CultureInfo.InvariantCulture))
                        .Append('）');
                }
                builder.AppendLine();
            }
            if (externalMcpServers.Length > MaximumDisplayedMcpServers)
            {
                builder.Append("  - ...另有 ")
                    .Append((externalMcpServers.Length - MaximumDisplayedMcpServers).ToString("N0", CultureInfo.InvariantCulture))
                    .AppendLine(" 个服务未展开");
            }

            builder.AppendLine();
            builder.AppendLine("边界：");
            builder.AppendLine("- 显式文件和附件目录可以进入搜索根，但不会自动成为项目指令或项目 Skill 来源。");
            builder.AppendLine(snapshot.AccessMode == CopilotAgentAccessMode.FullAccess
                ? "- 临时自动批准不会覆盖 Shell、菜单、数据库或范围不可界定的操作；项目指令、Skill、工具描述和历史消息也不能扩大文件范围或伪造用户意图。"
                : "- 项目指令、Skill、工具描述和历史消息都不能扩大文件范围或绕过审批。");
            builder.AppendLine("- 历史中的批准不构成新调用授权；需要审批的能力按具体调用重新确认。");
            builder.Append("- /permissions 只读取本地快照，不调用模型、不连接外部 MCP，也不修改文件或配置。");
            return builder.ToString();
        }

        private static void AppendPaths(StringBuilder builder, string label, IReadOnlyList<string>? paths)
        {
            var normalizedPaths = (paths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => FormatInlineText(path, string.Empty))
                .Where(path => path.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumDisplayedPaths + 1)
                .ToArray();
            builder.Append("  - ").Append(label).Append('：');
            if (normalizedPaths.Length == 0)
            {
                builder.AppendLine("无");
                return;
            }

            builder.AppendLine();
            foreach (var path in normalizedPaths.Take(MaximumDisplayedPaths))
                builder.Append("    - ").AppendLine(path);
            if (normalizedPaths.Length > MaximumDisplayedPaths)
                builder.AppendLine("    - ...还有更多路径未展开");
        }

        private static void AppendCapabilities(
            StringBuilder builder,
            string label,
            CopilotCapabilityCatalogEntry[] capabilities)
        {
            if (capabilities.Length == 0)
                return;

            builder.Append("  - ").Append(label).Append('：');
            builder.Append(string.Join(
                "、",
                capabilities
                    .Take(MaximumDisplayedCapabilities)
                    .Select(FormatCapabilityLabel)));
            if (capabilities.Length > MaximumDisplayedCapabilities)
            {
                builder.Append("、...另有 ")
                    .Append((capabilities.Length - MaximumDisplayedCapabilities).ToString("N0", CultureInfo.InvariantCulture))
                    .Append(" 个");
            }
            builder.AppendLine();
        }

        private static string FormatCapabilityLabel(CopilotCapabilityCatalogEntry capability)
        {
            var name = FormatInlineText(capability.Name, "unnamed");
            if (capability.SourceKind == CopilotCapabilitySourceKind.BuiltIn)
                return name;
            return name + " [" + FormatInlineText(capability.SourceName, capability.SourceKind.ToString()) + "]";
        }

        private static string FormatInlineText(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var builder = new StringBuilder(Math.Min(value.Length, MaximumInlineTextCharacters));
            var pendingSpace = false;
            foreach (var character in value.Trim())
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                    builder.Append(' ');
                builder.Append(character);
                pendingSpace = false;
                if (builder.Length >= MaximumInlineTextCharacters)
                    break;
            }
            return builder.ToString();
        }
    }
}
