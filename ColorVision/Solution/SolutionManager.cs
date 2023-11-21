using ColorVision.SettingUp;
using System.Diagnostics;
using System;
using System.IO;
using ColorVision.Util;
using ColorVision.RecentFile;
using ColorVision.MySql;
using log4net;
using System.Windows;
using ColorVision.HotKey;
using System.Windows.Input;
using System.Security.Cryptography.X509Certificates;
using ColorVision.Extension;

namespace ColorVision.Solution
{
    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));

        private static SolutionManager _instance;
        private static readonly object _locker = new();
        public static SolutionManager GetInstance() { lock (_locker) { return _instance ??= new SolutionManager(); } }

        //工程配置文件
        public SolutionConfig CurrentSolution { get => SoftwareConfig.SolutionConfig; }
        public SolutionSetting Setting { get => SoftwareConfig.SolutionSetting; }
        public SoftwareConfig SoftwareConfig { get; private set; }
        public RecentFileList SolutionHistory { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        /// <summary>
        /// 工程初始化的时候
        /// </summary>
        public event EventHandler SolutionInitialized;
        /// <summary>
        /// 工程打开的时候
        /// </summary>
        public event EventHandler SolutionLoaded;


        public SolutionManager()
        {
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            Application.Current.MainWindow.AddHotKeys(new HotKeys("打开工程", new Hotkey(Key.O, ModifierKeys.Control), OpenSolutionWindow));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("新建工程", new Hotkey(Key.N, ModifierKeys.Control), NewCreateWindow));

            OpenSolution(CurrentSolution.FullName);
        }

        public bool OpenSolution(string SolutionFullPath)
        {
            if (string.IsNullOrWhiteSpace(SolutionFullPath))
                return false;
            log.Debug("正在打开工程:" + SolutionFullPath);

            if (Directory.Exists(SolutionFullPath))
            {
                DirectoryInfo Info = new DirectoryInfo(SolutionFullPath);
                CurrentSolution.SolutionName = Info.Name;
                CurrentSolution.FullName = Info.FullName;
                SolutionHistory.InsertFile(Info.FullName);
                SolutionLoaded?.Invoke(CurrentSolution,new EventArgs());
                return true;
            }
            else
            {
                CurrentSolution.SolutionName = string.Empty ;
                CurrentSolution.FullName = string.Empty;

                log.Debug("工程文件打开失败:" + SolutionFullPath);
                MessageBox.Show("工程文件已被删除，在使用之前请重新创建", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return false;
            }
        }

        public void CreateSolution(DirectoryInfo Info)
        {
            Tool.CreateDirectoryMax(Info.FullName +"\\Cache");
            Tool.CreateDirectoryMax(Info.FullName + "\\Cfg");
            Tool.CreateDirectoryMax(Info.FullName + "\\Image");
            Tool.CreateDirectoryMax(Info.FullName + "\\Flow");

            CurrentSolution.SolutionName = Info.Name;
            CurrentSolution.FullName = Info.FullName;

            CurrentSolution.ToJsonNFile(Info.FullName + "\\" + Info.Name + ".cvsln");
            SolutionInitialized.Invoke(CurrentSolution, new EventArgs());
        }

        public void OpenSolutionWindow() => OpenSolutionWindow(Application.Current.MainWindow);
        public void OpenSolutionWindow(Window window)
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Closed += delegate
            {
                if (!string.IsNullOrWhiteSpace(openSolutionWindow.FullName))
                {
                    if (Directory.Exists(openSolutionWindow.FullName))
                        OpenSolution(openSolutionWindow.FullName);
                    else
                        MessageBox.Show("找不到工程");
                }

            };
            openSolutionWindow.Show();
        }
        public void NewCreateWindow() => NewCreateWindow(Application.Current.MainWindow);

        public void NewCreateWindow(Window window)
        {
            NewCreateWindow newCreatWindow = new NewCreateWindow() { Owner = window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    CreateSolution(new DirectoryInfo(SolutionDirectoryPath));
                    OpenSolution(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }

    }
}
