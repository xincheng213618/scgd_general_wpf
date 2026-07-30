using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandKind
    {
        Status,
        Context,
        Permissions,
        Skills,
        Mcp,
        Diff,
        Compact,
        Review,
        Plan,
        Goal,
        ResumeConversation,
        RenameConversation,
        CopyResponse,
        SelectModel,
        NewConversation,
        ClearConversation,
        ForkConversation,
        SideQuestion,
        Skill,
        Hooks,
    }

    public sealed record CopilotLocalCommand(
        string Name,
        string Description,
        CopilotLocalCommandKind Kind,
        bool AcceptsArguments = false,
        bool AvailableWhileAgentRuns = false);

    public sealed record CopilotLocalCommandInvocation(
        CopilotLocalCommand Command,
        string Arguments);

    public static class CopilotLocalCommandCatalog
    {
        private const int MaxSuggestions = 20;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/status", "查看模型、Agent、工作区与连接状态", CopilotLocalCommandKind.Status, AvailableWhileAgentRuns: true),
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context, AvailableWhileAgentRuns: true),
            new("/permissions", "查看当前文件范围、能力与审批策略", CopilotLocalCommandKind.Permissions, AvailableWhileAgentRuns: true),
            new("/hooks", "查看生效 Hook、模块来源与最近运行健康度", CopilotLocalCommandKind.Hooks, AvailableWhileAgentRuns: true),
            new("/skills", "查看 Skill 使用率、连续未加载与降级状态", CopilotLocalCommandKind.Skills, AvailableWhileAgentRuns: true),
            new("/mcp", "查看本地 MCP 服务、审批与最近调用状态", CopilotLocalCommandKind.Mcp, AvailableWhileAgentRuns: true),
            new("/diff", "查看已暂存、未暂存补丁和未跟踪文件", CopilotLocalCommandKind.Diff, AcceptsArguments: true),
            new("/compact", "压缩早期对话，可在命令后补充聚焦要求", CopilotLocalCommandKind.Compact, AcceptsArguments: true),
            new("/review", "只读审查当前工作区变更，可补充关注点", CopilotLocalCommandKind.Review, AcceptsArguments: true),
            new("/plan", "只读分析并生成可执行计划，可在命令后直接填写任务", CopilotLocalCommandKind.Plan, AcceptsArguments: true),
            new("/goal", "查看或管理当前会话的持续目标", CopilotLocalCommandKind.Goal, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/resume", "搜索并切换已有 Copilot 会话，可补充标题或关键词", CopilotLocalCommandKind.ResumeConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/rename", "重命名当前 Copilot 会话；省略名称时打开输入窗口", CopilotLocalCommandKind.RenameConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/copy", "复制最近已完成的回答；可用 /copy 2 选择倒数第二条", CopilotLocalCommandKind.CopyResponse, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/model", "选择当前会话使用的模型 Profile；可补充 Profile 名或模型 ID", CopilotLocalCommandKind.SelectModel, AcceptsArguments: true),
            new("/new", "开始一个新的 Copilot 会话", CopilotLocalCommandKind.NewConversation),
            new("/clear", "清空当前上下文并开始新会话；可先命名旧会话", CopilotLocalCommandKind.ClearConversation, AcceptsArguments: true),
            new("/fork", "复制当前会话到新会话分支；Agent 运行时创建可见快照", CopilotLocalCommandKind.ForkConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/branch", "同 /fork；只分叉会话，不创建 Git 分支", CopilotLocalCommandKind.ForkConversation, AcceptsArguments: true, AvailableWhileAgentRuns: true),
            new("/btw", "从当前会话上下文回答一次旁路问题，不影响主任务", CopilotLocalCommandKind.SideQuestion, AcceptsArguments: true, AvailableWhileAgentRuns: true),
        ];

        public static IReadOnlyList<CopilotLocalCommand> All => Commands;

        public static CopilotLocalCommand? FindExact(string? input)
        {
            var invocation = Parse(input);
            return invocation is { Arguments.Length: 0 } ? invocation.Command : null;
        }

        public static CopilotLocalCommandInvocation? Parse(string? input)
        {
            var normalized = Normalize(input);
            if (normalized.Length == 0)
                return null;

            var separatorIndex = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
            var name = separatorIndex < 0 ? normalized : normalized[..separatorIndex];
            var arguments = separatorIndex < 0 ? string.Empty : normalized[(separatorIndex + 1)..].Trim();
            var command = Commands.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (command == null || (!command.AcceptsArguments && arguments.Length > 0))
                return null;

            return new CopilotLocalCommandInvocation(command, arguments);
        }

        public static IReadOnlyList<CopilotLocalCommand> Suggest(
            string? input,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? skills = null)
        {
            var normalized = Normalize(input);
            if (normalized.Length == 0
                || normalized[0] is not '/' and not '$'
                || normalized.Any(char.IsWhiteSpace)
                || normalized.StartsWith('/') && FindExact(normalized) != null)
            {
                return Array.Empty<CopilotLocalCommand>();
            }

            var suggestions = normalized.StartsWith('/')
                ? Commands.Where(command => command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                : Enumerable.Empty<CopilotLocalCommand>();
            var skillSuggestions = (skills ?? Array.Empty<CopilotAgentSkillCatalogItem>())
                .Select(skill => new CopilotLocalCommand(
                    normalized[0] + skill.Name,
                    "Skill · " + skill.Description,
                    CopilotLocalCommandKind.Skill,
                    AcceptsArguments: true))
                .Where(command => command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(command.Name, normalized, StringComparison.OrdinalIgnoreCase));
            return suggestions
                .Concat(skillSuggestions)
                .DistinctBy(command => command.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxSuggestions)
                .ToArray();
        }

        private static string Normalize(string? input)
        {
            return (input ?? string.Empty).Trim();
        }
    }
}
