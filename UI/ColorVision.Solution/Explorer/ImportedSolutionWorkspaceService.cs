using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// Projects an external, read-only solution definition into ColorVision's
    /// private workspace format. Source files are never modified.
    /// </summary>
    internal static class ImportedSolutionWorkspaceService
    {
        public static bool TryCreate(
            FileInfo sourceFile,
            out string solutionPath,
            out string displayName,
            out string errorMessage)
        {
            solutionPath = string.Empty;
            displayName = string.Empty;
            errorMessage = string.Empty;
            if (!SolutionFileProviderRegistry.TryLoadSolution(
                sourceFile,
                out SolutionFileDefinition? definition,
                out errorMessage)
                || definition == null)
            {
                return false;
            }

            try
            {
                string importedSolutionDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ColorVision",
                    "ImportedSolutions");
                Directory.CreateDirectory(importedSolutionDirectory);
                string sourceKey = Tool.GetMD5(definition.SourceFile.FullName.ToUpperInvariant());
                solutionPath = Path.Combine(importedSolutionDirectory, $"{sourceKey}.cvsln");
                SolutionConfig config = File.Exists(solutionPath)
                    ? SolutionConfigStore.Load(solutionPath).Config
                    : new SolutionConfig();

                string rootPath = Path.GetFullPath(definition.RootDirectory.FullName);
                List<(SolutionFileProject Project, string Reference)> projects = definition.Projects
                    .Select(project => (project, NormalizeReference(rootPath, project.Path)))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Item2))
                    .GroupBy(item => item.Item2, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();
                config.RootPath = rootPath;
                config.ProjectMode = SolutionProjectMode.Explicit;
                config.Projects = new ObservableCollection<string>(projects.Select(item => item.Reference));

                Dictionary<string, string> folderIds = definition.Folders
                    .Where(folder => !string.IsNullOrWhiteSpace(folder.Path))
                    .GroupBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => CreateStableId(group.Key),
                        StringComparer.OrdinalIgnoreCase);
                config.SolutionFolders = new ObservableCollection<SolutionFolderDefinition>(definition.Folders
                    .Where(folder => folderIds.ContainsKey(folder.Path))
                    .GroupBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .Select(folder => new SolutionFolderDefinition
                    {
                        Id = folderIds[folder.Path],
                        Name = folder.Name,
                        ParentId = folder.ParentPath != null
                            && folderIds.TryGetValue(folder.ParentPath, out string? parentId)
                                ? parentId
                                : null,
                    }));

                config.ProjectSolutionFolders = projects
                    .Where(item => item.Project.SolutionFolderPath != null
                        && folderIds.ContainsKey(item.Project.SolutionFolderPath))
                    .ToDictionary(
                        item => item.Reference,
                        item => folderIds[item.Project.SolutionFolderPath!],
                        StringComparer.OrdinalIgnoreCase);
                config.ProjectConfigurations = projects.ToDictionary(
                    item => item.Reference,
                    item => new Dictionary<string, string>(
                        item.Project.Configurations,
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);

                config.SolutionItems = new ObservableCollection<SolutionItemDefinition>(definition.Folders
                    .Where(folder => folderIds.ContainsKey(folder.Path))
                    .SelectMany(folder => folder.Files.Select(file => new SolutionItemDefinition
                    {
                        Id = CreateStableId(folder.Path, file),
                        Path = NormalizeReference(rootPath, file),
                        SolutionFolderId = folderIds[folder.Path],
                    }))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Path))
                    .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First()));

                if (!config.Projects.Contains(config.StartupProject, StringComparer.OrdinalIgnoreCase))
                    config.StartupProject = string.Empty;
                if (!definition.Configurations.Contains(config.ActiveConfiguration, StringComparer.OrdinalIgnoreCase))
                {
                    config.ActiveConfiguration = definition.Configurations.FirstOrDefault(configuration =>
                            string.Equals(configuration, "Debug", StringComparison.OrdinalIgnoreCase))
                        ?? (definition.Configurations.Count > 0 ? definition.Configurations[0] : null)
                        ?? "Debug";
                }
                config.ExtensionData ??= new Dictionary<string, JToken>();
                config.ExtensionData["ImportedSolutionProvider"] = new JValue(definition.ProviderId);
                config.ExtensionData["ImportedSolutionSource"] = new JValue(definition.SourceFile.FullName);
                SolutionConfigStore.Save(solutionPath, config);

                displayName = definition.Name;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                errorMessage = $"创建导入解决方案工作区失败：{ex.Message}";
                solutionPath = string.Empty;
                displayName = string.Empty;
                return false;
            }
        }

        private static string NormalizeReference(string rootPath, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(Path.IsPathRooted(reference)
                    ? reference
                    : Path.Combine(rootPath, reference));
                return Path.GetRelativePath(rootPath, fullPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return reference.Trim();
            }
        }

        private static string CreateStableId(params string[] values)
        {
            return $"imported-{Tool.GetMD5(string.Join('|', values).ToUpperInvariant())}";
        }
    }
}
