#pragma warning disable CA1863,CS4014
using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.Solution.Editor;
using ColorVision.Solution.Workspace;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.MultiImageViewer
{
    public record class ZoomEditorToolContextMenu(EditorContext context) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems()
        {
            var MenuItemMetadatas = new List<MenuItemMetadata>();
            string filepath = context.Config.FilePath;
            if (File.Exists(filepath))
            {
                string DirectoryPath = Directory.GetParent(filepath)?.FullName;
                if (DirectoryPath != null)
                {
                    RelayCommand OpenMultiImageViewerEditorCommand = new RelayCommand((o) =>
                    {
                        Window window = new Window();
                        ColorVision.Themes.ThemeManagerExtensions.ApplyCaption(window);
                        MultiImageViewer multiImageViewer = new MultiImageViewer();
                        multiImageViewer.FilePath = filepath;
                        window.Content = multiImageViewer;
                        window.Closed += (_, _) => multiImageViewer.Dispose();
                        window.Show();
                        multiImageViewer.LoadFromFolderAsync(DirectoryPath);
                    });
                    MenuItemMetadatas.Add(new MenuItemMetadata() { GuidId = "MultiImageViewerEditor", Order = 10, Header = Properties.Resources.MultiImageViewer, Command = OpenMultiImageViewerEditorCommand });
                }
            }
            return MenuItemMetadatas;
        }
    }


    [FolderEditor("MultiImageViewerEditor", editorId: "colorvision.folder.multi-image", priority: 80)]
    public class MultiImageViewerEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!Directory.Exists(filePath))
                return;

            EditorDocumentService.Open(
                filePath,
                GetType(),
                Path.GetFileName(filePath),
                () =>
                {
                    var viewer = new MultiImageViewer();
                    viewer.LoadFromFolderAsync(filePath);
                    return viewer;
                },
                viewer => viewer.Dispose());
        }
    }


    /// <summary>
    /// 多图预览控件 - 支持文件夹浏览和图片切换
    /// </summary>
    public partial class MultiImageViewer : UserControl, IDisposable
    {
        public static MultiImageViewerConfig Config => MultiImageViewerConfig.Instance;

        /// <summary>
        /// 文件列表
        /// </summary>
        public ObservableCollection<ImageFileInfo> ImageFiles => _imageFiles;
        private readonly ImageFileCollection _imageFiles = new();

        /// <summary>
        /// 当前显示的文件夹路径
        /// </summary>
        public string? CurrentFolderPath { get; private set; }

        /// <summary>
        /// 当前显示的文件列表
        /// </summary>
        private List<string>? _currentFileList;

        private bool _disposed;
        private bool _publishingFileList;
        private CancellationTokenSource? _fileLoad;
        private static readonly SemaphoreSlim CachedDimensionsSlot = new(1, 1);

        public MultiImageViewer()
        {
            InitializeComponent();
            ObserveThumbnailViewport();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Config;
            FileListBox.ItemsSource = ImageFiles;
            UpdateFileCountText();
        }

        public string FilePath { get; set; }

        /// <summary>
        /// 从文件夹加载图片
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        public Task LoadFromFolderAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                MessageBox.Show(string.Format(Properties.Resources.Sol_Fusion_FileNotExist, folderPath), "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return Task.CompletedTask;
            }

            return LoadFilesAsync(() => Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly), folderPath, null);
        }

        /// <summary>
        /// 从文件列表加载图片
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        public Task LoadFromFilesAsync(List<string> filePaths)
            => filePaths is { Count: > 0 } ? LoadFileListAsync(filePaths.ToList()) : Task.CompletedTask;

        /// <summary>
        /// 从文件列表加载图片
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        public Task LoadFromFilesAsync(IEnumerable<string> filePaths)
        {
            return LoadFileListAsync(filePaths.ToList());
        }

        private Task LoadFileListAsync(List<string> snapshot)
            => snapshot.Count > 0 ? LoadFilesAsync(() => snapshot, null, snapshot) : Task.CompletedTask;

        private async Task LoadFilesAsync(Func<IEnumerable<string>> enumeratePaths, string? folderPath, List<string>? fileList)
        {
            if (_disposed) return;
            using var load = new CancellationTokenSource();
            CancellationTokenSource? previous = _fileLoad;
            _fileLoad = load;
            CancellationToken token = load.Token;
            try
            {
                previous?.Cancel();
                token.ThrowIfCancellationRequested();
                int limit = Config.MaxDisplayCount;
                var thumbnailSettings = (Config.ShowThumbnail, Config.ThumbnailSize, Config.EnableThumbnailCache);
                List<ImageFileInfo> items;
                try
                {
                    items = await Task.Run(() => PrepareFileList(enumeratePaths, limit, token), token);
                }
                catch (Exception error) when (folderPath != null && error is (IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException))
                {
                    if (!token.IsCancellationRequested)
                    {
                        string message = error is DirectoryNotFoundException or ArgumentException
                            ? string.Format(Properties.Resources.Sol_Fusion_FileNotExist, folderPath) : error.Message;
                        MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                token.ThrowIfCancellationRequested();
                bool wasPublishing = _publishingFileList;
                _publishingFileList = true;
                try
                {
                    ResetThumbnailRows();
                    token.ThrowIfCancellationRequested();
                    ImageView.Clear();
                    token.ThrowIfCancellationRequested();
                    CurrentFolderPath = folderPath;
                    _currentFileList = fileList;
                    _thumbnailSettings = thumbnailSettings;
                    NoImageHint.Visibility = Visibility.Visible;
                    _imageFiles.ReplaceWith(items);
                    token.ThrowIfCancellationRequested();
                    UpdateFileCountText();
                    token.ThrowIfCancellationRequested();
                    int index = items.FindLastIndex(item => item.FilePath.Equals(FilePath, StringComparison.OrdinalIgnoreCase));
                    FileListBox.SelectedIndex = items.Count == 0 ? -1 : Math.Max(0, index);
                }
                finally { _publishingFileList = wasPublishing; }

                token.ThrowIfCancellationRequested();
                if (FileListBox.SelectedItem is ImageFileInfo selected)
                {
                    OpenImage(selected);
                    token.ThrowIfCancellationRequested();
                    FileListBox.ScrollIntoView(selected);
                }
                await LoadCachedDimensionsAsync(items, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally
            {
                if (ReferenceEquals(_fileLoad, load)) _fileLoad = null;
            }
        }

        private static List<ImageFileInfo> PrepareFileList(Func<IEnumerable<string>> enumeratePaths, int limit, CancellationToken token)
        {
            var items = new List<ImageFileInfo>();
            foreach (string path in enumeratePaths())
            {
                token.ThrowIfCancellationRequested();
                if (!MultiImageViewerConfig.SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())) continue;
                items.Add(new ImageFileInfo(path, loadCachedInfo: false));
                if (items.Count == limit) break;
            }
            return items;
        }

        private static async Task LoadCachedDimensionsAsync(List<ImageFileInfo> items, CancellationToken token)
        {
            string[] paths = items.Where(item => item.FileExists).Select(item => item.FilePath).ToArray();
            if (paths.Length == 0) return;

            // At most one worker can wait on SQLite for a list. Retired queued lists cancel before taking a worker.
            await CachedDimensionsSlot.WaitAsync(token);
            IReadOnlyDictionary<string, ThumbnailCacheEntry> metadata;
            try
            {
                metadata = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var cached = ThumbnailCacheManager.Instance.GetCachedInfos(paths, token);
                    var current = new Dictionary<string, ThumbnailCacheEntry>(StringComparer.Ordinal);
                    foreach (var (path, entry) in cached)
                    {
                        token.ThrowIfCancellationRequested();
                        var file = new FileInfo(path);
                        if (file.Exists && file.Length == entry.FileSize && file.LastWriteTime == entry.FileLastModified)
                            current.Add(path, entry);
                    }
                    return current;
                }, token);
            }
            finally { CachedDimensionsSlot.Release(); }

            foreach (ImageFileInfo item in items)
            {
                token.ThrowIfCancellationRequested();
                if (metadata.TryGetValue(item.FilePath, out var entry)) item.ApplyCachedDimensions(entry);
            }
        }

        private sealed class ImageFileCollection : ObservableCollection<ImageFileInfo>
        {
            public void ReplaceWith(IReadOnlyList<ImageFileInfo> items)
            {
                CheckReentrancy();
                Items.Clear();
                foreach (ImageFileInfo item in items) Items.Add(item);
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_disposed && !_publishingFileList && FileListBox.SelectedItem is ImageFileInfo selectedFile)
            {
                OpenImage(selectedFile);
            }
        }

        private void OpenImage(ImageFileInfo fileInfo)
        {
            if (string.IsNullOrWhiteSpace(fileInfo.FilePath))
                return;

            // 检查是否是当前已打开的文件
            if (fileInfo.FilePath.Equals(ImageView.Config?.FilePath, StringComparison.OrdinalIgnoreCase))
                return;

            ImageView.OpenImage(fileInfo.FilePath);
            if (!_disposed && ReferenceEquals(FileListBox.SelectedItem, fileInfo))
                NoImageHint.Visibility = Visibility.Collapsed;
        }

        private void UpdateFileCountText()
        {
            FileCountText.Text = $"{ImageFiles.Count} files";
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(CurrentFolderPath))
            {
                await LoadFromFolderAsync(CurrentFolderPath);
            }
            else if (_currentFileList != null)
            {
                await LoadFromFilesAsync(_currentFileList);
            }
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is ImageFileInfo selectedFile)
            {
                selectedFile.OpenInExplorer();
            }
        }

        private void CopyFilePath_Click(object sender, RoutedEventArgs e)
        {
            if (FileListBox.SelectedItem is ImageFileInfo selectedFile)
            {
                try
                {
                    Clipboard.SetText(selectedFile.FilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CopyFilePath Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 获取当前选中的文件
        /// </summary>
        public ImageFileInfo? GetSelectedFile()
        {
            return FileListBox.SelectedItem as ImageFileInfo;
        }

        /// <summary>
        /// 选中指定的文件
        /// </summary>
        public void SelectFile(string filePath)
        {
            var file = ImageFiles.FirstOrDefault(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
            if (file != null)
            {
                FileListBox.SelectedItem = file;
                FileListBox.ScrollIntoView(file);
            }
        }

        /// <summary>
        /// 选中下一个文件
        /// </summary>
        public void SelectNext()
        {
            if (FileListBox.SelectedIndex < ImageFiles.Count - 1)
            {
                FileListBox.SelectedIndex++;
                FileListBox.ScrollIntoView(FileListBox.SelectedItem);
            }
        }

        /// <summary>
        /// 选中上一个文件
        /// </summary>
        public void SelectPrevious()
        {
            if (FileListBox.SelectedIndex > 0)
            {
                FileListBox.SelectedIndex--;
                FileListBox.ScrollIntoView(FileListBox.SelectedItem);
            }
        }

        /// <summary>
        /// 清除所有内容
        /// </summary>
        public void Clear()
        {
            _fileLoad?.Cancel();
            ResetThumbnailRows();
            ImageFiles.Clear();
            ImageView.Clear();
            CurrentFolderPath = null;
            _currentFileList = null;
            NoImageHint.Visibility = Visibility.Visible;
            UpdateFileCountText();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Clear();
            ImageView?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
