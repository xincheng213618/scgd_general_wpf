using ColorVision.Solution.Terminal;
using System.IO;
using System.Xml.Linq;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Read-only adapter for standard .NET and Visual C++ MSBuild projects.
    /// It deliberately avoids evaluating imports or modifying the project file;
    /// full MSBuild evaluation can be supplied later by a dedicated integration.
    /// </summary>
    [ProjectProvider(100)]
    public sealed class MsBuildProjectProvider : IProjectProvider, IProjectFileFormatProvider, IProjectCapabilityProvider, IProjectCommandProvider
    {
        public const string ProviderId = "msbuild.project";

        private static readonly string[] SupportedExtensions = [".csproj", ".fsproj", ".vbproj", ".vcxproj"];
        private static readonly ProjectItemRules DefaultItemRules = new(
            [
                "bin", "bin/**",
                "obj", "obj/**",
                ".vs", ".vs/**",
                ".git", ".git/**",
                ".idea", ".idea/**",
            ]);

        public string Id => ProviderId;
        public IReadOnlyList<string> ProjectFilePatterns { get; } = ["*.csproj", "*.fsproj", "*.vbproj", "*.vcxproj"];

        public bool CanLoad(FileInfo projectFile)
        {
            return projectFile.Exists
                && SupportedExtensions.Contains(projectFile.Extension, StringComparer.OrdinalIgnoreCase);
        }

        public ProjectDefinition Load(FileInfo projectFile)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(projectFile.Name);
            try
            {
                XDocument document = XDocument.Load(projectFile.FullName, LoadOptions.None);
                if (document.Root == null
                    || !string.Equals(document.Root.Name.LocalName, "Project", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("文件不是有效的 MSBuild Project 文档。");
                }

                bool isSdkStyle = GetSdkNames(document).Count > 0;
                bool isVisualCpp = string.Equals(projectFile.Extension, ".vcxproj", StringComparison.OrdinalIgnoreCase);
                bool isRunnable = IsRunnableProject(document, isSdkStyle);
                var commands = new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectCapabilityIds.Build] = new(CreateBuildCommand(isSdkStyle)),
                };
                if (isSdkStyle && isRunnable)
                    commands[ProjectCapabilityIds.Run] = new("dotnet run --project \"{ProjectFile}\" --configuration \"{Configuration}\"");

                string name = GetLiteralProperty(document, "AssemblyName")
                    ?? GetLiteralProperty(document, "RootNamespace")
                    ?? fallbackName;
                string version = GetLiteralProperty(document, "Version")
                    ?? GetLiteralProperty(document, "VersionPrefix")
                    ?? string.Empty;
                return new ProjectDefinition(
                    Id,
                    name,
                    version,
                    projectFile,
                    Commands: commands,
                    RootDirectory: projectFile.Directory,
                    ItemRules: DefaultItemRules,
                    Dependencies: ReadProjectReferences(document),
                    Configurations: ReadConfigurations(document, isVisualCpp));
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or System.Xml.XmlException
                or InvalidDataException)
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
            return TryGetCommand(project, capabilityId, out _)
                && project.ProjectFile.Exists
                && project.ProjectDirectory.Exists;
        }

        public bool ExecuteCapability(ProjectDefinition project, string capabilityId)
        {
            return TryCreateInvocation(project, capabilityId, out ProjectCommandInvocation? invocation)
                && invocation != null
                && TerminalService.GetInstance().TrySendCommand(invocation.Command, invocation.WorkingDirectory);
        }

        public bool TryCreateInvocation(ProjectDefinition project, string capabilityId, out ProjectCommandInvocation? invocation)
        {
            invocation = null;
            if (!CanExecuteCapability(project, capabilityId)
                || !TryGetCommand(project, capabilityId, out ProjectCommandDefinition command))
            {
                return false;
            }

            try
            {
                string configuration = string.IsNullOrWhiteSpace(project.ActiveConfiguration)
                    ? "Debug"
                    : project.ActiveConfiguration;
                string? platform = null;
                if (string.Equals(project.ProjectFile.Extension, ".vcxproj", StringComparison.OrdinalIgnoreCase)
                    && TrySplitConfigurationPlatform(configuration, out string configurationName, out string platformName))
                {
                    configuration = configurationName;
                    platform = platformName;
                }
                string workingDirectory = string.IsNullOrWhiteSpace(command.WorkingDirectory)
                    ? project.ProjectDirectory.FullName
                    : Path.GetFullPath(Path.IsPathRooted(command.WorkingDirectory)
                        ? command.WorkingDirectory
                        : Path.Combine(project.ProjectDirectory.FullName, command.WorkingDirectory));
                string expandedCommand = command.Command
                    .Replace("{ProjectDir}", project.ProjectDirectory.FullName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{ProjectFile}", project.ProjectFile.FullName, StringComparison.OrdinalIgnoreCase)
                    .Replace("{Configuration}", configuration, StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(platform)
                    && string.Equals(capabilityId, ProjectCapabilityIds.Build, StringComparison.OrdinalIgnoreCase)
                    && !expandedCommand.Contains("/p:Platform=", StringComparison.OrdinalIgnoreCase))
                {
                    expandedCommand += $" /p:Platform=\"{platform}\"";
                }
                invocation = new ProjectCommandInvocation(project, capabilityId, expandedCommand, workingDirectory);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static string CreateBuildCommand(bool isSdkStyle)
        {
            return isSdkStyle
                ? "dotnet build \"{ProjectFile}\" --configuration \"{Configuration}\""
                : "msbuild \"{ProjectFile}\" /t:Build /p:Configuration=\"{Configuration}\"";
        }

        private static bool IsRunnableProject(XDocument document, bool isSdkStyle)
        {
            string? outputType = GetLiteralProperty(document, "OutputType");
            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(outputType, "WinExe", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return isSdkStyle && GetSdkNames(document).Any(sdk =>
                sdk.Contains(".Web", StringComparison.OrdinalIgnoreCase)
                || sdk.Contains(".Worker", StringComparison.OrdinalIgnoreCase));
        }

        private static List<string> GetSdkNames(XDocument document)
        {
            var sdkNames = new List<string>();
            string? rootSdk = document.Root?.Attribute("Sdk")?.Value;
            if (!string.IsNullOrWhiteSpace(rootSdk))
                sdkNames.Add(rootSdk.Trim());
            sdkNames.AddRange(document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "Sdk", StringComparison.OrdinalIgnoreCase))
                .Select(element => element.Attribute("Name")?.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()));
            return sdkNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string? GetLiteralProperty(XDocument document, string propertyName)
        {
            return document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, propertyName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(element => HasCondition(element) ? 1 : 0)
                .Select(element => element.Value.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)
                    && !value.Contains("$(", StringComparison.Ordinal));
        }

        private static bool HasCondition(XElement element)
        {
            return element.Attribute("Condition") != null
                || element.Parent?.Attribute("Condition") != null;
        }

        private static List<string> ReadProjectReferences(XDocument document)
        {
            return document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                .SelectMany(element => (element.Attribute("Include")?.Value ?? string.Empty)
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, ProjectConfigurationDefinition> ReadConfigurations(
            XDocument document,
            bool isVisualCpp)
        {
            if (isVisualCpp)
            {
                Dictionary<string, ProjectConfigurationDefinition> visualCppConfigurations =
                    ReadVisualCppConfigurations(document);
                if (visualCppConfigurations.Count > 0)
                    return visualCppConfigurations;
            }

            return ReadNamedConfigurations(document);
        }

        private static Dictionary<string, ProjectConfigurationDefinition> ReadNamedConfigurations(XDocument document)
        {
            List<string> configurations = document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "Configurations", StringComparison.OrdinalIgnoreCase))
                .SelectMany(element => element.Value.Split(
                    [';', ','],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(configuration => !configuration.Contains("$(", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (configurations.Count == 0)
                configurations.AddRange(["Debug", "Release"]);

            return configurations.ToDictionary(
                configuration => configuration,
                _ => new ProjectConfigurationDefinition(
                    new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);
        }

        private static Dictionary<string, ProjectConfigurationDefinition> ReadVisualCppConfigurations(XDocument document)
        {
            var entries = document.Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "ProjectConfiguration", StringComparison.OrdinalIgnoreCase))
                .Select(element =>
                {
                    string include = element.Attribute("Include")?.Value?.Trim() ?? string.Empty;
                    string[] includeParts = include.Split('|', 2, StringSplitOptions.TrimEntries);
                    string configuration = GetChildValue(element, "Configuration")
                        ?? includeParts.FirstOrDefault()
                        ?? string.Empty;
                    string platform = GetChildValue(element, "Platform")
                        ?? (includeParts.Length == 2 ? includeParts[1] : string.Empty);
                    return new { Configuration = configuration, Platform = platform };
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Configuration)
                    && !entry.Configuration.Contains("$(", StringComparison.Ordinal))
                .ToList();

            var result = new Dictionary<string, ProjectConfigurationDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var configurationGroup in entries.GroupBy(
                entry => entry.Configuration,
                StringComparer.OrdinalIgnoreCase))
            {
                string preferredPlatform = configurationGroup
                    .Where(entry => !string.IsNullOrWhiteSpace(entry.Platform))
                    .OrderBy(entry => GetPlatformPreference(entry.Platform))
                    .ThenBy(entry => entry.Platform, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => entry.Platform)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault() ?? string.Empty;
                result[configurationGroup.Key] = CreateVisualCppConfiguration(preferredPlatform);
            }
            return result;
        }

        private static ProjectConfigurationDefinition CreateVisualCppConfiguration(string platform)
        {
            var commands = new Dictionary<string, ProjectCommandDefinition>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(platform))
            {
                commands[ProjectCapabilityIds.Build] = new ProjectCommandDefinition(
                    $"msbuild \"{{ProjectFile}}\" /t:Build /p:Configuration=\"{{Configuration}}\" /p:Platform=\"{platform}\"");
            }
            return new ProjectConfigurationDefinition(commands);
        }

        private static bool TrySplitConfigurationPlatform(
            string value,
            out string configuration,
            out string platform)
        {
            int separatorIndex = value.IndexOf('|');
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                configuration = value;
                platform = string.Empty;
                return false;
            }

            configuration = value[..separatorIndex].Trim();
            platform = value[(separatorIndex + 1)..].Trim();
            return !string.IsNullOrWhiteSpace(configuration)
                && !string.IsNullOrWhiteSpace(platform);
        }

        private static string? GetChildValue(XElement element, string childName)
        {
            return element.Elements()
                .FirstOrDefault(child => string.Equals(child.Name.LocalName, childName, StringComparison.OrdinalIgnoreCase))
                ?.Value
                .Trim();
        }

        private static int GetPlatformPreference(string platform)
        {
            return platform.ToLowerInvariant() switch
            {
                "x64" => 0,
                "win32" => 1,
                "any cpu" or "anycpu" => 2,
                "arm64" => 3,
                _ => 10,
            };
        }

        private static bool TryGetCommand(
            ProjectDefinition project,
            string capabilityId,
            out ProjectCommandDefinition command)
        {
            if (project.Commands != null
                && project.Commands.TryGetValue(capabilityId, out ProjectCommandDefinition? configuredCommand)
                && configuredCommand != null
                && !string.IsNullOrWhiteSpace(configuredCommand.Command))
            {
                command = configuredCommand;
                return true;
            }

            command = null!;
            return false;
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
