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
    /// <summary>
    /// 解决方案资源管理器，管理目录、配置、命令及事件
    /// </summary>
    public partial class SolutionExplorer : SolutionNode, ISolutionContainerNode, ISolutionPhysicalContainer, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SolutionExplorer));
        public DirectoryInfo DirectoryInfo { get; private set; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand EditCommand { get; }

        public static SolutionSetting Setting => SolutionSetting.Instance;
        private FileSystemWatcher _fileSystemWatcher;
        private DispatcherTimer _changedDebounceTimer;
        private DispatcherTimer _projectChangedDebounceTimer;
        private readonly SemaphoreSlim _solutionRefreshGate = new(1, 1);
        private CancellationTokenSource? _projectRefreshCancellation;
        private readonly EventHandler _processExitHandler;
        private bool _disposed;
        private int _cacheRebuildPending;
        private readonly object _directoryIndexSync = new();
        private readonly Dictionary<string, string> _pendingDirectoryIndexes = new(StringComparer.OrdinalIgnoreCase);
        private bool _directoryIndexWorkerRunning;
        private int _trackedMutationDepth;
        private bool _operationHistoryEnabled;
        private bool _isApplyingOperationSnapshot;
        private Dictionary<string, ProjectReferenceLoadResult>? _preparedProjectReferences;
        public SolutionConfig Config { get; private set; }
        public FileInfo ConfigFileInfo { get; private set; }
        public SolutionEnvironments SolutionEnvironments { get; }
        public DriveInfo DriveInfo { get; private set; }
        public event EventHandler? VisualChildrenEventHandler;
        internal event EventHandler? SolutionStateReloading;
        internal event EventHandler? SolutionStateReloaded;
        internal event EventHandler? Disposing;

        public SolutionCache? Cache { get; private set; }
        public override bool CanOpen => DirectoryInfo.Exists;
        public override bool CanRefresh => true;
        public override bool CanShowProperties => DirectoryInfo.Exists;
        public override string? ExplorerResourcePath => DirectoryInfo.Exists
            ? DirectoryInfo.FullName
            : null;
        public override string? EditorResourcePath
        {
            get
            {
                if (PrivateWorkspaceService.TryResolveSourcePath(
                    ConfigFileInfo.FullName,
                    out string sourcePath))
                {
                    return sourcePath;
                }

                return ConfigFileInfo.FullName;
            }
        }
        public string PhysicalContainerPath => DirectoryInfo.FullName;
        public SolutionContainerAction SupportedContainerActions => DirectoryInfo.Exists
            ? SolutionContainerAction.AddNewItem
                | SolutionContainerAction.AddExistingItem
                | SolutionContainerAction.CreateFolder
                | SolutionContainerAction.AddNewProject
                | SolutionContainerAction.AddExistingProject
                | SolutionContainerAction.CreateSolutionFolder
            : SolutionContainerAction.None;
        internal SolutionOperationHistory OperationHistory { get; } = new();
        internal bool IsExplicitProjectMode => Config.ProjectMode == SolutionProjectMode.Explicit;
        public string ActiveConfiguration => Config.ActiveConfiguration;
        public string ActivePlatform => Config.ActivePlatform;
        public string ActiveConfigurationDisplay =>
            $"{Config.ActiveConfiguration} | {Config.ActivePlatform}";

        internal IReadOnlyList<string> GetDocumentResourceRoots()
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                DirectoryInfo.FullName,
                ConfigFileInfo.FullName,
            };
            foreach (SolutionNode node in VisualChildren.GetAllVisualChildren())
            {
                switch (node)
                {
                    case ProjectNode projectNode:
                        roots.Add(projectNode.Project.ProjectDirectory.FullName);
                        roots.Add(projectNode.Project.ProjectFile.FullName);
                        break;
                    case SolutionItemNode itemNode:
                        roots.Add(itemNode.FullPath);
                        break;
                    case UnavailableProjectNode unavailableNode when !string.IsNullOrWhiteSpace(unavailableNode.ResolvedPath):
                        roots.Add(unavailableNode.ResolvedPath);
                        break;
                }
            }
            return roots.ToList();
        }

        public SolutionExplorer(SolutionEnvironments solutionEnvironments)
            : this(solutionEnvironments, preparation: null)
        {
        }

        internal SolutionExplorer(
            SolutionEnvironments solutionEnvironments,
            SolutionExplorerPreparation? preparation)
        {
            SolutionEnvironments = solutionEnvironments ?? throw new ArgumentNullException(nameof(solutionEnvironments));
            OperationHistory.Changed += (_, _) => CommandManager.InvalidateRequerySuggested();

            InitializeSolution(preparation);
            FullPath = DirectoryInfo.FullName;
            CanDelete = false;
            CanReName = false;
            CanCopy = false;
            CanCut = false;

            SaveCommand = new RelayCommand(_ => SaveConfigWithUserFeedback());
            EditCommand = new RelayCommand(_ =>
            {
                ExecuteTrackedMutation("编辑解决方案属性", () =>
                {
                    new PropertyEditorWindow(Config)
                    {
                        Owner = Application.Current.GetActiveWindow(),
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    }.ShowDialog();
                    return SaveConfigWithUserFeedback();
                }, result => result);
            });
            // Initialize SQLite cache BEFORE file watcher to avoid watcher picking up cache.db creation
            try
            {
                Cache = new SolutionCache(SolutionEnvironments.SolutionPath);
            }
            catch (Exception ex)
            {
                Logger.Warn($"缓存初始化失败，使用文件系统加载: {ex.Message}");
                Cache = null;
            }

            InitializeFileSystemWatcher();

            var stopwatch = Stopwatch.StartNew();
            if (!HasRootProjectReference())
                SolutionNodeFactory.PopulateChildren(this, DirectoryInfo, Cache);
            ReconcileExplicitProjects();
            EnsureStartupProject();
            stopwatch.Stop();
            Logger.Info($"工程初始化时间: {stopwatch.Elapsed.TotalSeconds} 秒");

            // Rebuild cache in background after loading if it was empty
            if (Cache != null && !Cache.HasCache())
            {
                Task.Run(() =>
                {
                    try
                    {
                        Cache.RebuildCache(DirectoryInfo.FullName);
                        Logger.Info("后台缓存构建完成");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"后台缓存构建失败: {ex.Message}");
                    }
                });
            }

            _operationHistoryEnabled = true;
            ProjectProviderRegistry.ProvidersChanged += ProjectProviderRegistry_ProvidersChanged;
            _processExitHandler = (_, __) =>
            {
                try
                {
                    SaveConfig();
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidDataException
                    or ArgumentException
                    or NotSupportedException)
                {
                    Logger.Error($"退出时保存解决方案失败: {ConfigFileInfo.FullName}", ex);
                }
            };
            AppDomain.CurrentDomain.ProcessExit += _processExitHandler;
        }

        private void ProjectProviderRegistry_ProvidersChanged(object? sender, EventArgs e)
        {
            if (_disposed)
                return;

            void RefreshProviders()
            {
                if (_disposed)
                    return;
                if (IsExplicitProjectMode)
                    RefreshExplicitProjectState();
                else
                    ReloadSolutionState();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(RefreshProviders);
            else
                RefreshProviders();
        }


        public override bool IsExpanded { get => true; set {} }

        /// <summary>
        /// 初始化解决方案配置及目录
        /// </summary>
        private void InitializeSolution(SolutionExplorerPreparation? preparation)
        {
            string fullPath = SolutionEnvironments.SolutionPath;
            if (File.Exists(fullPath) && fullPath.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFileInfo = new FileInfo(fullPath);
                Name1 = string.IsNullOrWhiteSpace(SolutionEnvironments.SolutionFileName)
                    ? Path.GetFileNameWithoutExtension(fullPath)
                    : SolutionEnvironments.SolutionFileName;
                SolutionConfigLoadResult loadResult = preparation?.LoadResult
                    ?? SolutionConfigStore.Load(fullPath);
                Config = loadResult.Config;
                Config.ActiveConfiguration = NormalizeConfigurationName(Config.ActiveConfiguration);
                Config.ActivePlatform = NormalizePlatformName(Config.ActivePlatform);
                if (loadResult.RecoveredFromBackup)
                {
                    string archiveMessage = string.IsNullOrWhiteSpace(loadResult.CorruptCopyPath)
                        ? string.Empty
                        : $"，损坏副本保存在 {loadResult.CorruptCopyPath}";
                    Logger.Warn($"解决方案配置已从备份恢复: {fullPath}{archiveMessage}");
                }
                else if (loadResult.SourceSchemaVersion < SolutionConfigStore.CurrentSchemaVersion)
                {
                    Logger.Info($"解决方案配置已从 SchemaVersion {loadResult.SourceSchemaVersion} 迁移到 {SolutionConfigStore.CurrentSchemaVersion}: {fullPath}");
                }
                DirectoryInfo = preparation?.RootDirectory
                    ?? ResolveRootDirectory(ConfigFileInfo, Config.RootPath);
                _preparedProjectReferences = preparation?.ProjectReferences;
                if (DirectoryInfo.Root != null)
                    DriveInfo = new DriveInfo(DirectoryInfo.Root.FullName);
            }
            else
            {
                throw new FileNotFoundException("Solution file not found or invalid extension.", fullPath);
            }
        }

        internal static async Task<SolutionExplorerPreparation> PrepareAsync(
            SolutionEnvironments solutionEnvironments,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(solutionEnvironments);
            Task<(SolutionConfigLoadResult LoadResult, DirectoryInfo RootDirectory)> preparationTask = Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fullPath = solutionEnvironments.SolutionPath;
                if (!File.Exists(fullPath)
                    || !fullPath.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException(
                        "Solution file not found or invalid extension.",
                        fullPath);
                }

                var configFile = new FileInfo(fullPath);
                SolutionConfigLoadResult loadResult = SolutionConfigStore.Load(fullPath);
                loadResult.Config.ActiveConfiguration = NormalizeConfigurationName(
                    loadResult.Config.ActiveConfiguration);
                loadResult.Config.ActivePlatform = NormalizePlatformName(
                    loadResult.Config.ActivePlatform);
                DirectoryInfo rootDirectory = ResolveRootDirectory(
                    configFile,
                    loadResult.Config.RootPath);
                return (loadResult, rootDirectory);
            }, CancellationToken.None);
            var preparation = await preparationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            Dictionary<string, ProjectReferenceLoadResult> projectReferences =
                await LoadProjectReferencesAsync(
                    preparation.RootDirectory.FullName,
                    preparation.LoadResult.Config,
                    cancellationToken)
                .ConfigureAwait(false);
            return new SolutionExplorerPreparation(
                preparation.LoadResult,
                preparation.RootDirectory,
                projectReferences);
        }

        private static async Task<Dictionary<string, ProjectReferenceLoadResult>> LoadProjectReferencesAsync(
            string rootDirectory,
            SolutionConfig config,
            CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, ProjectReferenceLoadResult>(StringComparer.OrdinalIgnoreCase);
            if (config.ProjectMode != SolutionProjectMode.Explicit)
                return results;

            List<string> references = config.Projects
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (string reference in references)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[reference] = await LoadProjectReferenceAsync(
                    rootDirectory,
                    reference,
                    cancellationToken)
                    .ConfigureAwait(false);
            }
            return results;
        }

        private static async Task<ProjectReferenceLoadResult> LoadProjectReferenceAsync(
            string rootDirectory,
            string reference,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return new ProjectReferenceLoadResult(reference, null, string.Empty, "项目引用为空。");

            try
            {
                string resolvedPath = Path.GetFullPath(Path.IsPathRooted(reference)
                    ? reference
                    : Path.Combine(rootDirectory, reference));
                ProjectLoadResult loadResult;
                if (File.Exists(resolvedPath))
                {
                    loadResult = await ProjectProviderRegistry.LoadProjectAsync(
                        new FileInfo(resolvedPath),
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (Directory.Exists(resolvedPath))
                {
                    loadResult = await ProjectProviderRegistry.LoadProjectAsync(
                        new DirectoryInfo(resolvedPath),
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    return new ProjectReferenceLoadResult(
                        reference,
                        null,
                        resolvedPath,
                        $"项目路径不存在：{resolvedPath}");
                }

                return new ProjectReferenceLoadResult(
                    reference,
                    loadResult.Succeeded ? loadResult.Project : null,
                    resolvedPath,
                    loadResult.ErrorMessage);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return new ProjectReferenceLoadResult(
                    reference,
                    null,
                    string.Empty,
                    $"项目引用无效：{ex.Message}");
            }
        }

        private void NormalizeSolutionOrganization()
        {
            SolutionConfigStore.Normalize(Config);
        }

        /// <summary>
        /// 初始化文件系统监控
        /// </summary>
        private void InitializeFileSystemWatcher()
        {
            if (DirectoryInfo?.Exists == true)
            {
                _changedDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _changedDebounceTimer.Tick += (s, e) =>
                {
                    _changedDebounceTimer.Stop();
                    VisualChildrenEventHandler?.Invoke(this, EventArgs.Empty);
                };
                _projectChangedDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _projectChangedDebounceTimer.Tick += async (_, _) =>
                {
                    _projectChangedDebounceTimer.Stop();
                    ProjectRefreshResult result = await RefreshExplicitProjectStateAsync();
                    if (!result.Succeeded && !result.Canceled)
                        Logger.Warn($"自动刷新项目失败: {result.ErrorMessage}");
                };
                _fileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    InternalBufferSize = 64 * 1024,
                    NotifyFilter = NotifyFilters.FileName |
                                   NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite |
                                   NotifyFilters.Size
                };
                _fileSystemWatcher.Created += FileSystemWatcher_Created;
                _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
                _fileSystemWatcher.Renamed += FileSystemWatcher_Renamed;
                _fileSystemWatcher.Changed += FileSystemWatcher_Changed;
                _fileSystemWatcher.Error += FileSystemWatcher_Error;
            }
        }

        private void FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            if (SolutionManager.IsProjectFilePath(e.FullPath))
                RefreshProjectFileState(e.FullPath);
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            // Skip internal solution files (e.g. .cvsln, .cache.db)
            if (e.Name != null && SolutionNodeFactory.IsInternalFile(e.Name))
            {
                if (SolutionManager.IsProjectFilePath(e.FullPath))
                    RefreshProjectFileState(e.FullPath);
                return;
            }

            if (IsRegisteredSolutionItemPath(e.FullPath))
            {
                string? registeredItemParent = Path.GetDirectoryName(e.FullPath);
                if (!string.IsNullOrWhiteSpace(registeredItemParent))
                    Cache?.AddFile(e.FullPath, registeredItemParent);
                Application.Current?.Dispatcher.BeginInvoke(() => ReconcileExplicitProjects(reloadLoadedProjects: true));
                NotifyVisualTreeChanged();
                return;
            }

            string? parentPath = Path.GetDirectoryName(e.FullPath);
            if (string.IsNullOrWhiteSpace(parentPath))
                return;

            // Update cache
            if (Cache != null)
            {
                if (File.Exists(e.FullPath))
                    Cache.AddFile(e.FullPath, parentPath);
                else if (Directory.Exists(e.FullPath))
                    QueueDirectoryIndex(e.FullPath, parentPath);
            }

            NotifyVisualTreeChanged();

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var parentNode = FindNodeByFullPath(parentPath) ?? this;
                if (parentNode is FolderNode unloadedFolder && !unloadedFolder.AreChildrenLoaded)
                {
                    unloadedFolder.MarkChildrenChanged();
                    return;
                }

                // Duplicate protection
                if (parentNode.VisualChildren.Any(c => PathEquals(c.FullPath, e.FullPath)))
                    return;

                if (File.Exists(e.FullPath))
                {
                    SolutionNodeFactory.AddFileNode(parentNode, new FileInfo(e.FullPath));
                }
                else if (Directory.Exists(e.FullPath))
                {
                    SolutionNodeFactory.AddFolderNode(parentNode, new DirectoryInfo(e.FullPath));
                }
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (IsRegisteredSolutionItemPath(e.FullPath))
            {
                Cache?.Remove(e.FullPath);
                Application.Current?.Dispatcher.BeginInvoke(() => ReconcileExplicitProjects(reloadLoadedProjects: true));
                NotifyVisualTreeChanged();
                return;
            }
            if (SolutionManager.IsProjectFilePath(e.FullPath))
            {
                RefreshProjectFileState(e.FullPath);
                return;
            }

            // Update cache
            Cache?.Remove(e.FullPath);
            NotifyVisualTreeChanged();

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var child = FindNodeByFullPath(e.FullPath);
                if (child != null)
                {
                    child.Parent?.RemoveChild(child);
                    if (child is IDisposable disposable)
                        disposable.Dispose();
                }
                else
                {
                    string? parentPath = Path.GetDirectoryName(e.FullPath);
                    if (!string.IsNullOrWhiteSpace(parentPath) && FindNodeByFullPath(parentPath) is FolderNode unloadedFolder)
                        unloadedFolder.MarkChildrenChanged();
                }
            });
        }

        private void FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            bool oldPathIsSolutionItem = IsRegisteredSolutionItemPath(e.OldFullPath);
            if (IsRegisteredSolutionItemPath(e.FullPath))
            {
                Cache?.Remove(e.OldFullPath);
                string? registeredItemParent = Path.GetDirectoryName(e.FullPath);
                if (!string.IsNullOrWhiteSpace(registeredItemParent))
                    Cache?.AddFile(e.FullPath, registeredItemParent);
                Application.Current?.Dispatcher.BeginInvoke(() => ReconcileExplicitProjects(reloadLoadedProjects: true));
                NotifyVisualTreeChanged();
                return;
            }
            if (SolutionManager.IsProjectFilePath(e.FullPath) || SolutionManager.IsProjectFilePath(e.OldFullPath))
            {
                RefreshProjectFileState(e.FullPath);
                return;
            }

            Cache?.Remove(e.OldFullPath);

            string? parentPath = Path.GetDirectoryName(e.FullPath);
            if (string.IsNullOrWhiteSpace(parentPath))
                return;

            if (File.Exists(e.FullPath))
                Cache?.AddFile(e.FullPath, parentPath);
            else if (Directory.Exists(e.FullPath))
                QueueDirectoryIndex(e.FullPath, parentPath);

            NotifyVisualTreeChanged();

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var oldNode = FindNodeByFullPath(e.OldFullPath);
                if (oldNode != null && oldNode is not SolutionItemNode)
                {
                    oldNode.Parent?.RemoveChild(oldNode);
                    if (oldNode is IDisposable disposable)
                        disposable.Dispose();
                }

                var parentNode = FindNodeByFullPath(parentPath) ?? this;
                if (parentNode is FolderNode unloadedFolder && !unloadedFolder.AreChildrenLoaded)
                {
                    unloadedFolder.MarkChildrenChanged();
                    return;
                }

                if (parentNode.VisualChildren.Any(c => PathEquals(c.FullPath, e.FullPath)))
                    return;

                if (File.Exists(e.FullPath))
                    SolutionNodeFactory.AddFileNode(parentNode, new FileInfo(e.FullPath));
                else if (Directory.Exists(e.FullPath))
                    SolutionNodeFactory.AddFolderNode(parentNode, new DirectoryInfo(e.FullPath));

                if (oldPathIsSolutionItem)
                    ReconcileExplicitProjects(reloadLoadedProjects: true);
            });
        }

        private void FileSystemWatcher_Error(object sender, ErrorEventArgs e)
        {
            Logger.Warn("解决方案文件监视器发生错误，将重建搜索缓存。", e.GetException());
            QueueCacheRebuild();
        }

        private void QueueDirectoryIndex(string directoryPath, string parentPath)
        {
            SolutionCache? cache = Cache;
            if (_disposed || cache == null)
                return;

            string normalizedDirectoryPath;
            try
            {
                normalizedDirectoryPath = Path.GetFullPath(directoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                Logger.Warn($"无法索引目录: {directoryPath}, {ex.Message}");
                return;
            }

            lock (_directoryIndexSync)
            {
                _pendingDirectoryIndexes[normalizedDirectoryPath] = parentPath;
                if (_directoryIndexWorkerRunning)
                    return;
                _directoryIndexWorkerRunning = true;
            }

            _ = Task.Run(ProcessPendingDirectoryIndexesAsync);
        }

        private async Task ProcessPendingDirectoryIndexesAsync()
        {
            while (!_disposed)
            {
                await Task.Delay(150).ConfigureAwait(false);
                List<KeyValuePair<string, string>> pendingIndexes;
                lock (_directoryIndexSync)
                {
                    pendingIndexes = ReduceDirectoryIndexRoots(_pendingDirectoryIndexes);
                    _pendingDirectoryIndexes.Clear();
                }

                SolutionCache? cache = Cache;
                if (cache != null)
                {
                    foreach ((string directoryPath, string parentPath) in pendingIndexes)
                        cache.AddDirectoryTree(directoryPath, parentPath);
                    NotifyVisualTreeChanged();
                }

                lock (_directoryIndexSync)
                {
                    if (_pendingDirectoryIndexes.Count > 0)
                        continue;
                    _directoryIndexWorkerRunning = false;
                    return;
                }
            }

            lock (_directoryIndexSync)
                _directoryIndexWorkerRunning = false;
        }

        internal static List<KeyValuePair<string, string>> ReduceDirectoryIndexRoots(
            IReadOnlyDictionary<string, string> pendingIndexes)
        {
            var roots = new List<KeyValuePair<string, string>>();
            foreach (KeyValuePair<string, string> candidate in pendingIndexes.OrderBy(entry => entry.Key.Length))
            {
                if (!roots.Any(root => IsSameOrDescendantPath(root.Key, candidate.Key)))
                    roots.Add(candidate);
            }
            return roots;
        }

        private static bool IsSameOrDescendantPath(string rootPath, string candidatePath)
        {
            if (string.Equals(rootPath, candidatePath, StringComparison.OrdinalIgnoreCase))
                return true;
            try
            {
                string relativePath = Path.GetRelativePath(rootPath, candidatePath);
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

        private void QueueCacheRebuild()
        {
            SolutionCache? cache = Cache;
            if (_disposed || cache == null || Interlocked.Exchange(ref _cacheRebuildPending, 1) != 0)
                return;

            _ = Task.Run(() =>
            {
                try
                {
                    cache.RebuildCache(DirectoryInfo.FullName);
                }
                catch (Exception ex)
                {
                    Logger.Warn($"文件监视器恢复时重建缓存失败: {ex.Message}", ex);
                }
                finally
                {
                    Interlocked.Exchange(ref _cacheRebuildPending, 0);
                    NotifyVisualTreeChanged();
                }
            });
        }

        internal void NotifyVisualTreeChanged()
        {
            if (_disposed)
                return;

            var dispatcher = _changedDebounceTimer.Dispatcher;
            void QueueNotification()
            {
                if (_disposed)
                    return;
                _changedDebounceTimer.Stop();
                _changedDebounceTimer.Start();
            }

            if (dispatcher.CheckAccess())
                QueueNotification();
            else
                _ = dispatcher.BeginInvoke(QueueNotification);
        }
    }
}
