﻿using ColorVision.Common.Utilities;
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
        public static SolutionManager GetInstance() { lock (_locker) { return _instance ??= new SolutionManager(); } }

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


        public SolutionManager()
        {
            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;

            if (File.Exists(App.SolutionPath))
            {
                CurrentSolution.FullName = App.SolutionPath;
            }
            else
            {
                SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            }
            Application.Current.MainWindow.AddHotKeys(new HotKeys("打开工程", new Hotkey(Key.O, ModifierKeys.Control), OpenSolutionWindow));
            Application.Current.MainWindow.AddHotKeys(new HotKeys("新建工程", new Hotkey(Key.N, ModifierKeys.Control), NewCreateWindow));

            //ClearCache();

            OpenSolutionDirectory(CurrentSolution.FullName);

        }

        public DirectoryInfo SolutionDirectory { get; private set; }

        public bool OpenSolutionDirectory(string SolutionFullPath)
        {
            log.Debug("正在打开工程:" + SolutionFullPath);

            if (!Directory.Exists(SolutionFullPath))
            {
                string DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision\\默认工程";
                if (!Directory.Exists(DefaultPath))
                    Directory.CreateDirectory(DefaultPath);
                CreateSolution(new DirectoryInfo(DefaultPath));
                SolutionFullPath = DefaultPath;
            }
            CurrentSolution.FullName = SolutionFullPath;
            SolutionDirectory = new DirectoryInfo(CurrentSolution.FullName);
            SolutionHistory.InsertFile(SolutionDirectory.FullName);
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
                CurrentSolution.FullName = FullPath;
                SolutionHistory.InsertFile(SolutionDirectory.FullName);
                SolutionLoaded?.Invoke(CurrentSolution, new EventArgs());

                SolutionExplorers.Clear();
                CurrentSolutionExplorer = new SolutionExplorer(FullPath);
                SolutionExplorers.Add(CurrentSolutionExplorer);
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

        public void OpenSolutionWindow() => OpenSolutionWindow(Application.Current.MainWindow);
        public void OpenSolutionWindow(Window window)
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = window, WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
