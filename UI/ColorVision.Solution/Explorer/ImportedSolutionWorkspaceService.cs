using ColorVision.Common.Utilities;
using ColorVision.Solution.Workspace;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    internal sealed record ImportedSolutionWorkspaceResult(
        bool Succeeded,
        string WorkspacePath = "",
        string DisplayName = "",
        SolutionConfig? Config = null,
        string ErrorMessage = "",
        bool Canceled = false);

    /// <summary>
    /// Projects an external, read-only solution definition into ColorVision's
    /// private workspace format. Source files are never modified.
    /// </summary>
    internal static class ImportedSolutionWorkspaceService
    {
        private static readonly object _workspaceProjectionSync = new();
        internal const string ProviderExtensionKey = "ImportedSolutionProvider";
        internal const string SourceExtensionKey = "ImportedSolutionSource";
        internal const string ConfigurationBaselineExtensionKey = "ImportedSolutionConfigurationBaseline";

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

            return TryCreateWorkspace(
                definition,
                out solutionPath,
                out displayName,
                out errorMessage);
        }

        public static async Task<ImportedSolutionWorkspaceResult> CreateAsync(
            FileInfo sourceFile,
            CancellationToken cancellationToken = default)
        {
            SolutionFileLoadResult loadResult = await SolutionFileProviderRegistry
                .LoadSolutionAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (!loadResult.Succeeded || loadResult.Definition == null)
            {
                return new ImportedSolutionWorkspaceResult(
                    false,
                    ErrorMessage: loadResult.ErrorMessage);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Task<ImportedSolutionWorkspaceResult> projectionTask = Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool succeeded = TryCreateWorkspace(
                    loadResult.Definition,
                    out string solutionPath,
                    out string displayName,
                    out string errorMessage,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return new ImportedSolutionWorkspaceResult(
                    succeeded,
                    solutionPath,
                    displayName,
                    ErrorMessage: errorMessage);
            }, CancellationToken.None);
            return await projectionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private static bool TryCreateWorkspace(
            SolutionFileDefinition definition,
            out string solutionPath,
            out string displayName,
            out string errorMessage,
            CancellationToken cancellationToken = default)
        {
            lock (_workspaceProjectionSync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return TryCreateWorkspaceCore(
                    definition,
                    out solutionPath,
                    out displayName,
                    out errorMessage);
            }
        }

        private static bool TryCreateWorkspaceCore(
            SolutionFileDefinition definition,
            out string solutionPath,
            out string displayName,
            out string errorMessage)
        {
            solutionPath = string.Empty;
            displayName = string.Empty;
            errorMessage = string.Empty;

            try
            {
                solutionPath = PrivateWorkspaceService.CreateWorkspacePath(
                    PrivateWorkspaceKind.ImportedSolution,
                    definition.SourceFile.FullName);
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
                Dictionary<string, Dictionary<string, string>> sourceProjectConfigurations = projects.ToDictionary(
                    item => item.Reference,
                    item => new Dictionary<string, string>(
                        item.Project.Configurations,
                        StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
                Dictionary<string, Dictionary<string, string>>? previousConfigurationBaseline =
                    ReadConfigurationBaseline(config);
                config.ProjectConfigurations = MergeProjectConfigurations(
                    sourceProjectConfigurations,
                    config.ProjectConfigurations,
                    previousConfigurationBaseline);

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
                if (!definition.Platforms.Contains(config.ActivePlatform, StringComparer.OrdinalIgnoreCase))
                {
                    config.ActivePlatform = definition.Platforms.FirstOrDefault(platform =>
                            string.Equals(
                                platform,
                                SolutionConfigurationIdentity.DefaultPlatform,
                                StringComparison.OrdinalIgnoreCase))
                        ?? (definition.Platforms.Count > 0 ? definition.Platforms[0] : null)
                        ?? SolutionConfigurationIdentity.DefaultPlatform;
                }
                config.ExtensionData ??= new Dictionary<string, JToken>();
                config.ExtensionData[ProviderExtensionKey] = new JValue(definition.ProviderId);
                config.ExtensionData[SourceExtensionKey] = new JValue(definition.SourceFile.FullName);
                config.ExtensionData[ConfigurationBaselineExtensionKey] = JToken.FromObject(sourceProjectConfigurations);
                PrivateWorkspaceService.SetSource(
                    config,
                    PrivateWorkspaceKind.ImportedSolution,
                    definition.SourceFile.FullName);
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

        public static bool IsImportedSolution(SolutionConfig config)
        {
            return TryGetSourceFile(config, out _);
        }

        public static bool TryGetSourceFile(SolutionConfig config, out FileInfo? sourceFile)
        {
            ArgumentNullException.ThrowIfNull(config);
            sourceFile = null;
            bool hasProvider = config.ExtensionData != null
                && config.ExtensionData.TryGetValue(ProviderExtensionKey, out JToken? providerToken)
                && !string.IsNullOrWhiteSpace(providerToken.Value<string>());
            string? sourcePath = hasProvider
                && config.ExtensionData!.TryGetValue(SourceExtensionKey, out JToken? sourceToken)
                    ? sourceToken.Value<string>()
                    : null;
            if (string.IsNullOrWhiteSpace(sourcePath))
                return false;

            try
            {
                sourceFile = new FileInfo(Path.GetFullPath(sourcePath));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public static bool TryRefresh(
            FileInfo workspaceFile,
            SolutionConfig currentConfig,
            out SolutionConfig? refreshedConfig,
            out string errorMessage)
        {
            ArgumentNullException.ThrowIfNull(workspaceFile);
            ArgumentNullException.ThrowIfNull(currentConfig);
            refreshedConfig = null;
            errorMessage = string.Empty;
            if (!TryGetSourceFile(currentConfig, out FileInfo? sourceFile) || sourceFile == null)
            {
                errorMessage = "当前工作区不是外部解决方案导入工作区。";
                return false;
            }

            sourceFile.Refresh();
            if (!sourceFile.Exists)
            {
                errorMessage = $"外部解决方案源文件不存在：{sourceFile.FullName}";
                return false;
            }

            if (!TryCreate(sourceFile, out string refreshedWorkspacePath, out _, out errorMessage))
                return false;
            if (!string.Equals(
                Path.GetFullPath(workspaceFile.FullName),
                Path.GetFullPath(refreshedWorkspacePath),
                StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"外部解决方案刷新到了意外的工作区：{refreshedWorkspacePath}";
                return false;
            }

            try
            {
                refreshedConfig = SolutionConfigStore.Load(workspaceFile.FullName).Config;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                errorMessage = $"重新加载导入工作区失败：{ex.Message}";
                return false;
            }
        }

        public static async Task<ImportedSolutionWorkspaceResult> RefreshAsync(
            FileInfo workspaceFile,
            SolutionConfig currentConfig,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(workspaceFile);
            ArgumentNullException.ThrowIfNull(currentConfig);
            if (!TryGetSourceFile(currentConfig, out FileInfo? sourceFile) || sourceFile == null)
            {
                return new ImportedSolutionWorkspaceResult(
                    false,
                    ErrorMessage: "当前工作区不是外部解决方案导入工作区。");
            }

            sourceFile.Refresh();
            if (!sourceFile.Exists)
            {
                return new ImportedSolutionWorkspaceResult(
                    false,
                    ErrorMessage: $"外部解决方案源文件不存在：{sourceFile.FullName}");
            }

            ImportedSolutionWorkspaceResult createResult = await CreateAsync(sourceFile, cancellationToken)
                .ConfigureAwait(false);
            if (!createResult.Succeeded)
                return createResult;
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(
                Path.GetFullPath(workspaceFile.FullName),
                Path.GetFullPath(createResult.WorkspacePath),
                StringComparison.OrdinalIgnoreCase))
            {
                return new ImportedSolutionWorkspaceResult(
                    false,
                    ErrorMessage: $"外部解决方案刷新到了意外的工作区：{createResult.WorkspacePath}");
            }

            try
            {
                Task<SolutionConfig> loadTask = Task.Run(
                    () => SolutionConfigStore.Load(workspaceFile.FullName).Config,
                    CancellationToken.None);
                SolutionConfig refreshedConfig = await loadTask
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return createResult with { Config = refreshedConfig };
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                return new ImportedSolutionWorkspaceResult(
                    false,
                    ErrorMessage: $"重新加载导入工作区失败：{ex.Message}");
            }
        }

        private static Dictionary<string, Dictionary<string, string>> MergeProjectConfigurations(
            Dictionary<string, Dictionary<string, string>> sourceConfigurations,
            Dictionary<string, Dictionary<string, string>> currentConfigurations,
            Dictionary<string, Dictionary<string, string>>? previousBaseline)
        {
            var merged = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var sourceProject in sourceConfigurations)
            {
                currentConfigurations.TryGetValue(sourceProject.Key, out Dictionary<string, string>? currentProject);
                Dictionary<string, string>? baselineProject = null;
                previousBaseline?.TryGetValue(sourceProject.Key, out baselineProject);
                var mergedProject = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var sourceMapping in sourceProject.Value)
                {
                    string? currentValue = null;
                    bool hasCurrentValue = currentProject?.TryGetValue(
                        sourceMapping.Key,
                        out currentValue) == true;
                    string? baselineValue = null;
                    bool hasBaselineValue = baselineProject?.TryGetValue(
                        sourceMapping.Key,
                        out baselineValue) == true;
                    bool hasLocalOverride = hasCurrentValue
                        && (!hasBaselineValue
                            || !string.Equals(currentValue, baselineValue, StringComparison.OrdinalIgnoreCase));
                    mergedProject[sourceMapping.Key] = hasLocalOverride
                        ? currentValue!
                        : sourceMapping.Value;
                }
                merged[sourceProject.Key] = mergedProject;
            }
            return merged;
        }

        private static Dictionary<string, Dictionary<string, string>>? ReadConfigurationBaseline(
            SolutionConfig config)
        {
            if (config.ExtensionData == null
                || !config.ExtensionData.TryGetValue(
                    ConfigurationBaselineExtensionKey,
                    out JToken? baselineToken)
                || baselineToken.Type != JTokenType.Object)
            {
                return null;
            }

            try
            {
                Dictionary<string, Dictionary<string, string>>? baseline = baselineToken
                    .ToObject<Dictionary<string, Dictionary<string, string>>>();
                return baseline?.Where(project => !string.IsNullOrWhiteSpace(project.Key))
                    .ToDictionary(
                        project => project.Key,
                        project => new Dictionary<string, string>(
                            project.Value ?? new Dictionary<string, string>(),
                            StringComparer.OrdinalIgnoreCase),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (JsonException)
            {
                return null;
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
