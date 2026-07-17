using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Editor;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.Solution.Workspace
{
    /// <summary>
    /// Owns document identity and the common AvalonDock lifecycle for editors.
    /// A resource may be opened by different editors, so identity includes both
    /// the normalized path and the stable editor registration id.
    /// </summary>
    public static class EditorDocumentService
    {
        private static readonly Dictionary<LayoutDocument, EditorDocumentSession> _sessions = new();

        internal static string CreateContentId(string resourcePath, Type editorType)
        {
            string normalizedPath = NormalizeResourcePath(resourcePath);
            EditorResourceKind resourceKind = Directory.Exists(normalizedPath)
                ? EditorResourceKind.Folder
                : EditorResourceKind.File;
            string extension = resourceKind == EditorResourceKind.File ? Path.GetExtension(normalizedPath) : string.Empty;
            string editorId = EditorManager.Instance.GetEditorDescriptor(editorType, resourceKind, extension)?.Id
                ?? editorType.FullName
                ?? editorType.Name;
            return CreateContentId(normalizedPath, editorId);
        }

        internal static string CreateContentId(string resourcePath, string editorId)
        {
            return Tool.GetMD5($"{editorId}\n{NormalizeResourcePath(resourcePath)}");
        }

        public static LayoutDocument Open<TContent>(
            string resourcePath,
            Type editorType,
            string title,
            Func<TContent> createContent,
            Action<TContent>? closeContent = null)
            where TContent : class
        {
            string editorId = ResolveEditorId(resourcePath, editorType);
            string contentId = CreateContentId(resourcePath, editorId);
            var existingDocument = WorkspaceManager.FindDocumentById(WorkspaceManager.layoutRoot, contentId);
            if (existingDocument != null)
            {
                Activate(existingDocument);
                return existingDocument;
            }

            TContent content = createContent();
            var document = new LayoutDocument
            {
                ContentId = contentId,
                Title = title,
                Content = content,
            };

            var session = new EditorDocumentSession(
                document,
                resourcePath,
                editorId,
                title,
                content as IEditorDocumentContent,
                closeContent == null ? null : () => closeContent(content));
            _sessions[document] = session;
            session.Attach();

            WorkspaceManager.LayoutDocumentPane.Children.Add(document);
            Activate(document);
            return document;
        }

        public static bool CanSaveActiveDocument()
        {
            return GetActiveSession()?.Content is { CanSave: true, IsDirty: true };
        }

        public static bool TrySaveActiveDocument()
        {
            return GetActiveSession()?.TrySave(showError: true) == true;
        }

        public static bool CanReloadActiveDocument()
        {
            return GetActiveSession()?.CanReload == true;
        }

        public static bool TryReloadActiveDocument()
        {
            return GetActiveSession()?.TryReload(confirmDirty: true, externalChange: false, showError: true) == true;
        }

        public static bool TryCloseAllDocuments()
        {
            return TryCloseSessions(_sessions.Values
                .Where(session => session.Document.Parent != null)
                .ToList(), closeDocuments: false);
        }

        public static bool TryCloseDocumentsForResources(IEnumerable<string> resourcePaths)
        {
            var roots = resourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizeResourcePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (roots.Count == 0)
                return true;

            var sessions = _sessions.Values
                .Where(session => session.Document.Parent != null
                    && roots.Any(root => IsSameOrDescendant(session.ResourcePath, root)))
                .ToList();
            return TryCloseSessions(sessions, closeDocuments: true);
        }

        public static bool TryPrepareResourceRename(string resourcePath)
        {
            string root = NormalizeResourcePath(resourcePath);
            var sessions = _sessions.Values
                .Where(session => session.Document.Parent != null
                    && IsSameOrDescendant(session.ResourcePath, root)
                    && session.Content is not IResourcePathAwareDocumentContent)
                .ToList();
            return TryCloseSessions(sessions, closeDocuments: true);
        }

        public static void NotifyResourceRenamed(string oldPath, string newPath)
        {
            foreach (EditorDocumentSession session in _sessions.Values.ToList())
            {
                if (TryMapResourcePathAfterRename(session.ResourcePath, oldPath, newPath, out string mappedPath))
                    session.UpdateResourcePath(mappedPath);
            }
        }

        internal static string FormatTitle(string title, bool isDirty)
        {
            return isDirty ? $"{title} *" : title;
        }

        internal static bool TryMapResourcePathAfterRename(
            string resourcePath,
            string oldPath,
            string newPath,
            out string mappedPath)
        {
            string normalizedResource = NormalizeResourcePath(resourcePath);
            string normalizedOldPath = NormalizeResourcePath(oldPath);
            string normalizedNewPath = NormalizeResourcePath(newPath);
            if (!IsSameOrDescendant(normalizedResource, normalizedOldPath))
            {
                mappedPath = normalizedResource;
                return false;
            }

            if (string.Equals(normalizedResource, normalizedOldPath, StringComparison.OrdinalIgnoreCase))
            {
                mappedPath = normalizedNewPath;
                return true;
            }

            string relativePath = Path.GetRelativePath(normalizedOldPath, normalizedResource);
            mappedPath = NormalizeResourcePath(Path.Combine(normalizedNewPath, relativePath));
            return true;
        }

        private static EditorDocumentSession? GetActiveSession()
        {
            LayoutDocument? document = WorkspaceManager.layoutRoot == null
                ? WorkspaceManager.FindDocumentActive(WorkspaceManager.LayoutDocumentPane)
                : WorkspaceManager.FindDocumentActive(WorkspaceManager.layoutRoot);
            return document != null && _sessions.TryGetValue(document, out EditorDocumentSession? session)
                ? session
                : null;
        }

        private static string ResolveEditorId(string resourcePath, Type editorType)
        {
            string normalizedPath = NormalizeResourcePath(resourcePath);
            EditorResourceKind resourceKind = Directory.Exists(normalizedPath)
                ? EditorResourceKind.Folder
                : EditorResourceKind.File;
            string extension = resourceKind == EditorResourceKind.File ? Path.GetExtension(normalizedPath) : string.Empty;
            return EditorManager.Instance.GetEditorDescriptor(editorType, resourceKind, extension)?.Id
                ?? editorType.FullName
                ?? editorType.Name;
        }

        private static bool TryCloseSessions(IReadOnlyList<EditorDocumentSession> sessions, bool closeDocuments)
        {
            var approvedSessions = new List<EditorDocumentSession>();
            foreach (EditorDocumentSession session in sessions)
            {
                if (!session.TryApproveClose())
                {
                    foreach (EditorDocumentSession approvedSession in approvedSessions)
                        approvedSession.ClearCloseApproval();
                    return false;
                }
                approvedSessions.Add(session);
            }

            if (!closeDocuments)
                return true;

            foreach (EditorDocumentSession session in sessions)
            {
                session.Document.Close();
                if (session.Document.Parent != null)
                {
                    foreach (EditorDocumentSession remainingSession in sessions)
                        remainingSession.ClearCloseApproval();
                    return false;
                }
            }
            return true;
        }

        private static bool IsSameOrDescendant(string resourcePath, string rootPath)
        {
            if (string.Equals(resourcePath, rootPath, StringComparison.OrdinalIgnoreCase))
                return true;

            string relativePath = Path.GetRelativePath(rootPath, resourcePath);
            return !Path.IsPathRooted(relativePath)
                && !string.Equals(relativePath, "..", StringComparison.Ordinal)
                && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
        }

        private static string NormalizeResourcePath(string resourcePath)
        {
            string fullPath = Path.GetFullPath(resourcePath);
            return Path.TrimEndingDirectorySeparator(fullPath);
        }

        private static void Activate(LayoutDocument document)
        {
            if (document.Parent is LayoutDocumentPane pane)
            {
                pane.SelectedContentIndex = pane.IndexOf(document);
                document.IsActive = true;
                return;
            }

            if (document.Parent is LayoutFloatingWindow floatingWindow)
            {
                Window.GetWindow(floatingWindow)?.Activate();
                document.IsActive = true;
                return;
            }

            WorkspaceManager.LayoutDocumentPane.Children.Add(document);
            WorkspaceManager.LayoutDocumentPane.SelectedContentIndex = WorkspaceManager.LayoutDocumentPane.IndexOf(document);
            document.IsActive = true;
        }

        private readonly record struct ResourceFileStamp(bool Exists, long Length, long LastWriteTicks);

        private sealed class EditorDocumentSession : IDisposable
        {
            private string _title;
            private readonly Action? _closeContent;
            private readonly string _editorId;
            private readonly bool _titleFollowsResource;
            private FileSystemWatcher? _resourceWatcher;
            private DispatcherTimer? _externalChangeTimer;
            private ResourceFileStamp _observedResourceStamp;
            private bool _resourceMissing;
            private bool _closeApproved;
            private bool _isClosed;

            public LayoutDocument Document { get; }
            public IEditorDocumentContent? Content { get; }
            public string ResourcePath { get; private set; }
            public bool CanReload => Content is IReloadableEditorDocumentContent && File.Exists(ResourcePath);

            public EditorDocumentSession(
                LayoutDocument document,
                string resourcePath,
                string editorId,
                string title,
                IEditorDocumentContent? content,
                Action? closeContent)
            {
                Document = document;
                ResourcePath = NormalizeResourcePath(resourcePath);
                _editorId = editorId;
                _title = title;
                _titleFollowsResource = string.Equals(title, GetResourceTitle(ResourcePath), StringComparison.Ordinal);
                Content = content;
                _closeContent = closeContent;
            }

            public void Attach()
            {
                Document.IsActiveChanged += Document_IsActiveChanged;
                Document.Closing += Document_Closing;
                Document.Closed += Document_Closed;
                if (Content != null)
                {
                    Content.DocumentStateChanged += Content_DocumentStateChanged;
                    UpdateTitle();
                }
                InitializeResourceWatcher();
            }

            public void UpdateResourcePath(string resourcePath)
            {
                string normalizedPath = NormalizeResourcePath(resourcePath);
                if (Content is IResourcePathAwareDocumentContent pathAware
                    && !pathAware.TryUpdateResourcePath(normalizedPath))
                {
                    return;
                }

                ResourcePath = normalizedPath;
                Document.ContentId = CreateContentId(ResourcePath, _editorId);
                if (_titleFollowsResource)
                    _title = GetResourceTitle(ResourcePath);
                InitializeResourceWatcher();
                UpdateTitle();
                if (Document.IsActive)
                    WorkspaceManager.OnContentIdSelected(ResourcePath);
            }

            public bool TrySave(bool showError)
            {
                if (Content is not { CanSave: true })
                    return false;

                try
                {
                    bool saved = Content.Save();
                    if (!saved && showError)
                        ShowSaveError(null);
                    if (saved)
                    {
                        CaptureResourceStamp();
                        _resourceMissing = false;
                        UpdateTitle();
                    }
                    return saved;
                }
                catch (Exception ex)
                {
                    if (showError)
                        ShowSaveError(ex);
                    return false;
                }
            }

            public bool TryReload(bool confirmDirty, bool externalChange, bool showError)
            {
                if (Content is not IReloadableEditorDocumentContent reloadable || !File.Exists(ResourcePath))
                    return false;

                if (confirmDirty && Content.IsDirty)
                {
                    string message = externalChange
                        ? $"磁盘上的“{_title}”已被外部程序修改。重新加载将放弃当前未保存的更改，是否继续？"
                        : $"重新加载“{_title}”将放弃当前未保存的更改，是否继续？";
                    if (MessageBox.Show(
                        Application.Current?.GetActiveWindow(),
                        message,
                        "ColorVision",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    {
                        return false;
                    }
                }

                try
                {
                    bool reloaded = reloadable.ReloadFromDisk();
                    if (!reloaded && showError)
                        ShowReloadError(null);
                    if (reloaded)
                    {
                        CaptureResourceStamp();
                        _resourceMissing = false;
                        UpdateTitle();
                    }
                    return reloaded;
                }
                catch (Exception ex)
                {
                    if (showError)
                        ShowReloadError(ex);
                    return false;
                }
            }

            public bool TryApproveClose()
            {
                if (Content?.IsDirty != true || _closeApproved)
                    return true;

                MessageBoxResult result = MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"是否保存对“{_title}”的更改？",
                    "ColorVision",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                if (result == MessageBoxResult.Cancel)
                    return false;
                if (result == MessageBoxResult.Yes)
                    return TrySave(showError: true);

                _closeApproved = true;
                return true;
            }

            public void ClearCloseApproval()
            {
                _closeApproved = false;
            }

            private void Document_Closing(object? sender, CancelEventArgs e)
            {
                if (_closeApproved)
                {
                    _closeApproved = false;
                    return;
                }

                if (!TryApproveClose())
                    e.Cancel = true;
                else
                    _closeApproved = false;
            }

            private void Document_Closed(object? sender, EventArgs e)
            {
                if (_isClosed)
                    return;
                _isClosed = true;

                Document.Closing -= Document_Closing;
                Document.Closed -= Document_Closed;
                Document.IsActiveChanged -= Document_IsActiveChanged;
                if (Content != null)
                    Content.DocumentStateChanged -= Content_DocumentStateChanged;
                Dispose();
                _sessions.Remove(Document);
                _closeContent?.Invoke();
            }

            private void Document_IsActiveChanged(object? sender, EventArgs e)
            {
                if (Document.IsActive)
                    WorkspaceManager.OnContentIdSelected(ResourcePath);
            }

            private void Content_DocumentStateChanged(object? sender, EventArgs e)
            {
                UpdateTitle();
                CommandManager.InvalidateRequerySuggested();
            }

            private void UpdateTitle()
            {
                string title = FormatTitle(_title, Content?.IsDirty == true);
                Document.Title = _resourceMissing ? $"{title} [已删除]" : title;
            }

            private void InitializeResourceWatcher()
            {
                DisposeResourceWatcher();
                CaptureResourceStamp();
                _resourceMissing = !_observedResourceStamp.Exists;

                if (Content is not IReloadableEditorDocumentContent)
                    return;

                string? directoryPath = Path.GetDirectoryName(ResourcePath);
                string fileName = Path.GetFileName(ResourcePath);
                if (string.IsNullOrWhiteSpace(directoryPath)
                    || string.IsNullOrWhiteSpace(fileName)
                    || !Directory.Exists(directoryPath))
                {
                    return;
                }

                try
                {
                    _resourceWatcher = new FileSystemWatcher(directoryPath, fileName)
                    {
                        NotifyFilter = NotifyFilters.FileName
                            | NotifyFilters.CreationTime
                            | NotifyFilters.LastWrite
                            | NotifyFilters.Size,
                    };
                    _resourceWatcher.Changed += ResourceWatcher_Changed;
                    _resourceWatcher.Created += ResourceWatcher_Changed;
                    _resourceWatcher.Deleted += ResourceWatcher_Changed;
                    _resourceWatcher.Renamed += ResourceWatcher_Renamed;
                    _resourceWatcher.EnableRaisingEvents = true;
                }
                catch
                {
                    DisposeResourceWatcher();
                }
            }

            private void ResourceWatcher_Changed(object sender, FileSystemEventArgs e)
            {
                ScheduleExternalResourceCheck(e.FullPath);
            }

            private void ResourceWatcher_Renamed(object sender, RenamedEventArgs e)
            {
                ScheduleExternalResourceCheck(e.FullPath);
                ScheduleExternalResourceCheck(e.OldFullPath);
            }

            private void ScheduleExternalResourceCheck(string changedPath)
            {
                string normalizedChangedPath = NormalizeResourcePath(changedPath);
                if (!string.Equals(normalizedChangedPath, ResourcePath, StringComparison.OrdinalIgnoreCase))
                    return;

                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (_isClosed
                        || !string.Equals(normalizedChangedPath, ResourcePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    _externalChangeTimer ??= CreateExternalChangeTimer();
                    _externalChangeTimer.Stop();
                    _externalChangeTimer.Start();
                });
            }

            private DispatcherTimer CreateExternalChangeTimer()
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                timer.Tick += ExternalChangeTimer_Tick;
                return timer;
            }

            private void ExternalChangeTimer_Tick(object? sender, EventArgs e)
            {
                _externalChangeTimer?.Stop();
                ResourceFileStamp currentStamp = GetResourceStamp(ResourcePath);
                if (currentStamp == _observedResourceStamp)
                    return;

                _observedResourceStamp = currentStamp;
                if (!currentStamp.Exists)
                {
                    _resourceMissing = true;
                    UpdateTitle();
                    return;
                }

                bool wasMissing = _resourceMissing;
                _resourceMissing = false;
                if (Content?.IsDirty == true)
                {
                    TryReload(confirmDirty: true, externalChange: true, showError: true);
                    UpdateTitle();
                    return;
                }

                if (!TryReload(confirmDirty: false, externalChange: !wasMissing, showError: true))
                    UpdateTitle();
            }

            private void CaptureResourceStamp()
            {
                _observedResourceStamp = GetResourceStamp(ResourcePath);
            }

            private void DisposeResourceWatcher()
            {
                if (_externalChangeTimer != null)
                {
                    _externalChangeTimer.Stop();
                    _externalChangeTimer.Tick -= ExternalChangeTimer_Tick;
                    _externalChangeTimer = null;
                }

                if (_resourceWatcher == null)
                    return;

                _resourceWatcher.EnableRaisingEvents = false;
                _resourceWatcher.Changed -= ResourceWatcher_Changed;
                _resourceWatcher.Created -= ResourceWatcher_Changed;
                _resourceWatcher.Deleted -= ResourceWatcher_Changed;
                _resourceWatcher.Renamed -= ResourceWatcher_Renamed;
                _resourceWatcher.Dispose();
                _resourceWatcher = null;
            }

            public void Dispose()
            {
                DisposeResourceWatcher();
            }

            private static ResourceFileStamp GetResourceStamp(string resourcePath)
            {
                try
                {
                    var fileInfo = new FileInfo(resourcePath);
                    fileInfo.Refresh();
                    return fileInfo.Exists
                        ? new ResourceFileStamp(true, fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks)
                        : default;
                }
                catch (IOException)
                {
                    return default;
                }
                catch (UnauthorizedAccessException)
                {
                    return default;
                }
            }

            private void ShowSaveError(Exception? exception)
            {
                string detail = exception == null ? string.Empty : $"{Environment.NewLine}{exception.Message}";
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"无法保存“{_title}”。{detail}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            private void ShowReloadError(Exception? exception)
            {
                string detail = exception == null ? string.Empty : $"{Environment.NewLine}{exception.Message}";
                MessageBox.Show(
                    Application.Current?.GetActiveWindow(),
                    $"无法重新加载“{_title}”。{detail}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            private static string GetResourceTitle(string resourcePath)
            {
                string title = Path.GetFileName(resourcePath);
                return string.IsNullOrWhiteSpace(title)
                    ? new DirectoryInfo(resourcePath).Name
                    : title;
            }
        }
    }
}
