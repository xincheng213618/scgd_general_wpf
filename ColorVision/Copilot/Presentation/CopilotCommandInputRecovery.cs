using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCommandInputRecovery(
        string Title,
        string Message);

    internal static class CopilotCommandInputRecoveryResolver
    {
        private const int MaximumSuggestionTokenLength = 128;
        private const int MaximumSuggestions = 3;

        public static bool TryResolve(
            string? input,
            IReadOnlyList<CopilotAgentSkillCatalogItem>? skills,
            out CopilotCommandInputRecovery recovery)
        {
            recovery = null!;
            var normalized = (input ?? string.Empty).Trim();
            if (normalized.Length == 0
                || normalized[0] is not '/' and not '$')
            {
                return false;
            }

            var separatorIndex = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
            var token = separatorIndex < 0 ? normalized : normalized[..separatorIndex];
            if (token.Length <= 1)
                return false;

            var availableSkills = skills ?? Array.Empty<CopilotAgentSkillCatalogItem>();
            var skillName = token[1..];
            if (availableSkills.Any(skill =>
                string.Equals(skill.Name, skillName, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (token[0] == '/')
            {
                var command = CopilotLocalCommandCatalog.All.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, token, StringComparison.OrdinalIgnoreCase));
                if (command != null)
                {
                    if (CopilotLocalCommandCatalog.Parse(normalized) != null)
                        return false;

                    recovery = new CopilotCommandInputRecovery(
                        command.Name + " · 用法",
                        $"该本地命令不接受这些参数，未发送给模型。\n\n用法：{command.Usage}");
                    return true;
                }
            }

            var suggestions = token.Length <= MaximumSuggestionTokenLength
                ? FindClosestCandidates(token, availableSkills)
                : [];
            // Skill discovery is intentionally bounded for composer suggestions. An unmatched
            // token may still name a valid skill outside that UI catalog, so only intercept
            // inputs when a concrete local correction is available.
            if (suggestions.Length == 0)
                return false;

            var commandKind = token[0] == '$' ? "Skill" : "本地命令或 Skill";
            recovery = new CopilotCommandInputRecovery(
                token + " · 未找到",
                $"未找到{commandKind}“{token}”，未发送给模型。\n\n你是否想输入：{string.Join("、", suggestions)}\n\n输入 /help 查看固定命令，或输入 /skills 查看可用 Skill。");
            return true;
        }

        private static string[] FindClosestCandidates(
            string token,
            IReadOnlyList<CopilotAgentSkillCatalogItem> skills)
        {
            var marker = token[0];
            var query = token[1..];
            var candidates = marker == '/'
                ? CopilotLocalCommandCatalog.All.Select(command => command.Name)
                    .Concat(skills.Select(skill => "/" + skill.Name))
                : skills.Select(skill => "$" + skill.Name);
            return candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Distance = CalculateEditDistance(query, candidate[1..]),
                })
                .Where(item => item.Distance <= ResolveMaximumDistance(
                    query.Length,
                    item.Candidate.Length - 1))
                .OrderBy(item => item.Distance)
                .ThenBy(item => item.Candidate, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumSuggestions)
                .Select(item => item.Candidate)
                .ToArray();
        }

        private static int ResolveMaximumDistance(int queryLength, int candidateLength)
        {
            var maximumLength = Math.Max(queryLength, candidateLength);
            return maximumLength switch
            {
                <= 4 => 1,
                <= 9 => 2,
                _ => 3,
            };
        }

        private static int CalculateEditDistance(string left, string right)
        {
            if (left.Length == 0)
                return right.Length;
            if (right.Length == 0)
                return left.Length;

            var previous = new int[right.Length + 1];
            var current = new int[right.Length + 1];
            for (var rightIndex = 0; rightIndex <= right.Length; rightIndex++)
                previous[rightIndex] = rightIndex;

            for (var leftIndex = 1; leftIndex <= left.Length; leftIndex++)
            {
                current[0] = leftIndex;
                for (var rightIndex = 1; rightIndex <= right.Length; rightIndex++)
                {
                    var substitutionCost = char.ToUpperInvariant(left[leftIndex - 1])
                        == char.ToUpperInvariant(right[rightIndex - 1])
                            ? 0
                            : 1;
                    current[rightIndex] = Math.Min(
                        Math.Min(
                            current[rightIndex - 1] + 1,
                            previous[rightIndex] + 1),
                        previous[rightIndex - 1] + substitutionCost);
                }

                (previous, current) = (current, previous);
            }

            return previous[right.Length];
        }
    }
}
