using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Extension;
using ColorVision.RecentFile;
using ColorVision.Solution.V;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using YamlDotNet.Core;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;

namespace ColorVision.Solution
{

    public interface IFileControl
    {
        string Name { get; set; }
        public string GuidId { get; }

        Control UserControl { get; }

        ImageSource IconSource { get; }

        void Open();
        void Close();
    }

    public class SolutionManagerInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public SolutionManagerInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 1;

        public async Task InitializeAsync()
        {
            _messageUpdater.UpdateMessage("正在加载工程目录");
            await Task.Delay(30);
            Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() => SolutionManager.GetInstance());
            });
            await Task.Delay(30);
        }
    }


    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));

        private static SolutionManager _instance;
        private static readonly object _locker = new();
        public static SolutionManager GetInstance() { lock (_locker) { _instance ??= new SolutionManager(); return _instance; } }

        //工程配置文件
        public SolutionConfig CurrentSolution { get; set; }
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

        public RelayCommand SolutionOpenCommand { get; set; }
        public RelayCommand SolutionCreateCommand { get; set; }

        public SolutionManager()
        {
            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            CurrentSolution = new SolutionConfig();

            SolutionLoaded += SolutionManager_SolutionLoaded;

            bool su = false;
            var parser = ArgumentParser.GetInstance();

            parser.AddArgument("solutionpath", false, "s");
            parser.Parse();
            var solutionpath = parser.GetValue("solutionpath");
            if (solutionpath != null)
            {
                su = OpenSolution(solutionpath);
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
                var SolutionPath = CreateSolution(DefaultSolution);
            }


            SolutionOpenCommand = new RelayCommand((a) => OpenSolutionWindow());
            SolutionCreateCommand = new RelayCommand((a) => NewCreateWindow());
        }


        public event EventHandler<IFileControl> OpenFile;


        public void OpenFileWindow(IFileControl userControl)
        {
            OpenFile?.Invoke(this, userControl);
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
                FileInfo fileInfo = new(FullPath);
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

        public bool CreateSolution(string SolutionDirectoryPath)
        {
            if (!Directory.Exists(SolutionDirectoryPath))
                Directory.CreateDirectory(SolutionDirectoryPath);

            DirectoryInfo directoryInfo = new DirectoryInfo(SolutionDirectoryPath);
            string slnName = directoryInfo.FullName + "\\" + directoryInfo.Name + ".cvsln";
            CurrentSolution.ToJsonNFile(slnName);

            SolutionCreated?.Invoke(slnName, new EventArgs());
            OpenSolution(slnName);
            return true;
        }



        public void CreateShortcut(string FileName)
        {
            if (SolutionDirectory!=null)
                Common.NativeMethods.ShortcutCreator.CreateShortcut(Path.GetFileName(FileName),SolutionDirectory.FullName +"\\Image", FileName);
        }

        public static void OpenSolutionWindow()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Show();
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

        public void ClearCache()
        {
            Directory.Delete(CurrentSolution.FullPath+ "\\Flow",true);
            Directory.Delete(CurrentSolution.FullPath+ "\\.Cache", true);
            Tool.CreateDirectoryMax(CurrentSolution.FullPath+ "\\Cache");
            Tool.CreateDirectoryMax(CurrentSolution.FullPath+ "\\Flow");
        }
    }
}
