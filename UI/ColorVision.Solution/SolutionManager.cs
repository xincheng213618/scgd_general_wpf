#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Solution.RecentFile;
using ColorVision.Solution.Workspace;
using ColorVision.Solution.Explorer;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using log4net;
using Newtonsoft.Json;
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
        public SolutionExplorer CurrentSolutionExplorer
        {
            get => _CurrentSolutionExplorer;
            set
            {
                if (ReferenceEquals(_CurrentSolutionExplorer, value))
                    return;

                _CurrentSolutionExplorer = value;
                OnPropertyChanged();
                MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Configuration);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }
        private SolutionExplorer _CurrentSolutionExplorer;

        public RelayCommand SettingCommand { get; set; } 

        public SolutionManager()
        {
            ColorVision.UI.FileProcessorFactory.GetInstance().WorkspaceOpenHandler ??= Editor.ResourceOpenService.TryOpenFile;
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

        internal static bool IsProjectFilePath(string? path)
        {
            return ProjectProviderRegistry.IsSupportedProjectFilePath(path);
        }

        internal static bool TryGetProjectDirectory(string? projectPath, out string directoryPath)
        {
            directoryPath = string.Empty;
            if (!IsProjectFilePath(projectPath) || !File.Exists(projectPath))
                return false;

            string? parentDirectory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
            if (string.IsNullOrWhiteSpace(parentDirectory) || !Directory.Exists(parentDirectory))
                return false;

            directoryPath = parentDirectory;
            return true;
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
            return Directory.Exists(normalizedPath)
                || (File.Exists(normalizedPath) && (IsSolutionFilePath(normalizedPath) || IsProjectFilePath(normalizedPath)));
        }

        private static bool IsFolderWorkspaceFile(string? path)
        {
            return IsSolutionFilePath(path)
                && string.Equals(Path.GetFileName(path), FolderWorkspaceFileName, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDirectorySolutionPath(DirectoryInfo directoryInfo)
        {
            return TryCreateFolderWorkspace(directoryInfo, out string workspacePath)
                ? workspacePath
                : string.Empty;
        }

        internal static bool TryCreateFolderWorkspace(DirectoryInfo directoryInfo, out string solutionPath)
        {
            ArgumentNullException.ThrowIfNull(directoryInfo);
            solutionPath = string.Empty;
            directoryInfo.Refresh();
            if (!directoryInfo.Exists)
                return false;

            try
            {
                string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryInfo.FullName));
                string workspaceDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ColorVision",
                    "FolderWorkspaces");
                Directory.CreateDirectory(workspaceDirectory);

                string workspaceKey = Tool.GetMD5(normalizedRoot.ToUpperInvariant());
                solutionPath = Path.Combine(workspaceDirectory, $"{workspaceKey}.cvsln");
                bool workspaceExists = File.Exists(solutionPath);
                SolutionConfig config = workspaceExists
                    ? SolutionConfigStore.Load(solutionPath).Config
                    : LoadLegacyFolderWorkspace(directoryInfo);
                bool rootChanged = !string.Equals(
                    config.RootPath,
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase);
                config.RootPath = normalizedRoot;
                if (!workspaceExists || rootChanged)
                    SolutionConfigStore.Save(solutionPath, config);
                return true;
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                log.Warn($"创建文件夹工作区失败: {directoryInfo.FullName}, {ex.Message}");
                solutionPath = string.Empty;
                return false;
            }
        }

        private static SolutionConfig LoadLegacyFolderWorkspace(DirectoryInfo directoryInfo)
        {
            string legacyWorkspacePath = Path.Combine(directoryInfo.FullName, FolderWorkspaceFileName);
            if (!File.Exists(legacyWorkspacePath))
                return new SolutionConfig();

            try
            {
                return SolutionConfigStore.DeserializeAndMigrate(
                    File.ReadAllText(legacyWorkspacePath),
                    out _);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                log.Warn($"读取旧文件夹工作区失败，将创建新工作区: {legacyWorkspacePath}, {ex.Message}");
                return new SolutionConfig();
            }
        }

        public bool OpenFolder(string folderPath)
        {
            return Directory.Exists(folderPath) && OpenSolution(folderPath);
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

            if (File.Exists(normalizedPath)
                && IsProjectFilePath(normalizedPath)
                && TryCreateImplicitProjectSolution(new FileInfo(normalizedPath), out solutionPath, out displayName))
            {
                historyPath = Path.GetFullPath(normalizedPath);
                return true;
            }

            return false;
        }

        internal static bool TryCreateImplicitProjectSolution(
            FileInfo projectFile,
            out string solutionPath,
            out string displayName)
        {
            solutionPath = string.Empty;
            displayName = string.Empty;
            if (!ProjectProviderRegistry.TryLoadProject(projectFile, out ProjectDefinition? project) || project == null)
                return false;

            try
            {
                string implicitSolutionDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ColorVision",
                    "ImplicitSolutions");
                Directory.CreateDirectory(implicitSolutionDirectory);
                string projectKey = Tool.GetMD5(project.ProjectFile.FullName.ToUpperInvariant());
                solutionPath = Path.Combine(implicitSolutionDirectory, $"{projectKey}.cvsln");
                string projectReference = Path.GetRelativePath(project.ProjectDirectory.FullName, project.ProjectFile.FullName);

                SolutionConfig config;
                if (File.Exists(solutionPath))
                {
                    config = SolutionConfigStore.Load(solutionPath).Config;
                }
                else
                {
                    config = new SolutionConfig();
                }

                config.RootPath = project.ProjectDirectory.FullName;
                config.ProjectMode = SolutionProjectMode.Explicit;
                if (!config.Projects.Any(reference => string.Equals(reference, projectReference, StringComparison.OrdinalIgnoreCase)))
                    config.Projects.Insert(0, projectReference);
                config.StartupProject = projectReference;
                SolutionConfigStore.Save(solutionPath, config);

                displayName = string.IsNullOrWhiteSpace(project.Name)
                    ? Path.GetFileNameWithoutExtension(project.ProjectFile.Name)
                    : project.Name;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                log.Warn($"创建隐式解决方案失败: {projectFile.FullName}, {ex.Message}");
                solutionPath = string.Empty;
                displayName = string.Empty;
                return false;
            }
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
            return result == true && GetInstance().OpenFolder(dialog.FolderName);
        }

        public bool OpenSolution(string FullPath)
        {
            if (!TryResolveOpenTarget(FullPath, out string solutionPath, out string historyPath, out string displayName))
            {
                string normalizedPath = NormalizeRecentPath(FullPath);
                if (!string.IsNullOrWhiteSpace(normalizedPath))
                {
                    SolutionHistory.RemoveFile(normalizedPath);
                }
                SolutionHistory.RemoveFile(FullPath);
                return false;
            }

            FileInfo fileInfo = new(solutionPath);
            var candidateEnvironment = new SolutionEnvironments
            {
                SolutionDir = Directory.GetParent(fileInfo.FullName)?.FullName ?? string.Empty,
                SolutionPath = fileInfo.FullName,
                SolutionExt = fileInfo.Extension,
                SolutionName = fileInfo.Name,
                SolutionFileName = displayName,
            };
            SolutionExplorer candidateExplorer;
            try
            {
                candidateExplorer = new SolutionExplorer(candidateEnvironment);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                log.Warn($"打开解决方案失败: {solutionPath}", ex);
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"无法打开解决方案。\n\n{ex.Message}",
                    "无法打开解决方案",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            DisposeSolutionExplorers();
            SolutionEnvironments = candidateEnvironment;
            CurrentSolutionExplorer = candidateExplorer;
            SolutionEnvironments.SolutionDir = candidateExplorer.DirectoryInfo.FullName;
            SolutionExplorers.Add(candidateExplorer);
            SolutionHistory.InsertFile(historyPath);
            if (!string.Equals(FullPath, historyPath, StringComparison.OrdinalIgnoreCase))
                SolutionHistory.RemoveFile(FullPath);
            SolutionLoaded?.Invoke(historyPath, EventArgs.Empty);
            return true;
        }

        public bool OpenProject(string projectPath)
        {
            var projectFile = new FileInfo(projectPath);
            if (!ProjectProviderRegistry.TryLoadProject(
                projectFile,
                out _,
                out string errorMessage))
            {
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    errorMessage,
                    "无法打开项目",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            return OpenSolution(projectPath);
        }

        public bool CreateSolution(string SolutionDirectoryPath)
        {
            if (!Directory.Exists(SolutionDirectoryPath))
                Directory.CreateDirectory(SolutionDirectoryPath);

            DirectoryInfo directoryInfo = new DirectoryInfo(SolutionDirectoryPath);
            string slnName = directoryInfo.FullName + "\\" + directoryInfo.Name + ".cvsln";

            SolutionConfigStore.Save(slnName, new SolutionConfig { ProjectMode = SolutionProjectMode.Explicit });

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
