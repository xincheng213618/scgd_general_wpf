using ColorVision.Common.MVVM;
using ColorVision.UI.Extension;
using ColorVision.RecentFile;
using ColorVision.Solution.V;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using ColorVision.UI.Shell;

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
        public static SolutionManager GetInstance() { lock (_locker) { _instance ??= new SolutionManager(); return _instance; } }

        public static SolutionSetting Setting => SolutionSetting.Instance;

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

        public RelayCommand SettingCommand { get; set; }

        public SolutionManager()
        {
            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            SolutionLoaded += SolutionManager_SolutionLoaded;

            bool su = false;
            var parser = ArgumentParser.GetInstance();

            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            var solutionpath = parser.GetValue("solutionpath");
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (solutionpath != null)
                {
                    su = OpenSolution(solutionpath);
                }
                else if (SolutionHistory.RecentFiles.Count > 0)
                {
                    su = OpenSolution(SolutionHistory.RecentFiles[0]);
                }
                JumpListManager jumpListManager = new JumpListManager();
                jumpListManager.AddRecentFiles(SolutionHistory.RecentFiles);
                if (!su)
                {
                    string Default = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision";
                    if (!Directory.Exists(Default))
                        Directory.CreateDirectory(Default);

                    string DefaultSolution = Default + "\\" + "Default";
                    if (Directory.Exists(DefaultSolution))
                        Directory.CreateDirectory(DefaultSolution);
                    var SolutionPath = CreateSolution(DefaultSolution);
                }
            });

            SettingCommand = SolutionSetting.Instance.EditCommand;
        }


        public event EventHandler<ISolutionProcess> OpenFile;


        public void OpenFileWindow(ISolutionProcess userControl)
        {
            OpenFile?.Invoke(this, userControl);
        }

        public SolutionEnvironments SolutionEnvironments { get; set; } = new SolutionEnvironments();

        private void SolutionManager_SolutionLoaded(object? sender, EventArgs e)
        {
            if (sender is string solutionConfig)
            {
                if (File.Exists(solutionConfig))
                {
                    FileInfo fileInfo = new FileInfo(solutionConfig);
                    SolutionEnvironments.SolutionDir = Directory.GetParent(fileInfo.FullName).FullName;
                    SolutionEnvironments.SolutionPath = fileInfo.FullName;
                    SolutionEnvironments.SolutionExt = fileInfo.Extension;
                    SolutionEnvironments.SolutionName = fileInfo.Name;
                    SolutionEnvironments.SolutionFileName = Path.GetFileName(solutionConfig);
                }
                SolutionExplorers.Clear();
                CurrentSolutionExplorer = new  SolutionExplorer               (SolutionEnvironments);
                SolutionExplorers.Add(CurrentSolutionExplorer);
            }
        }
        public DirectoryInfo? SolutionDirectory { get; private set; }

        public bool OpenSolution(string FullPath)
        {
            if (File.Exists(FullPath)&& FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new(FullPath);
                SolutionDirectory = fileInfo.Directory;
                SolutionHistory.InsertFile(FullPath);
                SolutionLoaded?.Invoke(FullPath, new EventArgs());
                return true;
            }
            else
            {
                SolutionHistory.RemoveFile(FullPath);
                return false;
            }
        }

        public bool CreateSolution(string SolutionDirectoryPath)
        {
            if (!Directory.Exists(SolutionDirectoryPath))
                Directory.CreateDirectory(SolutionDirectoryPath);

            DirectoryInfo directoryInfo = new DirectoryInfo(SolutionDirectoryPath);
            string slnName = directoryInfo.FullName + "\\" + directoryInfo.Name + ".cvsln";

            new CVSolutionConfig().ToJsonNFile(slnName);

            SolutionCreated?.Invoke(slnName, new EventArgs());
            OpenSolution(slnName);
            return true;
        }



        public void CreateShortcut(string FileName)
        {
            if (SolutionDirectory!=null)
                Common.NativeMethods.ShortcutCreator.CreateShortcut(Path.GetFileName(FileName),SolutionDirectory.FullName +"\\Image", FileName);
        }

        public void OpenSolutionWindow()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.ShowDialog();
        }

        public void NewCreateWindow()
        {
            NewCreateWindow newCreatWindow = new() { Owner = WindowHelpers.GetActiveWindow() , WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    CreateSolution(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }

    }
}
