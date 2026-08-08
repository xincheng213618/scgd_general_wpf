using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentSkillSourceKind
    {
        Project,
        User,
        BuiltIn,
    }

    public sealed record CopilotAgentSkillCatalogItem(string Name, string Description)
    {
        internal string SkillFilePath { get; init; } = string.Empty;

        internal string SearchRootPath { get; init; } = string.Empty;

        internal CopilotAgentSkillSourceKind SourceKind { get; init; }

        internal bool IsBuiltIn => SourceKind == CopilotAgentSkillSourceKind.BuiltIn;
    }

    public static class CopilotAgentSkillCatalog
    {
        public const int MaxCatalogEntries = 64;
        private const int MaxCandidateFiles = 256;
        private const int MaxSkillFileBytes = 262_144;
        private const int MaxFrontmatterCharacters = 16_384;
        private const int MaxDescriptionCharacters = 180;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
        private static readonly object CacheSync = new();
        private static readonly CopilotAgentSkillCatalogMonitor ChangeMonitor = new(HandleWatchedSkillChange);
        private static string _cacheKey = string.Empty;
        private static DateTimeOffset _cacheExpiresAtUtc;
        private static IReadOnlyList<CopilotAgentSkillCatalogItem> _cachedItems = Array.Empty<CopilotAgentSkillCatalogItem>();
        private static long _cacheRevision;

        internal static event EventHandler? CatalogChanged;

        public static IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverCached(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory = null)
        {
            return DiscoverCached(
                trustedProjectRootPaths,
                overrides,
                applicationBaseDirectory,
                userProfileDirectory: null);
        }

        internal static IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverCached(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory,
            string? userProfileDirectory)
        {
            return DiscoverCached(
                trustedProjectRootPaths,
                overrides,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath: null);
        }

        internal static IReadOnlyList<CopilotAgentSkillCatalogItem> DiscoverCached(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory,
            string? userProfileDirectory,
            string? activeDocumentPath)
        {
            var searchRequest = CreateSearchRequest(trustedProjectRootPaths, activeDocumentPath);
            ChangeMonitor.UpdateRoots(CopilotAgentSkills.ResolveSearchPathCandidates(
                searchRequest,
                applicationBaseDirectory,
                userProfileDirectory));
            var skillRoots = CopilotAgentSkills.ResolveSearchPaths(
                searchRequest,
                applicationBaseDirectory,
                userProfileDirectory);
            var builtInSkillRoot = ResolveBuiltInSkillRoot(applicationBaseDirectory);
            var userSkillRoot = CopilotAgentSkills.ResolveUserSkillRoot(userProfileDirectory);
            var cacheKey = BuildCacheKey(skillRoots, overrides, builtInSkillRoot);
            var now = DateTimeOffset.UtcNow;
            long revision;
            lock (CacheSync)
            {
                if (string.Equals(_cacheKey, cacheKey, StringComparison.Ordinal)
                    && now < _cacheExpiresAtUtc)
                {
                    return _cachedItems;
                }
                revision = _cacheRevision;
            }

            var discovered = DiscoverFromSkillRoots(skillRoots, overrides, builtInSkillRoot, userSkillRoot);
            lock (CacheSync)
            {
                if (revision != _cacheRevision)
                    return discovered;

                _cacheKey = cacheKey;
                _cacheExpiresAtUtc = DateTimeOffset.UtcNow.Add(CacheDuration);
                _cachedItems = discovered;
                return _cachedItems;
            }
        }

        public static IReadOnlyList<CopilotAgentSkillCatalogItem> Discover(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory = null)
        {
            return Discover(
                trustedProjectRootPaths,
                overrides,
                applicationBaseDirectory,
                userProfileDirectory: null);
        }

        internal static IReadOnlyList<CopilotAgentSkillCatalogItem> Discover(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory,
            string? userProfileDirectory)
        {
            return Discover(
                trustedProjectRootPaths,
                overrides,
                applicationBaseDirectory,
                userProfileDirectory,
                activeDocumentPath: null);
        }

        internal static IReadOnlyList<CopilotAgentSkillCatalogItem> Discover(
            IEnumerable<string>? trustedProjectRootPaths,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string? applicationBaseDirectory,
            string? userProfileDirectory,
            string? activeDocumentPath)
        {
            return DiscoverFromSkillRoots(
                ResolveSkillRoots(
                    trustedProjectRootPaths,
                    applicationBaseDirectory,
                    userProfileDirectory,
                    activeDocumentPath),
                overrides,
                ResolveBuiltInSkillRoot(applicationBaseDirectory),
                CopilotAgentSkills.ResolveUserSkillRoot(userProfileDirectory));
        }

        internal static void Invalidate()
        {
            lock (CacheSync)
            {
                _cacheRevision++;
                _cacheKey = string.Empty;
                _cacheExpiresAtUtc = DateTimeOffset.MinValue;
                _cachedItems = Array.Empty<CopilotAgentSkillCatalogItem>();
            }
        }

        private static CopilotAgentSkillCatalogItem[] DiscoverFromSkillRoots(
            IReadOnlyList<string> skillRoots,
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string builtInSkillRoot,
            string userSkillRoot)
        {
            var discovered = new Dictionary<string, CopilotAgentSkillCatalogItem>(StringComparer.OrdinalIgnoreCase);
            var candidateCount = 0;
            foreach (var root in skillRoots)
            {
                foreach (var skillFilePath in EnumerateSkillFiles(root))
                {
                    if (++candidateCount > MaxCandidateFiles)
                        break;

                    var item = TryReadItem(
                        skillFilePath,
                        root,
                        ResolveSourceKind(root, builtInSkillRoot, userSkillRoot));
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

        private static IReadOnlyList<string> ResolveSkillRoots(
            IEnumerable<string>? trustedProjectRootPaths,
            string? applicationBaseDirectory,
            string? userProfileDirectory,
            string? activeDocumentPath)
        {
            return CopilotAgentSkills.ResolveSearchPaths(
                CreateSearchRequest(trustedProjectRootPaths, activeDocumentPath),
                applicationBaseDirectory,
                userProfileDirectory);
        }

        private static CopilotAgentSkillSourceKind ResolveSourceKind(
            string root,
            string builtInSkillRoot,
            string userSkillRoot)
        {
            if (string.Equals(root, builtInSkillRoot, StringComparison.OrdinalIgnoreCase))
                return CopilotAgentSkillSourceKind.BuiltIn;
            if (string.Equals(root, userSkillRoot, StringComparison.OrdinalIgnoreCase))
                return CopilotAgentSkillSourceKind.User;
            return CopilotAgentSkillSourceKind.Project;
        }

        private static CopilotAgentRequest CreateSearchRequest(
            IEnumerable<string>? trustedProjectRootPaths,
            string? activeDocumentPath = null)
        {
            return new CopilotAgentRequest
            {
                TrustedProjectRootPaths = (trustedProjectRootPaths ?? Array.Empty<string>()).ToArray(),
                ActiveDocumentPath = activeDocumentPath ?? string.Empty,
            };
        }

        private static string ResolveBuiltInSkillRoot(string? applicationBaseDirectory)
        {
            var baseDirectory = string.IsNullOrWhiteSpace(applicationBaseDirectory)
                ? AppContext.BaseDirectory
                : applicationBaseDirectory;
            try
            {
                return Path.GetFullPath(Path.Combine(baseDirectory, "Copilot", "Skills"));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void HandleWatchedSkillChange()
        {
            Invalidate();
            var handlers = CatalogChanged;
            if (handlers == null)
                return;

            foreach (EventHandler handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(null, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    Trace.TraceError($"Copilot Agent Skill catalog subscriber failed: {exception}");
                }
            }
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

        private static CopilotAgentSkillCatalogItem? TryReadItem(
            string skillFilePath,
            string searchRootPath,
            CopilotAgentSkillSourceKind sourceKind)
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
                    {
                        SkillFilePath = skillFilePath,
                        SearchRootPath = searchRootPath,
                        SourceKind = sourceKind,
                    }
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
            IReadOnlyDictionary<string, CopilotAgentSkillOverrideState>? overrides,
            string builtInSkillRoot)
        {
            var builder = new StringBuilder();
            builder.Append("builtin=").Append(builtInSkillRoot).Append('\n');
            foreach (var root in skillRoots)
                builder.Append(root).Append('\n');
            foreach (var item in (overrides ?? new Dictionary<string, CopilotAgentSkillOverrideState>())
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                builder.Append(item.Key.ToLowerInvariant()).Append('=').Append((int)item.Value).Append('\n');
            return builder.ToString();
        }
    }
}
