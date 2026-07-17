using ColorVision.Common.MVVM;
using ColorVision.Solution.FolderMeta;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// A project is a first-class workspace node backed by one provider-owned
    /// project file. It is intentionally stricter than a normal folder until
    /// project rename/move transactions are implemented.
    /// </summary>
    public sealed class ProjectNode : FolderNode
    {
        internal override string? PhysicalDeletePath => SolutionExplorer?.IsExplicitProjectMode == true
            ? null
            : base.PhysicalDeletePath;

        private FileSystemWatcher? _externalProjectWatcher;
        private FileSystemWatcher? _externalProjectFileWatcher;
        private readonly object _externalChangeSync = new();
        private bool _projectDisposed;

        public ProjectDefinition Project { get; private set; }
        public IReadOnlyList<ProjectCapabilityDescriptor> Capabilities { get; private set; }
        public SolutionExplorer? SolutionExplorer { get; }
        public override string? EditorResourcePath => Project.ProjectFile.FullName;
        public override SolutionDeleteKind DeleteKind => SolutionExplorer?.IsExplicitProjectMode == true
            ? SolutionDeleteKind.RemoveFromSolution
            : base.DeleteKind;

        public RelayCommand EditProjectFileCommand { get; }
        public RelayCommand RemoveFromSolutionCommand { get; }
        public RelayCommand ToggleShowAllFilesCommand { get; }
        public bool ShowAllFiles { get; private set; }
        public override bool IsStartupProject => _isStartupProject;
        private bool _isStartupProject;

        public ProjectNode(IFolderMeta folderMeta, ProjectDefinition project, SolutionExplorer? solutionExplorer = null)
            : base(folderMeta)
        {
            Project = project;
            SolutionExplorer = solutionExplorer;
            string projectName = string.IsNullOrWhiteSpace(project.Name) ? DirectoryInfo.Name : project.Name;
            Name1 = string.IsNullOrWhiteSpace(project.LoadError) ? projectName : $"{projectName} (加载失败)";
            CanReName = false;
            CanCut = false;
            CanCopy = false;
            CanDelete = solutionExplorer?.CanModifySolutionStructure != false;
            Capabilities = ProjectProviderRegistry.GetCapabilities(GetExecutionProject());
            EditProjectFileCommand = new RelayCommand(_ => EditProjectFile(), _ => Project.ProjectFile.Exists);
            ToggleShowAllFilesCommand = new RelayCommand(_ => ToggleShowAllFiles());
            RemoveFromSolutionCommand = new RelayCommand(
                _ => SolutionExplorer?.RemoveProject(Project),
                _ => SolutionExplorer?.IsExplicitProjectMode == true
                    && SolutionExplorer.CanModifySolutionStructure);
            SetStartupProjectState(SolutionExplorer?.IsConfiguredStartupProject(Project) == true);
            InitializeExternalProjectWatcher();
        }

        public override void Refresh()
        {
            if (SolutionExplorer != null)
                SolutionExplorer.ReloadSolutionState();
            else
                base.Refresh();
        }

        public bool CanExecuteCapability(string capabilityId)
        {
            ProjectDefinition executionProject = GetExecutionProject();
            if (string.Equals(capabilityId, ProjectCapabilityIds.Build, StringComparison.OrdinalIgnoreCase)
                && SolutionExplorer != null)
            {
                return global::ColorVision.Solution.Explorer.SolutionExplorer.CanBuildProject(executionProject);
            }
            return ProjectProviderRegistry.CanExecuteCapability(executionProject, capabilityId);
        }

        public bool ExecuteCapability(string capabilityId)
        {
            if (string.Equals(capabilityId, ProjectCapabilityIds.Build, StringComparison.OrdinalIgnoreCase)
                && SolutionExplorer != null)
            {
                return SolutionExplorer.BuildProject(Project);
            }

            bool executed = ProjectProviderRegistry.ExecuteCapability(GetExecutionProject(), capabilityId);
            if (!executed)
                ShowUserInfo("当前项目能力无法执行，请检查项目命令配置和终端状态。");
            return executed;
        }

        public bool IncludesPath(string fullPath)
        {
            return ShowAllFiles || Project.ItemRules?.IsVisible(Project.ProjectDirectory, fullPath) != false;
        }

        public bool IsPathIncludedByProjectRules(string fullPath)
        {
            return Project.ItemRules?.Includes(Project.ProjectDirectory, fullPath) != false;
        }

        internal static ProjectNode? FindOwningProject(SolutionNode node)
        {
            for (SolutionNode? current = node; current != null; current = current.Parent)
            {
                if (current is ProjectNode projectNode)
                    return projectNode;
            }
            return null;
        }

        internal void SetStartupProjectState(bool isStartupProject)
        {
            if (_isStartupProject == isStartupProject)
                return;

            _isStartupProject = isStartupProject;
            NotifyPropertyChanged(nameof(IsStartupProject));
        }

        internal void RefreshConfigurationState()
        {
            Capabilities = ProjectProviderRegistry.GetCapabilities(GetExecutionProject());
            InvalidateMenuItems();
            NotifyPropertyChanged(nameof(Capabilities));
        }

        internal bool CanReuseFor(ProjectDefinition project)
        {
            return string.Equals(Project.ProviderId, project.ProviderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    Project.ProjectFile.FullName,
                    project.ProjectFile.FullName,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    Project.ProjectDirectory.FullName,
                    project.ProjectDirectory.FullName,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal void UpdateProjectDefinition(ProjectDefinition project)
        {
            if (!CanReuseFor(project))
                throw new InvalidOperationException("不能使用不同 Provider、项目文件或根目录的定义更新现有项目节点。");

            bool reloadTree = !HaveEquivalentItemRules(Project.ItemRules, project.ItemRules);
            Project = project;
            string projectName = string.IsNullOrWhiteSpace(project.Name) ? DirectoryInfo.Name : project.Name;
            Name1 = string.IsNullOrWhiteSpace(project.LoadError) ? projectName : $"{projectName} (加载失败)";
            NotifyPropertyChanged(nameof(Name));
            RefreshConfigurationState();
            if (reloadTree)
                ReloadChildren();
        }

        private static bool HaveEquivalentItemRules(ProjectItemRules? left, ProjectItemRules? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            return left.Exclude.SequenceEqual(right.Exclude, StringComparer.OrdinalIgnoreCase)
                && left.Include.SequenceEqual(right.Include, StringComparer.OrdinalIgnoreCase);
        }

        private ProjectDefinition GetExecutionProject()
        {
            return SolutionExplorer?.ApplyActiveConfiguration(Project) ?? Project;
        }

        private void ToggleShowAllFiles()
        {
            ShowAllFiles = !ShowAllFiles;
            NotifyPropertyChanged(nameof(ShowAllFiles));
            ReloadChildren();
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (SolutionExplorer?.IsExplicitProjectMode == true)
            {
                if (showConfirmation
                    && MessageBox.Show(
                        Application.Current?.GetActiveWindow(),
                        $"从解决方案中移除项目“{Name}”吗？项目文件和目录不会被删除。",
                        "ColorVision",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question) != MessageBoxResult.OK)
                {
                    return false;
                }
                return SolutionExplorer.RemoveProject(Project);
            }

            if (!base.TryDelete(showConfirmation))
                return false;

            (SolutionExplorer ?? SolutionManager.GetInstance().CurrentSolutionExplorer)?.UnregisterProject(Project);
            return true;
        }

        internal override bool CompletePhysicalDelete()
        {
            bool completed = base.CompletePhysicalDelete();
            (SolutionExplorer ?? SolutionManager.GetInstance().CurrentSolutionExplorer)?.UnregisterProject(Project);
            return completed;
        }

        private void EditProjectFile()
        {
            EditorManager.Instance.TryOpenFile(Project.ProjectFile.FullName);
        }

        private void InitializeExternalProjectWatcher()
        {
            if (SolutionExplorer?.IsExplicitProjectMode != true)
            {
                return;
            }

            DirectoryInfo projectDirectory = Project.ProjectDirectory;
            if (projectDirectory.Exists
                && !SolutionExplorer.IsPathWithinSolution(projectDirectory.FullName))
            {
                _externalProjectWatcher = CreateExternalWatcher(projectDirectory.FullName);
            }

            if (!SolutionExplorer.IsPathWithinSolution(Project.ProjectFile.FullName)
                && Project.ProjectFile.Directory is { Exists: true } projectFileDirectory
                && !IsPathWithin(projectDirectory.FullName, Project.ProjectFile.FullName))
            {
                _externalProjectFileWatcher = CreateExternalWatcher(
                    projectFileDirectory.FullName,
                    Project.ProjectFile.Name);
            }
        }

        private FileSystemWatcher CreateExternalWatcher(string path, string filter = "*")
        {
            var watcher = new FileSystemWatcher(path, filter)
            {
                IncludeSubdirectories = string.Equals(filter, "*", StringComparison.Ordinal),
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,
            };
            watcher.Created += ExternalProjectWatcher_Created;
            watcher.Changed += ExternalProjectWatcher_Changed;
            watcher.Deleted += ExternalProjectWatcher_Deleted;
            watcher.Renamed += ExternalProjectWatcher_Renamed;
            watcher.Error += ExternalProjectWatcher_Error;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void ExternalProjectWatcher_Created(object sender, FileSystemEventArgs e)
        {
            if (IsProjectDefinitionPath(e.FullPath))
            {
                SolutionExplorer?.RefreshExplicitProjectState();
                return;
            }
            if (e.Name != null && SolutionNodeFactory.IsInternalFile(e.Name))
                return;
            InvokeExternalChange(() => ApplyExternalCreated(e.FullPath));
        }

        private void ExternalProjectWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (IsProjectDefinitionPath(e.FullPath))
                SolutionExplorer?.RefreshExplicitProjectState();
        }

        private void ExternalProjectWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (IsProjectDefinitionPath(e.FullPath))
            {
                SolutionExplorer?.RefreshExplicitProjectState();
                return;
            }
            if (e.Name != null && SolutionNodeFactory.IsInternalFile(e.Name))
                return;
            InvokeExternalChange(() => ApplyExternalDeleted(e.FullPath));
        }

        private void ExternalProjectWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (IsProjectDefinitionPath(e.FullPath) || IsProjectDefinitionPath(e.OldFullPath))
            {
                SolutionExplorer?.RefreshExplicitProjectState();
                return;
            }
            InvokeExternalChange(() => ApplyExternalRenamed(e.OldFullPath, e.FullPath));
        }

        private void ExternalProjectWatcher_Error(object sender, ErrorEventArgs e)
        {
            LogError("外部项目文件监视器发生错误，将重新加载项目树。", e.GetException());
            InvokeExternalChange(() =>
            {
                lock (_externalChangeSync)
                {
                    if (_projectDisposed)
                        return;
                    ReloadChildren();
                    SolutionExplorer?.NotifyVisualTreeChanged();
                }
            });
        }

        internal void ApplyExternalCreated(string fullPath)
        {
            lock (_externalChangeSync)
            {
                if (_projectDisposed || !IncludesPath(fullPath))
                    return;
                string? parentPath = Path.GetDirectoryName(fullPath);
                SolutionNode? parentNode = FindLoadedNode(parentPath);
                if (parentNode == null)
                {
                    SolutionExplorer?.NotifyVisualTreeChanged();
                    return;
                }
                if (parentNode is FolderNode unloadedFolder && !unloadedFolder.AreChildrenLoaded)
                {
                    unloadedFolder.MarkChildrenChanged();
                    SolutionExplorer?.NotifyVisualTreeChanged();
                    return;
                }
                if (!parentNode.VisualChildren.Any(child => PathEquals(child.FullPath, fullPath)))
                {
                    if (File.Exists(fullPath))
                        SolutionNodeFactory.AddFileNode(parentNode, new FileInfo(fullPath));
                    else if (Directory.Exists(fullPath))
                        SolutionNodeFactory.AddFolderNode(parentNode, new DirectoryInfo(fullPath));
                }
                SolutionExplorer?.NotifyVisualTreeChanged();
            }
        }

        internal void ApplyExternalDeleted(string fullPath)
        {
            lock (_externalChangeSync)
            {
                if (_projectDisposed)
                    return;
                SolutionNode? node = FindLoadedNode(fullPath);
                if (node != null && !ReferenceEquals(node, this))
                {
                    node.Parent?.RemoveChild(node);
                    if (node is IDisposable disposable)
                        disposable.Dispose();
                }
                SolutionExplorer?.NotifyVisualTreeChanged();
            }
        }

        internal void ApplyExternalRenamed(string oldFullPath, string newFullPath)
        {
            ApplyExternalDeleted(oldFullPath);
            ApplyExternalCreated(newFullPath);
        }

        private SolutionNode? FindLoadedNode(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;
            if (PathEquals(FullPath, fullPath))
                return this;
            return VisualChildren
                .GetAllVisualChildren()
                .FirstOrDefault(node => PathEquals(node.FullPath, fullPath));
        }

        private bool IsProjectDefinitionPath(string fullPath)
        {
            return PathEquals(Project.ProjectFile.FullName, fullPath);
        }

        private void InvokeExternalChange(Action action)
        {
            if (_projectDisposed)
                return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                _ = dispatcher.BeginInvoke(action);
        }

        private static bool IsPathWithin(string rootPath, string fullPath)
        {
            try
            {
                string relativePath = Path.GetRelativePath(rootPath, fullPath);
                return !Path.IsPathRooted(relativePath)
                    && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                    && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool PathEquals(string? left, string? right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_externalChangeSync)
                    _projectDisposed = true;
                _externalProjectWatcher?.Dispose();
                _externalProjectWatcher = null;
                _externalProjectFileWatcher?.Dispose();
                _externalProjectFileWatcher = null;
            }
            base.Dispose(disposing);
        }

        internal static string GetCapabilityIcon(string capabilityId)
        {
            return capabilityId.ToLowerInvariant() switch
            {
                ProjectCapabilityIds.Build => "DIBuild",
                ProjectCapabilityIds.Run => "DIRun",
                ProjectCapabilityIds.Debug => "DIDebug",
                _ => "DICommand",
            };
        }
    }

    /// <summary>
    /// Keeps an explicit project reference visible when its file is missing or
    /// no installed provider can load it.
    /// </summary>
    public sealed class UnavailableProjectNode : SolutionNode
    {
        private readonly SolutionExplorer _solutionExplorer;

        public string ProjectReference { get; }
        public string ResolvedPath { get; private set; }
        public string LoadError { get; private set; }
        public override bool CanRefresh => true;
        public override string? EditorResourcePath => ResolvedPath;
        public override SolutionDeleteKind DeleteKind => SolutionDeleteKind.RemoveFromSolution;
        internal SolutionExplorer SolutionExplorer => _solutionExplorer;

        public RelayCommand RemoveFromSolutionCommand { get; }
        public RelayCommand ShowLoadErrorCommand { get; }
        public RelayCommand OpenContainingFolderCommand { get; }

        public UnavailableProjectNode(
            SolutionExplorer solutionExplorer,
            string projectReference,
            string resolvedPath,
            string? loadError = null)
        {
            _solutionExplorer = solutionExplorer;
            ProjectReference = projectReference;
            LoadError = string.IsNullOrWhiteSpace(loadError)
                ? "项目文件不存在，或没有已安装的 Provider 能加载该项目。"
                : loadError;
            ResolvedPath = resolvedPath;
            FullPath = GetNodeIdentityPath(resolvedPath);
            Name1 = $"{GetDisplayName(projectReference, resolvedPath)} (不可用)";
            CanCopy = false;
            CanCut = false;
            CanDelete = solutionExplorer.CanModifySolutionStructure;
            CanReName = false;
            Initialize();
            RemoveFromSolutionCommand = new RelayCommand(
                _ => _solutionExplorer.RemoveProjectReference(ProjectReference),
                _ => _solutionExplorer.CanModifySolutionStructure);
            ShowLoadErrorCommand = new RelayCommand(_ => MessageBox.Show(
                Application.Current?.GetActiveWindow(),
                BuildDiagnosticMessage(),
                "项目加载错误",
                MessageBoxButton.OK,
                MessageBoxImage.Warning));
            OpenContainingFolderCommand = new RelayCommand(
                _ => OpenContainingFolder(),
                _ => GetExistingContainerPath() != null);
        }

        internal void UpdateUnavailableState(string resolvedPath, string? loadError)
        {
            LoadError = string.IsNullOrWhiteSpace(loadError)
                ? "项目文件不存在，或没有已安装的 Provider 能加载该项目。"
                : loadError;
            ResolvedPath = resolvedPath;
            FullPath = GetNodeIdentityPath(resolvedPath);
            Name1 = $"{GetDisplayName(ProjectReference, resolvedPath)} (不可用)";
            NotifyPropertyChanged(nameof(Name));
            NotifyPropertyChanged(nameof(LoadError));
            InvalidateMenuItems();
        }

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "ShowUnavailableProjectError",
                Order = 2,
                Header = "查看加载错误(_E)...",
                Command = ShowLoadErrorCommand,
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "OpenUnavailableProjectContainer",
                Order = 3,
                Header = "在文件资源管理器中打开(_X)",
                Command = OpenContainingFolderCommand,
            });
        }

        public override void Refresh()
        {
            _solutionExplorer.ReloadSolutionState();
        }

        internal override bool TryDelete(bool showConfirmation)
        {
            if (showConfirmation
                && MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"从解决方案中移除项目引用“{Name}”吗？",
                    "ColorVision",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question) != MessageBoxResult.OK)
            {
                return false;
            }

            return _solutionExplorer.RemoveProjectReference(ProjectReference);
        }

        internal string BuildDiagnosticMessage()
        {
            return string.Join(Environment.NewLine, new[]
            {
                $"项目引用：{ProjectReference}",
                $"解析路径：{ResolvedPath}",
                string.Empty,
                LoadError,
                string.Empty,
                "请恢复项目文件或安装对应的项目 Provider，然后选择“重新加载项目”。",
            });
        }

        private void OpenContainingFolder()
        {
            string? containerPath = GetExistingContainerPath();
            if (containerPath == null)
                return;
            if (File.Exists(ResolvedPath))
                ColorVision.Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(ResolvedPath);
            else
                ColorVision.Common.Utilities.PlatformHelper.OpenFolder(containerPath);
        }

        private string? GetExistingContainerPath()
        {
            if (Directory.Exists(ResolvedPath))
                return ResolvedPath;

            try
            {
                string? directoryPath = Path.GetDirectoryName(ResolvedPath);
                return directoryPath != null && Directory.Exists(directoryPath)
                    ? directoryPath
                    : null;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return null;
            }
        }

        public override void CopyFullPath()
        {
            if (!string.IsNullOrWhiteSpace(ResolvedPath))
                Common.Clipboard.SetText(ResolvedPath);
        }

        private static string GetNodeIdentityPath(string resolvedPath)
        {
            return Directory.Exists(resolvedPath)
                ? Path.Combine(resolvedPath, ".missing.cvproj")
                : resolvedPath;
        }

        private static string GetDisplayName(string projectReference, string resolvedPath)
        {
            try
            {
                string path = string.IsNullOrWhiteSpace(resolvedPath) ? projectReference : resolvedPath;
                string name = Path.GetFileNameWithoutExtension(Path.TrimEndingDirectorySeparator(path));
                return string.IsNullOrWhiteSpace(name) ? projectReference : name;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                return projectReference;
            }
        }
    }
}
