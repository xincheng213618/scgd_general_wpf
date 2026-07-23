#pragma warning disable CS4014,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Explorer
{
    public partial class SolutionExplorer
    {
        internal void ShowAddNewItemDialog(string? solutionFolderId = null)
        {
            var window = new AddNewItemWindow(DirectoryInfo.FullName)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (window.ShowDialog() == true && window.SelectedTemplate != null && window.NewFileName != null)
            {
                string fullPath = Path.Combine(DirectoryInfo.FullName, window.NewFileName);
                if (window.OverwriteExisting
                    && !EditorDocumentService.TryCloseDocumentsForResources([fullPath]))
                {
                    return;
                }

                SolutionPhysicalItemResult createResult = SolutionPhysicalItemOperations.CreateFromTemplate(
                    window.SelectedTemplate,
                    DirectoryInfo.FullName,
                    window.NewFileName,
                    window.OverwriteExisting);
                if (!createResult.IsComplete)
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        SolutionPhysicalItemOperations.BuildFailureMessage(createResult),
                        "新建项失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!RegisterSolutionItems(createResult.SuccessfulPaths, solutionFolderId, out string errorMessage))
                {
                    bool rollbackAttempted = createResult.NewlyCreatedPaths.Count > 0;
                    bool rolledBack = rollbackAttempted
                        && TryRollbackNewSolutionItems(createResult.NewlyCreatedPaths);
                    string rollbackMessage = rolledBack
                        ? "已撤销本次新建文件。"
                        : rollbackAttempted
                            ? "未能完全撤销本次新建文件，请检查磁盘状态。"
                            : "文件内容已经写入，但未能登记为解决方案项。";
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        $"{errorMessage}{Environment.NewLine}{Environment.NewLine}{rollbackMessage}",
                        "添加解决方案项失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    foreach (string path in createResult.SuccessfulPaths)
                        RestorePhysicalSolutionItem(path);
                }
            }
        }

        private bool TryRollbackNewSolutionItems(IReadOnlyList<string> paths)
        {
            bool succeeded = true;
            foreach (string path in paths)
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    Cache?.Remove(path);
                }
                catch
                {
                    succeeded = false;
                }
            }
            return succeeded;
        }

        internal void AddExistingItem(string? solutionFolderId = null)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "添加现有项",
                Filter = "所有文件 (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                if (!RegisterSolutionItems(dialog.FileNames, solutionFolderId, out string errorMessage))
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        errorMessage,
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        internal void ShowAddNewProjectDialog(string? solutionFolderId = null)
        {
            var window = new AddNewProjectWindow(DirectoryInfo.FullName)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (window.ShowDialog() == true && window.SelectedTemplate != null && window.ProjectName != null)
            {
                if (!ProjectTemplateRegistry.TryCreateFromTemplate(
                    window.SelectedTemplate,
                    DirectoryInfo.FullName,
                    window.ProjectName,
                    out DirectoryInfo? projectDirectory,
                    out string errorMessage)
                    || projectDirectory == null)
                {
                    MessageBox.Show(
                        Application.Current?.GetActiveWindow(),
                        errorMessage,
                        "创建项目失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (!RegisterProject(projectDirectory, solutionFolderId))
                {
                    MessageBox.Show(
                        Application.Current?.GetActiveWindow(),
                        $"项目已创建在“{projectDirectory.FullName}”，但未能添加到当前解决方案。",
                        "添加项目失败",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        internal void AddExistingProject(string? solutionFolderId = null)
        {
            string projectPatterns = ProjectProviderRegistry.GetProjectFileDialogPattern();
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "添加现有项目",
                Filter = $"项目文件 ({projectPatterns})|{projectPatterns}|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                if (!RegisterProject(
                    new FileInfo(dialog.FileName),
                    solutionFolderId,
                    out string errorMessage))
                {
                    MessageBox.Show(
                        Application.Current.GetActiveWindow(),
                        errorMessage,
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
        }

        public void ExecuteContainerAction(SolutionContainerAction action)
        {
            if (!CanAdd || !this.Supports(action))
                return;

            switch (action)
            {
                case SolutionContainerAction.AddNewItem:
                    ShowAddNewItemDialog();
                    break;
                case SolutionContainerAction.AddExistingItem:
                    AddExistingItem();
                    break;
                case SolutionContainerAction.CreateFolder:
                    SolutionNodeFactory.CreateNewFolder(this, DirectoryInfo.FullName);
                    break;
                case SolutionContainerAction.AddNewProject:
                    ShowAddNewProjectDialog();
                    break;
                case SolutionContainerAction.AddExistingProject:
                    AddExistingProject();
                    break;
                case SolutionContainerAction.CreateSolutionFolder:
                    CreateSolutionFolder();
                    break;
            }
        }

        internal void ShowConfigurationManager()
        {
            new SolutionConfigurationWindow(this)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.ShowDialog();
        }

        internal bool CanBuildSolution()
        {
            return DirectoryInfo.Exists;
        }

        internal bool BuildSolution()
        {
            ProjectBuildPlan plan = CreateBuildPlan(target: null);
            return ExecuteBuildPlan(plan);
        }

        internal static bool CanBuildProject(ProjectDefinition project)
        {
            return ProjectProviderRegistry.HasCapability(project, ProjectCapabilityIds.Build)
                && ProjectProviderRegistry.CanExecuteCapability(project, ProjectCapabilityIds.Build);
        }

        internal bool BuildProject(ProjectDefinition project)
        {
            ProjectBuildPlan plan = CreateBuildPlan(project);
            return ExecuteBuildPlan(plan);
        }

        internal bool CanSetStartupProject(ProjectDefinition project)
        {
            ProjectDefinition configuredProject = ApplyActiveConfiguration(project);
            return IsProjectIncluded(project)
                && (ProjectProviderRegistry.HasCapability(configuredProject, ProjectCapabilityIds.Run)
                    || ProjectProviderRegistry.HasCapability(configuredProject, ProjectCapabilityIds.Debug));
        }

        internal bool SetStartupProject(ProjectDefinition project)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation($"设置启动项目“{project.Name}”", () => SetStartupProject(project), result => result);
            if (!CanSetStartupProject(project))
                return false;

            Config.StartupProject = Path.GetRelativePath(DirectoryInfo.FullName, project.ProjectFile.FullName);
            SaveConfig();
            UpdateStartupProjectState();
            return true;
        }

        internal bool IsConfiguredStartupProject(ProjectDefinition project)
        {
            return !string.IsNullOrWhiteSpace(Config.StartupProject)
                && ProjectReferenceMatches(DirectoryInfo.FullName, Config.StartupProject, project);
        }

        internal bool TryGetStartupProject(out ProjectDefinition? project)
        {
            project = SelectStartupProject(
                LoadProjects(),
                DirectoryInfo.FullName,
                Config.StartupProject);
            return project != null;
        }

        internal bool CanExecuteStartupProject(string capabilityId)
        {
            return TryGetStartupProject(out ProjectDefinition? project)
                && project != null
                && ProjectProviderRegistry.CanExecuteCapability(project, capabilityId);
        }

        internal bool ExecuteStartupProject(string capabilityId)
        {
            if (TryGetStartupProject(out ProjectDefinition? project)
                && project != null
                && ProjectProviderRegistry.ExecuteCapability(project, capabilityId))
            {
                return true;
            }

            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                "启动项目无法执行该命令，请检查启动项目和项目命令配置。",
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        internal ProjectBuildPlan CreateBuildPlan(ProjectDefinition? target)
        {
            List<ProjectDefinition> projects = LoadProjects();
            ProjectDefinition? configuredTarget = null;
            if (target != null)
            {
                configuredTarget = projects.FirstOrDefault(project => PathEquals(
                    project.ProjectFile.FullName,
                    target.ProjectFile.FullName));
                if (configuredTarget == null)
                {
                    configuredTarget = ApplyActiveConfiguration(target);
                    projects.Add(configuredTarget);
                }
            }

            return ProjectBuildPlanner.Create(
                projects,
                configuredTarget == null ? null : [configuredTarget]);
        }

        private List<ProjectDefinition> LoadProjects(bool applyActiveConfiguration = true)
        {
            var projects = new List<ProjectDefinition>();
            if (IsExplicitProjectMode)
            {
                projects.AddRange(VisualChildren.GetAllVisualChildren()
                    .OfType<ProjectNode>()
                    .Select(node => applyActiveConfiguration
                        ? ApplyActiveConfiguration(node.Project)
                        : node.Project));
            }
            else
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                };
                try
                {
                    foreach (string projectFilePath in ProjectProviderRegistry.GetProjectFilePatterns()
                        .SelectMany(pattern => Directory.EnumerateFiles(
                            DirectoryInfo.FullName,
                            pattern,
                            options))
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        if (ProjectProviderRegistry.TryLoadProject(
                            new FileInfo(projectFilePath),
                            out ProjectDefinition? project)
                            && project != null)
                        {
                            projects.Add(applyActiveConfiguration ? ApplyActiveConfiguration(project) : project);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Logger.Warn($"枚举解决方案项目失败: {ex.Message}");
                }
            }

            return projects
                .GroupBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        internal IReadOnlyList<ProjectDefinition> LoadProjectsForConfigurationEditing()
        {
            return LoadProjects(applyActiveConfiguration: false);
        }

        internal ProjectDefinition ApplyActiveConfiguration(ProjectDefinition project)
        {
            return project.ForConfiguration(GetProjectConfigurationName(project));
        }

        internal string GetProjectConfigurationName(ProjectDefinition project)
        {
            return ResolveProjectConfigurationName(
                DirectoryInfo.FullName,
                Config.ActiveConfiguration,
                Config.ActivePlatform,
                Config.ProjectConfigurations,
                project);
        }

        internal static string ResolveProjectConfigurationName(
            string solutionDirectory,
            string? activeConfiguration,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations,
            ProjectDefinition project)
        {
            return ResolveProjectConfigurationName(
                solutionDirectory,
                activeConfiguration,
                activePlatform: null,
                projectConfigurations,
                project);
        }

        internal static string ResolveProjectConfigurationName(
            string solutionDirectory,
            string? activeConfiguration,
            string? activePlatform,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations,
            ProjectDefinition project)
        {
            string normalizedActiveConfiguration = NormalizeConfigurationName(activeConfiguration);
            if (projectConfigurations == null)
                return normalizedActiveConfiguration;

            foreach (var projectMapping in projectConfigurations)
            {
                if (!ProjectReferenceMatches(solutionDirectory, projectMapping.Key, project))
                    continue;
                if (projectMapping.Value == null)
                    return normalizedActiveConfiguration;

                string? mappedConfiguration = null;
                if (!string.IsNullOrWhiteSpace(activePlatform))
                {
                    string configurationKey = SolutionConfigurationIdentity.CreateKey(
                        normalizedActiveConfiguration,
                        activePlatform);
                    mappedConfiguration = projectMapping.Value
                        .FirstOrDefault(pair => string.Equals(
                            pair.Key,
                            configurationKey,
                            StringComparison.OrdinalIgnoreCase))
                        .Value;
                }
                mappedConfiguration ??= projectMapping.Value
                    .FirstOrDefault(pair => string.Equals(
                        pair.Key,
                        normalizedActiveConfiguration,
                        StringComparison.OrdinalIgnoreCase))
                    .Value;
                return string.IsNullOrWhiteSpace(mappedConfiguration)
                    ? normalizedActiveConfiguration
                    : mappedConfiguration.Trim();
            }
            return normalizedActiveConfiguration;
        }

        internal IReadOnlyList<string> GetAvailableSolutionConfigurations()
        {
            return GetAvailableSolutionConfigurations(
                LoadProjects(),
                Config.ActiveConfiguration,
                Config.ProjectConfigurations);
        }

        internal static IReadOnlyList<string> GetAvailableSolutionConfigurations(
            IEnumerable<ProjectDefinition> projects,
            string? activeConfiguration,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Debug",
                "Release",
                NormalizeConfigurationName(activeConfiguration),
            };
            foreach (ProjectDefinition project in projects)
            {
                if (project.Configurations == null)
                    continue;
                foreach (string configuration in project.Configurations.Keys.Where(name => !string.IsNullOrWhiteSpace(name)))
                    result.Add(configuration.Trim());
            }
            if (projectConfigurations != null)
            {
                foreach (string configuration in projectConfigurations.Values
                    .SelectMany(mapping => mapping.Keys)
                    .Where(name => !string.IsNullOrWhiteSpace(name)))
                {
                    string configurationName = configuration.Trim();
                    if (SolutionConfigurationIdentity.TryParseKey(
                        configuration,
                        out string parsedConfiguration,
                        out _))
                    {
                        configurationName = parsedConfiguration;
                    }
                    result.Add(configurationName);
                }
            }

            return result
                .OrderBy(configuration => configuration.ToLowerInvariant() switch
                {
                    "debug" => 0,
                    "release" => 1,
                    _ => 2,
                })
                .ThenBy(configuration => configuration, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        internal IReadOnlyList<string> GetAvailableSolutionPlatforms()
        {
            return GetAvailableSolutionPlatforms(
                Config.ActivePlatform,
                Config.ProjectConfigurations);
        }

        internal static IReadOnlyList<string> GetAvailableSolutionPlatforms(
            string? activePlatform,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizePlatformName(activePlatform),
            };
            bool hasPlatformMappings = false;
            if (projectConfigurations != null)
            {
                foreach (string configurationKey in projectConfigurations.Values
                    .SelectMany(mapping => mapping.Keys)
                    .Where(name => !string.IsNullOrWhiteSpace(name)))
                {
                    if (SolutionConfigurationIdentity.TryParseKey(
                        configurationKey,
                        out _,
                        out string platform))
                    {
                        hasPlatformMappings = true;
                        result.Add(platform);
                    }
                }
            }
            if (!hasPlatformMappings)
                result.Add(SolutionConfigurationIdentity.DefaultPlatform);

            return result
                .OrderBy(platform => string.Equals(
                    platform,
                    SolutionConfigurationIdentity.DefaultPlatform,
                    StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(platform => platform, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        internal bool SetActiveConfiguration(string configurationName)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("更改活动解决方案配置", () => SetActiveConfiguration(configurationName), result => result);
            string normalizedName = NormalizeConfigurationName(configurationName);
            if (string.Equals(Config.ActiveConfiguration, normalizedName, StringComparison.OrdinalIgnoreCase))
                return false;

            Config.ActiveConfiguration = normalizedName;
            SaveConfig();
            foreach (ProjectNode projectNode in VisualChildren.GetAllVisualChildren().OfType<ProjectNode>())
                projectNode.RefreshConfigurationState();
            EnsureStartupProject();
            RefreshConfigurationCommandSurfaces();
            return true;
        }

        internal bool SetActivePlatform(string platformName)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("更改活动解决方案平台", () => SetActivePlatform(platformName), result => result);
            string normalizedName = NormalizePlatformName(platformName);
            if (string.Equals(Config.ActivePlatform, normalizedName, StringComparison.OrdinalIgnoreCase))
                return false;

            Config.ActivePlatform = normalizedName;
            SaveConfig();
            foreach (ProjectNode projectNode in VisualChildren.GetAllVisualChildren().OfType<ProjectNode>())
                projectNode.RefreshConfigurationState();
            EnsureStartupProject();
            RefreshConfigurationCommandSurfaces();
            return true;
        }

        internal bool TryApplyConfigurationChanges(
            SolutionConfigurationChanges changes,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            List<ProjectDefinition> projects = LoadProjects(applyActiveConfiguration: false);
            var projectedProjects = new List<ProjectDefinition>();
            foreach (ProjectDefinition project in projects)
            {
                IReadOnlyList<string> dependencies = FindDependencyChanges(changes, project)
                    ?? project.Dependencies
                    ?? Array.Empty<string>();
                string projectConfiguration = ResolveProjectConfigurationName(
                    DirectoryInfo.FullName,
                    changes.ActiveConfiguration,
                    changes.ActivePlatform,
                    changes.ProjectConfigurations,
                    project);
                if (project.Configurations?.Count > 0
                    && project.Commands?.Count == 0
                    && !project.Configurations.Keys.Any(configuration => string.Equals(
                        configuration,
                        projectConfiguration,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    errorMessage = $"项目“{project.Name}”不存在配置“{projectConfiguration}”。";
                    return false;
                }
                projectedProjects.Add((project with { Dependencies = dependencies })
                    .ForConfiguration(projectConfiguration));
            }

            ProjectBuildPlan plan = ProjectBuildPlanner.Create(projectedProjects);
            if (!plan.IsValid)
            {
                errorMessage = plan.FormatDiagnostics();
                return false;
            }
            if (changes.StartupProject != null)
            {
                ProjectDefinition? startupProject = projectedProjects.FirstOrDefault(project => PathEquals(
                    project.ProjectFile.FullName,
                    changes.StartupProject.ProjectFile.FullName));
                if (startupProject == null)
                {
                    errorMessage = "选择的启动项目不属于当前解决方案。";
                    return false;
                }
                if (!ProjectProviderRegistry.HasCapability(startupProject, ProjectCapabilityIds.Run)
                    && !ProjectProviderRegistry.HasCapability(startupProject, ProjectCapabilityIds.Debug))
                {
                    errorMessage = $"启动项目“{startupProject.Name}”在配置“{startupProject.ActiveConfiguration}”下没有 Run 或 Debug 命令。";
                    return false;
                }
            }

            var changedDependencies = new List<(ProjectDefinition Project, IReadOnlyList<string> Dependencies)>();
            foreach (ProjectDefinition project in projects)
            {
                IReadOnlyList<string>? dependencies = FindDependencyChanges(changes, project);
                if (dependencies == null || DependencyReferencesEqual(project.Dependencies, dependencies))
                    continue;
                if (!ProjectProviderRegistry.CanChangeProjectDependencies(project))
                {
                    errorMessage = $"项目“{project.Name}”不支持修改依赖或项目文件不可写。";
                    return false;
                }
                changedDependencies.Add((project, dependencies));
            }

            var projectSnapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string solutionSnapshot;
            try
            {
                solutionSnapshot = File.ReadAllText(ConfigFileInfo.FullName);
                foreach (var item in changedDependencies)
                    projectSnapshots[item.Project.ProjectFile.FullName] = File.ReadAllText(item.Project.ProjectFile.FullName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errorMessage = $"无法创建配置事务快照：{ex.Message}";
                return false;
            }

            var updatedProjects = new List<ProjectDefinition>();
            try
            {
                foreach (var item in changedDependencies)
                {
                    if (!ProjectProviderRegistry.TrySetProjectDependencies(
                        item.Project,
                        item.Dependencies,
                        out ProjectDefinition? updatedProject,
                        out errorMessage)
                        || updatedProject == null)
                    {
                        throw new InvalidOperationException(errorMessage);
                    }
                    updatedProjects.Add(updatedProject);
                }

                Config.ActiveConfiguration = NormalizeConfigurationName(changes.ActiveConfiguration);
                Config.ActivePlatform = NormalizePlatformName(changes.ActivePlatform);
                Config.ProjectConfigurations = CloneProjectConfigurations(changes.ProjectConfigurations);
                Config.StartupProject = changes.StartupProject == null
                    ? string.Empty
                    : Path.GetRelativePath(
                        DirectoryInfo.FullName,
                        changes.StartupProject.ProjectFile.FullName).Replace('\\', '/');
                SaveConfig();
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException
                or JsonException)
            {
                foreach (var snapshot in projectSnapshots)
                {
                    try
                    {
                        WriteTextAtomically(snapshot.Key, snapshot.Value);
                    }
                    catch (Exception rollbackException)
                    {
                        Logger.Error($"回滚项目配置失败: {snapshot.Key}", rollbackException);
                    }
                }
                try
                {
                    WriteTextAtomically(ConfigFileInfo.FullName, solutionSnapshot);
                    Config = SolutionConfigStore.DeserializeAndMigrate(solutionSnapshot, out _);
                }
                catch (Exception rollbackException)
                {
                    Logger.Error("回滚解决方案配置失败。", rollbackException);
                }
                errorMessage = string.IsNullOrWhiteSpace(errorMessage)
                    ? $"保存解决方案配置失败：{ex.Message}"
                    : errorMessage;
                return false;
            }

            ApplyProjectMutations(updatedProjects);
            OperationHistory.Clear();
            return true;
        }

        internal static string NormalizeConfigurationName(string? configurationName)
        {
            return SolutionConfigurationIdentity.NormalizeConfiguration(configurationName);
        }

        internal static string NormalizePlatformName(string? platformName)
        {
            return SolutionConfigurationIdentity.NormalizePlatform(platformName);
        }

        private static IReadOnlyList<string>? FindDependencyChanges(
            SolutionConfigurationChanges changes,
            ProjectDefinition project)
        {
            return changes.ProjectDependencies.FirstOrDefault(pair => string.Equals(
                pair.Key,
                project.ProjectFile.FullName,
                StringComparison.OrdinalIgnoreCase)).Value;
        }

        private static bool DependencyReferencesEqual(
            IReadOnlyList<string>? current,
            IReadOnlyList<string> proposed)
        {
            var currentSet = (current ?? Array.Empty<string>())
                .Select(reference => reference.Replace('\\', '/').Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return currentSet.SetEquals(proposed
                .Select(reference => reference.Replace('\\', '/').Trim()));
        }

        private static Dictionary<string, Dictionary<string, string>> CloneProjectConfigurations(
            IReadOnlyDictionary<string, Dictionary<string, string>> configurations)
        {
            return configurations.ToDictionary(
                pair => pair.Key,
                pair => new Dictionary<string, string>(pair.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        private void EnsureStartupProject()
        {
            if (_operationHistoryEnabled && _trackedMutationDepth == 0 && !_isApplyingOperationSnapshot)
            {
                ExecuteTrackedMutation("更新启动项目", () =>
                {
                    EnsureStartupProject();
                    return true;
                }, result => result);
                return;
            }
            ProjectDefinition? startupProject = SelectStartupProject(
                VisualChildren.GetAllVisualChildren()
                    .OfType<ProjectNode>()
                    .Select(node => ApplyActiveConfiguration(node.Project)),
                DirectoryInfo.FullName,
                Config.StartupProject);
            if (startupProject != null && string.IsNullOrWhiteSpace(Config.StartupProject))
            {
                Config.StartupProject = Path.GetRelativePath(
                    DirectoryInfo.FullName,
                    startupProject.ProjectFile.FullName);
                SaveConfig();
            }

            UpdateStartupProjectState();
        }

        internal static ProjectDefinition? SelectStartupProject(
            IEnumerable<ProjectDefinition> projects,
            string solutionDirectory,
            string? configuredReference)
        {
            List<ProjectDefinition> availableProjects = projects
                .GroupBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (!string.IsNullOrWhiteSpace(configuredReference))
            {
                return availableProjects.FirstOrDefault(project =>
                    ProjectReferenceMatches(solutionDirectory, configuredReference, project));
            }

            return availableProjects.FirstOrDefault(project =>
                ProjectProviderRegistry.HasCapability(project, ProjectCapabilityIds.Run)
                || ProjectProviderRegistry.HasCapability(project, ProjectCapabilityIds.Debug));
        }

        private void UpdateStartupProjectState()
        {
            foreach (ProjectNode projectNode in VisualChildren.GetAllVisualChildren().OfType<ProjectNode>())
                projectNode.SetStartupProjectState(IsConfiguredStartupProject(projectNode.Project));
        }

        private static bool ExecuteBuildPlan(ProjectBuildPlan plan)
        {
            if (ProjectBuildExecutor.Execute(plan, out string errorMessage))
                return true;

            MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                errorMessage,
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

    }
}
