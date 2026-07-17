using ColorVision.UI;
using ColorVision.Solution.Terminal;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Solution.Explorer
{
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

}
