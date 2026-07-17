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
        internal SolutionFolderDefinition CreateSolutionFolder(string? parentId = null)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("新建解决方案文件夹", () => CreateSolutionFolder(parentId), _ => true);
            EnsureExplicitProjectModePreservingProjects();
            NormalizeSolutionOrganization();
            string? normalizedParentId = IsKnownSolutionFolder(parentId) ? parentId : null;
            string baseName = "新建解决方案文件夹";
            var siblingNames = Config.SolutionFolders
                .Where(folder => string.Equals(folder.ParentId, normalizedParentId, StringComparison.OrdinalIgnoreCase))
                .Select(folder => folder.Name)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            string name = baseName;
            for (int suffix = 2; siblingNames.Contains(name); suffix++)
                name = $"{baseName} {suffix}";

            var definition = new SolutionFolderDefinition
            {
                Name = name,
                ParentId = normalizedParentId,
            };
            Config.SolutionFolders.Add(definition);
            SaveConfig();
            ReconcileExplicitProjects();
            if (FindSolutionFolderNode(definition.Id) is { } node)
            {
                node.IsExpanded = true;
                node.IsSelected = true;
                node.IsEditMode = true;
            }
            return definition;
        }

        internal bool TryRenameSolutionFolder(string folderId, string name)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("重命名解决方案文件夹", () => TryRenameSolutionFolder(folderId, name), result => result);
            string normalizedName = name?.Trim() ?? string.Empty;
            SolutionFolderDefinition? folder = Config.SolutionFolders.FirstOrDefault(item =>
                string.Equals(item.Id, folderId, StringComparison.OrdinalIgnoreCase));
            if (folder == null || string.IsNullOrWhiteSpace(normalizedName))
                return false;
            if (Config.SolutionFolders.Any(item =>
                !ReferenceEquals(item, folder)
                && string.Equals(item.ParentId, folder.ParentId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"同一级已存在名为“{normalizedName}”的解决方案文件夹。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return false;
            }

            folder.Name = normalizedName;
            SaveConfig();
            return true;
        }

        internal bool RemoveSolutionFolder(string folderId)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("删除解决方案文件夹", () => RemoveSolutionFolder(folderId), result => result);
            SolutionFolderDefinition? folder = Config.SolutionFolders.FirstOrDefault(item =>
                string.Equals(item.Id, folderId, StringComparison.OrdinalIgnoreCase));
            if (folder == null)
                return false;

            foreach (SolutionFolderDefinition child in Config.SolutionFolders.Where(item =>
                string.Equals(item.ParentId, folder.Id, StringComparison.OrdinalIgnoreCase)))
            {
                child.ParentId = folder.ParentId;
            }
            foreach (string reference in Config.ProjectSolutionFolders
                .Where(pair => string.Equals(pair.Value, folder.Id, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .ToList())
            {
                if (string.IsNullOrWhiteSpace(folder.ParentId))
                    Config.ProjectSolutionFolders.Remove(reference);
                else
                    Config.ProjectSolutionFolders[reference] = folder.ParentId;
            }
            foreach (SolutionItemDefinition item in Config.SolutionItems.Where(item => string.Equals(
                item.SolutionFolderId,
                folder.Id,
                StringComparison.OrdinalIgnoreCase)))
            {
                item.SolutionFolderId = folder.ParentId;
            }

            Config.SolutionFolders.Remove(folder);
            SaveConfig();
            ReconcileExplicitProjects();
            return true;
        }

        internal IReadOnlyList<(string? Id, string DisplayName)> GetSolutionFolderOptions()
        {
            NormalizeSolutionOrganization();
            var foldersById = Config.SolutionFolders.ToDictionary(
                folder => folder.Id,
                StringComparer.OrdinalIgnoreCase);
            string GetPath(SolutionFolderDefinition folder)
            {
                var names = new Stack<string>();
                SolutionFolderDefinition? current = folder;
                while (current != null)
                {
                    names.Push(current.Name);
                    current = current.ParentId != null
                        && foldersById.TryGetValue(current.ParentId, out SolutionFolderDefinition? parent)
                            ? parent
                            : null;
                }
                return string.Join(" / ", names);
            }

            return new[] { ((string?)null, "(解决方案根)") }
                .Concat(Config.SolutionFolders
                    .Select(folder => ((string?)folder.Id, GetPath(folder)))
                    .OrderBy(item => item.Item2, StringComparer.CurrentCultureIgnoreCase))
                .ToList();
        }

        internal IReadOnlyList<(string? Id, string DisplayName)> GetSolutionFolderMoveOptions(string folderId)
        {
            return GetSolutionFolderOptions()
                .Where(option => CanMoveSolutionItemsToFolder(
                    [],
                    [folderId],
                    option.Id,
                    out _))
                .ToList();
        }

        internal string? GetSolutionFolderParentId(string folderId)
        {
            return Config.SolutionFolders.FirstOrDefault(folder => string.Equals(
                folder.Id,
                folderId,
                StringComparison.OrdinalIgnoreCase))?.ParentId;
        }

        internal string? GetProjectSolutionFolderId(ProjectDefinition project)
        {
            string? reference = Config.Projects.FirstOrDefault(item =>
                ProjectReferenceMatches(DirectoryInfo.FullName, item, project));
            if (reference == null)
                return null;

            return Config.ProjectSolutionFolders
                .FirstOrDefault(pair => ProjectReferencesEqual(
                    DirectoryInfo.FullName,
                    pair.Key,
                    reference))
                .Value;
        }

        internal bool MoveProjectToSolutionFolder(ProjectDefinition project, string? folderId)
        {
            return MoveSolutionItemsToFolder([project], [], folderId, out _);
        }

        internal bool CanMoveSolutionItemsToFolder(
            IReadOnlyList<ProjectDefinition> projects,
            IReadOnlyList<string> solutionFolderIds,
            string? targetFolderId,
            out string errorMessage)
        {
            return CanMoveSolutionItemsToFolder(
                projects,
                solutionFolderIds,
                [],
                targetFolderId,
                out errorMessage);
        }

        internal bool CanMoveSolutionItemsToFolder(
            IReadOnlyList<ProjectDefinition> projects,
            IReadOnlyList<string> solutionFolderIds,
            IReadOnlyList<string> solutionItemIds,
            string? targetFolderId,
            out string errorMessage)
        {
            errorMessage = string.Empty;
            if (!IsExplicitProjectMode)
            {
                errorMessage = "只有显式项目模式支持解决方案文件夹组织。";
                return false;
            }
            if (targetFolderId != null && !IsKnownSolutionFolder(targetFolderId))
            {
                errorMessage = "目标解决方案文件夹已经不存在。";
                return false;
            }

            foreach (ProjectDefinition project in projects)
            {
                if (!Config.Projects.Any(reference =>
                    ProjectReferenceMatches(DirectoryInfo.FullName, reference, project)))
                {
                    errorMessage = $"项目“{project.Name}”不属于当前解决方案。";
                    return false;
                }
            }
            foreach (string solutionItemId in solutionItemIds)
            {
                if (!Config.SolutionItems.Any(item => string.Equals(
                    item.Id,
                    solutionItemId,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    errorMessage = "要移动的解决方案项已经不存在。";
                    return false;
                }
            }

            var sourceFolderIds = solutionFolderIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var foldersById = Config.SolutionFolders.ToDictionary(
                folder => folder.Id,
                StringComparer.OrdinalIgnoreCase);
            foreach (string sourceFolderId in sourceFolderIds)
            {
                if (!foldersById.ContainsKey(sourceFolderId))
                {
                    errorMessage = "要移动的解决方案文件夹已经不存在。";
                    return false;
                }
            }

            string? ancestorId = targetFolderId;
            while (ancestorId != null && foldersById.TryGetValue(ancestorId, out SolutionFolderDefinition? ancestor))
            {
                if (sourceFolderIds.Contains(ancestorId))
                {
                    errorMessage = "不能将解决方案文件夹移动到自身或其子文件夹中。";
                    return false;
                }
                ancestorId = ancestor.ParentId;
            }

            List<SolutionFolderDefinition> movedFolders = sourceFolderIds
                .Select(id => foldersById[id])
                .Where(folder => !HasSelectedSolutionFolderAncestor(folder, sourceFolderIds, foldersById))
                .ToList();
            var targetNames = Config.SolutionFolders
                .Where(folder => string.Equals(folder.ParentId, targetFolderId, StringComparison.OrdinalIgnoreCase)
                    && !sourceFolderIds.Contains(folder.Id))
                .Select(folder => folder.Name)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            foreach (SolutionFolderDefinition folder in movedFolders)
            {
                if (!targetNames.Add(folder.Name))
                {
                    errorMessage = $"目标位置已经存在名为“{folder.Name}”的解决方案文件夹。";
                    return false;
                }
            }

            if (projects.Count == 0 && movedFolders.Count == 0 && solutionItemIds.Count == 0)
            {
                errorMessage = "没有可移动的解决方案项。";
                return false;
            }
            return true;
        }

        internal bool MoveSolutionItemsToFolder(
            IReadOnlyList<ProjectDefinition> projects,
            IReadOnlyList<string> solutionFolderIds,
            string? targetFolderId,
            out string errorMessage)
        {
            return MoveSolutionItemsToFolder(
                projects,
                solutionFolderIds,
                [],
                targetFolderId,
                out errorMessage);
        }

        internal bool MoveSolutionItemsToFolder(
            IReadOnlyList<ProjectDefinition> projects,
            IReadOnlyList<string> solutionFolderIds,
            IReadOnlyList<string> solutionItemIds,
            string? targetFolderId,
            out string errorMessage)
        {
            if (_trackedMutationDepth == 0)
            {
                string trackedError = string.Empty;
                bool result = ExecuteTrackedMutation(
                    "移动解决方案项",
                    () => MoveSolutionItemsToFolder(
                        projects,
                        solutionFolderIds,
                        solutionItemIds,
                        targetFolderId,
                        out trackedError),
                    succeeded => succeeded);
                errorMessage = trackedError;
                return result;
            }
            if (!CanMoveSolutionItemsToFolder(
                projects,
                solutionFolderIds,
                solutionItemIds,
                targetFolderId,
                out errorMessage))
                return false;

            bool changed = false;
            foreach (ProjectDefinition project in projects
                .GroupBy(item => item.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()))
            {
                string projectReference = Config.Projects.First(reference =>
                    ProjectReferenceMatches(DirectoryInfo.FullName, reference, project));
                string? currentFolderId = GetProjectSolutionFolderId(project);
                if (string.Equals(currentFolderId, targetFolderId, StringComparison.OrdinalIgnoreCase))
                    continue;

                RemoveProjectSolutionFolderMappings(projectReference);
                if (targetFolderId != null)
                    Config.ProjectSolutionFolders[projectReference] = targetFolderId;
                changed = true;
            }

            var sourceFolderIds = solutionFolderIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var foldersById = Config.SolutionFolders.ToDictionary(
                folder => folder.Id,
                StringComparer.OrdinalIgnoreCase);
            foreach (SolutionFolderDefinition folder in sourceFolderIds
                .Where(foldersById.ContainsKey)
                .Select(id => foldersById[id])
                .Where(folder => !HasSelectedSolutionFolderAncestor(folder, sourceFolderIds, foldersById)))
            {
                if (string.Equals(folder.ParentId, targetFolderId, StringComparison.OrdinalIgnoreCase))
                    continue;
                folder.ParentId = targetFolderId;
                changed = true;
            }

            foreach (SolutionItemDefinition item in Config.SolutionItems.Where(item =>
                solutionItemIds.Contains(item.Id, StringComparer.OrdinalIgnoreCase)))
            {
                if (string.Equals(item.SolutionFolderId, targetFolderId, StringComparison.OrdinalIgnoreCase))
                    continue;
                item.SolutionFolderId = targetFolderId;
                changed = true;
            }

            if (changed)
            {
                SaveConfig();
                ReconcileExplicitProjects();
            }
            return true;
        }

        private static bool HasSelectedSolutionFolderAncestor(
            SolutionFolderDefinition folder,
            HashSet<string> selectedFolderIds,
            Dictionary<string, SolutionFolderDefinition> foldersById)
        {
            string? parentId = folder.ParentId;
            while (parentId != null && foldersById.TryGetValue(parentId, out SolutionFolderDefinition? parent))
            {
                if (selectedFolderIds.Contains(parentId))
                    return true;
                parentId = parent.ParentId;
            }
            return false;
        }

        private void EnsureExplicitProjectModePreservingProjects()
        {
            if (Config.ProjectMode == SolutionProjectMode.Explicit)
                return;

            foreach (ProjectDefinition project in LoadProjects(applyActiveConfiguration: false))
            {
                string projectReference = Path.GetRelativePath(
                    DirectoryInfo.FullName,
                    project.ProjectFile.FullName);
                if (!Config.Projects.Any(reference => ProjectReferencesEqual(
                    DirectoryInfo.FullName,
                    reference,
                    projectReference)))
                {
                    Config.Projects.Add(projectReference);
                }
            }
            Config.ProjectMode = SolutionProjectMode.Explicit;
        }

        private bool IsKnownSolutionFolder(string? folderId)
        {
            return !string.IsNullOrWhiteSpace(folderId)
                && Config.SolutionFolders.Any(folder => string.Equals(
                    folder.Id,
                    folderId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private void RemoveProjectSolutionFolderMappings(string projectReference)
        {
            foreach (string mappingReference in Config.ProjectSolutionFolders.Keys
                .Where(reference => ProjectReferencesEqual(
                    DirectoryInfo.FullName,
                    reference,
                    projectReference))
                .ToList())
            {
                Config.ProjectSolutionFolders.Remove(mappingReference);
            }
        }

        internal bool RegisterProject(DirectoryInfo projectDirectory, string? solutionFolderId = null)
        {
            if (!ProjectProviderRegistry.TryLoadProject(projectDirectory, out ProjectDefinition? project))
                return false;
            return RegisterProject(project, solutionFolderId);
        }

        internal bool RegisterProject(FileInfo projectFile, string? solutionFolderId = null)
        {
            return RegisterProject(projectFile, solutionFolderId, out _);
        }

        internal bool RegisterProject(
            FileInfo projectFile,
            string? solutionFolderId,
            out string errorMessage)
        {
            if (!ProjectProviderRegistry.TryLoadProject(
                projectFile,
                out ProjectDefinition? project,
                out errorMessage))
                return false;
            bool registered = RegisterProject(project!, solutionFolderId);
            if (!registered && string.IsNullOrWhiteSpace(errorMessage))
                errorMessage = $"无法将项目“{projectFile.Name}”添加到解决方案。";
            return registered;
        }

        private bool RegisterProject(ProjectDefinition project, string? solutionFolderId)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation($"添加项目“{project.Name}”", () => RegisterProject(project, solutionFolderId), result => result);
            EnsureExplicitProjectModePreservingProjects();
            foreach (string existingReference in Config.Projects
                .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                .ToList())
            {
                Config.Projects.Remove(existingReference);
                RemoveProjectSolutionFolderMappings(existingReference);
            }

            string projectReference = Path.GetRelativePath(DirectoryInfo.FullName, project.ProjectFile.FullName);
            if (!Config.Projects.Any(reference => string.Equals(reference, projectReference, StringComparison.OrdinalIgnoreCase)))
                Config.Projects.Add(projectReference);
            if (IsKnownSolutionFolder(solutionFolderId))
                Config.ProjectSolutionFolders[projectReference] = solutionFolderId!;

            SaveConfig();
            ReconcileExplicitProjects();
            EnsureStartupProject();
            return true;
        }

        internal bool RegisterSolutionItems(
            IEnumerable<string> paths,
            string? solutionFolderId,
            out string errorMessage)
        {
            if (_trackedMutationDepth == 0)
            {
                string trackedError = string.Empty;
                bool result = ExecuteTrackedMutation(
                    "添加解决方案项",
                    () => RegisterSolutionItems(paths, solutionFolderId, out trackedError),
                    succeeded => succeeded);
                errorMessage = trackedError;
                return result;
            }
            errorMessage = string.Empty;
            if (solutionFolderId != null && !IsKnownSolutionFolder(solutionFolderId))
            {
                errorMessage = "目标解决方案文件夹已经不存在。";
                return false;
            }

            var fullPaths = new List<string>();
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(path);
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    errorMessage = $"解决方案项路径无效：{ex.Message}";
                    return false;
                }
                if (!File.Exists(fullPath))
                {
                    errorMessage = $"解决方案项文件不存在：{fullPath}";
                    return false;
                }
                if (SolutionManager.IsSolutionFilePath(fullPath)
                    || ProjectProviderRegistry.IsSupportedProjectFilePath(fullPath))
                {
                    errorMessage = $"“{Path.GetFileName(fullPath)}”是解决方案或项目文件，请使用对应的打开/添加命令。";
                    return false;
                }
                fullPaths.Add(fullPath);
            }
            if (fullPaths.Count == 0)
            {
                errorMessage = "没有可添加的解决方案项。";
                return false;
            }

            EnsureExplicitProjectModePreservingProjects();
            string targetFolderId = solutionFolderId ?? EnsureDefaultSolutionItemsFolder();
            foreach (string fullPath in fullPaths)
            {
                SolutionItemDefinition? existing = Config.SolutionItems.FirstOrDefault(item =>
                    TryResolveSolutionItemPath(item.Path, out string existingPath)
                    && PathEquals(existingPath, fullPath));
                if (existing != null)
                {
                    existing.SolutionFolderId = targetFolderId;
                    continue;
                }

                Config.SolutionItems.Add(new SolutionItemDefinition
                {
                    Path = Path.GetRelativePath(DirectoryInfo.FullName, fullPath),
                    SolutionFolderId = targetFolderId,
                });
            }

            SaveConfig();
            ReconcileExplicitProjects();
            return true;
        }

        internal string? GetSolutionItemFolderId(string itemId)
        {
            return Config.SolutionItems.FirstOrDefault(item => string.Equals(
                item.Id,
                itemId,
                StringComparison.OrdinalIgnoreCase))?.SolutionFolderId;
        }

        internal bool MoveSolutionItemToFolder(string itemId, string? solutionFolderId)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("移动解决方案项", () => MoveSolutionItemToFolder(itemId, solutionFolderId), result => result);
            if (solutionFolderId != null && !IsKnownSolutionFolder(solutionFolderId))
                return false;
            SolutionItemDefinition? item = Config.SolutionItems.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                itemId,
                StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return false;
            if (string.Equals(item.SolutionFolderId, solutionFolderId, StringComparison.OrdinalIgnoreCase))
                return true;

            item.SolutionFolderId = solutionFolderId;
            SaveConfig();
            ReconcileExplicitProjects();
            return true;
        }

        internal bool RemoveSolutionItem(string itemId)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("从解决方案移除解决方案项", () => RemoveSolutionItem(itemId), result => result);
            SolutionItemDefinition? item = Config.SolutionItems.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                itemId,
                StringComparison.OrdinalIgnoreCase));
            if (item == null)
                return false;

            TryResolveSolutionItemPath(item.Path, out string fullPath);
            Config.SolutionItems.Remove(item);
            SaveConfig();
            ReconcileExplicitProjects();
            RestorePhysicalSolutionItem(fullPath);
            return true;
        }

        private void RestorePhysicalSolutionItem(string fullPath)
        {
            if (!File.Exists(fullPath) || IsRegisteredSolutionItemPath(fullPath))
                return;
            string? parentPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(parentPath))
                return;

            SolutionNode? parent = PathEquals(parentPath, DirectoryInfo.FullName)
                ? this
                : FindNodeByFullPath(parentPath);
            if (parent != null && !parent.VisualChildren.Any(node => PathEquals(node.FullPath, fullPath)))
                SolutionNodeFactory.AddFileNode(parent, new FileInfo(fullPath));
        }

        internal bool IsRegisteredSolutionItemPath(string fullPath)
        {
            return Config.SolutionItems.Any(item =>
                TryResolveSolutionItemPath(item.Path, out string itemPath)
                && PathEquals(itemPath, fullPath));
        }

        internal bool ShouldOmitPhysicalSolutionItem(ISolutionNode parent, FileInfo file)
        {
            return ReferenceEquals(parent, this) && IsRegisteredSolutionItemPath(file.FullName);
        }

        private string EnsureDefaultSolutionItemsFolder()
        {
            SolutionFolderDefinition? folder = Config.SolutionFolders.FirstOrDefault(item =>
                item.ParentId == null
                && string.Equals(item.Name, "解决方案项", StringComparison.OrdinalIgnoreCase));
            if (folder != null)
                return folder.Id;

            folder = new SolutionFolderDefinition { Name = "解决方案项" };
            Config.SolutionFolders.Add(folder);
            return folder.Id;
        }

        private bool TryResolveSolutionItemPath(string reference, out string fullPath)
        {
            try
            {
                fullPath = Path.GetFullPath(Path.IsPathRooted(reference)
                    ? reference
                    : Path.Combine(DirectoryInfo.FullName, reference));
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                fullPath = reference;
                return false;
            }
        }

        internal bool RegisterDroppedProjects(
            IEnumerable<string> paths,
            string? solutionFolderId,
            out string errorMessage)
        {
            if (_trackedMutationDepth == 0)
            {
                string trackedError = string.Empty;
                bool result = ExecuteTrackedMutation(
                    "添加项目",
                    () => RegisterDroppedProjects(paths, solutionFolderId, out trackedError),
                    succeeded => succeeded);
                errorMessage = trackedError;
                return result;
            }
            errorMessage = string.Empty;
            if (solutionFolderId != null && !IsKnownSolutionFolder(solutionFolderId))
            {
                errorMessage = "目标解决方案文件夹已经不存在。";
                return false;
            }

            var projects = new List<ProjectDefinition>();
            var errors = new List<string>();
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ProjectDefinition? project = null;
                string loadError = string.Empty;
                bool loaded;
                if (File.Exists(path))
                {
                    loaded = ProjectProviderRegistry.TryLoadProject(
                        new FileInfo(path),
                        out project,
                        out loadError);
                }
                else if (Directory.Exists(path))
                {
                    loaded = ProjectProviderRegistry.TryLoadProject(
                        new DirectoryInfo(path),
                        out project,
                        out loadError);
                }
                else
                {
                    loaded = false;
                    loadError = $"项目路径不存在：{path}";
                }
                if (loaded && project != null)
                    projects.Add(project);
                else
                    errors.Add(string.IsNullOrWhiteSpace(loadError) ? $"无法识别项目：{path}" : loadError);
            }
            if (errors.Count > 0)
            {
                errorMessage = string.Join(Environment.NewLine, errors);
                return false;
            }

            projects = projects
                .GroupBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            if (projects.Count == 0)
            {
                errorMessage = "没有可添加的项目。";
                return false;
            }

            EnsureExplicitProjectModePreservingProjects();
            foreach (ProjectDefinition project in projects)
            {
                foreach (string existingReference in Config.Projects
                    .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                    .ToList())
                {
                    Config.Projects.Remove(existingReference);
                    RemoveProjectSolutionFolderMappings(existingReference);
                }

                string projectReference = Path.GetRelativePath(DirectoryInfo.FullName, project.ProjectFile.FullName);
                Config.Projects.Add(projectReference);
                if (solutionFolderId != null)
                    Config.ProjectSolutionFolders[projectReference] = solutionFolderId;
            }

            SaveConfig();
            ReconcileExplicitProjects();
            EnsureStartupProject();
            return true;
        }

        internal bool RegisterDroppedSolutionResources(
            IEnumerable<string> paths,
            string? solutionFolderId,
            out string errorMessage)
        {
            return RegisterDroppedSolutionResources(paths, solutionFolderId, out _, out errorMessage);
        }

        internal bool RegisterDroppedSolutionResources(
            IEnumerable<string> paths,
            string? solutionFolderId,
            out IReadOnlyList<SolutionNode> registeredNodes,
            out string errorMessage)
        {
            if (_trackedMutationDepth == 0)
            {
                IReadOnlyList<SolutionNode> trackedNodes = Array.Empty<SolutionNode>();
                string trackedError = string.Empty;
                bool result = ExecuteTrackedMutation(
                    "添加项目和解决方案项",
                    () => RegisterDroppedSolutionResources(
                        paths,
                        solutionFolderId,
                        out trackedNodes,
                        out trackedError),
                    succeeded => succeeded);
                registeredNodes = trackedNodes;
                errorMessage = trackedError;
                return result;
            }
            registeredNodes = Array.Empty<SolutionNode>();
            errorMessage = string.Empty;
            if (solutionFolderId != null && !IsKnownSolutionFolder(solutionFolderId))
            {
                errorMessage = "目标解决方案文件夹已经不存在。";
                return false;
            }

            var projects = new List<ProjectDefinition>();
            var solutionItemPaths = new List<string>();
            var errors = new List<string>();
            foreach (string path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (Directory.Exists(path))
                {
                    if (ProjectProviderRegistry.TryLoadProject(
                        new DirectoryInfo(path),
                        out ProjectDefinition? directoryProject,
                        out string directoryError)
                        && directoryProject != null)
                    {
                        projects.Add(directoryProject);
                    }
                    else
                    {
                        errors.Add(directoryError);
                    }
                    continue;
                }
                if (!File.Exists(path))
                {
                    errors.Add($"路径不存在：{path}");
                    continue;
                }
                if (SolutionManager.IsSolutionFilePath(path))
                {
                    errors.Add($"“{Path.GetFileName(path)}”是解决方案文件，应使用打开解决方案。" );
                    continue;
                }
                if (ProjectProviderRegistry.IsSupportedProjectFilePath(path))
                {
                    if (ProjectProviderRegistry.TryLoadProject(
                        new FileInfo(path),
                        out ProjectDefinition? fileProject,
                        out string projectError)
                        && fileProject != null)
                    {
                        projects.Add(fileProject);
                    }
                    else
                    {
                        errors.Add(projectError);
                    }
                    continue;
                }
                solutionItemPaths.Add(Path.GetFullPath(path));
            }
            if (errors.Count > 0)
            {
                errorMessage = string.Join(Environment.NewLine, errors);
                return false;
            }

            projects = projects
                .GroupBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            solutionItemPaths = solutionItemPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (projects.Count == 0 && solutionItemPaths.Count == 0)
            {
                errorMessage = "没有可添加的项目或解决方案项。";
                return false;
            }

            EnsureExplicitProjectModePreservingProjects();
            foreach (ProjectDefinition project in projects)
            {
                foreach (string existingReference in Config.Projects
                    .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                    .ToList())
                {
                    Config.Projects.Remove(existingReference);
                    RemoveProjectSolutionFolderMappings(existingReference);
                }

                string projectReference = Path.GetRelativePath(DirectoryInfo.FullName, project.ProjectFile.FullName);
                Config.Projects.Add(projectReference);
                if (solutionFolderId != null)
                    Config.ProjectSolutionFolders[projectReference] = solutionFolderId;
            }

            string? solutionItemFolderId = solutionItemPaths.Count == 0
                ? null
                : solutionFolderId ?? EnsureDefaultSolutionItemsFolder();
            foreach (string fullPath in solutionItemPaths)
            {
                SolutionItemDefinition? existing = Config.SolutionItems.FirstOrDefault(item =>
                    TryResolveSolutionItemPath(item.Path, out string existingPath)
                    && PathEquals(existingPath, fullPath));
                if (existing == null)
                {
                    Config.SolutionItems.Add(new SolutionItemDefinition
                    {
                        Path = Path.GetRelativePath(DirectoryInfo.FullName, fullPath),
                        SolutionFolderId = solutionItemFolderId,
                    });
                }
                else
                {
                    existing.SolutionFolderId = solutionItemFolderId;
                }
            }

            SaveConfig();
            ReconcileExplicitProjects();
            EnsureStartupProject();
            var projectPaths = projects
                .Select(project => project.ProjectFile.FullName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var itemPaths = solutionItemPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            registeredNodes = VisualChildren.GetAllVisualChildren()
                .Where(node => node switch
                {
                    ProjectNode projectNode => projectPaths.Contains(projectNode.Project.ProjectFile.FullName),
                    SolutionItemNode solutionItemNode => itemPaths.Contains(solutionItemNode.FullPath),
                    _ => false,
                })
                .ToList();
            return true;
        }

        internal bool UnregisterProject(ProjectDefinition project)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation($"从解决方案移除项目“{project.Name}”", () => UnregisterProject(project), result => result);
            bool changed = false;
            foreach (string existingReference in Config.Projects
                .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                .ToList())
            {
                changed |= Config.Projects.Remove(existingReference);
            }

            if (changed)
            {
                foreach (string mappingReference in Config.ProjectConfigurations.Keys
                    .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                    .ToList())
                {
                    Config.ProjectConfigurations.Remove(mappingReference);
                }
                foreach (string projectReference in Config.ProjectSolutionFolders.Keys
                    .Where(reference => ProjectReferenceMatches(DirectoryInfo.FullName, reference, project))
                    .ToList())
                {
                    Config.ProjectSolutionFolders.Remove(projectReference);
                }
                if (IsConfiguredStartupProject(project))
                    Config.StartupProject = string.Empty;
                SaveConfig();
                EnsureStartupProject();
            }
            return changed;
        }

        internal bool RemoveProject(ProjectDefinition project)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation($"从解决方案移除项目“{project.Name}”", () => RemoveProject(project), result => result);
            if (!IsExplicitProjectMode || !UnregisterProject(project))
                return false;

            RemoveNodesByPath(project.ProjectDirectory.FullName);
            if (PathEquals(project.ProjectDirectory.FullName, DirectoryInfo.FullName))
                ReloadSolutionState();
            else
                RestorePhysicalFolder(project.ProjectDirectory);
            return true;
        }

        internal bool RemoveProjectReference(string projectReference)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("移除不可用项目引用", () => RemoveProjectReference(projectReference), result => result);
            if (!Config.Projects.Remove(projectReference))
                return false;

            if (ProjectReferencesEqual(DirectoryInfo.FullName, Config.StartupProject, projectReference))
                Config.StartupProject = string.Empty;
            foreach (string mappingReference in Config.ProjectConfigurations.Keys
                .Where(reference => ProjectReferencesEqual(DirectoryInfo.FullName, reference, projectReference))
                .ToList())
            {
                Config.ProjectConfigurations.Remove(mappingReference);
            }
            RemoveProjectSolutionFolderMappings(projectReference);
            SaveConfig();
            foreach (UnavailableProjectNode node in VisualChildren.OfType<UnavailableProjectNode>()
                .Where(node => string.Equals(node.ProjectReference, projectReference, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                RemoveChild(node);
            }
            EnsureStartupProject();
            return true;
        }

    }
}
