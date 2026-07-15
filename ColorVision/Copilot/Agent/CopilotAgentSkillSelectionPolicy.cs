using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotAgentSkillSelectionPolicy
    {
        private const int MaxInvocationPolicyFileBytes = 32_768;

        public static CopilotAgentSkillSelection Select(
            IReadOnlyList<AgentSkill> skills,
            string? userText,
            IReadOnlySet<string> historicalExplicitOnlySkillNames,
            int maximumCount,
            int maximumMetadataCharacters)
        {
            var query = userText?.Trim() ?? string.Empty;
            var queryWords = ExtractAsciiWords(query);
            var queryCjkBigrams = ExtractCjkBigrams(query);
            var metadataExplicitOnlyNames = new List<string>();
            var historicalExplicitOnlyNames = new List<string>();
            var candidates = new List<SkillCandidate>(skills.Count);
            for (var index = 0; index < skills.Count; index++)
            {
                var skill = skills[index];
                if (!IsExplicitlyRequested(query, skill.Frontmatter.Name))
                {
                    if (DisallowsImplicitInvocation(skill))
                    {
                        metadataExplicitOnlyNames.Add(skill.Frontmatter.Name);
                        continue;
                    }
                    if (historicalExplicitOnlySkillNames.Contains(skill.Frontmatter.Name))
                    {
                        historicalExplicitOnlyNames.Add(skill.Frontmatter.Name);
                        continue;
                    }
                }
                candidates.Add(new SkillCandidate(
                    skill,
                    index,
                    CalculateRelevanceScore(skill.Frontmatter, query, queryWords, queryCjkBigrams)));
            }
            var ranked = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Index)
                .ToArray();
            var selected = new List<(AgentSkill Skill, int Index)>();
            var shortenedDescriptionNames = new List<string>();
            var advertisedCharacters = 0;
            for (var rankedIndex = 0; rankedIndex < ranked.Length; rankedIndex++)
            {
                var candidate = ranked[rankedIndex];
                if (selected.Count >= maximumCount)
                    break;

                var remainingCharacters = maximumMetadataCharacters - advertisedCharacters;
                var minimumCharacters = candidate.Skill.Frontmatter.Name.Length + 1;
                if (remainingCharacters < minimumCharacters)
                    continue;

                var remainingCandidateCount = Math.Min(maximumCount - selected.Count, ranked.Length - rankedIndex);
                var fairCharacterShare = Math.Max(minimumCharacters, remainingCharacters / Math.Max(1, remainingCandidateCount));
                var maximumDescriptionCharacters = Math.Max(1, fairCharacterShare - candidate.Skill.Frontmatter.Name.Length);
                var selectedSkill = candidate.Skill;
                if (candidate.Skill.Frontmatter.Description.Length > maximumDescriptionCharacters)
                {
                    selectedSkill = new MetadataBudgetAgentSkill(candidate.Skill, Shorten(candidate.Skill.Frontmatter.Description, maximumDescriptionCharacters));
                    shortenedDescriptionNames.Add(candidate.Skill.Frontmatter.Name);
                }

                var metadataCharacters = selectedSkill.Frontmatter.Name.Length + selectedSkill.Frontmatter.Description.Length;
                selected.Add((selectedSkill, candidate.Index));
                advertisedCharacters += metadataCharacters;
            }
            var selectedSkills = selected
                .OrderBy(candidate => candidate.Index)
                .Select(candidate => candidate.Skill)
                .ToArray();
            return new CopilotAgentSkillSelection(
                selectedSkills,
                metadataExplicitOnlyNames.ToArray(),
                historicalExplicitOnlyNames.ToArray(),
                shortenedDescriptionNames.ToArray());
        }

        private static string Shorten(string value, int maximumCharacters)
        {
            if (value.Length <= maximumCharacters)
                return value;
            if (maximumCharacters <= 1)
                return value[..maximumCharacters];
            return value[..(maximumCharacters - 1)].TrimEnd() + "…";
        }

        private static bool IsExplicitlyRequested(string query, string skillName)
        {
            var startIndex = 0;
            while (startIndex < query.Length)
            {
                var index = query.IndexOf(skillName, startIndex, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                    return false;

                var beforeIsBoundary = index == 0 || !IsSkillNameCharacter(query[index - 1]);
                var endIndex = index + skillName.Length;
                var afterIsBoundary = endIndex == query.Length || !IsSkillNameCharacter(query[endIndex]);
                if (beforeIsBoundary && afterIsBoundary)
                    return true;
                startIndex = index + 1;
            }
            return false;
        }

        private static bool IsSkillNameCharacter(char character)
        {
            return character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-';
        }

        private static bool DisallowsImplicitInvocation(AgentSkill skill)
        {
            if (skill is not AgentFileSkill fileSkill)
                return false;

            try
            {
                var skillDirectoryPath = Path.GetFullPath(fileSkill.Path);
                var agentsDirectoryPath = Path.GetFullPath(Path.Combine(skillDirectoryPath, "agents"));
                var policyFilePath = Path.GetFullPath(Path.Combine(agentsDirectoryPath, "openai.yaml"));
                if (!IsDescendantPath(skillDirectoryPath, policyFilePath)
                    || !Directory.Exists(agentsDirectoryPath)
                    || (File.GetAttributes(agentsDirectoryPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                var file = new FileInfo(policyFilePath);
                if (!file.Exists
                    || file.Length <= 0
                    || file.Length > MaxInvocationPolicyFileBytes
                    || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                var inPolicy = false;
                foreach (var rawLine in File.ReadLines(policyFilePath))
                {
                    var commentIndex = rawLine.IndexOf('#');
                    var normalizedLine = (commentIndex < 0 ? rawLine : rawLine[..commentIndex]).Trim();
                    if (normalizedLine.Length == 0)
                        continue;

                    var indentation = rawLine.Length - rawLine.TrimStart().Length;
                    if (indentation == 0)
                    {
                        inPolicy = string.Equals(normalizedLine, "policy:", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    if (!inPolicy)
                        continue;

                    const string key = "allow_implicit_invocation:";
                    if (!normalizedLine.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var value = normalizedLine[key.Length..].Trim().Trim('\'', '"');
                    return string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
            }
            return false;
        }

        private static bool IsDescendantPath(string parentPath, string candidatePath)
        {
            var normalizedParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parentPath)) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(candidatePath).StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static int CalculateRelevanceScore(
            AgentSkillFrontmatter frontmatter,
            string query,
            HashSet<string> queryWords,
            HashSet<string> queryCjkBigrams)
        {
            if (query.Length == 0)
                return 0;

            var score = query.Contains(frontmatter.Name, StringComparison.OrdinalIgnoreCase) ? 10_000 : 0;
            foreach (var segment in frontmatter.Name.Split('-', StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.Length >= 3 && query.Contains(segment, StringComparison.OrdinalIgnoreCase))
                    score += 200;
            }
            foreach (var word in ExtractAsciiWords(frontmatter.Description))
            {
                if (queryWords.Contains(word))
                    score += 10;
            }
            foreach (var bigram in ExtractCjkBigrams(frontmatter.Description))
            {
                if (queryCjkBigrams.Contains(bigram))
                    score += 3;
            }
            return score;
        }

        private static HashSet<string> ExtractAsciiWords(string value)
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var builder = new StringBuilder();
            foreach (var character in value ?? string.Empty)
            {
                if (character <= 127 && char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                    continue;
                }

                AddWord();
            }
            AddWord();
            return words;

            void AddWord()
            {
                if (builder.Length >= 3)
                    words.Add(builder.ToString());
                builder.Clear();
            }
        }

        private static HashSet<string> ExtractCjkBigrams(string value)
        {
            var bigrams = new HashSet<string>(StringComparer.Ordinal);
            char? previous = null;
            foreach (var character in value ?? string.Empty)
            {
                if (character is not (>= '\u3400' and <= '\u4dbf' or >= '\u4e00' and <= '\u9fff'))
                {
                    previous = null;
                    continue;
                }

                if (previous.HasValue)
                    bigrams.Add(string.Concat(previous.Value, character));
                previous = character;
            }
            return bigrams;
        }

        private sealed record SkillCandidate(AgentSkill Skill, int Index, int Score);

        private sealed class MetadataBudgetAgentSkill : AgentSkill
        {
            private readonly AgentSkill _inner;
            private readonly AgentSkillFrontmatter _frontmatter;

            public MetadataBudgetAgentSkill(AgentSkill inner, string description)
            {
                _inner = inner;
                var source = inner.Frontmatter;
                _frontmatter = new AgentSkillFrontmatter(source.Name, description, source.Compatibility)
                {
                    AllowedTools = source.AllowedTools,
                    License = source.License,
                    Metadata = source.Metadata,
                };
            }

            public override AgentSkillFrontmatter Frontmatter => _frontmatter;

            public override ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default)
            {
                return _inner.GetContentAsync(cancellationToken);
            }

            public override ValueTask<AgentSkillResource?> GetResourceAsync(string name, CancellationToken cancellationToken = default)
            {
                return _inner.GetResourceAsync(name, cancellationToken);
            }

            public override ValueTask<AgentSkillScript?> GetScriptAsync(string name, CancellationToken cancellationToken = default)
            {
                return _inner.GetScriptAsync(name, cancellationToken);
            }
        }
    }

    internal sealed record CopilotAgentSkillSelection(
        IReadOnlyList<AgentSkill> SelectedSkills,
        IReadOnlyList<string> MetadataExplicitOnlyNames,
        IReadOnlyList<string> HistoricalExplicitOnlyNames,
        IReadOnlyList<string> ShortenedDescriptionNames);
}
