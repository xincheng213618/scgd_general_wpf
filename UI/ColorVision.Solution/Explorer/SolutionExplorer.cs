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
    public enum SolutionProjectMode
    {
        AutoDiscover,
        Explicit,
    }

    public sealed class SolutionFolderDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "解决方案文件夹";
        public string? ParentId { get; set; }
    }

    public sealed class SolutionItemDefinition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Path { get; set; } = string.Empty;
        public string? SolutionFolderId { get; set; }
    }

    /// <summary>
    /// 配置解决方案的模型类，支持MVVM绑定
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {
        public int SchemaVersion { get; set; } = SolutionConfigStore.CurrentSchemaVersion;
        public string FilePath { get; set; }
        public string VirtualPath { get; set; }
        public string RootPath { get; set; } = string.Empty;
        public bool IsSetting { get; set; }
        public bool IsSetting1 { get; set; }
        public ObservableCollection<string> Paths { get; set; }

        /// <summary>
        /// 项目引用列表 - 存储相对于解决方案目录的项目路径
        /// 类似VS的.sln，项目路径信息保存在解决方案配置中
        /// </summary>
        public ObservableCollection<string> Projects { get; set; } = new ObservableCollection<string>();

        /// <summary>
        /// The project used by solution-level Run and Debug commands. The path
        /// is stored relative to the solution root whenever possible.
        /// </summary>
        public string StartupProject { get; set; } = string.Empty;

        /// <summary>
        /// The solution-wide configuration selected by build/run/debug. Project
        /// mappings may translate it to a differently named project profile.
        /// </summary>
        public string ActiveConfiguration { get; set; } = "Debug";

        /// <summary>
        /// Project reference -> solution configuration -> project configuration.
        /// This mirrors the configuration mapping role of a Visual Studio solution.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> ProjectConfigurations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Virtual solution folders. These organize projects without changing
        /// their physical paths, matching the role of Visual Studio solution
        /// folders rather than ordinary file-system directories.
        /// </summary>
        public ObservableCollection<SolutionFolderDefinition> SolutionFolders { get; set; } = new();

        /// <summary>
        /// Project reference -> virtual solution-folder id.
        /// Projects not present in this map remain at the solution root.
        /// </summary>
        public Dictionary<string, string> ProjectSolutionFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Files referenced by the solution independently of any project.
        /// Removing one of these entries never deletes the physical file.
        /// </summary>
        public ObservableCollection<SolutionItemDefinition> SolutionItems { get; set; } = new();

        [JsonConverter(typeof(StringEnumConverter))]
        public SolutionProjectMode ProjectMode { get; set; } = SolutionProjectMode.AutoDiscover;

        [JsonExtensionData]
        public IDictionary<string, JToken>? ExtensionData { get; set; }
    }

    /// <summary>
    /// 解决方案资源管理器，管理目录、配置、命令及事件
    /// </summary>
    public class SolutionExplorer : SolutionNode, IDisposable
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SolutionExplorer));
        public DirectoryInfo DirectoryInfo { get; private set; }
        public RelayCommand OpenFileInExplorerCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand AddDirCommand { get; }

        public static SolutionSetting Setting => SolutionSetting.Instance;
        private FileSystemWatcher _fileSystemWatcher;
        private DispatcherTimer _changedDebounceTimer;
        private DispatcherTimer _projectChangedDebounceTimer;
        private readonly EventHandler _processExitHandler;
        private bool _disposed;
        private int _trackedMutationDepth;
        private bool _operationHistoryEnabled;
        private bool _isApplyingOperationSnapshot;
        public SolutionConfig Config { get; private set; }
        public FileInfo ConfigFileInfo { get; private set; }
        public SolutionEnvironments SolutionEnvironments { get; }
        public DriveInfo DriveInfo { get; private set; }
        public event EventHandler? VisualChildrenEventHandler;
        internal event EventHandler? SolutionStateReloading;
        internal event EventHandler? SolutionStateReloaded;
        internal event EventHandler? Disposing;

        public SolutionCache? Cache { get; private set; }
        internal SolutionOperationHistory OperationHistory { get; } = new();
        internal bool IsExplicitProjectMode => Config.ProjectMode == SolutionProjectMode.Explicit;
        public string ActiveConfiguration => Config.ActiveConfiguration;

        public SolutionExplorer(SolutionEnvironments solutionEnvironments)
        {
            SolutionEnvironments = solutionEnvironments ?? throw new ArgumentNullException(nameof(solutionEnvironments));
            OperationHistory.Changed += (_, _) => CommandManager.InvalidateRequerySuggested();

            InitializeSolution();
            FullPath = DirectoryInfo.FullName;
            CanDelete = false;
            CanReName = false;
            CanCopy = false;
            CanCut = false;
            Initialize();

            CopyFullPathCommand = new RelayCommand(_ => Common.Clipboard.SetText(FullPath), _ => DirectoryInfo.Exists);
            OpenFileInExplorerCommand = new RelayCommand(_ => Process.Start("explorer.exe", DirectoryInfo.FullName), _ => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(_ => SolutionNodeFactory.CreateNewFolder(this, DirectoryInfo.FullName));
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


        public override bool IsExpanded { get => true; set {} }

        /// <summary>
        /// 初始化解决方案配置及目录
        /// </summary>
        private void InitializeSolution()
        {
            string fullPath = SolutionEnvironments.SolutionPath;
            if (File.Exists(fullPath) && fullPath.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFileInfo = new FileInfo(fullPath);
                Name1 = string.IsNullOrWhiteSpace(SolutionEnvironments.SolutionFileName)
                    ? Path.GetFileNameWithoutExtension(fullPath)
                    : SolutionEnvironments.SolutionFileName;
                SolutionConfigLoadResult loadResult = SolutionConfigStore.Load(fullPath);
                Config = loadResult.Config;
                Config.ActiveConfiguration = NormalizeConfigurationName(Config.ActiveConfiguration);
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
                DirectoryInfo = ResolveRootDirectory(ConfigFileInfo, Config.RootPath);
                if (DirectoryInfo.Root != null)
                    DriveInfo = new DriveInfo(DirectoryInfo.Root.FullName);
            }
            else
            {
                throw new FileNotFoundException("Solution file not found or invalid extension.", fullPath);
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
                _projectChangedDebounceTimer.Tick += (_, _) =>
                {
                    _projectChangedDebounceTimer.Stop();
                    ReconcileExplicitProjects(reloadLoadedProjects: true);
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
                _fileSystemWatcher.Changed += (s, e) => Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (SolutionManager.IsProjectFilePath(e.FullPath))
                        RefreshProjectFileState(e.FullPath);
                    _changedDebounceTimer.Stop();
                    _changedDebounceTimer.Start();
                });
            }
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
                    Cache.AddDirectory(e.FullPath, parentPath);
            }

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
                return;
            }
            if (SolutionManager.IsProjectFilePath(e.FullPath))
            {
                RefreshProjectFileState(e.FullPath);
                return;
            }

            // Update cache
            Cache?.Remove(e.FullPath);

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
                VisualChildrenEventHandler?.Invoke(this, EventArgs.Empty);
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
                Cache?.AddDirectory(e.FullPath, parentPath);

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

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Clear();
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "Open",
                Order = 1,
                Header = Resources.MenuOpen,
                Command = OpenCommand
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = SolutionCommandIds.Refresh,
                Order = 2,
                Header = Resources.Refresh,
                Command = System.Windows.Input.NavigationCommands.Refresh
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "BuildSolution",
                Order = 5,
                Header = "生成解决方案(_B)",
                Command = SolutionProjectCommands.BuildSolution,
                Icon = MenuItemIcon.TryFindResource("DIBuild")
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "RunStartupProject",
                Order = 6,
                Header = "运行启动项目(_R)",
                Command = SolutionProjectCommands.Run,
                Icon = MenuItemIcon.TryFindResource("DIRun"),
                InputGestureText = "Ctrl+F5",
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "DebugStartupProject",
                Order = 7,
                Header = "调试启动项目(_D)",
                Command = SolutionProjectCommands.Debug,
                Icon = MenuItemIcon.TryFindResource("DIDebug"),
                InputGestureText = "F5",
            });

            const string configurationMenuId = "ActiveSolutionConfiguration";
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = configurationMenuId,
                Order = 8,
                Header = $"活动配置: {Config.ActiveConfiguration}",
            });
            int configurationOrder = 0;
            foreach (string configuration in GetAvailableSolutionConfigurations())
            {
                string selectedConfiguration = configuration;
                MenuItemMetadatas.Add(new MenuItemMetadata
                {
                    OwnerGuid = configurationMenuId,
                    GuidId = $"SolutionConfiguration.{configuration}",
                    Order = configurationOrder++,
                    Header = configuration,
                    Command = new RelayCommand(_ => SetActiveConfiguration(selectedConfiguration)),
                    IsChecked = string.Equals(
                        Config.ActiveConfiguration,
                        configuration,
                        StringComparison.OrdinalIgnoreCase),
                });
            }

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "SolutionConfigurationManager",
                Order = 9,
                Header = "配置管理器(_C)...",
                Command = SolutionProjectCommands.ConfigurationManager,
                Icon = MenuItemIcon.TryFindResource("DISetting"),
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "Edit",
                Order = 50,
                Header = ColorVision.Solution.Properties.Resources.EditSolution,
                Command = EditCommand
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "Add",
                Order = 10,
                Header = Resources.MenuAdd
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddNewItem",
                Order = 1,
                Header = "新建项(_N)...",
                Command = new RelayCommand(_ => ShowAddNewItemDialog())
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddExistingItem",
                Order = 2,
                Header = "现有项(_E)...",
                Command = new RelayCommand(_ => AddExistingItem())
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddFolder",
                Order = 10,
                Header = ColorVision.Solution.Properties.Resources.AddFolder,
                Command = AddDirCommand
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddNewProject",
                Order = 15,
                Header = "新建项目(_P)...",
                Command = new RelayCommand(_ => ShowAddNewProjectDialog())
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddExistingProject",
                Order = 20,
                Header = "现有项目(_E)...",
                Command = new RelayCommand(_ => AddExistingProject())
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                OwnerGuid = "Add",
                GuidId = "AddSolutionFolder",
                Order = 25,
                Header = "新建解决方案文件夹(_F)",
                Command = new RelayCommand(_ => CreateSolutionFolder())
            });

            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "CopyFullPath",
                Order = 200,
                Command = CopyFullPathCommand,
                Header = ColorVision.Solution.Properties.Resources.CopyFullPath,
                Icon = MenuItemIcon.TryFindResource("DICopyFullPath")
            });
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "MenuOpenFileInExplorer",
                Order = 200,
                Command = OpenFileInExplorerCommand,
                Header = Resources.MenuOpenFileInExplorer
            });
        }

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
                string? content = window.SelectedTemplate.GetDefaultContent(window.NewFileName);
                if (content != null)
                    File.WriteAllText(fullPath, content);
                else
                    File.Create(fullPath).Dispose();

                if (!RegisterSolutionItems([fullPath], solutionFolderId, out string errorMessage))
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
                var dirInfo = ProjectTemplateRegistry.CreateFromTemplate(
                    window.SelectedTemplate, DirectoryInfo.FullName, window.ProjectName);

                // Register project path in solution config (like VS .sln)
                if (dirInfo != null)
                {
                    RegisterProject(dirInfo, solutionFolderId);
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
                foreach (string reference in Config.Projects.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (TryResolveProjectReference(
                        DirectoryInfo.FullName,
                        reference,
                        out ProjectDefinition? project,
                        out _)
                        && project != null)
                    {
                        projects.Add(applyActiveConfiguration ? ApplyActiveConfiguration(project) : project);
                    }
                }
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
                Config.ProjectConfigurations,
                project);
        }

        internal static string ResolveProjectConfigurationName(
            string solutionDirectory,
            string? activeConfiguration,
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

                string? mappedConfiguration = projectMapping.Value
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
                    result.Add(configuration.Trim());
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

        internal bool SetActiveConfiguration(string configurationName)
        {
            if (_trackedMutationDepth == 0)
                return ExecuteTrackedMutation("更改活动解决方案配置", () => SetActiveConfiguration(configurationName), result => result);
            string normalizedName = NormalizeConfigurationName(configurationName);
            if (string.Equals(Config.ActiveConfiguration, normalizedName, StringComparison.OrdinalIgnoreCase))
                return false;

            Config.ActiveConfiguration = normalizedName;
            SaveConfig();
            InvalidateMenuItems();
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

            try
            {
                foreach (var item in changedDependencies)
                {
                    if (!ProjectProviderRegistry.TrySetProjectDependencies(
                        item.Project,
                        item.Dependencies,
                        out _,
                        out errorMessage))
                    {
                        throw new InvalidOperationException(errorMessage);
                    }
                }

                Config.ActiveConfiguration = NormalizeConfigurationName(changes.ActiveConfiguration);
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

            ReloadSolutionState();
            OperationHistory.Clear();
            return true;
        }

        internal static string NormalizeConfigurationName(string? configurationName)
        {
            return string.IsNullOrWhiteSpace(configurationName) ? "Debug" : configurationName.Trim();
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
                LoadProjects(),
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
            var projects = new List<(string Reference, ProjectDefinition Project)>();
            var unavailable = new List<(string Reference, string ResolvedPath, string ErrorMessage)>();
            foreach (string reference in Config.Projects.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (TryResolveProjectReference(
                    DirectoryInfo.FullName,
                    reference,
                    out ProjectDefinition? project,
                    out string resolvedPath,
                    out string loadError)
                    && project != null)
                {
                    projects.Add((reference, project));
                }
                else
                {
                    unavailable.Add((reference, resolvedPath, loadError));
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
                string unavailablePath = Directory.Exists(item.ResolvedPath)
                    ? Path.Combine(item.ResolvedPath, ".missing.cvproj")
                    : item.ResolvedPath;
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
                        unavailablePath,
                        item.ErrorMessage);
                }
                else
                {
                    unavailableNode.UpdateUnavailableState(unavailablePath, item.ErrorMessage);
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
                TryResolveProjectReference(DirectoryInfo.FullName, reference, out ProjectDefinition? project, out _)
                && project != null
                && PathEquals(project.ProjectDirectory.FullName, DirectoryInfo.FullName));
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
            out string resolvedPath)
        {
            return TryResolveProjectReference(
                solutionDirectory,
                projectReference,
                out project,
                out resolvedPath,
                out _);
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

            var dispatcher = Application.Current?.Dispatcher;
            void QueueRefresh()
            {
                _projectChangedDebounceTimer.Stop();
                _projectChangedDebounceTimer.Start();
            }

            if (dispatcher == null || dispatcher.CheckAccess())
                QueueRefresh();
            else
                dispatcher.BeginInvoke(QueueRefresh);
        }

        internal void ApplyProjectMutation(ProjectDefinition updatedProject)
        {
            if (IsExplicitProjectMode)
            {
                ReconcileExplicitProjects(reloadLoadedProjects: true);
                return;
            }

            ReloadSolutionState();
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
            SolutionNodeFactory.PopulateChildren(this, DirectoryInfo, cache: null);
            ReconcileExplicitProjects();

            foreach (SolutionNode node in VisualChildren.GetAllVisualChildren()
                .Where(node => expandedPaths.Contains(node.FullPath)))
            {
                node.IsExpanded = true;
            }

            EnsureStartupProject();
            InvalidateMenuItems();
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
            MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Configuration);
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

        internal bool IsPathWithinSolution(string fullPath)
        {
            string relativePath = Path.GetRelativePath(DirectoryInfo.FullName, fullPath);
            return !Path.IsPathRooted(relativePath)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }


        public override void Open()
        {
            new SolutionEditor().Open(FullPath);
        }

        private TResult ExecuteTrackedMutation<TResult>(
            string description,
            Func<TResult> mutation,
            Func<TResult, bool> succeeded)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentNullException.ThrowIfNull(mutation);
            ArgumentNullException.ThrowIfNull(succeeded);

            bool isOuterMutation = _trackedMutationDepth == 0;
            string? beforeSnapshot = isOuterMutation && _operationHistoryEnabled && !_isApplyingOperationSnapshot
                ? SolutionConfigStore.Serialize(Config)
                : null;
            _trackedMutationDepth++;
            try
            {
                TResult result = mutation();
                if (beforeSnapshot != null && succeeded(result))
                {
                    OperationHistory.Record(
                        description,
                        beforeSnapshot,
                        SolutionConfigStore.Serialize(Config));
                }
                return result;
            }
            finally
            {
                _trackedMutationDepth--;
            }
        }

        internal bool CanUndoSolutionOperation => OperationHistory.CanUndo;
        internal bool CanRedoSolutionOperation => OperationHistory.CanRedo;

        internal bool TryUndoSolutionOperation(out string errorMessage)
        {
            string applyError = string.Empty;
            bool applied = OperationHistory.TryUndo(snapshot =>
                TryApplyOperationSnapshot(snapshot, out applyError));
            errorMessage = applied
                ? string.Empty
                : string.IsNullOrWhiteSpace(applyError) ? "没有可撤销的解决方案操作。" : applyError;
            return applied;
        }

        internal bool TryRedoSolutionOperation(out string errorMessage)
        {
            string applyError = string.Empty;
            bool applied = OperationHistory.TryRedo(snapshot =>
                TryApplyOperationSnapshot(snapshot, out applyError));
            errorMessage = applied
                ? string.Empty
                : string.IsNullOrWhiteSpace(applyError) ? "没有可重做的解决方案操作。" : applyError;
            return applied;
        }

        private bool TryApplyOperationSnapshot(string snapshot, out string errorMessage)
        {
            string rollbackSnapshot = SolutionConfigStore.Serialize(Config);
            _isApplyingOperationSnapshot = true;
            try
            {
                ApplyOperationSnapshot(snapshot);
                errorMessage = string.Empty;
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or InvalidOperationException
                or ArgumentException
                or NotSupportedException)
            {
                Logger.Error("应用解决方案操作历史失败。", ex);
                try
                {
                    ApplyOperationSnapshot(rollbackSnapshot);
                }
                catch (Exception rollbackException)
                {
                    Logger.Error("回滚解决方案操作历史失败。", rollbackException);
                }
                errorMessage = $"无法应用解决方案操作历史：{ex.Message}";
                return false;
            }
            finally
            {
                _isApplyingOperationSnapshot = false;
            }
        }

        private void ApplyOperationSnapshot(string snapshot)
        {
            Config = SolutionConfigStore.DeserializeAndMigrate(snapshot, out _);
            SaveConfig();
            ReloadSolutionState();
            NotifyPropertyChanged(nameof(ActiveConfiguration));
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        public void SaveConfig()
        {
            if (Config == null)
                return;
            SolutionConfigStore.Save(ConfigFileInfo.FullName, Config);
        }

        private bool SaveConfigWithUserFeedback()
        {
            try
            {
                SaveConfig();
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                Logger.Error($"保存解决方案失败: {ConfigFileInfo.FullName}", ex);
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"无法保存解决方案配置。\n\n{ex.Message}",
                    "保存解决方案失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        private static void WriteTextAtomically(string filePath, string content)
        {
            string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporaryPath, content);
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private SolutionNode? FindNodeByFullPath(string fullPath)
        {
            foreach (var child in VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    return child;

                var found = FindNodeByFullPath(child, fullPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private List<SolutionNode> FindNodesByFullPath(string fullPath)
        {
            var result = new List<SolutionNode>();
            CollectNodesByFullPath(this, fullPath, result);
            return result;
        }

        private static void CollectNodesByFullPath(SolutionNode parent, string fullPath, List<SolutionNode> result)
        {
            foreach (SolutionNode child in parent.VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    result.Add(child);
                CollectNodesByFullPath(child, fullPath, result);
            }
        }

        private static SolutionNode? FindNodeByFullPath(SolutionNode node, string fullPath)
        {
            foreach (var child in node.VisualChildren)
            {
                if (PathEquals(child.FullPath, fullPath))
                    return child;

                var found = FindNodeByFullPath(child, fullPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static bool PathEquals(string? left, string? right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static void DisposeVisualChildren(SolutionNode parent)
        {
            foreach (SolutionNode child in parent.VisualChildren.ToList())
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
                else
                    DisposeVisualChildren(child);
            }
        }

        /// <summary>
        /// 显示文件夹属性
        /// </summary>
        public override void ShowProperty()
        {
            Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Disposing?.Invoke(this, EventArgs.Empty);
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            _fileSystemWatcher?.Dispose();
            _changedDebounceTimer?.Stop();
            _projectChangedDebounceTimer?.Stop();
            DisposeVisualChildren(this);
            VisualChildren.Clear();
            Cache?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
