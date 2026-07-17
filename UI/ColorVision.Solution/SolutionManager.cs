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
    internal sealed record SolutionOpenOperationResult(
        bool Succeeded,
        string ErrorMessage = "",
        bool Canceled = false);

    internal sealed record SolutionOpenTargetResolution(
        bool Succeeded,
        string SolutionPath = "",
        string HistoryPath = "",
        string DisplayName = "",
        string ErrorMessage = "");

    /// <summary>
    /// 工程模块控制中心
    /// </summary>
    public class SolutionManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionManager));
        internal const string FolderWorkspaceFileName = ".ColorVision.cvsln";
        private static readonly string[] NativeSolutionFilePatterns = ["*.cvsln"];

        private static SolutionManager _instance;
        private static readonly object _locker = new();
        public static SolutionManager GetInstance() { lock (_locker) { _instance ??= new SolutionManager(); return _instance; } }
        private readonly object _workspaceOpenSync = new();
        private CancellationTokenSource? _workspaceOpenCancellation;
        private int _workspaceOpenVersion;

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

        public bool IsOpeningWorkspace
        {
            get => _isOpeningWorkspace;
            private set
            {
                if (_isOpeningWorkspace == value)
                    return;
                _isOpeningWorkspace = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WorkspaceOpenStatus));
            }
        }
        private bool _isOpeningWorkspace;

        public string OpeningWorkspacePath
        {
            get => _openingWorkspacePath;
            private set
            {
                if (string.Equals(_openingWorkspacePath, value, StringComparison.Ordinal))
                    return;
                _openingWorkspacePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WorkspaceOpenStatus));
            }
        }
        private string _openingWorkspacePath = string.Empty;
        public string WorkspaceOpenStatus => IsOpeningWorkspace
            ? $"正在打开：{Path.GetFileName(OpeningWorkspacePath)}"
            : "就绪";

        public RelayCommand SettingCommand { get; set; } 

        public SolutionManager() : this(restoreLastWorkspace: true)
        {
        }

        internal SolutionManager(bool restoreLastWorkspace)
        {
            ColorVision.UI.FileProcessorFactory.GetInstance().WorkspaceOpenHandler ??= Editor.ResourceOpenService.TryOpenFile;
            SolutionHistory.RecentFilesChanged +=(s,e) => MenuManager.GetInstance().RefreshMenuItemsByGuid(nameof(MenuRecentFile));

            SolutionExplorers = new ObservableCollection<SolutionExplorer>();

            if (restoreLastWorkspace && Application.Current != null)
            {
                string? solutionPath = ArgumentParser.GetInstance().GetValue("solutionpath");
                Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    string? restorePath = solutionPath
                        ?? SolutionHistory.RecentFiles.FirstOrDefault();
                    bool succeeded = false;
                    if (!string.IsNullOrWhiteSpace(restorePath))
                    {
                        SolutionOpenOperationResult result = await OpenSolutionAsync(restorePath);
                        succeeded = result.Succeeded;
                        if (result.Canceled)
                            return;
                    }
                    JumpListManager jumpListManager = new JumpListManager();
                    jumpListManager.AddRecentFiles(SolutionHistory.RecentFiles);
                    if (!succeeded
                        && CurrentSolutionExplorer == null
                        && !IsOpeningWorkspace)
                    {
                        string defaultRoot = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "ColorVision");
                        Directory.CreateDirectory(defaultRoot);
                        string defaultSolution = Path.Combine(defaultRoot, "Default");
                        Directory.CreateDirectory(defaultSolution);
                        CreateSolution(defaultSolution);
                    }
                });
            }

            SettingCommand = restoreLastWorkspace
                ? SolutionSetting.Instance.EditCommand
                : new RelayCommand(_ => { });

            if (restoreLastWorkspace)
                WorkspaceManager.ContentIdSelected += (s, e) => CurrentSolutionExplorer?.SetSelected(e);
        }

        public SolutionEnvironments SolutionEnvironments { get; set; } = new SolutionEnvironments();

        internal static bool IsSolutionFilePath(string? path)
        {
            return IsNativeSolutionFilePath(path)
                || SolutionFileProviderRegistry.IsSupportedSolutionFilePath(path);
        }

        internal static bool IsNativeSolutionFilePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetSolutionFileDialogPattern()
        {
            return string.Join(';', NativeSolutionFilePatterns
                .Concat(SolutionFileProviderRegistry.GetSolutionFilePatterns())
                .Distinct(StringComparer.OrdinalIgnoreCase));
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
            return IsNativeSolutionFilePath(path)
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
            return TryOpenFolder(folderPath, out _);
        }

        public bool TryOpenFolder(string folderPath, out string errorMessage)
        {
            if (!Directory.Exists(folderPath))
            {
                errorMessage = $"文件夹不存在：{folderPath}";
                return false;
            }
            return TryOpenSolution(folderPath, out errorMessage);
        }

        internal static bool TryResolveOpenTarget(
            string path,
            out string solutionPath,
            out string historyPath,
            out string displayName,
            out string errorMessage)
        {
            solutionPath = string.Empty;
            historyPath = string.Empty;
            displayName = string.Empty;
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                errorMessage = "要打开的资源路径为空。";
                return false;
            }

            string normalizedPath = NormalizeRecentPath(path);

            if (Directory.Exists(normalizedPath))
            {
                DirectoryInfo directoryInfo = new(normalizedPath);
                solutionPath = ResolveDirectorySolutionPath(directoryInfo);
                historyPath = directoryInfo.FullName;
                displayName = directoryInfo.Name;
                if (File.Exists(solutionPath))
                    return true;
                errorMessage = $"无法为文件夹创建工作区：{directoryInfo.FullName}";
                return false;
            }

            if (File.Exists(normalizedPath) && IsNativeSolutionFilePath(normalizedPath))
            {
                FileInfo fileInfo = new(normalizedPath);
                solutionPath = fileInfo.FullName;
                historyPath = fileInfo.FullName;
                displayName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                return true;
            }

            if (File.Exists(normalizedPath)
                && SolutionFileProviderRegistry.IsSupportedSolutionFilePath(normalizedPath)
                && TryCreateImportedSolution(new FileInfo(normalizedPath), out solutionPath, out displayName, out errorMessage))
            {
                historyPath = Path.GetFullPath(normalizedPath);
                return true;
            }

            if (File.Exists(normalizedPath)
                && SolutionFileProviderRegistry.IsSupportedSolutionFilePath(normalizedPath))
            {
                return false;
            }

            if (File.Exists(normalizedPath)
                && IsProjectFilePath(normalizedPath)
                && TryCreateImplicitProjectSolution(
                    new FileInfo(normalizedPath),
                    out solutionPath,
                    out displayName,
                    out errorMessage))
            {
                historyPath = Path.GetFullPath(normalizedPath);
                return true;
            }

            if (File.Exists(normalizedPath) && IsProjectFilePath(normalizedPath))
                return false;

            errorMessage = File.Exists(normalizedPath)
                ? $"不支持打开此文件：{normalizedPath}"
                : $"要打开的资源不存在：{normalizedPath}";
            return false;
        }

        internal static async Task<SolutionOpenTargetResolution> ResolveOpenTargetAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new SolutionOpenTargetResolution(
                    false,
                    ErrorMessage: "要打开的资源路径为空。");
            }

            cancellationToken.ThrowIfCancellationRequested();
            string normalizedPath = NormalizeRecentPath(path);
            if (Directory.Exists(normalizedPath))
            {
                DirectoryInfo directoryInfo = new(normalizedPath);
                Task<string> workspaceTask = Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return ResolveDirectorySolutionPath(directoryInfo);
                }, CancellationToken.None);
                string solutionPath = await workspaceTask
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return File.Exists(solutionPath)
                    ? new SolutionOpenTargetResolution(
                        true,
                        solutionPath,
                        directoryInfo.FullName,
                        directoryInfo.Name)
                    : new SolutionOpenTargetResolution(
                        false,
                        ErrorMessage: $"无法为文件夹创建工作区：{directoryInfo.FullName}");
            }

            if (File.Exists(normalizedPath) && IsNativeSolutionFilePath(normalizedPath))
            {
                FileInfo fileInfo = new(normalizedPath);
                return new SolutionOpenTargetResolution(
                    true,
                    fileInfo.FullName,
                    fileInfo.FullName,
                    Path.GetFileNameWithoutExtension(fileInfo.Name));
            }

            if (File.Exists(normalizedPath)
                && SolutionFileProviderRegistry.IsSupportedSolutionFilePath(normalizedPath))
            {
                ImportedSolutionWorkspaceResult result = await ImportedSolutionWorkspaceService
                    .CreateAsync(new FileInfo(normalizedPath), cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return result.Succeeded
                    ? new SolutionOpenTargetResolution(
                        true,
                        result.WorkspacePath,
                        Path.GetFullPath(normalizedPath),
                        result.DisplayName)
                    : new SolutionOpenTargetResolution(
                        false,
                        ErrorMessage: result.ErrorMessage);
            }

            if (File.Exists(normalizedPath) && IsProjectFilePath(normalizedPath))
            {
                Task<SolutionOpenTargetResolution> projectTask = Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bool succeeded = TryCreateImplicitProjectSolution(
                        new FileInfo(normalizedPath),
                        out string solutionPath,
                        out string displayName,
                        out string errorMessage);
                    cancellationToken.ThrowIfCancellationRequested();
                    return succeeded
                        ? new SolutionOpenTargetResolution(
                            true,
                            solutionPath,
                            Path.GetFullPath(normalizedPath),
                            displayName)
                        : new SolutionOpenTargetResolution(
                            false,
                            ErrorMessage: errorMessage);
                }, CancellationToken.None);
                return await projectTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return new SolutionOpenTargetResolution(
                false,
                ErrorMessage: File.Exists(normalizedPath)
                    ? $"不支持打开此文件：{normalizedPath}"
                    : $"要打开的资源不存在：{normalizedPath}");
        }

        internal static bool TryCreateImplicitProjectSolution(
            FileInfo projectFile,
            out string solutionPath,
            out string displayName)
        {
            return TryCreateImplicitProjectSolution(
                projectFile,
                out solutionPath,
                out displayName,
                out _);
        }

        internal static bool TryCreateImplicitProjectSolution(
            FileInfo projectFile,
            out string solutionPath,
            out string displayName,
            out string errorMessage)
        {
            solutionPath = string.Empty;
            displayName = string.Empty;
            errorMessage = string.Empty;
            if (!ProjectProviderRegistry.TryLoadProject(
                projectFile,
                out ProjectDefinition? project,
                out errorMessage)
                || project == null)
            {
                return false;
            }

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
                errorMessage = $"创建项目工作区失败：{ex.Message}";
                solutionPath = string.Empty;
                displayName = string.Empty;
                return false;
            }
        }

        internal static bool TryCreateImportedSolution(
            FileInfo sourceFile,
            out string solutionPath,
            out string displayName)
        {
            return TryCreateImportedSolution(sourceFile, out solutionPath, out displayName, out _);
        }

        internal static bool TryCreateImportedSolution(
            FileInfo sourceFile,
            out string solutionPath,
            out string displayName,
            out string errorMessage)
        {
            if (ImportedSolutionWorkspaceService.TryCreate(
                sourceFile,
                out solutionPath,
                out displayName,
                out errorMessage))
            {
                return true;
            }

            log.Warn($"导入解决方案失败: {sourceFile.FullName}, {errorMessage}");
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
            return result == true
                && Editor.ResourceOpenService.Instance.TryOpenWithFeedback(dialog.FolderName, owner);
        }

        public static async Task<bool> OpenFolderDialogAsync(
            CancellationToken cancellationToken = default)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = ColorVision.UI.Properties.Resources.OpenFolder,
                Multiselect = false,
            };

            Window? owner = WindowHelpers.GetActiveWindow();
            bool? result = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return result == true
                && await Editor.ResourceOpenService.Instance.TryOpenWithFeedbackAsync(
                    dialog.FolderName,
                    owner,
                    cancellationToken);
        }

        public bool OpenSolution(string FullPath)
        {
            return TryOpenSolution(FullPath, out _);
        }

        public bool TryOpenSolution(string fullPath, out string errorMessage)
        {
            CancelWorkspaceOpen();
            if (!TryResolveOpenTarget(
                fullPath,
                out string solutionPath,
                out string historyPath,
                out string displayName,
                out errorMessage))
            {
                string normalizedPath = NormalizeRecentPath(fullPath);
                if (!string.IsNullOrWhiteSpace(normalizedPath)
                    && !IsSupportedOpenPath(normalizedPath))
                {
                    SolutionHistory.RemoveFile(normalizedPath);
                    SolutionHistory.RemoveFile(fullPath);
                }
                log.Warn($"无法解析要打开的解决方案或工作区: {fullPath}, {errorMessage}");
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
                errorMessage = $"无法打开解决方案：{ex.Message}";
                return false;
            }

            DisposeSolutionExplorers();
            SolutionEnvironments = candidateEnvironment;
            CurrentSolutionExplorer = candidateExplorer;
            SolutionEnvironments.SolutionDir = candidateExplorer.DirectoryInfo.FullName;
            SolutionExplorers.Add(candidateExplorer);
            SolutionHistory.InsertFile(historyPath);
            if (!string.Equals(fullPath, historyPath, StringComparison.OrdinalIgnoreCase))
                SolutionHistory.RemoveFile(fullPath);
            SolutionLoaded?.Invoke(historyPath, EventArgs.Empty);
            errorMessage = string.Empty;
            return true;
        }

        internal async Task<SolutionOpenOperationResult> OpenSolutionAsync(
            string fullPath,
            CancellationToken cancellationToken = default)
        {
            CancellationTokenSource requestCancellation = BeginWorkspaceOpen(
                fullPath,
                cancellationToken,
                out int requestVersion);
            try
            {
                SolutionOpenTargetResolution resolution = await ResolveOpenTargetAsync(
                    fullPath,
                    requestCancellation.Token);
                requestCancellation.Token.ThrowIfCancellationRequested();
                if (!resolution.Succeeded)
                {
                    string normalizedPath = NormalizeRecentPath(fullPath);
                    if (!string.IsNullOrWhiteSpace(normalizedPath)
                        && !IsSupportedOpenPath(normalizedPath))
                    {
                        SolutionHistory.RemoveFile(normalizedPath);
                        SolutionHistory.RemoveFile(fullPath);
                    }
                    log.Warn($"无法解析要打开的解决方案或工作区: {fullPath}, {resolution.ErrorMessage}");
                    return new SolutionOpenOperationResult(false, resolution.ErrorMessage);
                }

                FileInfo fileInfo = new(resolution.SolutionPath);
                var candidateEnvironment = new SolutionEnvironments
                {
                    SolutionDir = Directory.GetParent(fileInfo.FullName)?.FullName ?? string.Empty,
                    SolutionPath = fileInfo.FullName,
                    SolutionExt = fileInfo.Extension,
                    SolutionName = fileInfo.Name,
                    SolutionFileName = resolution.DisplayName,
                };
                SolutionExplorer candidateExplorer;
                try
                {
                    SolutionExplorerPreparation preparation = await SolutionExplorer.PrepareAsync(
                        candidateEnvironment,
                        requestCancellation.Token);
                    requestCancellation.Token.ThrowIfCancellationRequested();
                    candidateExplorer = new SolutionExplorer(candidateEnvironment, preparation);
                }
                catch (Exception ex) when (ex is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidDataException
                    or ArgumentException
                    or NotSupportedException)
                {
                    log.Warn($"打开解决方案失败: {resolution.SolutionPath}", ex);
                    return new SolutionOpenOperationResult(false, $"无法打开解决方案：{ex.Message}");
                }

                if (requestCancellation.IsCancellationRequested
                    || !IsCurrentWorkspaceOpenRequest(requestCancellation, requestVersion))
                {
                    candidateExplorer.Dispose();
                    return new SolutionOpenOperationResult(false, "打开工作区已取消。", Canceled: true);
                }

                DisposeSolutionExplorers();
                SolutionEnvironments = candidateEnvironment;
                CurrentSolutionExplorer = candidateExplorer;
                SolutionEnvironments.SolutionDir = candidateExplorer.DirectoryInfo.FullName;
                SolutionExplorers.Add(candidateExplorer);
                SolutionHistory.InsertFile(resolution.HistoryPath);
                if (!string.Equals(fullPath, resolution.HistoryPath, StringComparison.OrdinalIgnoreCase))
                    SolutionHistory.RemoveFile(fullPath);
                SolutionLoaded?.Invoke(resolution.HistoryPath, EventArgs.Empty);
                return new SolutionOpenOperationResult(true);
            }
            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
            {
                return new SolutionOpenOperationResult(false, "打开工作区已取消。", Canceled: true);
            }
            catch (Exception ex) when (ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
            {
                log.Warn($"异步打开解决方案失败: {fullPath}", ex);
                return new SolutionOpenOperationResult(false, $"无法打开解决方案：{ex.Message}");
            }
            finally
            {
                EndWorkspaceOpen(requestCancellation, requestVersion);
            }
        }

        public void CancelWorkspaceOpen()
        {
            CancellationTokenSource? cancellation;
            lock (_workspaceOpenSync)
                cancellation = _workspaceOpenCancellation;
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private CancellationTokenSource BeginWorkspaceOpen(
            string fullPath,
            CancellationToken cancellationToken,
            out int requestVersion)
        {
            CancellationTokenSource requestCancellation = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            CancellationTokenSource? previousCancellation;
            lock (_workspaceOpenSync)
            {
                previousCancellation = _workspaceOpenCancellation;
                _workspaceOpenCancellation = requestCancellation;
                requestVersion = ++_workspaceOpenVersion;
            }
            try
            {
                previousCancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            OpeningWorkspacePath = fullPath;
            IsOpeningWorkspace = true;
            return requestCancellation;
        }

        private bool IsCurrentWorkspaceOpenRequest(
            CancellationTokenSource requestCancellation,
            int requestVersion)
        {
            lock (_workspaceOpenSync)
            {
                return ReferenceEquals(_workspaceOpenCancellation, requestCancellation)
                    && _workspaceOpenVersion == requestVersion;
            }
        }

        private void EndWorkspaceOpen(
            CancellationTokenSource requestCancellation,
            int requestVersion)
        {
            bool wasCurrent;
            lock (_workspaceOpenSync)
            {
                wasCurrent = ReferenceEquals(_workspaceOpenCancellation, requestCancellation)
                    && _workspaceOpenVersion == requestVersion;
                if (wasCurrent)
                    _workspaceOpenCancellation = null;
            }
            requestCancellation.Dispose();
            if (!wasCurrent)
                return;
            IsOpeningWorkspace = false;
            OpeningWorkspacePath = string.Empty;
        }

        public bool OpenProject(string projectPath)
        {
            return TryOpenProject(projectPath, out _);
        }

        public bool TryOpenProject(string projectPath, out string errorMessage)
        {
            if (!File.Exists(projectPath))
            {
                errorMessage = $"项目文件不存在：{projectPath}";
                return false;
            }
            if (!IsProjectFilePath(projectPath))
            {
                errorMessage = $"不支持此项目文件：{projectPath}";
                return false;
            }
            return TryOpenSolution(projectPath, out errorMessage);
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
