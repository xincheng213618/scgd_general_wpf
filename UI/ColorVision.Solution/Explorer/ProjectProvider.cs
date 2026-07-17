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
    }

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

    /// <summary>
    /// Backward-compatible provider for the existing folder project format.
    /// ProjectType was absent in older files and defaults to "folder".
    /// </summary>
    [ProjectProvider(int.MinValue)]
    public sealed class FolderProjectProvider : IProjectProvider, IProjectFileFormatProvider, IProjectCapabilityProvider, IProjectCommandProvider, IProjectItemMutationProvider, IProjectDependencyMutationProvider
    {
        public const string ProviderId = "colorvision.folder-project";

        public string Id => ProviderId;
        public IReadOnlyList<string> ProjectFilePatterns { get; } = ["*.cvproj"];

        public static void CreateProjectFile(
            string projectFilePath,
            string projectName,
            string version = "1.0",
            IReadOnlyDictionary<string, ProjectCommandDefinition>? commands = null,
            string? rootPath = null,
            IReadOnlyList<string>? excludedPaths = null,
            IReadOnlyList<string>? dependencies = null,
            IReadOnlyList<string>? includedPaths = null,
            IReadOnlyDictionary<string, ProjectConfigurationDefinition>? configurations = null)
        {
            var json = new JObject
            {
                ["ProjectType"] = "folder",
                ["Name"] = projectName,
                ["Version"] = version
            };
            if (!string.IsNullOrWhiteSpace(rootPath))
                json["RootPath"] = rootPath;
            if (excludedPaths?.Count > 0 || includedPaths?.Count > 0)
            {
                var items = new JObject();
                if (excludedPaths?.Count > 0)
                    items["Exclude"] = new JArray(excludedPaths);
                if (includedPaths?.Count > 0)
                    items["Include"] = new JArray(includedPaths);
                json["Items"] = items;
            }
            if (dependencies?.Count > 0)
                json["Dependencies"] = new JArray(dependencies);
            if (commands?.Count > 0)
                json["Commands"] = WriteCommands(commands);
            if (configurations?.Count > 0)
            {
                var configurationJson = new JObject();
                foreach (var pair in configurations)
                {
                    if (string.IsNullOrWhiteSpace(pair.Key))
                        continue;
                    configurationJson[pair.Key.Trim()] = new JObject
                    {
                        ["Commands"] = WriteCommands(pair.Value.Commands),
                    };
                }
                if (configurationJson.HasValues)
                    json["Configurations"] = configurationJson;
            }
            File.WriteAllText(projectFilePath, json.ToString());
        }

        public bool CanLoad(FileInfo projectFile)
        {
            if (!projectFile.Exists
                || !string.Equals(projectFile.Extension, ".cvproj", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                var json = JObject.Parse(File.ReadAllText(projectFile.FullName));
                string? projectType = json.Value<string>("ProjectType");
                return string.IsNullOrWhiteSpace(projectType)
                    || string.Equals(projectType, "folder", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(projectType, ProviderId, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // The extension still belongs to this provider. Load() returns a
                // visible error project instead of silently treating it as a folder.
                return true;
            }
        }

        public ProjectDefinition Load(FileInfo projectFile)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(projectFile.Name);
            try
            {
                var json = JObject.Parse(File.ReadAllText(projectFile.FullName));
                string name = json.Value<string>("Name")?.Trim() ?? fallbackName;
                string version = json.Value<string>("Version")?.Trim() ?? string.Empty;
                DirectoryInfo rootDirectory = ResolveProjectDirectory(projectFile, json.Value<string>("RootPath"), out string? rootError);
                return new ProjectDefinition(
                    Id,
                    name,
                    version,
                    projectFile,
                    LoadError: rootError,
                    Commands: ReadCommands(json),
                    RootDirectory: rootDirectory,
                    ItemRules: ReadItemRules(json),
                    Dependencies: ReadStringList(json["Dependencies"]),
                    Configurations: ReadConfigurations(json));
            }
            catch (Exception ex)
            {
                return new ProjectDefinition(Id, fallbackName, string.Empty, projectFile, ex.Message);
            }
        }

        public IReadOnlyList<ProjectCapabilityDescriptor> GetCapabilities(ProjectDefinition project)
        {
            if (project.Commands == null)
                return Array.Empty<ProjectCapabilityDescriptor>();

            return project.Commands.Keys
                .Select(id => new ProjectCapabilityDescriptor(id, GetCapabilityHeader(id), GetCapabilityOrder(id)))
                .OrderBy(capability => capability.Order)
                .ThenBy(capability => capability.Header, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        public bool CanExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            if (!TryGetCommand(project, capabilityId, out ProjectCommandDefinition command))
                return false;

            try
            {
                string workingDirectory = ResolveWorkingDirectory(project, command);
                return !string.IsNullOrWhiteSpace(command.Command) && Directory.Exists(workingDirectory);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public bool ExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            if (!TryCreateInvocation(project, capabilityId, out ProjectCommandInvocation? invocation)
                || invocation == null)
                return false;

            return TerminalService.GetInstance().TrySendCommand(invocation.Command, invocation.WorkingDirectory);
        }

        public bool TryCreateInvocation(ProjectDefinition project, string capabilityId, out ProjectCommandInvocation? invocation)
        {
            invocation = null;
            if (!TryGetCommand(project, capabilityId, out ProjectCommandDefinition command)
                || !CanExecuteCapability(project, capabilityId))
                return false;

            try
            {
                string expandedCommand = command.Command
                    .Replace("{ProjectDir}", project.ProjectDirectory.FullName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{ProjectFile}", project.ProjectFile.FullName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Configuration}", project.ActiveConfiguration ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                invocation = new ProjectCommandInvocation(
                    project,
                    capabilityId,
                    expandedCommand,
                    ResolveWorkingDirectory(project, command));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public bool CanChangeItemMembership(ProjectDefinition project, string fullPath)
        {
            project.ProjectFile.Refresh();
            return project.ProjectFile.Exists
                && !project.ProjectFile.IsReadOnly
                && TryGetProjectRelativePath(project, fullPath, out _);
        }

        public bool TrySetItemMembership(
            ProjectDefinition project,
            IReadOnlyList<string> fullPaths,
            bool included,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            updatedProject = null;
            errorMessage = string.Empty;
            if (fullPaths.Count == 0)
            {
                errorMessage = "没有可更新的项目项。";
                return false;
            }

            var projectItems = new List<(string FullPath, string RelativePath)>();
            foreach (string fullPath in fullPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!CanChangeItemMembership(project, fullPath)
                    || !TryGetProjectRelativePath(project, fullPath, out string relativePath))
                {
                    errorMessage = $"“{fullPath}”不属于项目“{project.Name}”，或项目文件不可写。";
                    return false;
                }
                projectItems.Add((Path.GetFullPath(fullPath), relativePath));
            }

            string? temporaryPath = null;
            try
            {
                var json = JObject.Parse(File.ReadAllText(project.ProjectFile.FullName));
                var items = json["Items"] as JObject ?? new JObject();
                var excludes = ReadStringList(items["Exclude"] ?? json["Exclude"]);
                var includes = ReadStringList(items["Include"] ?? json["Include"]);

                foreach (var item in projectItems)
                {
                    RemovePath(excludes, item.RelativePath);
                    RemovePath(includes, item.RelativePath);
                    if (included)
                        includes.Add(item.RelativePath);
                    else
                        excludes.Add(item.RelativePath);
                }

                WriteStringList(items, "Exclude", excludes);
                WriteStringList(items, "Include", includes);
                json.Remove("Exclude");
                json.Remove("Include");
                if (items.HasValues)
                    json["Items"] = items;
                else
                    json.Remove("Items");

                temporaryPath = $"{project.ProjectFile.FullName}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(temporaryPath, json.ToString());
                File.Move(temporaryPath, project.ProjectFile.FullName, overwrite: true);
                temporaryPath = null;
                updatedProject = Load(new FileInfo(project.ProjectFile.FullName));
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or Newtonsoft.Json.JsonException
                or ArgumentException
                or NotSupportedException)
            {
                errorMessage = $"更新项目文件失败：{ex.Message}";
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public bool CanChangeDependencies(ProjectDefinition project)
        {
            project.ProjectFile.Refresh();
            return project.ProjectFile.Exists && !project.ProjectFile.IsReadOnly;
        }

        public bool TrySetDependencies(
            ProjectDefinition project,
            IReadOnlyList<string> dependencies,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            updatedProject = null;
            errorMessage = string.Empty;
            if (!CanChangeDependencies(project))
            {
                errorMessage = $"项目“{project.Name}”的项目文件不存在或不可写。";
                return false;
            }

            List<string> normalizedDependencies = dependencies
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference.Trim().Replace('\\', '/'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (normalizedDependencies.Any(reference => IsSelfDependency(project, reference)))
            {
                errorMessage = $"项目“{project.Name}”不能依赖自身。";
                return false;
            }

            string? temporaryPath = null;
            try
            {
                var json = JObject.Parse(File.ReadAllText(project.ProjectFile.FullName));
                if (normalizedDependencies.Count == 0)
                    json.Remove("Dependencies");
                else
                    json["Dependencies"] = new JArray(normalizedDependencies);

                temporaryPath = $"{project.ProjectFile.FullName}.{Guid.NewGuid():N}.tmp";
                File.WriteAllText(temporaryPath, json.ToString());
                File.Move(temporaryPath, project.ProjectFile.FullName, overwrite: true);
                temporaryPath = null;
                updatedProject = Load(new FileInfo(project.ProjectFile.FullName));
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or Newtonsoft.Json.JsonException
                or ArgumentException
                or NotSupportedException)
            {
                errorMessage = $"更新项目依赖失败：{ex.Message}";
                return false;
            }
            finally
            {
                if (temporaryPath != null)
                {
                    try
                    {
                        File.Delete(temporaryPath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static Dictionary<string, ProjectCommandDefinition> ReadCommands(JObject json)
        {
            if (json["Commands"] is not JObject commands)
                return new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase);

            return ReadCommandObject(commands);
        }

        private static Dictionary<string, ProjectCommandDefinition> ReadCommandObject(JObject commands)
        {
            var result = new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in commands.Properties())
            {
                if (property.Value.Type == JTokenType.String)
                {
                    string? commandText = property.Value.Value<string>();
                    if (!string.IsNullOrWhiteSpace(commandText))
                        result[property.Name] = new ProjectCommandDefinition(commandText);
                    continue;
                }

                if (property.Value is not JObject commandObject)
                    continue;

                string? command = commandObject.Value<string>("Command");
                if (!string.IsNullOrWhiteSpace(command))
                    result[property.Name] = new ProjectCommandDefinition(command, commandObject.Value<string>("WorkingDirectory"));
            }
            return result;
        }

        private static Dictionary<string, ProjectConfigurationDefinition> ReadConfigurations(JObject json)
        {
            var result = new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase);
            if (json["Configurations"] is not JObject configurations)
                return result;

            foreach (var property in configurations.Properties())
            {
                if (property.Value is not JObject configurationObject
                    || configurationObject["Commands"] is not JObject commands)
                {
                    continue;
                }

                result[property.Name] = new ProjectConfigurationDefinition(ReadCommandObject(commands));
            }
            return result;
        }

        private static JObject WriteCommands(IReadOnlyDictionary<string, ProjectCommandDefinition> commands)
        {
            var result = new JObject();
            foreach (var pair in commands)
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || string.IsNullOrWhiteSpace(pair.Value.Command))
                {
                    continue;
                }

                result[pair.Key] = new JObject
                {
                    ["Command"] = pair.Value.Command,
                    ["WorkingDirectory"] = pair.Value.WorkingDirectory,
                };
            }
            return result;
        }

        private static ProjectItemRules ReadItemRules(JObject json)
        {
            var items = json["Items"] as JObject;
            return new ProjectItemRules(
                ReadStringList(items?["Exclude"] ?? json["Exclude"]),
                ReadStringList(items?["Include"] ?? json["Include"]));
        }

        private static List<string> ReadStringList(JToken? token)
        {
            IEnumerable<string> values = token switch
            {
                JArray array => array.Values<string>().OfType<string>(),
                JValue value when value.Type == JTokenType.String => [value.Value<string>()!],
                _ => Array.Empty<string>(),
            };
            return values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryGetProjectRelativePath(
            ProjectDefinition project,
            string fullPath,
            out string relativePath)
        {
            relativePath = string.Empty;
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            try
            {
                string normalizedPath = Path.GetFullPath(fullPath);
                string candidate = Path.GetRelativePath(project.ProjectDirectory.FullName, normalizedPath);
                if (string.Equals(candidate, ".", StringComparison.Ordinal)
                    || Path.IsPathRooted(candidate)
                    || string.Equals(candidate, "..", StringComparison.Ordinal)
                    || candidate.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || candidate.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    return false;
                }

                relativePath = candidate.Replace('\\', '/').Trim('/');
                return !string.IsNullOrWhiteSpace(relativePath)
                    && !SolutionNodeFactory.IsInternalFile(Path.GetFileName(normalizedPath));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool IsSelfDependency(ProjectDefinition project, string dependencyReference)
        {
            try
            {
                string baseDirectory = project.ProjectFile.Directory?.FullName
                    ?? project.ProjectDirectory.FullName;
                string dependencyPath = Path.GetFullPath(Path.IsPathRooted(dependencyReference)
                    ? dependencyReference
                    : Path.Combine(baseDirectory, dependencyReference));
                return string.Equals(dependencyPath, project.ProjectFile.FullName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        Path.TrimEndingDirectorySeparator(dependencyPath),
                        Path.TrimEndingDirectorySeparator(project.ProjectDirectory.FullName),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static void RemovePath(List<string> paths, string relativePath)
        {
            paths.RemoveAll(path => string.Equals(
                path.Replace('\\', '/').Trim('/'),
                relativePath,
                StringComparison.OrdinalIgnoreCase));
        }

        private static void WriteStringList(JObject parent, string propertyName, List<string> values)
        {
            if (values.Count == 0)
            {
                parent.Remove(propertyName);
                return;
            }

            parent[propertyName] = new JArray(values.Distinct(StringComparer.OrdinalIgnoreCase));
        }

        private static DirectoryInfo ResolveProjectDirectory(FileInfo projectFile, string? rootPath, out string? loadError)
        {
            DirectoryInfo projectFileDirectory = projectFile.Directory
                ?? throw new InvalidOperationException("项目文件没有有效的父目录。");
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                loadError = null;
                return projectFileDirectory;
            }

            string fullPath = Path.GetFullPath(Path.IsPathRooted(rootPath)
                ? rootPath
                : Path.Combine(projectFileDirectory.FullName, rootPath));
            var rootDirectory = new DirectoryInfo(fullPath);
            loadError = rootDirectory.Exists ? null : $"项目根目录不存在: {rootDirectory.FullName}";
            return rootDirectory;
        }

        private static bool TryGetCommand(ProjectDefinition project, string capabilityId, out ProjectCommandDefinition command)
        {
            if (project.Commands != null
                && project.Commands.TryGetValue(capabilityId, out ProjectCommandDefinition? configuredCommand)
                && configuredCommand != null)
            {
                command = configuredCommand;
                return true;
            }

            command = null!;
            return false;
        }

        private static string ResolveWorkingDirectory(ProjectDefinition project, ProjectCommandDefinition command)
        {
            if (string.IsNullOrWhiteSpace(command.WorkingDirectory))
                return project.ProjectDirectory.FullName;

            return Path.GetFullPath(Path.IsPathRooted(command.WorkingDirectory)
                ? command.WorkingDirectory
                : Path.Combine(project.ProjectDirectory.FullName, command.WorkingDirectory));
        }

        private static string GetCapabilityHeader(string capabilityId)
        {
            return capabilityId.ToLowerInvariant() switch
            {
                ProjectCapabilityIds.Build => "生成项目(_B)",
                ProjectCapabilityIds.Run => "运行项目(_R)",
                ProjectCapabilityIds.Debug => "调试项目(_D)",
                _ => capabilityId,
            };
        }

        private static int GetCapabilityOrder(string capabilityId)
        {
            return capabilityId.ToLowerInvariant() switch
            {
                ProjectCapabilityIds.Build => 10,
                ProjectCapabilityIds.Run => 20,
                ProjectCapabilityIds.Debug => 30,
                _ => 100,
            };
        }
    }

    public static class ProjectProviderRegistry
    {
        private sealed record Registration(IProjectProvider Provider, int Priority);

        private static readonly List<Registration> _providers = new();
        private static readonly HashSet<Assembly> _registeredAssemblies = new();
        private static readonly object _syncRoot = new();
        private static string[] _projectFilePatterns = [];
        private static bool _initialized;
        private static bool _assemblyLoadSubscribed;

        public static event EventHandler? ProvidersChanged;

        public static void Initialize()
        {
            if (_initialized)
                return;

            bool changed = false;
            lock (_syncRoot)
            {
                if (_initialized)
                    return;

                if (!_assemblyLoadSubscribed)
                {
                    AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
                    _assemblyLoadSubscribed = true;
                }

                Assembly[] assemblies = AssemblyService.Instance?.GetAssemblies()
                    ?? AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in assemblies)
                    changed |= RegisterProvidersFromAssemblyCore(assembly);
                _initialized = true;
            }

            if (changed)
                ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Register(IProjectProvider provider, int priority = 0)
        {
            ArgumentNullException.ThrowIfNull(provider);
            if (string.IsNullOrWhiteSpace(provider.Id))
                throw new ArgumentException("项目 Provider Id 不允许为空。", nameof(provider));
            lock (_syncRoot)
            {
                RegisterCore(provider, priority);
            }
            ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void CurrentDomain_AssemblyLoad(object? sender, AssemblyLoadEventArgs e)
        {
            bool changed;
            lock (_syncRoot)
                changed = RegisterProvidersFromAssemblyCore(e.LoadedAssembly);
            if (changed)
                ProvidersChanged?.Invoke(null, EventArgs.Empty);
        }

        private static bool RegisterProvidersFromAssemblyCore(Assembly assembly)
        {
            if (!_registeredAssemblies.Add(assembly))
                return false;

            bool changed = false;
            foreach (Type type in GetLoadableTypes(assembly))
            {
                var attribute = type.GetCustomAttribute<ProjectProviderAttribute>();
                if (attribute == null
                    || !typeof(IProjectProvider).IsAssignableFrom(type)
                    || type.IsAbstract
                    || type.IsInterface)
                {
                    continue;
                }

                try
                {
                    RegisterCore((IProjectProvider)Activator.CreateInstance(type)!, attribute.Priority);
                    changed = true;
                }
                catch
                {
                }
            }
            return changed;
        }

        private static void RegisterCore(IProjectProvider provider, int priority)
        {
            _providers.RemoveAll(item => string.Equals(item.Provider.Id, provider.Id, StringComparison.OrdinalIgnoreCase));
            _providers.Add(new Registration(provider, priority));
            _providers.Sort((left, right) => right.Priority.CompareTo(left.Priority));
            _projectFilePatterns = CreateProjectFilePatterns();
        }

        public static bool TryLoadProject(DirectoryInfo directory, out ProjectDefinition? project)
        {
            return TryLoadProject(directory, out project, out _);
        }

        public static bool TryLoadProject(
            DirectoryInfo directory,
            out ProjectDefinition? project,
            out string errorMessage)
        {
            project = null;
            errorMessage = string.Empty;
            if (!directory.Exists)
            {
                errorMessage = $"项目目录不存在：{directory.FullName}";
                return false;
            }

            var errors = new List<string>();
            foreach (FileInfo projectFile in EnumerateProjectFiles(directory, SearchOption.TopDirectoryOnly))
            {
                if (TryLoadProject(projectFile, out project, out string loadError))
                    return true;
                if (!string.IsNullOrWhiteSpace(loadError))
                    errors.Add(loadError);
            }
            errorMessage = errors.Count > 0
                ? string.Join(Environment.NewLine, errors)
                : $"目录“{directory.FullName}”中没有已注册 Provider 支持的项目文件。";
            return false;
        }

        public static bool TryLoadProject(FileInfo projectFile, out ProjectDefinition? project)
        {
            return TryLoadProject(projectFile, out project, out _);
        }

        public static bool TryLoadProject(
            FileInfo projectFile,
            out ProjectDefinition? project,
            out string errorMessage)
        {
            Initialize();
            project = null;
            errorMessage = string.Empty;
            projectFile.Refresh();
            if (!projectFile.Exists)
            {
                errorMessage = $"项目文件不存在：{projectFile.FullName}";
                return false;
            }

            var providerErrors = new List<string>();
            lock (_syncRoot)
            {
                foreach (Registration registration in _providers)
                {
                    bool canLoad;
                    try
                    {
                        canLoad = registration.Provider.CanLoad(projectFile);
                    }
                    catch (Exception ex)
                    {
                        providerErrors.Add($"Provider“{registration.Provider.Id}”识别失败：{ex.Message}");
                        continue;
                    }
                    if (!canLoad)
                        continue;

                    try
                    {
                        project = registration.Provider.Load(projectFile);
                        if (project != null)
                            return true;
                        providerErrors.Add($"Provider“{registration.Provider.Id}”没有返回项目定义。");
                    }
                    catch (Exception ex)
                    {
                        providerErrors.Add($"Provider“{registration.Provider.Id}”加载失败：{ex.Message}");
                    }
                    break;
                }
            }

            string? declaredProviderId = GetDeclaredProviderId(projectFile);
            errorMessage = providerErrors.Count > 0
                ? string.Join(Environment.NewLine, providerErrors)
                : IsSupportedProjectFilePath(projectFile.FullName)
                    ? $"没有已安装的项目 Provider 能加载“{projectFile.Name}”。"
                        + (declaredProviderId == null ? string.Empty : $"项目声明需要 Provider“{declaredProviderId}”；")
                        + "可能缺少对应插件，或项目类型标识不受支持。"
                    : $"没有项目 Provider 声明支持文件“{projectFile.Name}”。";
            return false;
        }

        /// <summary>
        /// Reads the provider identity declared by the built-in .cvproj
        /// envelope without attempting to load the project. This keeps missing
        /// plugin diagnostics actionable while leaving other formats opaque.
        /// </summary>
        public static string? GetDeclaredProviderId(FileInfo projectFile)
        {
            ArgumentNullException.ThrowIfNull(projectFile);
            projectFile.Refresh();
            if (!projectFile.Exists
                || !string.Equals(projectFile.Extension, ".cvproj", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                string? projectType = JObject.Parse(File.ReadAllText(projectFile.FullName))
                    .Value<string>("ProjectType")?
                    .Trim();
                if (string.IsNullOrWhiteSpace(projectType))
                    return FolderProjectProvider.ProviderId;
                return string.Equals(projectType, "folder", StringComparison.OrdinalIgnoreCase)
                    ? FolderProjectProvider.ProviderId
                    : projectType;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or Newtonsoft.Json.JsonException)
            {
                return null;
            }
        }

        public static bool IsSupportedProjectFilePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            string fileName;
            try
            {
                fileName = Path.GetFileName(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            return GetProjectFilePatternSnapshot().Any(pattern =>
                System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                    pattern,
                    fileName,
                    ignoreCase: true));
        }

        public static IReadOnlyList<string> GetProjectFilePatterns()
        {
            return GetProjectFilePatternSnapshot().ToArray();
        }

        public static IEnumerable<FileInfo> EnumerateProjectFiles(
            DirectoryInfo directory,
            SearchOption searchOption)
        {
            if (!directory.Exists)
                return Array.Empty<FileInfo>();

            var files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (string pattern in GetProjectFilePatterns())
            {
                try
                {
                    foreach (FileInfo file in directory.EnumerateFiles(pattern, searchOption))
                        files.TryAdd(file.FullName, file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }
            return files.Values.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string GetProjectFileDialogPattern()
        {
            IReadOnlyList<string> patterns = GetProjectFilePatterns();
            return patterns.Count == 0 ? "*.*" : string.Join(';', patterns);
        }

        private static string? NormalizeProjectFilePattern(string? pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return null;

            string normalized = pattern.Trim();
            if (normalized.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
                return null;
            if (!normalized.Contains('*') && !normalized.Contains('?'))
                normalized = normalized.StartsWith('.') ? $"*{normalized}" : $"*.{normalized.TrimStart('.')}";
            return normalized;
        }

        private static string[] GetProjectFilePatternSnapshot()
        {
            Initialize();
            return Volatile.Read(ref _projectFilePatterns);
        }

        private static string[] CreateProjectFilePatterns()
        {
            return _providers
                .Select(registration => registration.Provider)
                .OfType<IProjectFileFormatProvider>()
                .SelectMany(provider => provider.ProjectFilePatterns ?? Array.Empty<string>())
                .Select(NormalizeProjectFilePattern)
                .Where(pattern => pattern != null)
                .Select(pattern => pattern!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pattern => pattern, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static IReadOnlyList<ProjectCapabilityDescriptor> GetCapabilities(ProjectDefinition project)
        {
            return FindCapabilityProvider(project.ProviderId)?.GetCapabilities(project)
                ?? Array.Empty<ProjectCapabilityDescriptor>();
        }

        public static bool CanExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            return FindCapabilityProvider(project.ProviderId)?.CanExecuteCapability(project, capabilityId) == true;
        }

        public static bool ExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            return FindCapabilityProvider(project.ProviderId)?.ExecuteCapability(project, capabilityId) == true;
        }

        public static bool HasCapability(ProjectDefinition project, string capabilityId)
        {
            return GetCapabilities(project).Any(capability =>
                string.Equals(capability.Id, capabilityId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool TryCreateCapabilityInvocation(
            ProjectDefinition project,
            string capabilityId,
            out ProjectCommandInvocation? invocation)
        {
            Initialize();
            lock (_syncRoot)
            {
                IProjectCommandProvider? provider = _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, project.ProviderId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectCommandProvider;
                if (provider != null)
                    return provider.TryCreateInvocation(project, capabilityId, out invocation);
            }

            invocation = null;
            return false;
        }

        public static bool CanChangeProjectItemMembership(ProjectDefinition project, string fullPath)
        {
            return FindItemMutationProvider(project.ProviderId)?.CanChangeItemMembership(project, fullPath) == true;
        }

        public static bool TrySetProjectItemMembership(
            ProjectDefinition project,
            IReadOnlyList<string> fullPaths,
            bool included,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            IProjectItemMutationProvider? provider = FindItemMutationProvider(project.ProviderId);
            if (provider != null)
            {
                return provider.TrySetItemMembership(
                    project,
                    fullPaths,
                    included,
                    out updatedProject,
                    out errorMessage);
            }

            updatedProject = null;
            errorMessage = $"项目 Provider“{project.ProviderId}”不支持修改项目项。";
            return false;
        }

        public static bool CanChangeProjectDependencies(ProjectDefinition project)
        {
            return FindDependencyMutationProvider(project.ProviderId)?.CanChangeDependencies(project) == true;
        }

        public static bool TrySetProjectDependencies(
            ProjectDefinition project,
            IReadOnlyList<string> dependencies,
            out ProjectDefinition? updatedProject,
            out string errorMessage)
        {
            IProjectDependencyMutationProvider? provider = FindDependencyMutationProvider(project.ProviderId);
            if (provider != null)
            {
                return provider.TrySetDependencies(
                    project,
                    dependencies,
                    out updatedProject,
                    out errorMessage);
            }

            updatedProject = null;
            errorMessage = $"项目 Provider“{project.ProviderId}”不支持修改项目依赖。";
            return false;
        }

        private static IProjectItemMutationProvider? FindItemMutationProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectItemMutationProvider;
            }
        }

        private static IProjectDependencyMutationProvider? FindDependencyMutationProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectDependencyMutationProvider;
            }
        }

        private static IProjectCapabilityProvider? FindCapabilityProvider(string providerId)
        {
            Initialize();
            lock (_syncRoot)
            {
                return _providers.FirstOrDefault(registration =>
                    string.Equals(registration.Provider.Id, providerId, StringComparison.OrdinalIgnoreCase))?.Provider
                    as IProjectCapabilityProvider;
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null)!;
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }
    }
}
