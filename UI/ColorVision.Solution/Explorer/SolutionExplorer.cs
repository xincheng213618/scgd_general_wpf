#pragma warning disable CS8604,CS4014
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
            InitializeFileSystemWatcher();

            // Initialize SQLite cache for faster subsequent loads
            try
            {
                Cache = new SolutionCache(SolutionEnvironments.SolutionPath);
            }
            catch (Exception ex)
            {
                Logger.Warn($"缓存初始化失败，使用文件系统加载: {ex.Message}");
                Cache = null;
            }

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

            AppDomain.CurrentDomain.ProcessExit += (_, __) => SaveConfig();
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
                Name1 = Path.GetFileNameWithoutExtension(fullPath);
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
                    IncludeSubdirectories = false
                };
                _fileSystemWatcher.Created += FileSystemWatcher_Created;
                _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
                _fileSystemWatcher.Changed += (s, e) => Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    _changedDebounceTimer.Stop();
                    _changedDebounceTimer.Start();
                });
            }
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            // Update cache
            if (Cache != null)
            {
                if (File.Exists(e.FullPath))
                    Cache.AddFile(e.FullPath, DirectoryInfo.FullName);
                else if (Directory.Exists(e.FullPath))
                    Cache.AddDirectory(e.FullPath, DirectoryInfo.FullName);
            }

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                // Duplicate protection
                if (VisualChildren.Any(c => c.FullPath == e.FullPath))
                    return;

                if (File.Exists(e.FullPath))
                {
                    SolutionNodeFactory.AddFileNode(this, new FileInfo(e.FullPath));
                }
                else if (Directory.Exists(e.FullPath))
                {
                    SolutionNodeFactory.AddFolderNode(this, new DirectoryInfo(e.FullPath));
                }
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            // Update cache
            Cache?.Remove(e.FullPath);

            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                var child = VisualChildren.FirstOrDefault(a => a.FullPath == e.FullPath);
                if (child != null)
                {
                    VisualChildren.Remove(child);
                }
                VisualChildrenEventHandler?.Invoke(this, EventArgs.Empty);
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

        /// <summary>
        /// 显示文件夹属性
        /// </summary>
        public override void ShowProperty()
        {
            Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public void Dispose()
        {
            _fileSystemWatcher?.Dispose();
            _changedDebounceTimer?.Stop();
            Cache?.Dispose();
            GC.SuppressFinalize(this);
        }

    }
}
