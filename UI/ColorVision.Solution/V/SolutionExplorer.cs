#pragma warning disable CS8602,CS8604,CS4014
using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.Solution.Searches;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.V
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
    public class SolutionExplorer : VObject
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(SolutionExplorer));
        public DirectoryInfo DirectoryInfo { get; private set; }
        public RelayCommand OpenFileInExplorerCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand AddDirCommand { get; }

        public static SolutionSetting Setting => SolutionSetting.Instance;
        private FileSystemWatcher _fileSystemWatcher;
        public SolutionConfig Config { get; private set; }
        public FileInfo ConfigFileInfo { get; private set; }
        public SolutionEnvironments SolutionEnvironments { get; }
        public DriveInfo DriveInfo { get; private set; }
        public EventHandler VisualChildrenEventHandler { get; set; }

        public SolutionExplorer(SolutionEnvironments solutionEnvironments)
        {
            SolutionEnvironments = solutionEnvironments ?? throw new ArgumentNullException(nameof(solutionEnvironments));
            CopyFullPathCommand = new RelayCommand(_ => Common.NativeMethods.Clipboard.SetText(SolutionEnvironments.SolutionPath));
            OpenFileInExplorerCommand = new RelayCommand(_ => Process.Start("explorer.exe", DirectoryInfo.FullName), _ => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(_ => VMUtil.CreatFolders(this, DirectoryInfo.FullName));
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

            var stopwatch = Stopwatch.StartNew();
            VMUtil.Instance.GeneralChild(this, DirectoryInfo);
            stopwatch.Stop();
            Logger.Info($"工程初始化时间: {stopwatch.Elapsed.TotalSeconds} 秒");

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
                _fileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };
                _fileSystemWatcher.Created += FileSystemWatcher_Created;
                _fileSystemWatcher.Deleted += FileSystemWatcher_Deleted;
                _fileSystemWatcher.Changed += (s, e) => Application.Current?.Dispatcher.Invoke(() =>
                {
                    VisualChildrenEventHandler?.Invoke(this, EventArgs.Empty);
                });
                _fileSystemWatcher.Renamed += (s, e) => { /* 可扩展: 重命名处理逻辑 */ };
            }
        }

        private void FileSystemWatcher_Created(object sender, FileSystemEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (File.Exists(e.FullPath))
                {
                    VMUtil.Instance.CreateFile(this, new FileInfo(e.FullPath));
                }
                else if (Directory.Exists(e.FullPath))
                {
                    VMUtil.Instance.CreateDir(this, new DirectoryInfo(e.FullPath));
                }
            });
        }

        private void FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            Application.Current?.Dispatcher.Invoke(() =>
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
                GuidId = "AddFolder",
                Order = 1,
                Header = ColorVision.Solution.Properties.Resources.AddFolder,
                Command = AddDirCommand
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

    }
}