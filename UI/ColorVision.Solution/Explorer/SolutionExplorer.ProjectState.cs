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
        internal static bool ProjectReferencesEqual(
            string solutionDirectory,
            string? firstReference,
            string? secondReference)
        {
            if (string.IsNullOrWhiteSpace(firstReference) || string.IsNullOrWhiteSpace(secondReference))
                return false;

            try
            {
                string firstPath = Path.GetFullPath(Path.IsPathRooted(firstReference)
                    ? firstReference
                    : Path.Combine(solutionDirectory, firstReference));
                string secondPath = Path.GetFullPath(Path.IsPathRooted(secondReference)
                    ? secondReference
                    : Path.Combine(solutionDirectory, secondReference));
                return PathEquals(firstPath, secondPath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        internal static bool ProjectReferenceMatches(string solutionDirectory, string projectReference, ProjectDefinition project)
        {
            if (string.IsNullOrWhiteSpace(projectReference))
                return false;

            try
            {
                string referencePath = Path.GetFullPath(Path.IsPathRooted(projectReference)
                    ? projectReference
                    : Path.Combine(solutionDirectory, projectReference));
                return PathEquals(referencePath, project.ProjectFile.FullName)
                    || PathEquals(referencePath, project.ProjectDirectory.FullName);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        internal bool IsProjectIncluded(ProjectDefinition project)
        {
            return IsProjectReferenceIncluded(Config.ProjectMode, DirectoryInfo.FullName, Config.Projects, project);
        }

        internal static bool IsProjectReferenceIncluded(
            SolutionProjectMode projectMode,
            string solutionDirectory,
            IEnumerable<string> projectReferences,
            ProjectDefinition project)
        {
            return projectMode != SolutionProjectMode.Explicit
                || projectReferences.Any(reference => ProjectReferenceMatches(solutionDirectory, reference, project));
        }

        internal bool ShouldOmitPhysicalProjectDirectory(ISolutionNode parent, DirectoryInfo directory)
        {
            if (!IsExplicitProjectMode || ReferenceEquals(parent, this))
                return false;

            return ProjectProviderRegistry.TryLoadProject(directory, out ProjectDefinition? project)
                && project != null
                && IsProjectIncluded(project);
        }

        private void ReconcileExplicitProjects(bool reloadLoadedProjects = false)
        {
            if (!IsExplicitProjectMode)
                return;

            NormalizeSolutionOrganization();
            Dictionary<string, ProjectReferenceLoadResult>? preparedProjectReferences =
                Interlocked.Exchange(ref _preparedProjectReferences, null);
            var projects = new List<(string Reference, ProjectDefinition Project)>();
            var unavailable = new List<(string Reference, string ResolvedPath, string ErrorMessage)>();
            foreach (string reference in Config.Projects.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ProjectReferenceLoadResult? loadResult = null;
                bool hasPreparedResult = preparedProjectReferences != null
                    && preparedProjectReferences.TryGetValue(reference, out loadResult);
                if (!hasPreparedResult)
                {
                    bool succeeded = TryResolveProjectReference(
                        DirectoryInfo.FullName,
                        reference,
                        out ProjectDefinition? project,
                        out string resolvedPath,
                        out string loadError);
                    loadResult = new ProjectReferenceLoadResult(
                        reference,
                        succeeded ? project : null,
                        resolvedPath,
                        loadError);
                }

                loadResult ??= new ProjectReferenceLoadResult(
                    reference,
                    null,
                    string.Empty,
                    "项目加载没有返回结果。");
                if (loadResult.Project != null)
                {
                    projects.Add((reference, loadResult.Project));
                }
                else
                {
                    unavailable.Add((reference, loadResult.ResolvedPath, loadResult.ErrorMessage));
                }
            }
            projects = projects
                .GroupBy(item => item.Project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            List<SolutionFolderNode> currentFolderNodes = VisualChildren.GetAllVisualChildren()
                .OfType<SolutionFolderNode>()
                .ToList();
            var retainedFolderNodes = new HashSet<SolutionFolderNode>();
            var folderNodes = new Dictionary<string, SolutionFolderNode>(StringComparer.OrdinalIgnoreCase);
            foreach (SolutionFolderDefinition definition in Config.SolutionFolders)
            {
                SolutionFolderNode? folderNode = currentFolderNodes.FirstOrDefault(node =>
                    !retainedFolderNodes.Contains(node)
                    && string.Equals(node.FolderId, definition.Id, StringComparison.OrdinalIgnoreCase));
                if (folderNode == null)
                {
                    folderNode = new SolutionFolderNode(this, definition);
                }
                else
                {
                    folderNode.UpdateDefinition(definition);
                }
                retainedFolderNodes.Add(folderNode);
                folderNodes.Add(definition.Id, folderNode);
            }
            foreach (SolutionFolderDefinition definition in Config.SolutionFolders)
            {
                SolutionNode parent = definition.ParentId != null
                    && folderNodes.TryGetValue(definition.ParentId, out SolutionFolderNode? parentFolder)
                        ? parentFolder
                        : this;
                ReparentSolutionNode(folderNodes[definition.Id], parent);
            }

            List<ProjectNode> currentProjectNodes = VisualChildren.GetAllVisualChildren()
                .OfType<ProjectNode>()
                .ToList();
            var retainedProjectNodes = new HashSet<ProjectNode>();
            foreach (var item in projects)
            {
                ProjectNode? projectNode = currentProjectNodes.FirstOrDefault(node =>
                    !retainedProjectNodes.Contains(node)
                    && string.Equals(
                        node.Project.ProjectFile.FullName,
                        item.Project.ProjectFile.FullName,
                        StringComparison.OrdinalIgnoreCase)
                    && node.CanReuseFor(item.Project));
                if (projectNode == null)
                {
                    foreach (ProjectNode replacedNode in currentProjectNodes.Where(node =>
                        !retainedProjectNodes.Contains(node)
                        && string.Equals(
                            node.Project.ProjectFile.FullName,
                            item.Project.ProjectFile.FullName,
                            StringComparison.OrdinalIgnoreCase)).ToList())
                    {
                        replacedNode.Parent?.RemoveChild(replacedNode);
                        replacedNode.Dispose();
                        currentProjectNodes.Remove(replacedNode);
                    }
                    projectNode = SolutionNodeFactory.CreateProjectNode(item.Project, this);
                }
                else if (reloadLoadedProjects)
                {
                    projectNode.UpdateProjectDefinition(item.Project);
                }

                retainedProjectNodes.Add(projectNode);
                projectNode.SetStartupProjectState(IsConfiguredStartupProject(item.Project));
                ReparentSolutionNode(projectNode, GetSolutionItemParent(item.Reference, folderNodes));
            }
            foreach (ProjectNode node in currentProjectNodes.Where(node => !retainedProjectNodes.Contains(node)))
            {
                node.Parent?.RemoveChild(node);
                node.Dispose();
            }

            List<UnavailableProjectNode> currentUnavailableNodes = VisualChildren.GetAllVisualChildren()
                .OfType<UnavailableProjectNode>()
                .ToList();
            var retainedUnavailableNodes = new HashSet<UnavailableProjectNode>();
            foreach (var item in unavailable)
            {
                RemovePhysicalNodeShadowingProjectReference(item.ResolvedPath);
                UnavailableProjectNode? unavailableNode = currentUnavailableNodes.FirstOrDefault(node =>
                    !retainedUnavailableNodes.Contains(node)
                    && ProjectReferencesEqual(
                        DirectoryInfo.FullName,
                        node.ProjectReference,
                        item.Reference));
                if (unavailableNode == null)
                {
                    unavailableNode = new UnavailableProjectNode(
                        this,
                        item.Reference,
                        item.ResolvedPath,
                        item.ErrorMessage);
                }
                else
                {
                    unavailableNode.UpdateUnavailableState(item.ResolvedPath, item.ErrorMessage);
                }
                retainedUnavailableNodes.Add(unavailableNode);
                ReparentSolutionNode(
                    unavailableNode,
                    GetSolutionItemParent(item.Reference, folderNodes));
            }
            foreach (UnavailableProjectNode node in currentUnavailableNodes.Where(node => !retainedUnavailableNodes.Contains(node)))
                node.Parent?.RemoveChild(node);

            List<SolutionItemNode> currentSolutionItemNodes = VisualChildren.GetAllVisualChildren()
                .OfType<SolutionItemNode>()
                .ToList();
            var retainedSolutionItemNodes = new HashSet<SolutionItemNode>();
            foreach (SolutionItemDefinition definition in Config.SolutionItems)
            {
                if (!TryResolveSolutionItemPath(definition.Path, out string solutionItemPath))
                    solutionItemPath = Path.Combine(DirectoryInfo.FullName, $".invalid-solution-item-{definition.Id}");

                SolutionItemNode? solutionItemNode = currentSolutionItemNodes.FirstOrDefault(node =>
                    !retainedSolutionItemNodes.Contains(node)
                    && node.CanReuseFor(definition, solutionItemPath));
                if (solutionItemNode == null)
                {
                    foreach (SolutionItemNode replacedNode in currentSolutionItemNodes.Where(node =>
                        !retainedSolutionItemNodes.Contains(node)
                        && string.Equals(node.ItemId, definition.Id, StringComparison.OrdinalIgnoreCase)).ToList())
                    {
                        replacedNode.Parent?.RemoveChild(replacedNode);
                        replacedNode.Dispose();
                        currentSolutionItemNodes.Remove(replacedNode);
                    }
                    solutionItemNode = new SolutionItemNode(this, definition, solutionItemPath);
                }
                else
                {
                    solutionItemNode.UpdateState(definition, solutionItemPath);
                }

                retainedSolutionItemNodes.Add(solutionItemNode);
                SolutionNode parent = GetSolutionItemParent(definition, folderNodes);
                foreach (FileNode physicalNode in VisualChildren.OfType<FileNode>()
                    .Where(node => node is not SolutionItemNode && PathEquals(node.FullPath, solutionItemPath))
                    .ToList())
                {
                    RemoveChild(physicalNode);
                }
                ReparentSolutionNode(solutionItemNode, parent);
            }
            foreach (SolutionItemNode node in currentSolutionItemNodes.Where(node => !retainedSolutionItemNodes.Contains(node)))
            {
                node.Parent?.RemoveChild(node);
                node.Dispose();
            }

            foreach (SolutionFolderNode node in currentFolderNodes.Where(node => !retainedFolderNodes.Contains(node)))
                node.Parent?.RemoveChild(node);
        }

        private void RemovePhysicalNodeShadowingProjectReference(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
                return;

            foreach (SolutionNode node in VisualChildren.GetAllVisualChildren()
                .Where(node => node is not ProjectNode
                    and not UnavailableProjectNode
                    and not SolutionItemNode
                    && !string.IsNullOrWhiteSpace(node.FullPath)
                    && string.Equals(node.FullPath, resolvedPath, StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                node.Parent?.RemoveChild(node);
                if (node is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private static void ReparentSolutionNode(SolutionNode node, SolutionNode parent)
        {
            if (ReferenceEquals(node.Parent, parent) && parent.VisualChildren.Contains(node))
                return;

            node.Parent?.RemoveChild(node);
            parent.AddChild(node);
        }

        private SolutionNode GetSolutionItemParent(
            string projectReference,
            Dictionary<string, SolutionFolderNode> folderNodes)
        {
            string? folderId = Config.ProjectSolutionFolders
                .FirstOrDefault(pair => ProjectReferencesEqual(
                    DirectoryInfo.FullName,
                    pair.Key,
                    projectReference))
                .Value;
            return folderId != null && folderNodes.TryGetValue(folderId, out SolutionFolderNode? folderNode)
                ? folderNode
                : this;
        }

        private SolutionNode GetSolutionItemParent(
            SolutionItemDefinition solutionItem,
            Dictionary<string, SolutionFolderNode> folderNodes)
        {
            return solutionItem.SolutionFolderId != null
                && folderNodes.TryGetValue(solutionItem.SolutionFolderId, out SolutionFolderNode? folderNode)
                    ? folderNode
                    : this;
        }

        private SolutionFolderNode? FindSolutionFolderNode(string folderId)
        {
            return VisualChildren.GetAllVisualChildren()
                .OfType<SolutionFolderNode>()
                .FirstOrDefault(node => string.Equals(
                    node.FolderId,
                    folderId,
                    StringComparison.OrdinalIgnoreCase));
        }

        private bool HasRootProjectReference()
        {
            if (!IsExplicitProjectMode)
                return false;

            return Config.Projects.Any(reference =>
            {
                if (_preparedProjectReferences?.TryGetValue(
                    reference,
                    out ProjectReferenceLoadResult? loadResult) == true)
                {
                    return loadResult.Project != null
                        && PathEquals(
                            loadResult.Project.ProjectDirectory.FullName,
                            DirectoryInfo.FullName);
                }
                return TryResolveProjectReference(DirectoryInfo.FullName, reference, out ProjectDefinition? project, out _, out _)
                    && project != null
                    && PathEquals(project.ProjectDirectory.FullName, DirectoryInfo.FullName);
            });
        }

        internal static DirectoryInfo ResolveRootDirectory(FileInfo solutionFile, string? configuredRootPath)
        {
            DirectoryInfo fallback = solutionFile.Directory ?? new DirectoryInfo(solutionFile.FullName);
            if (string.IsNullOrWhiteSpace(configuredRootPath))
                return fallback;

            try
            {
                string fullPath = Path.GetFullPath(Path.IsPathRooted(configuredRootPath)
                    ? configuredRootPath
                    : Path.Combine(fallback.FullName, configuredRootPath));
                return Directory.Exists(fullPath) ? new DirectoryInfo(fullPath) : fallback;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return fallback;
            }
        }

        internal static bool TryResolveProjectReference(
            string solutionDirectory,
            string projectReference,
            out ProjectDefinition? project,
            out string resolvedPath,
            out string errorMessage)
        {
            project = null;
            resolvedPath = string.Empty;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(projectReference))
            {
                errorMessage = "项目引用为空。";
                return false;
            }

            try
            {
                resolvedPath = Path.GetFullPath(Path.IsPathRooted(projectReference)
                    ? projectReference
                    : Path.Combine(solutionDirectory, projectReference));
                if (File.Exists(resolvedPath))
                    return ProjectProviderRegistry.TryLoadProject(
                        new FileInfo(resolvedPath),
                        out project,
                        out errorMessage);
                if (Directory.Exists(resolvedPath))
                    return ProjectProviderRegistry.TryLoadProject(
                        new DirectoryInfo(resolvedPath),
                        out project,
                        out errorMessage);
                errorMessage = $"项目路径不存在：{resolvedPath}";
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errorMessage = $"项目引用无效：{ex.Message}";
            }
            return false;
        }

        private void RefreshProjectFileState(string projectFilePath)
        {
            if (!IsExplicitProjectMode)
            {
                RefreshDirectoryNode(Path.GetDirectoryName(projectFilePath));
                return;
            }

            RefreshExplicitProjectState();
        }

        internal void RefreshExplicitProjectState()
        {
            if (!IsExplicitProjectMode)
                return;

            CancelActiveProjectRefresh();
            var dispatcher = Application.Current?.Dispatcher;
            void QueueRefresh()
            {
                if (_disposed)
                    return;
                _projectChangedDebounceTimer.Stop();
                _projectChangedDebounceTimer.Start();
            }

            if (dispatcher == null || dispatcher.CheckAccess())
                QueueRefresh();
            else
                dispatcher.BeginInvoke(QueueRefresh);
        }

        internal async Task<ProjectRefreshResult> RefreshExplicitProjectStateAsync(
            bool reloadSolutionState = false,
            CancellationToken cancellationToken = default)
        {
            if (!IsExplicitProjectMode)
            {
                return new ProjectRefreshResult(
                    false,
                    ErrorMessage: "当前解决方案没有使用显式项目引用。");
            }

            _projectChangedDebounceTimer.Stop();
            var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource? previousCancellation = Interlocked.Exchange(
                ref _projectRefreshCancellation,
                requestCancellation);
            try
            {
                previousCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            bool refreshGateEntered = false;
            try
            {
                await _solutionRefreshGate.WaitAsync(requestCancellation.Token);
                refreshGateEntered = true;
                requestCancellation.Token.ThrowIfCancellationRequested();
                Dictionary<string, ProjectReferenceLoadResult> preparedProjectReferences =
                    await LoadProjectReferencesAsync(
                        DirectoryInfo.FullName,
                        Config,
                        requestCancellation.Token);
                requestCancellation.Token.ThrowIfCancellationRequested();
                if (_disposed
                    || !ReferenceEquals(
                        Interlocked.CompareExchange(ref _projectRefreshCancellation, null, null),
                        requestCancellation))
                {
                    return new ProjectRefreshResult(false, Canceled: true);
                }

                _preparedProjectReferences = preparedProjectReferences;
                if (reloadSolutionState)
                {
                    ReloadSolutionState();
                }
                else
                {
                    ReconcileExplicitProjects(reloadLoadedProjects: true);
                    EnsureStartupProject();
                    RefreshConfigurationCommandSurfaces();
                }
                return new ProjectRefreshResult(true);
            }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
            {
                return new ProjectRefreshResult(
                    false,
                    ErrorMessage: "刷新项目已取消。",
                    Canceled: true);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                return new ProjectRefreshResult(false, ErrorMessage: ex.Message);
            }
            finally
            {
                if (refreshGateEntered)
                    _solutionRefreshGate.Release();
                Interlocked.CompareExchange(
                    ref _projectRefreshCancellation,
                    null,
                    requestCancellation);
                requestCancellation.Dispose();
            }
        }

        internal async Task RefreshExplicitProjectStateWithFeedbackAsync(
            bool reloadSolutionState = false)
        {
            ProjectRefreshResult result = await RefreshExplicitProjectStateAsync(reloadSolutionState);
            if (!result.Succeeded && !result.Canceled)
                ShowUserError(result.ErrorMessage);
        }

        private void CancelActiveProjectRefresh()
        {
            try
            {
                Interlocked.CompareExchange(ref _projectRefreshCancellation, null, null)?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        internal void ApplyProjectMutation(ProjectDefinition updatedProject)
        {
            ArgumentNullException.ThrowIfNull(updatedProject);
            ApplyProjectMutations([updatedProject]);
        }

        private void ApplyProjectMutations(List<ProjectDefinition> updatedProjects)
        {
            ArgumentNullException.ThrowIfNull(updatedProjects);
            if (!IsExplicitProjectMode)
            {
                ReloadSolutionState();
                return;
            }

            _projectChangedDebounceTimer.Stop();
            CancelActiveProjectRefresh();
            if (updatedProjects.Count > 0)
            {
                _preparedProjectReferences = CaptureLoadedProjectReferences(updatedProjects);
                ReconcileExplicitProjects(reloadLoadedProjects: true);
            }

            foreach (ProjectNode projectNode in VisualChildren.GetAllVisualChildren().OfType<ProjectNode>())
                projectNode.RefreshConfigurationState();
            UpdateStartupProjectState();
            RefreshConfigurationCommandSurfaces();
        }

        private Dictionary<string, ProjectReferenceLoadResult> CaptureLoadedProjectReferences(
            List<ProjectDefinition> updatedProjects)
        {
            List<ProjectNode> projectNodes = VisualChildren.GetAllVisualChildren()
                .OfType<ProjectNode>()
                .ToList();
            List<UnavailableProjectNode> unavailableNodes = VisualChildren.GetAllVisualChildren()
                .OfType<UnavailableProjectNode>()
                .ToList();
            var results = new Dictionary<string, ProjectReferenceLoadResult>(StringComparer.OrdinalIgnoreCase);
            foreach (string reference in Config.Projects.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ProjectDefinition? project = updatedProjects.FirstOrDefault(item =>
                        ProjectReferenceMatches(DirectoryInfo.FullName, reference, item))
                    ?? projectNodes
                        .Select(node => node.Project)
                        .FirstOrDefault(item => ProjectReferenceMatches(
                            DirectoryInfo.FullName,
                            reference,
                            item));
                if (project != null)
                {
                    results[reference] = new ProjectReferenceLoadResult(
                        reference,
                        project,
                        project.ProjectFile.FullName,
                        string.Empty);
                    continue;
                }

                UnavailableProjectNode? unavailableNode = unavailableNodes.FirstOrDefault(node =>
                    ProjectReferencesEqual(
                        DirectoryInfo.FullName,
                        reference,
                        node.ProjectReference));
                results[reference] = new ProjectReferenceLoadResult(
                    reference,
                    null,
                    unavailableNode?.ResolvedPath ?? ResolveProjectReferencePath(reference),
                    unavailableNode?.LoadError ?? "项目当前未加载。");
            }
            return results;
        }

        private string ResolveProjectReferencePath(string projectReference)
        {
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(projectReference)
                    ? projectReference
                    : Path.Combine(DirectoryInfo.FullName, projectReference));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return string.Empty;
            }
        }

        internal void ReloadSolutionState()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(ReloadSolutionState);
                return;
            }

            _projectChangedDebounceTimer.Stop();
            SolutionStateReloading?.Invoke(this, EventArgs.Empty);
            var expandedPaths = VisualChildren.GetAllVisualChildren()
                .Where(node => node.IsExpanded && !string.IsNullOrWhiteSpace(node.FullPath))
                .Select(node => node.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            DisposeVisualChildren(this);
            VisualChildren.Clear();

            // A user-requested refresh must observe the current file system,
            // rather than replaying a possibly stale persisted tree cache.
            if (!HasRootProjectReference())
                SolutionNodeFactory.PopulateChildren(this, DirectoryInfo, cache: null);
            ReconcileExplicitProjects();

            foreach (SolutionNode node in VisualChildren.GetAllVisualChildren()
                .Where(node => expandedPaths.Contains(node.FullPath)))
            {
                node.IsExpanded = true;
            }

            EnsureStartupProject();
            RefreshConfigurationCommandSurfaces();

            if (Cache != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        Cache.RebuildCache(DirectoryInfo.FullName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"刷新后重建缓存失败: {ex.Message}");
                    }
                });
            }

            SolutionStateReloaded?.Invoke(this, EventArgs.Empty);
        }

        private void RefreshConfigurationCommandSurfaces()
        {
            NotifyPropertyChanged(nameof(ActiveConfiguration));
            NotifyPropertyChanged(nameof(ActivePlatform));
            NotifyPropertyChanged(nameof(ActiveConfigurationDisplay));
            MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Configuration);
            MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Platform);
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshDirectoryNode(string? directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
                return;

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                SolutionNode? existingNode = FindNodeByFullPath(directoryPath);
                if (existingNode?.Parent == null)
                    return;

                SolutionNode parent = existingNode.Parent;
                bool wasExpanded = existingNode.IsExpanded;
                parent.RemoveChild(existingNode);
                if (existingNode is IDisposable disposable)
                    disposable.Dispose();

                FolderNode replacement = SolutionNodeFactory.CreateFolderNode(new DirectoryInfo(directoryPath), this);
                parent.AddChild(replacement);
                replacement.IsExpanded = wasExpanded;
            });
        }

        private void RemoveNodesByPath(string fullPath)
        {
            foreach (SolutionNode node in FindNodesByFullPath(fullPath))
            {
                node.Parent?.RemoveChild(node);
                if (node is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private void RestorePhysicalFolder(DirectoryInfo projectDirectory)
        {
            if (!projectDirectory.Exists || !IsPathWithinSolution(projectDirectory.FullName))
                return;

            DirectoryInfo? parentDirectory = projectDirectory.Parent;
            if (parentDirectory == null || PathEquals(projectDirectory.FullName, DirectoryInfo.FullName))
                return;

            SolutionNode? parentNode = PathEquals(parentDirectory.FullName, DirectoryInfo.FullName)
                ? this
                : FindNodeByFullPath(parentDirectory.FullName);
            if (parentNode == null)
                return;
            if (parentNode is FolderNode unloadedFolder && !unloadedFolder.AreChildrenLoaded)
            {
                unloadedFolder.MarkChildrenChanged();
                return;
            }

            SolutionNodeFactory.AddFolderNode(parentNode, projectDirectory);
        }

    }
}
