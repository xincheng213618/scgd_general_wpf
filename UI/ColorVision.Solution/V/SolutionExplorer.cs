using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.UI.PropertyEditor;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.V
{
    public class SolutionExplorer: VObject
    {
        private static readonly ILog log  = LogManager.GetLogger(typeof(SolutionExplorer));
        public DirectoryInfo DirectoryInfo { get; set; }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand CopyFullPathCommand { get; set; }

        public static SolutionSetting Setting => SolutionSetting.Instance;

        FileSystemWatcher FileSystemWatcher { get; set; }

        public CVSolutionConfig Config { get; set; }
        
        public FileInfo ConfigFileInfo { get; set; }

        public SolutionEnvironments SolutionEnvironments { get; set; }
        public SolutionExplorer(SolutionEnvironments solutionEnvironments) 
        {
            SolutionEnvironments = solutionEnvironments;
            CopyFullPathCommand = new RelayCommand(a => Common.NativeMethods.Clipboard.SetText(SolutionEnvironments.SolutionPath));

            DisableExpanded = true;
            IsExpanded = true;

            string FullPath = solutionEnvironments.SolutionPath;
            if (File.Exists(FullPath) && FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFileInfo = new(FullPath);
                if (ConfigFileInfo != null)
                {
                    DirectoryInfo = ConfigFileInfo.Directory ?? new DirectoryInfo(FullPath);
                    Name1 = Path.GetFileNameWithoutExtension(FullPath);
                    if (DirectoryInfo != null)
                    {
                        DirectoryInfo rootDirectory = DirectoryInfo.Root;
                        DriveInfo = new DriveInfo(rootDirectory.FullName);
                    }
                }
                Config = JsonConvert.DeserializeObject<CVSolutionConfig>(File.ReadAllText(FullPath)) ?? new CVSolutionConfig();
            }

            OpenFileInExplorerCommand = new RelayCommand(a => System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            AddDirCommand = new RelayCommand(a => VMUtil.CreatFolders(this, DirectoryInfo.FullName));
            EditCommand = new RelayCommand(a => { new PropertyEditorWindow(this.Config).ShowDialog(); Config.ToJsonNFile(FullPath); });

            DriveMonitor();
            if (DirectoryInfo !=null && DirectoryInfo.Exists)
            {
                FileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName);
                FileSystemWatcher.Created += (s, e) =>
                {
                    if (File.Exists(e.FullPath))
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            VMUtil.Instance.CreateFile(this, new FileInfo(e.FullPath));
                        });
                        return;
                    }
                    if (Directory.Exists(e.FullPath))
                    {
                        Application.Current?.Dispatcher.Invoke(async () =>
                        {
                            await VMUtil.Instance.CreateDir(this, new DirectoryInfo(e.FullPath));
                        }); ;
                        return;
                    }
                };
                FileSystemWatcher.Deleted += (s, e) => {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        var a = VisualChildren.FirstOrDefault(a => a.FullPath == e.FullPath);
                        if (a != null)
                        {
                            VisualChildren.Remove(a);
                        }
                        VisualChildrenEventHandler?.Invoke(this, new EventArgs());
                    });
                };
                FileSystemWatcher.Changed += (s, e) => {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        VisualChildrenEventHandler?.Invoke(this, new EventArgs());
                    });
                };
                FileSystemWatcher.Renamed += (s, e) =>
                {

                };
                FileSystemWatcher.EnableRaisingEvents = true;
            }
            var _stopwatch = Stopwatch.StartNew();
            VMUtil.Instance.GeneralChild(this, DirectoryInfo);
            _stopwatch.Stop();
            log.Info($"工程初始化时间: {_stopwatch.Elapsed.TotalSeconds} 秒");
            AppDomain.CurrentDomain.ProcessExit += (s, e) => SaveConfig();
            SaveCommand = new RelayCommand(a => SaveConfig());
        }


        public override void InitMenuItem()
        {
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Edit", Order = 50, Header = "编辑解决方案", Command = EditCommand, });

            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "Add", Order = 10, Header = "添加" });
            MenuItemMetadatas.Add(new MenuItemMetadata() { OwnerGuid = "Add", GuidId = "AddFolder", Order = 1, Header = "添加文件夹", Command = AddDirCommand });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "CopyFullPath", Order = 200, Command = CopyFullPathCommand, Header = "复制完整路径" , Icon = MenuItemIcon.TryFindResource("DICopyFullPath") });
            MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "MenuOpenFileInExplorer", Order = 200, Command = OpenFileInExplorerCommand, Header = Resources.MenuOpenFileInExplorer });   
        }

        public void SaveConfig()
        {
            Config?.ToJsonNFile(ConfigFileInfo.FullName);
        }
        public override void ShowProperty()
        {
            Common.NativeMethods.FileProperties.ShowFolderProperties(DirectoryInfo.FullName);
        }

        public EventHandler VisualChildrenEventHandler { get; set; }


        public void DriveMonitor()
        {
            Task.Run(async () =>
            {
                bool IsMonitor = true;
                while (IsMonitor)
                {
                    await Task.Delay(100);
                    if (Setting.IsLackWarning)
                    {
                        if (DriveInfo.IsReady)
                        {
                            if (DriveInfo.AvailableFreeSpace < 1024 * 1024 * 1024 )
                            {

                                Setting.IsMemoryLackWarning = false;
                                MessageBox.Show("磁盘空间不足");
                            }
                        }
                        else
                        {
                            IsMonitor = false;
                        }
                    }
                }
            });
        }

        public DriveInfo DriveInfo { get; set; }
        public RelayCommand EditCommand { get; set; }
        public RelayCommand AddDirCommand { get; set; }
    }
}
