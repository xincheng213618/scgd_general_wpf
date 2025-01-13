using ColorVision.Common.MVVM;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.Util.Solution.V;
using log4net;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.V
{

    /// <summary>
    /// 解决方案配置
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {

        public string FullPath
        {
            get => _FullPath;
            set
            {
                _FullPath = value;
                NotifyPropertyChanged();
            }
        }
        private string _FullPath;

        public ObservableCollection<string> Path { get; set; }

        public bool Istrue
        {
            get => _Istrue;
            set
            {
                _Istrue = value;
                NotifyPropertyChanged();
            }
        }
        private bool _Istrue;

    }

    public class VMCreate
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(VMCreate));
        public static VMCreate Instance { get; set; } = new VMCreate();

        public VMCreate()
        {
            GeneraFileTypes();
        }

        public void AddDir(VObject vObject,string FullName)
        {
            try
            {
                string name = Path.Combine(FullName, "NewFolder");
                Directory.CreateDirectory(name);
                DirectoryInfo directoryInfo = new DirectoryInfo(name);
                VMCreate.Instance.ManagerObject.Add(directoryInfo.FullName);
                VFolder vFolder = new VFolder(new BaseFolder(directoryInfo));
                vObject.AddChild(vFolder);
                if (!vObject.IsExpanded)
                    vObject.IsExpanded = true;
                vFolder.IsExpanded = true;
                vFolder.IsEditMode = true;
                vFolder.IsSelected = true;

            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        public List<string> ManagerObject { get; set; } = new List<string>();

        public  async Task GeneralChild(VObject vObject, DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }
                BaseFolder folder = new(item);
                var vFolder = new VFolder(folder);
                vObject.AddChild(vFolder);
                await GeneralChild(vFolder, item);
            }

            foreach (var item in directoryInfo.GetFiles())
            {
                i++;
                if (i == 10)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                var _stopwatch = Stopwatch.StartNew();
                CreateFile(vObject, item);
                _stopwatch.Stop();
                log.Debug($"{item.FullName}加载时间: {_stopwatch.Elapsed.TotalSeconds} 秒");
            }
        }

        int i;
        public async Task CreateDir(VObject vObject, DirectoryInfo directoryInfo)
        {
            if (VMCreate.Instance.ManagerObject.Contains(directoryInfo.FullName))
                return;
            VMCreate.Instance.ManagerObject.Add(directoryInfo.FullName);
            BaseFolder folder = new(directoryInfo);
            var vFolder = new VFolder(folder);
            vObject.AddChild(vFolder);
            await GeneralChild(vFolder, directoryInfo);

            foreach (var item in directoryInfo.GetFiles())
            {
                i++;
                if (i == 100)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                CreateFile(vObject, item);
            }
        }


        public Dictionary<string, Type> FileTypes { get; set; }

        public void GeneraFileTypes()
        {
            FileTypes = new Dictionary<string, Type>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFileMeta).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        if (Activator.CreateInstance(type) is IFileMeta page)
                        {
                            FileTypes.Add(page.Extension, type);
                        }
                    }
                }
            }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }


        public void CreateFile(VObject vObject, FileInfo fileInfo)
        {
            if (fileInfo.Extension.Contains("cvsln")) return;
            if (VMCreate.Instance.ManagerObject.Contains(fileInfo.FullName))
            {
                return;
            }
            VMCreate.Instance.ManagerObject.Add(fileInfo.FullName);

            string extension = fileInfo.Extension;
            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                extension = Path.GetExtension(targetPath);
                fileInfo = new FileInfo(targetPath);
            }
            List<Type> matchingTypes = new List<Type>();
            if (FileTypes.TryGetValue(extension, out Type specificTypes))
            {
                matchingTypes.Add(specificTypes);
            }
            foreach (var key in FileTypes.Keys)
            {
                if (key.Contains(extension))
                    matchingTypes.Add(FileTypes[key]);
            }
            foreach (var key in FileTypes.Keys)
            {
                var subKeys = key.Split('|');
                foreach (var subKey in subKeys)
                {
                    if (WildcardToRegex(subKey).IsMatch(extension))
                    {
                        matchingTypes.Add(FileTypes[key]);
                        break;
                    }
                }
            }
            if (matchingTypes.Count > 0)
            {
                if (Activator.CreateInstance(matchingTypes[0], fileInfo) is IFileMeta file)
                {
                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        VFile vFile = new VFile(file);
                        vObject.AddChild(vFile);
                    });
                }

            }

        }



    }

    public class SolutionExplorer: VObject
    {
        private static readonly ILog log  = LogManager.GetLogger(typeof(SolutionExplorer));
        public DirectoryInfo DirectoryInfo { get; set; }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand ClearCacheCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public static SolutionSetting Setting => SolutionSetting.Instance;

        FileSystemWatcher FileSystemWatcher { get; set; }

        public SolutionConfig Config { get; set; }
        
        public FileInfo ConfigFileInfo { get; set; }

        public  SolutionExplorer(string FullPath)
        {
            if (File.Exists(FullPath) && FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                ConfigFileInfo = new(FullPath);
                if (ConfigFileInfo != null)
                {
                    DirectoryInfo = ConfigFileInfo.Directory ?? new DirectoryInfo(FullPath);
                    Name = Path.GetFileNameWithoutExtension(FullPath);
                    if (DirectoryInfo != null)
                    {
                        DirectoryInfo rootDirectory = DirectoryInfo.Root;
                        DriveInfo = new DriveInfo(rootDirectory.FullName);
                    }
                }

               var config = JsonConvert.DeserializeObject<SolutionConfig>(File.ReadAllText(FullPath));

                if (config == null)
                    MessageBox.Show("打开失败");
                Config = config ?? new SolutionConfig();

                Config?.ToJsonNFile(FullPath);
            }
            else if(Directory.Exists(FullPath))
            {
                DirectoryInfo = new DirectoryInfo(FullPath);
                Name = DirectoryInfo.Name;
                DirectoryInfo rootDirectory = DirectoryInfo.Root;
                DriveInfo = new DriveInfo(rootDirectory.FullName);
            }

            GeneralContextMenu();
            IsExpanded = true;
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
                            VMCreate.Instance.CreateFile(this, new FileInfo(e.FullPath));
                        });
                        return;
                    }
                    if (Directory.Exists(e.FullPath))
                    {
                        Application.Current?.Dispatcher.Invoke(async () =>
                        {
                            await VMCreate.Instance.CreateDir(this, new DirectoryInfo(e.FullPath));
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
            VMCreate.Instance.GeneralChild(this, DirectoryInfo);
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
            AddDirCommand = new RelayCommand(a => VMCreate.Instance.AddDir(this, DirectoryInfo.FullName));
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new() { Header = "打开工程文件夹", Command = OpenFileInExplorerCommand };
            ContextMenu.Items.Add(menuItem);
            MenuItem menuItem2 = new() { Header = "清除缓存", Command = ClearCacheCommand };
            ContextMenu.Items.Add(menuItem2);
            MenuItem menuItem3 = new() { Header = "添加" };
            MenuItem menuItem4 = new() { Header = "添加文件夹", Command = AddDirCommand };
            menuItem3.Items.Add(menuItem4);
            ContextMenu.Items.Add(menuItem3);
            EditCommand = new RelayCommand(a => new EditSolutionConfig(this).ShowDialog());
            ContextMenu.Items.Add( new MenuItem (){ Header = "编辑", Command = EditCommand });
        }



    
    }
}
