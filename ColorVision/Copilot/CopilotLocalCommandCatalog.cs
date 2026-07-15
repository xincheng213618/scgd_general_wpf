using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotLocalCommandKind
    {
        Context,
        Skills,
        Mcp,
        Compact,
        NewConversation,
    }

    public sealed record CopilotLocalCommand(
        string Name,
        string Description,
        CopilotLocalCommandKind Kind,
        bool AcceptsArguments = false);

    public sealed record CopilotLocalCommandInvocation(
        CopilotLocalCommand Command,
        string Arguments);

    public static class CopilotLocalCommandCatalog
    {
        private const int MaxSuggestions = 8;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context),
            new("/skills", "查看 Skill 使用率、连续未加载与降级状态", CopilotLocalCommandKind.Skills),
            new("/mcp", "查看本地 MCP 服务、审批与最近调用状态", CopilotLocalCommandKind.Mcp),
            new("/compact", "压缩早期对话，可在命令后补充聚焦要求", CopilotLocalCommandKind.Compact, AcceptsArguments: true),
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

        public static IReadOnlyList<CopilotLocalCommand> Suggest(string? input)
        {
            var normalized = Normalize(input);
            if (!normalized.StartsWith('/')
                || normalized.Any(char.IsWhiteSpace)
                || FindExact(normalized) != null)
            {
                return Array.Empty<CopilotLocalCommand>();
            }

            return Commands
                .Where(command => command.Name.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .Take(MaxSuggestions)
                .ToArray();
        }

        private static string Normalize(string? input)
        {
            return (input ?? string.Empty).Trim();
        }
    }
}
