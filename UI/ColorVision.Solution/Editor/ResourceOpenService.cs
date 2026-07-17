using System.IO;
using System.Windows;
using ColorVision.UI;

namespace ColorVision.Solution.Editor
{
    public enum ResourceOpenKind
    {
        Missing,
        File,
        Folder,
        Solution,
        Project,
    }

    public sealed record ResourceOpenResult(
        ResourceOpenKind Kind,
        bool Succeeded,
        string ErrorMessage = "",
        bool? DefaultEditorUpdated = null,
        bool Canceled = false);

    public sealed record ResourceOpenFailure(
        string ResourcePath,
        ResourceOpenKind Kind,
        string Message);

    internal sealed record WorkspaceResourceInfo(
        ResourceOpenKind Kind,
        string DisplayName,
        string FullPath,
        DateTime CreationTime)
    {
        public string KindDisplayName => ResourceOpenService.GetResourceKindName(Kind);
    }

    public sealed class ResourceOpenBatchResult
    {
        public int RequestedCount { get; }
        public IReadOnlyList<string> SuccessfulPaths { get; }
        public IReadOnlyList<ResourceOpenFailure> Failures { get; }
        public bool IsComplete => RequestedCount > 0 && SuccessfulPaths.Count == RequestedCount;

        public ResourceOpenBatchResult(
            int requestedCount,
            IReadOnlyList<string> successfulPaths,
            IReadOnlyList<ResourceOpenFailure> failures)
        {
            RequestedCount = requestedCount;
            SuccessfulPaths = successfulPaths;
            Failures = failures;
        }
    }

    /// <summary>
    /// Canonical application-level open router. It separates workspace/project
    /// activation from editor selection so callers do not need extension logic.
    /// </summary>
    public sealed class ResourceOpenService
    {
        public static ResourceOpenService Instance { get; } = new();
        private readonly EditorManager _editorManager = EditorManager.Instance;

        private ResourceOpenService()
        {
        }

        public ResourceOpenResult Open(string? path)
        {
            ResourceOpenKind kind = Classify(path);
            if (kind == ResourceOpenKind.Missing || string.IsNullOrWhiteSpace(path))
                return new ResourceOpenResult(kind, false, "要打开的资源不存在。");

            try
            {
                if (kind == ResourceOpenKind.File)
                {
                    FileOpenRouteResult actionResult = FileProcessorFactory.GetInstance()
                        .TryOpenFileAction(path);
                    if (actionResult.Handled)
                    {
                        return new ResourceOpenResult(
                            kind,
                            actionResult.Succeeded,
                            actionResult.ErrorMessage,
                            Canceled: actionResult.Canceled);
                    }
                }

                bool succeeded;
                string errorMessage = string.Empty;
                switch (kind)
                {
                    case ResourceOpenKind.Folder:
                        succeeded = SolutionManager.GetInstance().TryOpenFolder(path, out errorMessage);
                        break;
                    case ResourceOpenKind.Solution:
                        succeeded = SolutionManager.GetInstance().TryOpenSolution(path, out errorMessage);
                        break;
                    case ResourceOpenKind.Project:
                        succeeded = SolutionManager.GetInstance().TryOpenProject(path, out errorMessage);
                        break;
                    case ResourceOpenKind.File:
                        succeeded = _editorManager.TryOpenFile(path, out errorMessage);
                        break;
                    default:
                        succeeded = false;
                        break;
                }

                if (succeeded)
                    return new ResourceOpenResult(kind, true);
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = $"无法打开{GetResourceKindName(kind)}。";
                return new ResourceOpenResult(kind, false, errorMessage);
            }
            catch (Exception ex)
            {
                return new ResourceOpenResult(kind, false, $"无法打开{GetResourceKindName(kind)}：{ex.Message}");
            }
        }

        public bool TryOpen(string path) => Open(path).Succeeded;

        internal FileOpenRouteResult RouteFileProcessorOpen(string path)
        {
            return ToFileOpenRouteResult(Open(path));
        }

        internal static FileOpenRouteResult ToFileOpenRouteResult(ResourceOpenResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            return new FileOpenRouteResult(
                true,
                result.Succeeded,
                result.ErrorMessage,
                result.Canceled);
        }

