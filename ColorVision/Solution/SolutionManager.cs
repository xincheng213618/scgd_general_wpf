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
        public SolutionConfig CurrentSolution { get; set; }
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

            CurrentSolution = new SolutionConfig();

            SoftwareConfig = ConfigHandler.GetInstance().SoftwareConfig;
            SolutionLoaded += SolutionManager_SolutionLoaded;

            bool su = false;
            if (File.Exists(App.SolutionPath))
            {
                CurrentSolution.FullPath = App.SolutionPath;
                su =OpenSolution(CurrentSolution.FullPath);
            }
            else if (SolutionHistory.RecentFiles.Count > 0)
            {
                su =OpenSolution(SolutionHistory.RecentFiles[0]);
            }

            if (!su)
            {
                string Default = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision";
                if (!Directory.Exists(Default))
                    Directory.CreateDirectory(Default);

                string DefaultSolution = Default + "\\" + "Default";
                if (Directory.Exists(DefaultSolution))
                    Directory.CreateDirectory(DefaultSolution);
                DirectoryInfo directoryInfo = new DirectoryInfo(DefaultSolution);
                var SolutionPath = CreateSolution(directoryInfo);
                OpenSolution(SolutionPath);
            }


            SolutionOpenCommand = new RelayCommand((a) => OpenSolutionWindow());
            SolutionCreateCommand = new RelayCommand((a) => NewCreateWindow());
        }

        private void SolutionManager_SolutionLoaded(object? sender, EventArgs e)
        {
            if (sender is SolutionConfig solutionConfig)
            {
                SolutionExplorers.Clear();
                CurrentSolutionExplorer = new SolutionExplorer(solutionConfig.FullPath);
                SolutionExplorers.Add(CurrentSolutionExplorer);
            }
        }


        public DirectoryInfo? SolutionDirectory { get; private set; }

        public bool OpenSolution(string FullPath)
        {
            if (File.Exists(FullPath)&& FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new FileInfo(FullPath);
                CurrentSolution.FullPath = FullPath;
                SolutionDirectory = fileInfo.Directory;
                SolutionHistory.InsertFile(FullPath);
                SolutionLoaded?.Invoke(CurrentSolution, new EventArgs());
                return true;
            }
            else
            {
                SolutionHistory.RemoveFile(FullPath);
                return false;
            }
        }

        public string CreateSolution(DirectoryInfo Info)
        {
            SolutionConfig solutionConfig = new SolutionConfig();
            Tool.CreateDirectoryMax(Info.FullName +"\\.cache");

            CurrentSolution.FullPath = Info.FullName;
            string slnName = Info.FullName + "\\" + Info.Name + ".cvsln";
            CurrentSolution.ToJsonNFile(slnName);
            SolutionCreated?.Invoke(slnName, new EventArgs());
            
            return slnName;
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
                        OpenSolution(openSolutionWindow.FullName);
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
                    string name = CreateSolution(new DirectoryInfo(SolutionDirectoryPath));
                    OpenSolution(name);
                }
            };
            newCreatWindow.ShowDialog();
        }

        public void ClearCache()
        {
            Directory.Delete(CurrentSolution.FullPath+ "\\Flow",true);
            Directory.Delete(CurrentSolution.FullPath+ "\\Cache", true);
            Tool.CreateDirectoryMax(CurrentSolution.FullPath+ "\\Cache");
            Tool.CreateDirectoryMax(CurrentSolution.FullPath+ "\\Flow");
        }
    }
}
