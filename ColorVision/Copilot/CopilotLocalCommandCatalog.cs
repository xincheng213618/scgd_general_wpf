using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandKind
    {
        Status,
        Context,
        Skills,
        Mcp,
        Compact,
        Review,
        NewConversation,
        Skill,
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
        private const int MaxSuggestions = 16;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/status", "查看模型、Agent、工作区与连接状态", CopilotLocalCommandKind.Status, AvailableWhileAgentRuns: true),
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context, AvailableWhileAgentRuns: true),
            new("/skills", "查看 Skill 使用率、连续未加载与降级状态", CopilotLocalCommandKind.Skills, AvailableWhileAgentRuns: true),
            new("/mcp", "查看本地 MCP 服务、审批与最近调用状态", CopilotLocalCommandKind.Mcp, AvailableWhileAgentRuns: true),
            new("/compact", "压缩早期对话，可在命令后补充聚焦要求", CopilotLocalCommandKind.Compact, AcceptsArguments: true),
            new("/review", "只读审查当前工作区变更，可补充关注点", CopilotLocalCommandKind.Review, AcceptsArguments: true),
            new("/new", "开始一个新的 Copilot 会话", CopilotLocalCommandKind.NewConversation),
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
