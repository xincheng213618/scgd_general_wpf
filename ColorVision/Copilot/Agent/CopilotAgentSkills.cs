using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentSkills : IDisposable
    {
        private const int MaxAdvertisedSkills = 128;
        private static readonly EnumerationOptions SkillEnumerationOptions = new()
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 2,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        private CopilotAgentSkills(IReadOnlyList<string> searchPaths, IReadOnlyList<string> skillNames, AgentFileSkillsSource? source)
        {
            SearchPaths = searchPaths;
            SkillNames = skillNames;
            Source = source;
        }

        public IReadOnlyList<string> SearchPaths { get; }

        public IReadOnlyList<string> SkillNames { get; }

        public AgentFileSkillsSource? Source { get; }

        public bool IsEnabled => Source != null && SkillNames.Count > 0;

        public static CopilotAgentSkills Create(CopilotAgentRequest request, string? applicationBaseDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            var searchPaths = ResolveSearchPaths(request, applicationBaseDirectory);
            var skillNames = DiscoverSkillNames(searchPaths);
            if (skillNames.Count == 0)
                return new CopilotAgentSkills(searchPaths, skillNames, null);

            var source = new AgentFileSkillsSource(
                searchPaths,
                scriptRunner: null,
                options: new AgentFileSkillsSourceOptions
                {
                    SearchDepth = 2,
                    ScriptFilter = _ => false,
                },
                loggerFactory: null);
            return new CopilotAgentSkills(searchPaths, skillNames, source);
        }

        internal static IReadOnlyList<string> ResolveSearchPaths(CopilotAgentRequest request, string? applicationBaseDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            var paths = new List<string>();
            foreach (var root in request.SearchRootPaths ?? Array.Empty<string>())
                AddExistingSkillRoot(paths, TryGetDirectory(root), Path.Combine(".agents", "skills"));

            var baseDirectory = string.IsNullOrWhiteSpace(applicationBaseDirectory)
                ? AppContext.BaseDirectory
                : applicationBaseDirectory;
            AddExistingSkillRoot(paths, baseDirectory, Path.Combine("Copilot", "Skills"));
            return paths;
        }

        internal static IReadOnlyList<string> DiscoverSkillNames(IReadOnlyList<string> searchPaths)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var searchPath in searchPaths ?? Array.Empty<string>())
            {
                IEnumerable<string> entries;
                try
                {
                    entries = Directory.EnumerateFiles(searchPath, "SKILL.md", SkillEnumerationOptions)
                        .Take(MaxAdvertisedSkills + 1)
                        .ToArray();
                }
                catch
                {
                    continue;
                }

                foreach (var entry in entries)
                {
                    var name = Path.GetFileName(Path.GetDirectoryName(entry));
                    if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                        names.Add(name);
                    if (names.Count >= MaxAdvertisedSkills)
                        return names;
                }
            }

            return names;
        }

        public void Dispose()
        {
            Source?.Dispose();
        }

        private static string? TryGetDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return Directory.Exists(fullPath)
                    ? fullPath
                    : File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void AddExistingSkillRoot(List<string> paths, string? parentDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(parentDirectory))
                return;

            try
            {
                var candidate = Path.GetFullPath(Path.Combine(parentDirectory, relativePath));
                if (!Directory.Exists(candidate))
                    return;
                if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                    return;
                if (!paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    paths.Add(candidate);
            }
            catch
            {
            }
        }
    }
}
