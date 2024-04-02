using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.HotKey;
using ColorVision.Common.MVVM;
using ColorVision.RecentFile;
using ColorVision.Settings;
using ColorVision.Solution.V;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution
{
    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));

        private static SolutionManager _instance;
        private static readonly object _locker = new();
        public static SolutionManager GetInstance() { 
            lock (_locker)
            { 
                if (_instance == null)
                    _instance = new SolutionManager();
                return _instance;
            } }

        //工程配置文件
        public SolutionConfig CurrentSolution { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting Setting { get => SoftwareConfig.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }
        public RecentFileList SolutionHistory { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };



        /// <summary>
        /// 工程初始化的时候
        /// </summary>
        public event EventHandler SolutionCreated;
        /// <summary>
        /// 工程打开的时候
        /// </summary>
        public event EventHandler SolutionLoaded;

        public ObservableCollection<SolutionExplorer> SolutionExplorers { get; set; }
        public SolutionExplorer CurrentSolutionExplorer { get => _CurrentSolutionExplorer; set { _CurrentSolutionExplorer = value; NotifyPropertyChanged(); } }
        private SolutionExplorer _CurrentSolutionExplorer;

        public RelayCommand SolutionOpenCommand { get; set; }
        public RelayCommand SolutionCreateCommand { get; set; }

        public SolutionManager()
        {
            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            if (File.Exists(App.SolutionPath))
            {
                CurrentSolution.FullName = App.SolutionPath;
                OpenSolution(CurrentSolution.FullName);
            }
            else
            {
                SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
                OpenSolutionDirectory(CurrentSolution.FullName);
            }

            SolutionOpenCommand = new RelayCommand((a) => OpenSolutionWindow());
            SolutionCreateCommand = new RelayCommand((a) => NewCreateWindow());
        }



        public void InitMenu()
        {
            var FileMenuItem = MenuManager.GetInstance().FileMenuItem;       
            MenuItem RecentListMenuItem = null;

            RecentListMenuItem ??= new MenuItem();
            RecentListMenuItem.Header = Properties.Resource.RecentFiles;
            RecentListMenuItem.SubmenuOpened += (s, e) =>
            {
                var firstMenuItem = RecentListMenuItem.Items[0];
                foreach (var item in SolutionHistory.RecentFiles)
                {
                    if (Directory.Exists(item))
                    {
                        MenuItem menuItem = new MenuItem();
                        menuItem.Header = item;
                        menuItem.Click += (sender, e) =>
                        {
                            OpenSolutionDirectory(item);
                        };
                        RecentListMenuItem.Items.Add(menuItem);
                    }
                    else
                    {
                        SolutionHistory.RecentFiles.Remove(item);
                    }
                };
                RecentListMenuItem.Items.Remove(firstMenuItem);

            };
            RecentListMenuItem.SubmenuClosed += (s, e) => {
                RecentListMenuItem.Items.Clear();
                RecentListMenuItem.Items.Add(new MenuItem());
            };
            RecentListMenuItem.Items.Add(new MenuItem());

            FileMenuItem?.Items.Insert(3, RecentListMenuItem);

        }



        public void AddHotKeys()
        {
            InitMenu();
            Application.Current.MainWindow.AddHotKeys(new HotKeys(Properties.Resource.OpenSolution, new Hotkey(Key.O, ModifierKeys.Control), OpenSolutionWindow));
            Application.Current.MainWindow.AddHotKeys(new HotKeys(Properties.Resource.NewSolution, new Hotkey(Key.N, ModifierKeys.Control), NewCreateWindow));
        }


        public DirectoryInfo? SolutionDirectory { get; private set; }

        public bool OpenSolutionDirectory(string SolutionFullPath)
        {
            log.Debug("正在打开工程:" + SolutionFullPath);

            if (!Directory.Exists(SolutionFullPath) && !File.Exists(SolutionFullPath))
            {
                string DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision\\默认工程";
                if (!Directory.Exists(DefaultPath))
                    Directory.CreateDirectory(DefaultPath);
                CreateSolution(new DirectoryInfo(DefaultPath));
                SolutionFullPath = DefaultPath;
            }

            CurrentSolution.FullName = SolutionFullPath;

            if (Directory.Exists(SolutionFullPath))
            {
                SolutionDirectory = new DirectoryInfo(CurrentSolution.FullName);
            }

            if (File.Exists(SolutionFullPath))
            {
                FileInfo fileInfo = new FileInfo(SolutionFullPath);
                SolutionDirectory = fileInfo.Directory;
            }


            SolutionHistory.InsertFile(CurrentSolution.FullName);
            SolutionLoaded?.Invoke(CurrentSolution, new EventArgs());

            SolutionExplorers.Clear();
            CurrentSolutionExplorer = new SolutionExplorer(SolutionFullPath);
            SolutionExplorers.Add(CurrentSolutionExplorer);
            return true;
        }

        public bool OpenSolution(string FullPath)
        {
            if (File.Exists(FullPath)&& FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new FileInfo(FullPath);
                CurrentSolution.FullName = FullPath;
                SolutionDirectory = fileInfo.Directory;
                SolutionHistory.InsertFile(FullPath);
                SolutionLoaded?.Invoke(CurrentSolution, new EventArgs());
                SolutionExplorers.Clear();
                CurrentSolutionExplorer = new SolutionExplorer(FullPath);
                SolutionExplorers.Add(CurrentSolutionExplorer);
            }
            else
            {
                MessageBox.Show("打开工程失败");
            }
            return true;
        }

        public void CreateSolution(DirectoryInfo Info)
        {
            Tool.CreateDirectoryMax(Info.FullName +"\\Cache");
            Tool.CreateDirectoryMax(Info.FullName + "\\Cfg");
            Tool.CreateDirectoryMax(Info.FullName + "\\Image");
            Tool.CreateDirectoryMax(Info.FullName + "\\Flow");

            CurrentSolution.FullName = Info.FullName;

            CurrentSolution.ToJsonNFile(Info.FullName + "\\" + Info.Name + ".cvsln");
            SolutionCreated?.Invoke(CurrentSolution, new EventArgs());
        }

        public void CreateShortcut(string FileName)
        {
            if (SolutionDirectory!=null)
                Common.NativeMethods.ShortcutCreator.CreateShortcut(Path.GetFileName(FileName),SolutionDirectory.FullName +"\\Image", FileName);
        }

        public void OpenSolutionWindow()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Closed += delegate
            {
                if (!string.IsNullOrWhiteSpace(openSolutionWindow.FullName))
                {
                    if (Directory.Exists(openSolutionWindow.FullName))
                        OpenSolutionDirectory(openSolutionWindow.FullName);
                    else
                        MessageBox.Show("找不到工程");
                }

            };
            openSolutionWindow.Show();
        }
        public void NewCreateWindow()
        {
            NewCreateWindow newCreatWindow = new NewCreateWindow() { Owner = WindowHelpers.GetActiveWindow() , WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    CreateSolution(new DirectoryInfo(SolutionDirectoryPath));
                    OpenSolutionDirectory(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }

        public void ClearCache()
        {
            Directory.Delete(CurrentSolution.FullName+ "\\Flow",true);
            Directory.Delete(CurrentSolution.FullName+ "\\Cache", true);
            Tool.CreateDirectoryMax(CurrentSolution.FullName+ "\\Cache");
            Tool.CreateDirectoryMax(CurrentSolution.FullName+ "\\Flow");
        }
    }
}
