#pragma warning disable CS4014,CS8602,CS8604
using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// 配置解决方案的模型类，支持MVVM绑定
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {
        public string FilePath { get; set; }
        public string VirtualPath { get; set; }
        public bool IsSetting { get; set; }
        public bool IsSetting1 { get; set; }
        public ObservableCollection<string> Paths { get; set; }

        /// <summary>
        /// 项目引用列表 - 存储相对于解决方案目录的项目路径
        /// 类似VS的.sln，项目路径信息保存在解决方案配置中
        /// </summary>
        public ObservableCollection<string> Projects { get; set; } = new ObservableCollection<string>();
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
        private readonly EventHandler _processExitHandler;
        public SolutionConfig Config { get; private set; }
        public FileInfo ConfigFileInfo { get; private set; }
        public SolutionEnvironments SolutionEnvironments { get; }
        public DriveInfo DriveInfo { get; private set; }
        public EventHandler VisualChildrenEventHandler { get; set; }

        public SolutionCache? Cache { get; private set; }

        public SolutionExplorer(SolutionEnvironments solutionEnvironments)
        {
            SolutionEnvironments = solutionEnvironments ?? throw new ArgumentNullException(nameof(solutionEnvironments));
            CopyFullPathCommand = new RelayCommand(_ => Common.NativeMethods.Clipboard.SetText(SolutionEnvironments.SolutionPath));
            OpenFileInExplorerCommand = new RelayCommand(_ => Process.Start("explorer.exe", DirectoryInfo.FullName), _ => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(_ => SolutionNodeFactory.CreateNewFolder(this, DirectoryInfo.FullName));
            SaveCommand = new RelayCommand(_ => SaveConfig());
            EditCommand = new RelayCommand(_ =>
            {
                new PropertyEditorWindow(Config)
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.ShowDialog();
                Config.ToJsonNFile(ConfigFileInfo.FullName);
            });

            InitializeSolution();

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
            SolutionNodeFactory.PopulateChildren(this, DirectoryInfo, Cache);
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

            _processExitHandler = (_, __) => SaveConfig();
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
                DirectoryInfo = ConfigFileInfo.Directory ?? new DirectoryInfo(fullPath);
                Name1 = string.IsNullOrWhiteSpace(SolutionEnvironments.SolutionFileName)
                    ? Path.GetFileNameWithoutExtension(fullPath)
                    : SolutionEnvironments.SolutionFileName;
                if (DirectoryInfo?.Root != null)
                {
                    DriveInfo = new DriveInfo(DirectoryInfo.Root.FullName);
                }
                Config = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(fullPath)) ?? new SolutionConfig();
            }
            else
            {
                throw new FileNotFoundException("Solution file not found or invalid extension.", fullPath);
            }
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
                    _changedDebounceTimer.Stop();
                    _changedDebounceTimer.Start();
                });
            }
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            // Skip internal solution files (e.g. .cvsln, .cache.db)
            if (e.Name != null && SolutionNodeFactory.IsInternalFile(e.Name))
                return;

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
                if (oldNode != null)
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
            });
        }

        public override void InitMenuItem()
        {
            MenuItemMetadatas.Add(new MenuItemMetadata
            {
                GuidId = "Open",
                Order = 1,
                Header = Resources.MenuOpen,
                Command = OpenCommand
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

        private void ShowAddNewItemDialog()
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

                var fileInfo = new FileInfo(fullPath);
                var fileNode = SolutionNodeFactory.CreateFileNode(fileInfo);
                AddChild(fileNode);
                fileNode.IsSelected = true;
            }
        }

        private void AddExistingItem()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "添加现有项",
                Filter = "所有文件 (*.*)|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var sourcePath in dialog.FileNames)
                {
                    string destPath = Path.Combine(DirectoryInfo.FullName, Path.GetFileName(sourcePath));
                    if (!File.Exists(destPath))
                    {
                        File.Copy(sourcePath, destPath);
                    }
                }
            }
        }

        private void ShowAddNewProjectDialog()
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
                    string relativePath = Path.GetRelativePath(DirectoryInfo.FullName, dirInfo.FullName);
                    if (!Config.Projects.Contains(relativePath))
                    {
                        Config.Projects.Add(relativePath);
                        SaveConfig();
                    }
                }
                // FileSystemWatcher will pick up the new folder automatically
            }
        }

        private void AddExistingProject()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "添加现有项目",
                Filter = "项目文件 (*.cvproj)|*.cvproj|所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                string sourcePath = dialog.FileName;
                string sourceDir = Path.GetDirectoryName(sourcePath) ?? "";
                string destDir = Path.Combine(DirectoryInfo.FullName, Path.GetFileName(sourceDir));

                if (!Directory.Exists(destDir))
                {
                    CopyDirectory(sourceDir, destDir);
                }

                // Register project path in solution config
                string relativePath = Path.GetRelativePath(DirectoryInfo.FullName, destDir);
                if (!Config.Projects.Contains(relativePath))
                {
                    Config.Projects.Add(relativePath);
                    SaveConfig();
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(destinationDir, Path.GetFileName(dir)));
        }


        public override void Open()
        {
            new SolutionEditor().Open(FullPath);
        }

        /// <summary>
        /// 保存当前配置到文件
        /// </summary>
        public void SaveConfig()
        {
            Config?.ToJsonNFile(ConfigFileInfo.FullName);
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

        /// <summary>
        /// 显示文件夹属性
        /// </summary>
        public override void ShowProperty()
        {
            Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
            _fileSystemWatcher?.Dispose();
            _changedDebounceTimer?.Stop();
            foreach (var child in VisualChildren.OfType<IDisposable>().ToList())
                child.Dispose();
            VisualChildren.Clear();
            Cache?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
