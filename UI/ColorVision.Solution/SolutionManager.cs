#pragma warning disable CS8602
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Mru;
using ColorVision.Solution.Workspace;
using ColorVision.Solution.Explorer;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.UI.Shell;
using log4net;
using Newtonsoft.Json;
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

    public sealed class SolutionWorkspaceEventArgs : EventArgs
    {
        public string WorkspacePath { get; }

        public SolutionWorkspaceEventArgs(string workspacePath)
        {
            WorkspacePath = workspacePath ?? string.Empty;
        }
    }

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
        private readonly Func<SolutionExplorer, bool> _tryCloseWorkspaceDocuments;
        private CancellationTokenSource? _workspaceOpenCancellation;
        private int _workspaceOpenVersion;
        private JumpListManager? _jumpListManager;
        internal Task InitialWorkspaceOpenTask { get; private set; } = Task.CompletedTask;

        public static SolutionSetting Setting => SolutionSetting.Instance;

        public MruPathService RecentWorkspaces { get; } = MruPathService.CreateLocal("recent-workspaces.json");

        /// <summary>
        /// 工程初始化的时候
        /// </summary>
        public event EventHandler SolutionCreated;
        /// <summary>
        /// 工程打开的时候
        /// </summary>
        public event EventHandler SolutionLoaded;
        public event EventHandler? CurrentWorkspaceChanged;
        /// <summary>
        /// 当前工作区关闭的时候；事件参数包含用户打开的文件夹、项目或解决方案路径。
        /// </summary>
        public event EventHandler<SolutionWorkspaceEventArgs>? SolutionClosed;

        public SolutionExplorer? CurrentSolutionExplorer
        {
            get => _CurrentSolutionExplorer;
            private set
            {
                if (ReferenceEquals(_CurrentSolutionExplorer, value))
                    return;

                _CurrentSolutionExplorer = value;
                if (value == null)
                    CurrentWorkspacePath = string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanCloseSolution));
                MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Configuration);
                MenuManager.GetInstance().RefreshMenuItemsByGuid(SolutionMenuIds.Platform);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                CurrentWorkspaceChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        private SolutionExplorer? _CurrentSolutionExplorer;

        public string CurrentWorkspacePath
        {
            get => _currentWorkspacePath;
            private set
            {
                if (string.Equals(_currentWorkspacePath, value, StringComparison.OrdinalIgnoreCase))
                    return;
                _currentWorkspacePath = value;
                OnPropertyChanged();
            }
        }
        private string _currentWorkspacePath = string.Empty;
        public bool CanCloseSolution => CurrentSolutionExplorer != null || IsOpeningWorkspace;

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
                OnPropertyChanged(nameof(CanCloseSolution));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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

        public SolutionManager() : this(restoreLastWorkspace: true, tryCloseWorkspaceDocuments: null)
        {
        }

        internal SolutionManager(
            bool restoreLastWorkspace,
            Func<SolutionExplorer, bool>? tryCloseWorkspaceDocuments = null)
        {
            _tryCloseWorkspaceDocuments = tryCloseWorkspaceDocuments
                ?? TryCloseWorkspaceDocumentsCore;
            ColorVision.UI.FileProcessorFactory.GetInstance().ResourceOpenHandlerAsync ??=
                Editor.ResourceOpenService.Instance.RouteFileProcessorOpenAsync;
            RecentWorkspaces.Changed += RecentWorkspaces_Changed;

            if (restoreLastWorkspace && Application.Current != null)
            {
                string? solutionPath = ArgumentParser.GetInstance().GetValue("solutionpath");
                InitialWorkspaceOpenTask = Application.Current.Dispatcher
                    .InvokeAsync(() => RestoreInitialWorkspaceAsync(solutionPath))
                    .Task
                    .Unwrap();
            }

            SettingCommand = restoreLastWorkspace
                ? SolutionSetting.Instance.EditCommand
                : new RelayCommand(_ => { });

            if (restoreLastWorkspace)
                WorkspaceManager.ContentIdSelected += (s, e) => CurrentSolutionExplorer?.SetSelected(e);
        }

        private async Task RestoreInitialWorkspaceAsync(string? solutionPath)
        {
            MruPathEntry? mostRecentWorkspace = RecentWorkspaces.Items
                .MaxBy(entry => entry.LastUsedUtc);
            string? restorePath = solutionPath
                ?? mostRecentWorkspace?.Path;
            bool succeeded = false;
            if (!string.IsNullOrWhiteSpace(restorePath))
            {
                SolutionOpenOperationResult result = await OpenSolutionAsync(restorePath);
                succeeded = result.Succeeded;
                if (result.Canceled)
                    return;
            }

            _jumpListManager = new JumpListManager();
            _jumpListManager.SetRecentWorkspaces(RecentWorkspaces.Items.Select(entry => entry.Path));
            if (succeeded || CurrentSolutionExplorer != null || IsOpeningWorkspace)
                return;

            string defaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ColorVision");
            Directory.CreateDirectory(defaultRoot);
            string defaultSolution = Path.Combine(defaultRoot, "Default");
            Directory.CreateDirectory(defaultSolution);
            await CreateSolutionAsync(defaultSolution);
        }

        private void RecentWorkspaces_Changed(object? sender, EventArgs e)
        {
            MenuManager.GetInstance().RefreshMenuItemsByGuid(nameof(MenuRecentWorkspace));
            _jumpListManager?.SetRecentWorkspaces(RecentWorkspaces.Items.Select(entry => entry.Path));
        }

        public SolutionEnvironments SolutionEnvironments { get; set; } = new SolutionEnvironments();

        internal static bool IsSolutionFilePath(string? path)
        {
            return IsNativeSolutionFilePath(path);
        }

        internal static bool IsNativeSolutionFilePath(string? path)
        {
            return !string.IsNullOrWhiteSpace(path)
                && path.EndsWith(".cvsln", StringComparison.OrdinalIgnoreCase);
        }

        internal static string GetSolutionFileDialogPattern()
        {
            return string.Join(';', NativeSolutionFilePatterns);
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

        internal static string NormalizeWorkspacePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (PrivateWorkspaceService.TryResolveSourcePath(path, out string sourcePath))
            {
                return sourcePath;
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

            string normalizedPath = NormalizeWorkspacePath(path);
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
                solutionPath = PrivateWorkspaceService.CreateWorkspacePath(
                    PrivateWorkspaceKind.Folder,
                    normalizedRoot);
                bool workspaceExists = File.Exists(solutionPath);
                SolutionConfig config = workspaceExists
                    ? SolutionConfigStore.Load(solutionPath).Config
                    : LoadLegacyFolderWorkspace(directoryInfo);
                bool rootChanged = !string.Equals(
                    config.RootPath,
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase);
                config.RootPath = normalizedRoot;
                bool sourceChanged = PrivateWorkspaceService.SetSource(
                    config,
                    PrivateWorkspaceKind.Folder,
                    normalizedRoot);
                if (!workspaceExists || rootChanged || sourceChanged)
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
            string normalizedPath = NormalizeWorkspacePath(path);
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
                solutionPath = PrivateWorkspaceService.CreateWorkspacePath(
                    PrivateWorkspaceKind.Project,
                    project.ProjectFile.FullName);
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
                PrivateWorkspaceService.SetSource(
                    config,
                    PrivateWorkspaceKind.Project,
                    project.ProjectFile.FullName);
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

        internal async Task<SolutionOpenOperationResult> OpenSolutionAsync(
            string fullPath,
            CancellationToken cancellationToken = default)
        {
            if (IsCurrentWorkspacePath(fullPath))
            {
                CancelWorkspaceOpen();
                return new SolutionOpenOperationResult(true);
            }
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

                if (!TryCloseCurrentWorkspaceDocuments())
                {
                    candidateExplorer.Dispose();
                    return new SolutionOpenOperationResult(false, "切换工作区已取消。", Canceled: true);
                }
                if (requestCancellation.IsCancellationRequested
                    || !IsCurrentWorkspaceOpenRequest(requestCancellation, requestVersion))
                {
                    candidateExplorer.Dispose();
                    return new SolutionOpenOperationResult(false, "打开工作区已取消。", Canceled: true);
                }

                DisposeCurrentSolutionExplorer();
                SolutionEnvironments = candidateEnvironment;
                CurrentWorkspacePath = resolution.HistoryPath;
                CurrentSolutionExplorer = candidateExplorer;
                SolutionEnvironments.SolutionDir = candidateExplorer.DirectoryInfo.FullName;
                RecentWorkspaces.Touch(resolution.HistoryPath, fullPath);
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

        public bool TryCloseSolution()
        {
            CancelWorkspaceOpen();
            SolutionExplorer? explorer = CurrentSolutionExplorer;
            if (explorer == null)
                return true;
            if (!_tryCloseWorkspaceDocuments(explorer))
                return false;

            string workspacePath = CurrentWorkspacePath;
            DisposeCurrentSolutionExplorer();
            CurrentSolutionExplorer = null;
            SolutionEnvironments = new SolutionEnvironments();
            SolutionClosed?.Invoke(this, new SolutionWorkspaceEventArgs(workspacePath));
            return true;
        }

        private bool TryCloseCurrentWorkspaceDocuments()
        {
            return CurrentSolutionExplorer is not { } explorer
                || _tryCloseWorkspaceDocuments(explorer);
        }

        private static bool TryCloseWorkspaceDocumentsCore(SolutionExplorer explorer)
        {
            return EditorDocumentService.TryCloseDocumentsForResources(
                explorer.GetDocumentResourceRoots());
        }

        private bool IsCurrentWorkspacePath(string? path)
        {
            if (CurrentSolutionExplorer == null
                || string.IsNullOrWhiteSpace(CurrentWorkspacePath)
                || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string requestedPath = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(NormalizeWorkspacePath(path)));
                string currentPath = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(CurrentWorkspacePath));
                return string.Equals(requestedPath, currentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        public async Task<bool> CreateSolutionAsync(
            string solutionDirectoryPath,
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(solutionDirectoryPath))
                Directory.CreateDirectory(solutionDirectoryPath);

            DirectoryInfo directoryInfo = new(solutionDirectoryPath);
            string solutionPath = Path.Combine(directoryInfo.FullName, directoryInfo.Name + ".cvsln");

            SolutionConfigStore.Save(solutionPath, new SolutionConfig { ProjectMode = SolutionProjectMode.Explicit });

            SolutionCreated?.Invoke(solutionPath, EventArgs.Empty);
            SolutionOpenOperationResult result = await OpenSolutionAsync(solutionPath, cancellationToken);
            return result.Succeeded;
        }

        private void DisposeCurrentSolutionExplorer()
        {
            if (CurrentSolutionExplorer is not { } explorer)
                return;

            try
            {
                explorer.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn($"释放旧工程资源失败: {ex.Message}", ex);
            }
        }
        public static void OpenSolutionWindow()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.ShowDialog();
        }

        public void NewCreateWindow()
        {
            NewCreateWindow newCreatWindow = new() { Owner = WindowHelpers.GetActiveWindow() , WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += async (_, _) =>
            {
                if (newCreatWindow.IsCreate)
                {
                    string solutionDirectoryPath = Path.Combine(
                        newCreatWindow.NewCreateViewMode.DirectoryPath,
                        newCreatWindow.NewCreateViewMode.Name);
                    await CreateSolutionAsync(solutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }

    }
}
