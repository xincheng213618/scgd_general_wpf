#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Solution.RecentFile;
using ColorVision.Solution.Workspace;
using ColorVision.Solution.Explorer;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using log4net;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.Solution
{
    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));
        internal const string FolderWorkspaceFileName = ".ColorVision.cvsln";

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
        public SolutionExplorer CurrentSolutionExplorer { get => _CurrentSolutionExplorer; set { _CurrentSolutionExplorer = value; OnPropertyChanged(); } }
        private SolutionExplorer _CurrentSolutionExplorer;

        public RelayCommand SettingCommand { get; set; } 

        public SolutionManager()
        {
            SolutionHistory.RecentFilesChanged +=(s,e) => MenuManager.GetInstance().RefreshMenuItemsByGuid(nameof(MenuRecentFile));

            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            bool su = false;
            var solutionpath = ArgumentParser.GetInstance().GetValue("solutionpath");
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
                if (!Directory.Exists(DefaultSolution))
                    Directory.CreateDirectory(DefaultSolution);
                var SolutionPath = CreateSolution(DefaultSolution);
            }
            });

            SettingCommand = SolutionSetting.Instance.EditCommand;

            WorkspaceManager.ContentIdSelected += (s, e) => CurrentSolutionExplorer?.SetSelected(e);
        }

        public SolutionEnvironments SolutionEnvironments { get; set; } = new SolutionEnvironments();

        internal static bool IsSolutionFilePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizeRecentPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (IsFolderWorkspaceFile(path))
            {
                return Path.GetDirectoryName(path) ?? path;
            }

            return path;
        }

        internal static bool IsSupportedOpenPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = NormalizeRecentPath(path);
            return Directory.Exists(normalizedPath) || (File.Exists(normalizedPath) && IsSolutionFilePath(normalizedPath));
        }

        private static bool IsFolderWorkspaceFile(string? path)
        {
            return IsSolutionFilePath(path)
                && string.Equals(Path.GetFileName(path), FolderWorkspaceFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDirectorySolutionPath(DirectoryInfo directoryInfo)
        {
            string folderWorkspacePath = Path.Combine(directoryInfo.FullName, FolderWorkspaceFileName);
            if (File.Exists(folderWorkspacePath))
            {
                return folderWorkspacePath;
            }

            string conventionalSolutionPath = Path.Combine(directoryInfo.FullName, directoryInfo.Name + ".cvsln");
            if (File.Exists(conventionalSolutionPath))
            {
                return conventionalSolutionPath;
            }

            string[] existingSolutions = Directory.GetFiles(directoryInfo.FullName, "*.cvsln", SearchOption.TopDirectoryOnly);
            if (existingSolutions.Length == 1)
            {
                return existingSolutions[0];
            }

            EnsureFolderWorkspaceFile(folderWorkspacePath);
            return folderWorkspacePath;
        }

        private static void EnsureFolderWorkspaceFile(string solutionPath)
        {
            if (File.Exists(solutionPath))
            {
                return;
            }

            new SolutionConfig().ToJsonNFile(solutionPath);

            try
            {
                File.SetAttributes(solutionPath, File.GetAttributes(solutionPath) | FileAttributes.Hidden);
            }
            catch (Exception ex)
            {
                log.Debug($"无法设置工作区配置文件为隐藏: {solutionPath}, {ex.Message}");
            }
        }

        private static bool TryResolveOpenTarget(string path, out string solutionPath, out string historyPath, out string displayName)
        {
            solutionPath = string.Empty;
            historyPath = string.Empty;
            displayName = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string normalizedPath = NormalizeRecentPath(path);

            if (Directory.Exists(normalizedPath))
            {
                DirectoryInfo directoryInfo = new(normalizedPath);
                solutionPath = ResolveDirectorySolutionPath(directoryInfo);
                historyPath = directoryInfo.FullName;
                displayName = directoryInfo.Name;
                return File.Exists(solutionPath);
            }

            if (File.Exists(normalizedPath) && IsSolutionFilePath(normalizedPath))
            {
                FileInfo fileInfo = new(normalizedPath);
                solutionPath = fileInfo.FullName;
                historyPath = fileInfo.FullName;
                displayName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                return true;
            }

            return false;
        }

        public static bool OpenFolderDialog()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = ColorVision.UI.Properties.Resources.OpenFolder,
                Multiselect = false,
            };

            Window? owner = WindowHelpers.GetActiveWindow();
            bool? result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return result == true && GetInstance().OpenSolution(dialog.FolderName);
        }

        public bool OpenSolution(string FullPath)
        {
            if (TryResolveOpenTarget(FullPath, out string solutionPath, out string historyPath, out string displayName))
            {
                FileInfo fileInfo = new(solutionPath);
                SolutionHistory.InsertFile(historyPath);
                if (!string.Equals(FullPath, historyPath, StringComparison.OrdinalIgnoreCase))
                {
                    SolutionHistory.RemoveFile(FullPath);
                }

                SolutionLoaded?.Invoke(historyPath, new EventArgs());
                if (fileInfo.Exists)
                {
                    SolutionEnvironments.SolutionDir = Directory.GetParent(fileInfo.FullName).FullName;
                    SolutionEnvironments.SolutionPath = fileInfo.FullName;
                    SolutionEnvironments.SolutionExt = fileInfo.Extension;
                    SolutionEnvironments.SolutionName = fileInfo.Name;
                    SolutionEnvironments.SolutionFileName = displayName;
                }
                DisposeSolutionExplorers();
                CurrentSolutionExplorer = new SolutionExplorer(SolutionEnvironments);
                SolutionExplorers.Add(CurrentSolutionExplorer);
                return true;
            }
            else
            {
                string normalizedPath = NormalizeRecentPath(FullPath);
                if (!string.IsNullOrWhiteSpace(normalizedPath))
                {
                    SolutionHistory.RemoveFile(normalizedPath);
                }
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

            new SolutionConfig().ToJsonNFile(slnName);

            SolutionCreated?.Invoke(slnName, new EventArgs());
            OpenSolution(slnName);
            return true;
        }

        private void DisposeSolutionExplorers()
        {
            foreach (var explorer in SolutionExplorers.ToList())
            {
                try
                {
                    explorer.Dispose();
                }
                catch (Exception ex)
                {
                    log.Warn($"释放旧工程资源失败: {ex.Message}", ex);
                }
            }
            SolutionExplorers.Clear();
        }
        public static void OpenSolutionWindow()
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
