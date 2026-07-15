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
        NewConversation,
    }

    public sealed record CopilotLocalCommand(
        string Name,
        string Description,
        CopilotLocalCommandKind Kind);

    public static class CopilotLocalCommandCatalog
    {
        private const int MaxSuggestions = 8;

        private static readonly CopilotLocalCommand[] Commands =
        [
            new("/context", "查看本地上下文、预算与注入统计", CopilotLocalCommandKind.Context),
            new("/skills", "查看 Skill 使用率、连续未加载与降级状态", CopilotLocalCommandKind.Skills),
            new("/mcp", "查看本地 MCP 服务、审批与最近调用状态", CopilotLocalCommandKind.Mcp),
            new("/new", "开始一个新的 Copilot 会话", CopilotLocalCommandKind.NewConversation),
        ];

        public static IReadOnlyList<CopilotLocalCommand> All => Commands;

        public static CopilotLocalCommand? FindExact(string? input)
        {
            var normalized = Normalize(input);
            if (normalized.Length == 0 || normalized.Any(char.IsWhiteSpace))
                return null;

            return Commands.FirstOrDefault(command => string.Equals(command.Name, normalized, StringComparison.OrdinalIgnoreCase));
        }

        public static IReadOnlyList<CopilotLocalCommand> Suggest(string? input)
        {
            var normalized = Normalize(input);
            if (!normalized.StartsWith("/", StringComparison.Ordinal)
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