        public async Task<ResourceOpenResult> OpenAsync(
            string? path,
            CancellationToken cancellationToken = default)
        {
            ResourceOpenKind kind = Classify(path);
            if (kind == ResourceOpenKind.Missing || string.IsNullOrWhiteSpace(path))
                return new ResourceOpenResult(kind, false, "要打开的资源不存在。");

            try
            {
                if (kind is ResourceOpenKind.Folder or ResourceOpenKind.Solution or ResourceOpenKind.Project)
                {
                    SolutionOpenOperationResult result = await SolutionManager.GetInstance()
                        .OpenSolutionAsync(path, cancellationToken);
                    return new ResourceOpenResult(
                        kind,
                        result.Succeeded,
                        result.ErrorMessage,
                        Canceled: result.Canceled);
                }

                cancellationToken.ThrowIfCancellationRequested();
                FileOpenRouteResult actionResult = FileProcessorFactory.GetInstance()
                    .TryOpenFileAction(path);
                if (actionResult.Handled)
                {
                    return new ResourceOpenResult(
                        kind,
                        actionResult.Succeeded,
                        actionResult.ErrorMessage,
                        Canceled: actionResult.Canceled);
                }
                bool succeeded = _editorManager.TryOpenFile(path, out string errorMessage);
                if (succeeded)
                    return new ResourceOpenResult(kind, true);
                if (string.IsNullOrWhiteSpace(errorMessage))
                    errorMessage = $"无法打开{GetResourceKindName(kind)}。";
                return new ResourceOpenResult(kind, false, errorMessage);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return new ResourceOpenResult(
                    kind,
                    false,
                    "打开资源已取消。",
                    Canceled: true);
            }
            catch (Exception ex)
            {
                return new ResourceOpenResult(kind, false, $"无法打开{GetResourceKindName(kind)}：{ex.Message}");
            }
        }

        public async Task<bool> TryOpenAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            ResourceOpenResult result = await OpenAsync(path, cancellationToken);
            return result.Succeeded;
        }

