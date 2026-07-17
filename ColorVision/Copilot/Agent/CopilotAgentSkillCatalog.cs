using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed record CopilotAgentSkillCatalogItem(string Name, string Description);

    public static class CopilotAgentSkillCatalog
    {
        public const int MaxCatalogEntries = 64;
        private const int MaxCandidateFiles = 256;
        private const int MaxSkillFileBytes = 262_144;
        private const int MaxFrontmatterCharacters = 16_384;
        private const int MaxDescriptionCharacters = 180;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
        private static readonly object CacheSync = new();
        private static string _cacheKey = string.Empty;
        private static DateTimeOffset _cacheExpiresAtUtc;
        private static IReadOnlyList<CopilotAgentSkillCatalogItem> _cachedItems = Array.Empty<CopilotAgentSkillCatalogItem>();

        public static IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverCached(
            IEnumerable<string>? searchRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory = null)
        {
            var skillRoots = ResolveSkillRoots(searchRootPaths, applicationBaseDirectory);
            var cacheKey = BuildCacheKey(skillRoots, overrides);
            var now = DateTimeOffset.UtcNow;
            lock (CacheSync)
            {
                if (string.Equals(_cacheKey, cacheKey, StringComparison.Ordinal)
                    && now < _cacheExpiresAtUtc)
                {
                    return _cachedItems;
                }
            }

            var discovered = DiscoverFromSkillRoots(skillRoots, overrides);
            lock (CacheSync)
            {
                _cacheKey = cacheKey;
                _cacheExpiresAtUtc = now.Add(CacheDuration);
                _cachedItems = discovered;
                return _cachedItems;
            }
        }

        public static IReadOnlyList<CopilotAgentSkillCatalogItem> Discover(
            IEnumerable<string>? searchRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory = null)
        {
            return DiscoverFromSkillRoots(ResolveSkillRoots(searchRootPaths, applicationBaseDirectory), overrides);
        }

        private static CopilotAgentSkillCatalogItem[] DiscoverFromSkillRoots(
            IReadOnlyList<string> skillRoots,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides)
        {
            var discovered = new Dictionary<string, CopilotAgentSkillCatalogItem>(StringComparer.OrdinalIgnoreCase);
            var candidateCount = 0;
            foreach (var root in skillRoots)
            {
                foreach (var skillFilePath in EnumerateSkillFiles(root))
                {
                    if (++candidateCount > MaxCandidateFiles)
                        break;

                    var item = TryReadItem(skillFilePath);
                    if (item == null
                        || overrides?.TryGetValue(item.Name, out var state) == true && state == CopilotAgentSkillOverrideState.Off)
                    {
                        continue;
                    }
                    discovered.TryAdd(item.Name, item);
                }
                if (candidateCount > MaxCandidateFiles)
                    break;
            }

            return discovered.Values
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxCatalogEntries)
                .ToArray();
        }

        private static IReadOnlyList<string> ResolveSkillRoots(IEnumerable<string>? searchRootPaths, string? applicationBaseDirectory)
        {
            return CopilotAgentSkills.ResolveSearchPaths(
                new CopilotAgentRequest { SearchRootPaths = (searchRootPaths ?? Array.Empty<string>()).ToArray() },
                applicationBaseDirectory);
        }

        private static IEnumerable<string> EnumerateSkillFiles(string root)
        {
            if (!IsSafeDirectory(root))
                yield break;

            var inspectedDirectories = 0;
            foreach (var firstLevelDirectory in EnumerateSafeDirectories(root, MaxCandidateFiles))
            {
                if (++inspectedDirectories > MaxCandidateFiles)
                    yield break;
                var firstLevelSkill = Path.Combine(firstLevelDirectory, "SKILL.md");
                if (IsSafeFile(firstLevelSkill))
                {
                    yield return firstLevelSkill;
                    continue;
                }

                foreach (var secondLevelDirectory in EnumerateSafeDirectories(
                    firstLevelDirectory,
                    MaxCandidateFiles - inspectedDirectories))
                {
                    if (++inspectedDirectories > MaxCandidateFiles)
                        yield break;
                    var secondLevelSkill = Path.Combine(secondLevelDirectory, "SKILL.md");
                    if (IsSafeFile(secondLevelSkill))
                        yield return secondLevelSkill;
                }
            }
        }

        private static IEnumerable<string> EnumerateSafeDirectories(string parentPath, int maximumCount)
        {
            if (maximumCount <= 0)
                yield break;

            string[] paths;
            try
            {
                paths = Directory.EnumerateDirectories(parentPath)
                    .Take(maximumCount)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                yield break;
            }

            foreach (var path in paths)
            {
                if (IsSafeDirectory(path))
                    yield return path;
            }
        }

        private static CopilotAgentSkillCatalogItem? TryReadItem(string skillFilePath)
        {
            try
            {
                var file = new FileInfo(skillFilePath);
                if (!file.Exists || file.Length <= 0 || file.Length > MaxSkillFileBytes || (file.Attributes & FileAttributes.ReparsePoint) != 0)
                    return null;

                using var stream = new FileStream(skillFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                if (!string.Equals(reader.ReadLine()?.Trim(), "---", StringComparison.Ordinal))
                    return null;

                var charactersRead = 0;
                var frontmatterClosed = false;
                var name = string.Empty;
                var description = string.Empty;
                while (reader.ReadLine() is { } line)
                {
                    charactersRead += line.Length;
                    if (charactersRead > MaxFrontmatterCharacters)
                        return null;
                    if (string.Equals(line.Trim(), "---", StringComparison.Ordinal))
                    {
                        frontmatterClosed = true;
                        break;
                    }

                    if (TryReadScalar(line, "name", out var value))
                        name = CopilotAgentSkillOverrideConfig.NormalizeName(value);
                    else if (TryReadScalar(line, "description", out value))
                        description = NormalizeDescription(value);
                }

                return frontmatterClosed && name.Length > 0 && description.Length > 0
                    ? new CopilotAgentSkillCatalogItem(name, description)
                    : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryReadScalar(string line, string key, out string value)
        {
            value = string.Empty;
            var normalized = line.Trim();
            var separatorIndex = normalized.IndexOf(':');
            if (separatorIndex <= 0 || !string.Equals(normalized[..separatorIndex].Trim(), key, StringComparison.OrdinalIgnoreCase))
                return false;

            value = normalized[(separatorIndex + 1)..].Trim();
            if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"')
                value = value[1..^1].Trim();
            return true;
        }

        private static string NormalizeDescription(string value)
        {
            var normalized = string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return normalized.Length <= MaxDescriptionCharacters
                ? normalized
                : normalized[..(MaxDescriptionCharacters - 1)].TrimEnd() + "…";
        }

        private static bool IsSafeDirectory(string path)
        {
            try
            {
                return Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSafeFile(string path)
        {
            try
            {
                return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string BuildCacheKey(
            IReadOnlyList<string> skillRoots,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides)
        {
            var builder = new StringBuilder();
            foreach (var root in skillRoots)
                builder.Append(root).Append('\n');
            foreach (var item in (overrides ?? new Dictionary<string, CopilotAgentSkillOverrideState>())
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                builder.Append(item.Key.ToLowerInvariant()).Append('=').Append((int)item.Value).Append('\n');
            return builder.ToString();
        }
    }
}
