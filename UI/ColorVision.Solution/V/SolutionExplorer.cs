using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.UI.PropertyEditor;
using log4net;
using Newtonsoft.Json;
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
        public RelayCommand ClearCacheCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public static SolutionSetting Setting => SolutionSetting.Instance;

        FileSystemWatcher FileSystemWatcher { get; set; }

        public CVSolutionConfig Config { get; set; }
        
        public FileInfo ConfigFileInfo { get; set; }
        public override bool IsExpanded { get => true; set {  } }

        public SolutionExplorer(SolutionEnvironments solutionEnvironments) 
        {
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

               var config = JsonConvert.DeserializeObject<CVSolutionConfig>(File.ReadAllText(FullPath));

                if (config == null)
                    MessageBox.Show("打开失败");
                Config = config ?? new CVSolutionConfig();

                Config?.ToJsonNFile(FullPath);
            }
            else if(Directory.Exists(FullPath))
            {
                DirectoryInfo = new DirectoryInfo(FullPath);
                DirectoryInfo rootDirectory = DirectoryInfo.Root;
                DriveInfo = new DriveInfo(rootDirectory.FullName);
            }
            GeneralContextMenu();
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

        public void SaveConfig()
        {
            Config?.ToJsonNFile(ConfigFileInfo.FullName);
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

        public void GeneralContextMenu()
        {
            OpenFileInExplorerCommand = new RelayCommand(a => System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            ClearCacheCommand = new RelayCommand(a => { VisualChildren.Clear(); });
            AddDirCommand = new RelayCommand(a => VMUtil.CreatFolders(this, DirectoryInfo.FullName));
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new() { Header = "打开工程文件夹", Command = OpenFileInExplorerCommand };
            ContextMenu.Items.Add(menuItem);
            MenuItem menuItem2 = new() { Header = "清除缓存", Command = ClearCacheCommand };
            ContextMenu.Items.Add(menuItem2);
            MenuItem menuItem3 = new() { Header = "添加" };
            MenuItem menuItem4 = new() { Header = "添加文件夹", Command = AddDirCommand };
            menuItem3.Items.Add(menuItem4);
            ContextMenu.Items.Add(menuItem3);
            EditCommand = new RelayCommand(a =>  new  PropertyEditorWindow(this.Config).ShowDialog());
            ContextMenu.Items.Add( new MenuItem (){ Header = "编辑", Command = EditCommand });
        }



    
    }
}