        public bool TryOpenWithFeedback(string path, Window? owner = null)
        {
            ResourceOpenResult result = Open(path);
            if (result.Succeeded)
                return true;

            Window? actualOwner = owner ?? Application.Current?.GetActiveWindow();
            if (actualOwner == null)
            {
                MessageBox.Show(
                    result.ErrorMessage,
                    "无法打开资源",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    actualOwner,
                    result.ErrorMessage,
                    "无法打开资源",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            return false;
        }

        public async Task<bool> TryOpenWithFeedbackAsync(
            string path,
            Window? owner = null,
            CancellationToken cancellationToken = default)
        {
            ResourceOpenResult result = await OpenAsync(path, cancellationToken);
            if (result.Succeeded)
                return true;
            if (result.Canceled)
                return false;

            Window? actualOwner = owner ?? Application.Current?.GetActiveWindow();
            if (actualOwner == null)
            {
                MessageBox.Show(
                    result.ErrorMessage,
                    "无法打开资源",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show(
                    actualOwner,
                    result.ErrorMessage,
                    "无法打开资源",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            return false;
        }

        public static bool CanOpenTogether(IEnumerable<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            List<string> resources = paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(ResourcePathIdentityComparer.Instance)
                .ToList();
            if (resources.Count == 0)
                return false;
            if (resources.Count == 1)
                return Classify(resources[0]) != ResourceOpenKind.Missing;
            return resources.All(path => Classify(path) == ResourceOpenKind.File);
        }

        public ResourceOpenBatchResult OpenMany(IEnumerable<string> paths)
        {
            ArgumentNullException.ThrowIfNull(paths);
            List<string> resources = NormalizeBatchPaths(paths);
            var successfulPaths = new List<string>();
            var failures = new List<ResourceOpenFailure>();

            foreach (string path in resources)
            {
                if (TryCreateBatchRestrictionFailure(path, resources.Count, out ResourceOpenFailure? failure))
                {
                    failures.Add(failure);
                    continue;
                }

                ResourceOpenResult result = Open(path);
                if (result.Succeeded)
                    successfulPaths.Add(path);
                else
                    failures.Add(new ResourceOpenFailure(path, result.Kind, result.ErrorMessage));
            }

            return new ResourceOpenBatchResult(resources.Count, successfulPaths, failures);
        }

        public async Task<ResourceOpenBatchResult> OpenManyAsync(
            IEnumerable<string> paths,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(paths);
            List<string> resources = NormalizeBatchPaths(paths);
            var successfulPaths = new List<string>();
            var failures = new List<ResourceOpenFailure>();

            foreach (string path in resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryCreateBatchRestrictionFailure(path, resources.Count, out ResourceOpenFailure? failure))
                {
                    failures.Add(failure);
                    continue;
                }

                ResourceOpenResult result = await OpenAsync(path, cancellationToken);
                if (result.Succeeded)
                    successfulPaths.Add(path);
                else
                    failures.Add(new ResourceOpenFailure(path, result.Kind, result.ErrorMessage));
            }

            return new ResourceOpenBatchResult(resources.Count, successfulPaths, failures);
        }

        public async Task<bool> TryOpenManyWithFeedbackAsync(
            IEnumerable<string> paths,
            Window? owner = null,
            CancellationToken cancellationToken = default)
        {
            ResourceOpenBatchResult result = await OpenManyAsync(paths, cancellationToken);
            if (result.Failures.Count > 0)
                ShowBatchOpenFailures(result, owner);
            return result.IsComplete;
        }

        public async Task<bool> TryOpenCommandLineWithFeedbackAsync(
            CommandLineResourceOpenRequest request,
            Window? owner = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            bool openedWorkspace = false;
            if (!string.IsNullOrWhiteSpace(request.WorkspacePath))
            {
                openedWorkspace = await TryOpenWithFeedbackAsync(
                    request.WorkspacePath,
                    owner,
                    cancellationToken);
                if (!openedWorkspace)
                    return false;
            }

            if (request.ResourcePaths.Count == 0)
                return openedWorkspace;
            return await TryOpenManyWithFeedbackAsync(
                request.ResourcePaths,
                owner,
                cancellationToken);
        }

        internal static List<string> NormalizeBatchPaths(IEnumerable<string> paths)
        {
            return paths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(ResourcePathIdentityComparer.Instance)
                .ToList();
        }

        private static bool TryCreateBatchRestrictionFailure(
            string path,
            int resourceCount,
            out ResourceOpenFailure failure)
        {
            ResourceOpenKind kind = Classify(path);
            if (resourceCount <= 1 || kind == ResourceOpenKind.File)
            {
                failure = null!;
                return false;
            }

            failure = new ResourceOpenFailure(
                path,
                kind,
                kind == ResourceOpenKind.Missing
                    ? "资源不存在。"
                    : "批量打开仅支持普通文件；文件夹、工程和解决方案需要单独打开。");
            return true;
        }

        private static void ShowBatchOpenFailures(ResourceOpenBatchResult result, Window? owner)
        {
            string details = string.Join(
                Environment.NewLine,
                result.Failures.Take(5).Select(failure =>
                    $"• {Path.GetFileName(failure.ResourcePath)}: {failure.Message}"));
            if (result.Failures.Count > 5)
                details += $"{Environment.NewLine}• …";

            MessageBox.Show(
                owner ?? Application.Current?.GetActiveWindow(),
                $"已打开 {result.SuccessfulPaths.Count}/{result.RequestedCount} 项。{Environment.NewLine}{Environment.NewLine}{details}",
                "部分文件未能打开",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        /// <summary>
        /// Opens an existing physical resource with one explicit editor. This
        /// intentionally bypasses solution/project activation: choosing an
        /// editor is an explicit user decision, not extension-based routing.
        /// </summary>
        public bool TryOpenWith(string path, string editorId)
        {
            return OpenWith(path, editorId).Succeeded;
        }

        public ResourceOpenResult OpenWith(
            string? path,
            string? editorId,
            bool setAsDefault = false)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(editorId))
                return new ResourceOpenResult(ResourceOpenKind.Missing, false, "资源路径或编辑器无效。", setAsDefault ? false : null);

            ResourceOpenKind kind = Classify(path);
            bool isFolder = Directory.Exists(path);
            if (!isFolder && !File.Exists(path))
                return new ResourceOpenResult(ResourceOpenKind.Missing, false, "要打开的资源不存在。", setAsDefault ? false : null);

            string errorMessage;
            bool opened = isFolder
                ? _editorManager.OpenFolderWith(path, editorId, out errorMessage)
                : _editorManager.OpenFileWith(path, editorId, out errorMessage);
            if (!opened)
                return new ResourceOpenResult(kind, false, errorMessage, setAsDefault ? false : null);
            if (!setAsDefault)
                return new ResourceOpenResult(kind, true);

            bool defaultUpdated = TrySetDefaultOpenWithEditor(path, editorId, out string defaultError);
            return defaultUpdated
                ? new ResourceOpenResult(kind, true, DefaultEditorUpdated: true)
                : new ResourceOpenResult(
                    kind,
                    true,
                    $"资源已经打开，但未能保存默认打开方式：{defaultError}",
                    DefaultEditorUpdated: false);
        }

        /// <summary>
        /// Returns explicit editor choices for one physical resource. Solution
        /// and project files stay files here: Open With must not activate them.
        /// </summary>
        public IReadOnlyList<EditorDescriptor> GetOpenWithEditors(string? path, bool visibleOnly = true)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Array.Empty<EditorDescriptor>();
            if (Directory.Exists(path))
                return _editorManager.GetFolderEditorDescriptors(visibleOnly);
            if (!File.Exists(path))
                return Array.Empty<EditorDescriptor>();
            return _editorManager.GetFileEditorDescriptors(Path.GetExtension(path), visibleOnly);
        }

        public EditorDescriptor? GetDefaultOpenWithEditor(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            if (Directory.Exists(path))
                return _editorManager.GetDefaultFolderEditorDescriptor();
            if (!File.Exists(path))
                return null;
            return _editorManager.GetDefaultFileEditorDescriptor(Path.GetExtension(path));
        }

        public bool SetDefaultOpenWithEditor(string? path, string editorId)
        {
            return TrySetDefaultOpenWithEditor(path, editorId, out _);
        }

        public bool TrySetDefaultOpenWithEditor(
            string? path,
            string editorId,
            out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(editorId))
            {
                errorMessage = "资源路径或编辑器无效。";
                return false;
            }
            if (Directory.Exists(path))
                return _editorManager.TrySetDefaultFolderEditor(editorId, out errorMessage);
            if (!File.Exists(path))
            {
                errorMessage = "要设置默认打开方式的资源不存在。";
                return false;
            }
            return _editorManager.TrySetDefaultEditor(Path.GetExtension(path), editorId, out errorMessage);
        }

        public static bool TryOpenFile(string filePath) => EditorManager.Instance.TryOpenFile(filePath);

        internal static ResourceOpenKind Classify(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ResourceOpenKind.Missing;
            if (Directory.Exists(path))
                return ResourceOpenKind.Folder;
            if (!File.Exists(path))
                return ResourceOpenKind.Missing;
            if (SolutionManager.IsSolutionFilePath(path))
                return ResourceOpenKind.Solution;
            if (SolutionManager.IsProjectFilePath(path))
                return ResourceOpenKind.Project;
            return ResourceOpenKind.File;
        }

        internal static bool TryDescribeWorkspaceResource(
            string? path,
            out WorkspaceResourceInfo resourceInfo)
        {
            resourceInfo = null!;
            string normalizedPath = SolutionManager.NormalizeRecentPath(path);
            ResourceOpenKind kind = Classify(normalizedPath);
            if (kind == ResourceOpenKind.Folder)
            {
                DirectoryInfo directoryInfo = new(normalizedPath);
                resourceInfo = new WorkspaceResourceInfo(
                    kind,
                    directoryInfo.Name,
                    directoryInfo.FullName,
                    directoryInfo.CreationTime);
                return true;
            }

            if (kind is not (ResourceOpenKind.Solution or ResourceOpenKind.Project))
                return false;

            FileInfo fileInfo = new(normalizedPath);
            resourceInfo = new WorkspaceResourceInfo(
                kind,
                Path.GetFileNameWithoutExtension(fileInfo.Name),
                fileInfo.FullName,
                fileInfo.CreationTime);
            return true;
        }

        internal static string GetResourceKindName(ResourceOpenKind kind)
        {
            return kind switch
            {
                ResourceOpenKind.Folder => "文件夹",
                ResourceOpenKind.Solution => "解决方案",
                ResourceOpenKind.Project => "项目",
                ResourceOpenKind.File => "文件",
                _ => "资源",
            };
        }
    }
}
