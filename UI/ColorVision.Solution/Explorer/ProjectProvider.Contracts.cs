using ColorVision.UI;
using ColorVision.Solution.Terminal;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Solution.Explorer
{
    public sealed record ProjectDefinition(
        string ProviderId,
        string Name,
        string Version,
        FileInfo ProjectFile,
        string? LoadError = null,
        IReadOnlyDictionary<string, ProjectCommandDefinition>? Commands = null,
        DirectoryInfo? RootDirectory = null,
        ProjectItemRules? ItemRules = null,
        IReadOnlyList<string>? Dependencies = null,
        IReadOnlyDictionary<string, ProjectConfigurationDefinition>? Configurations = null,
        string? ActiveConfiguration = null)
    {
        public DirectoryInfo ProjectDirectory => RootDirectory ?? ProjectFile.Directory!;

        public ProjectDefinition ForConfiguration(string? configurationName)
        {
            string? normalizedName = string.IsNullOrWhiteSpace(configurationName)
                ? null
                : configurationName.Trim();
            var commands = new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            if (Commands != null)
            {
                foreach (var pair in Commands)
                    commands[pair.Key] = pair.Value;
            }
            string? effectiveConfigurationName = normalizedName;
            if (normalizedName != null && Configurations != null)
            {
                var configurationPair = Configurations
                    .FirstOrDefault(pair => string.Equals(pair.Key, normalizedName, StringComparison.OrdinalIgnoreCase));
                ProjectConfigurationDefinition? configuration = configurationPair.Value;
                if (configuration?.Commands != null)
                {
                    effectiveConfigurationName = configurationPair.Key;
                    foreach (var pair in configuration.Commands)
                        commands[pair.Key] = pair.Value;
                }
            }

            return this with
            {
                Commands = commands,
                ActiveConfiguration = effectiveConfigurationName,
            };
        }
    }

    public sealed record ProjectCommandDefinition(string Command, string? WorkingDirectory = null);

    public sealed record ProjectConfigurationDefinition(
        IReadOnlyDictionary<string, ProjectCommandDefinition> Commands);

    public sealed record ProjectCommandInvocation(
        ProjectDefinition Project,
        string CapabilityId,
        string Command,
        string WorkingDirectory);

    public sealed class ProjectItemRules
    {
        private readonly List<Regex> _excludePatterns;
        private readonly List<Regex> _includePatterns;

        public IReadOnlyList<string> Exclude { get; }
        public IReadOnlyList<string> Include { get; }

        public ProjectItemRules(IEnumerable<string>? exclude, IEnumerable<string>? include = null)
        {
            Exclude = exclude == null
                ? Array.Empty<string>()
                : exclude
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(NormalizePattern)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            Include = include == null
                ? Array.Empty<string>()
                : include
                    .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                    .Select(NormalizePattern)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            _excludePatterns = Exclude.Select(CreateExcludeRegex).ToList();
            _includePatterns = Include.Select(CreateExcludeRegex).ToList();
        }

        public bool Includes(DirectoryInfo projectDirectory, string fullPath)
        {
            if (!TryGetRelativePath(projectDirectory, fullPath, out string relativePath))
                return false;

            int includeSpecificity = GetBestMatchSpecificity(Include, _includePatterns, relativePath);
            int excludeSpecificity = GetBestMatchSpecificity(Exclude, _excludePatterns, relativePath);
            if (includeSpecificity < 0)
                return excludeSpecificity < 0;
            if (excludeSpecificity < 0)
                return true;
            return includeSpecificity >= excludeSpecificity;
        }

        public bool IsVisible(DirectoryInfo projectDirectory, string fullPath)
        {
            if (Includes(projectDirectory, fullPath))
                return true;
            if (!Directory.Exists(fullPath)
                || !TryGetRelativePath(projectDirectory, fullPath, out string relativePath))
            {
                return false;
            }

            string directoryPrefix = $"{relativePath.TrimEnd('/')}" + "/";
            return Include.Any(pattern =>
            {
                int wildcardIndex = pattern.IndexOfAny(['*', '?']);
                string literalPrefix = wildcardIndex < 0 ? pattern : pattern[..wildcardIndex];
                return literalPrefix.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase);
            });
        }

        internal static bool IsExcluded(string relativePath, IEnumerable<string> exclude)
        {
            string normalizedPath = relativePath.Replace('\\', '/').TrimStart('/');
            return exclude
                .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
                .Select(pattern => CreateExcludeRegex(NormalizePattern(pattern)))
                .Any(pattern => pattern.IsMatch(normalizedPath));
        }

        private static string NormalizePattern(string pattern)
        {
            string normalized = pattern.Trim().Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized[2..];
            normalized = normalized.Trim('/');
            if (normalized.EndsWith("/**", StringComparison.Ordinal))
                normalized = normalized[..^3].TrimEnd('/');
            return normalized;
        }

        private static bool TryGetRelativePath(
            DirectoryInfo projectDirectory,
            string fullPath,
            out string relativePath)
        {
            try
            {
                string candidate = Path.GetRelativePath(projectDirectory.FullName, fullPath);
                if (string.Equals(candidate, ".", StringComparison.Ordinal))
                {
                    relativePath = string.Empty;
                    return true;
                }
                if (Path.IsPathRooted(candidate)
                    || string.Equals(candidate, "..", StringComparison.Ordinal)
                    || candidate.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || candidate.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    relativePath = string.Empty;
                    return false;
                }

                relativePath = candidate.Replace('\\', '/');
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                relativePath = string.Empty;
                return false;
            }
        }

        private static Regex CreateExcludeRegex(string pattern)
        {
            var expression = new StringBuilder("^");
            for (int index = 0; index < pattern.Length; index++)
            {
                char value = pattern[index];
                if (value == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
                {
                    index++;
                    if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                    {
                        index++;
                        expression.Append("(?:.*/)?");
                    }
                    else
                    {
                        expression.Append(".*");
                    }
                }
                else if (value == '*')
                {
                    expression.Append("[^/]*");
                }
                else if (value == '?')
                {
                    expression.Append("[^/]");
                }
                else
                {
                    expression.Append(Regex.Escape(value.ToString()));
                }
            }
            expression.Append("(?:/.*)?$");
            return new Regex(expression.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static int GetBestMatchSpecificity(
            IReadOnlyList<string> patterns,
            List<Regex> expressions,
            string relativePath)
        {
            int best = -1;
            for (int index = 0; index < expressions.Count; index++)
            {
                if (!expressions[index].IsMatch(relativePath))
                    continue;

                string pattern = patterns[index];
                int literalCount = pattern.Count(value => value is not '*' and not '?');
                int specificity = literalCount + (pattern.IndexOfAny(['*', '?']) < 0 ? 10_000 : 0);
                best = Math.Max(best, specificity);
            }
            return best;
        }
    }

    public sealed record ProjectCapabilityDescriptor(string Id, string Header, int Order = 0);

    public static class ProjectCapabilityIds
    {
        public const string Build = "build";
        public const string Run = "run";
        public const string Debug = "debug";
    }

    /// <summary>
    /// Loads one concrete project-file format. Providers own parsing and later
    /// may supply build/debug capabilities without coupling them to tree nodes.
    /// </summary>
    public interface IProjectProvider
    {
        string Id { get; }

        bool CanLoad(FileInfo projectFile);

        ProjectDefinition Load(FileInfo projectFile);

        Task<ProjectDefinition> LoadAsync(
            FileInfo projectFile,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Load(projectFile), cancellationToken);
        }
    }

    public sealed record ProjectLoadResult(
        bool Succeeded,
        ProjectDefinition? Project = null,
        string ErrorMessage = "");

    /// <summary>
    /// Declares the file-name patterns owned by a project provider. Open
    /// routing and project discovery use these patterns before asking the
    /// provider to parse the file, so project formats are no longer coupled to
    /// the built-in .cvproj extension.
    /// </summary>
    public interface IProjectFileFormatProvider
    {
        IReadOnlyList<string> ProjectFilePatterns { get; }
    }

    /// <summary>
    /// Optional execution surface implemented by providers that expose build,
    /// run, debug, or custom project-level actions.
    /// </summary>
    public interface IProjectCapabilityProvider
    {
        IReadOnlyList<ProjectCapabilityDescriptor> GetCapabilities(ProjectDefinition project);

        bool CanExecuteCapability(ProjectDefinition project, string capabilityId);

        bool ExecuteCapability(ProjectDefinition project, string capabilityId);
    }

    /// <summary>
    /// Optional command projection used when multiple project operations need
    /// to be planned and submitted to the terminal as one ordered batch.
    /// </summary>
    public interface IProjectCommandProvider
    {
        bool TryCreateInvocation(ProjectDefinition project, string capabilityId, out ProjectCommandInvocation? invocation);
    }

    public interface IProjectItemMutationProvider
    {
        bool CanChangeItemMembership(ProjectDefinition project, string fullPath);

        bool TrySetItemMembership(
            ProjectDefinition project,
            IReadOnlyList<string> fullPaths,
            bool included,
            out ProjectDefinition? updatedProject,
            out string errorMessage);
    }

    public interface IProjectDependencyMutationProvider
    {
        bool CanChangeDependencies(ProjectDefinition project);

        bool TrySetDependencies(
            ProjectDefinition project,
            IReadOnlyList<string> dependencies,
            out ProjectDefinition? updatedProject,
            out string errorMessage);
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ProjectProviderAttribute : Attribute
    {
        public int Priority { get; }

        public ProjectProviderAttribute(int priority = 0)
        {
            Priority = priority;
        }
    }
}
