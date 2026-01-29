using ColorVision.ImageEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 多图预览控件 - 支持文件夹浏览和图片切换
    /// </summary>
    public partial class MultiImageViewer : UserControl
    {
        public static MultiImageViewerConfig Config => MultiImageViewerConfig.Instance;

        /// <summary>
        /// 文件列表
        /// </summary>
        public ObservableCollection<ImageFileInfo> ImageFiles { get; } = new ObservableCollection<ImageFileInfo>();

        /// <summary>
        /// 当前显示的文件夹路径
        /// </summary>
        public string? CurrentFolderPath { get; private set; }

        /// <summary>
        /// 当前显示的文件列表
        /// </summary>
        private List<string>? _currentFileList;

        public MultiImageViewer()
        {
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = Config;
            FileListBox.ItemsSource = ImageFiles;
            UpdateFileCountText();
        }

        /// <summary>
        /// 从文件夹加载图片
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        public async Task LoadFromFolderAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                MessageBox.Show($"文件夹不存在: {folderPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CurrentFolderPath = folderPath;
            _currentFileList = null;

            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => MultiImageViewerConfig.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Take(Config.MaxDisplayCount)
                .ToList();

            await LoadFilesAsync(files);
        }

        /// <summary>
        /// 从文件列表加载图片
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        public async Task LoadFromFilesAsync(List<string> filePaths)
        {
            if (filePaths == null || filePaths.Count == 0)
                return;

            CurrentFolderPath = null;
            _currentFileList = filePaths;

            var validFiles = filePaths
                .Where(f => MultiImageViewerConfig.SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Take(Config.MaxDisplayCount)
                .ToList();

            await LoadFilesAsync(validFiles);
        }

        /// <summary>
        /// 从文件列表加载图片
        /// </summary>
        /// <param name="filePaths">文件路径列表</param>
        public async Task LoadFromFilesAsync(IEnumerable<string> filePaths)
        {
            await LoadFromFilesAsync(filePaths.ToList());
        }

        private async Task LoadFilesAsync(List<string> files)
        {
            ImageFiles.Clear();
            ImageView.Clear();
            NoImageHint.Visibility = Visibility.Visible;

            foreach (var file in files)
            {
                var fileInfo = new ImageFileInfo(file);
                ImageFiles.Add(fileInfo);
            }

            UpdateFileCountText();

            // 异步加载缩略图
            if (Config.ShowThumbnail)
            {
                await LoadThumbnailsAsync();
            }

            // 自动选中第一个
            if (ImageFiles.Count > 0)
            {
                FileListBox.SelectedIndex = 0;
            }
        }

        private async Task LoadThumbnailsAsync()
        {
            var thumbnailTasks = ImageFiles
                .Where(f => f.FileExists)
                .Select(f => f.LoadThumbnailAsync(Config.ThumbnailSize, Config.EnableThumbnailCache));

            await Task.WhenAll(thumbnailTasks);
        }

        private void FileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileListBox.SelectedItem is ImageFileInfo selectedFile)
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

            if (fileInfo.FileExists)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var fi = new FileInfo(fileInfo.FilePath);
                        // 尝试打开文件以确认可读
                        using (fi.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) { }

                        if (fi.Length > 0)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ImageView.OpenImage(fileInfo.FilePath);
                                NoImageHint.Visibility = Visibility.Collapsed;
                            });
                        }
                    }
                    catch
                    {
                        // 文件可能正在写入，延迟重试
                        await Task.Delay(Config.ImageReadDelay);
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                ImageView.OpenImage(fileInfo.FilePath);
                                NoImageHint.Visibility = Visibility.Collapsed;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"OpenImage Error: {ex.Message}");
                            }
                        });
                    }
                });
            }
        }

        private void UpdateFileCountText()
        {
            FileCountText.Text = $"共 {ImageFiles.Count} 个文件";
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
                catch { }
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
            ImageFiles.Clear();
            ImageView.Clear();
            CurrentFolderPath = null;
            _currentFileList = null;
            NoImageHint.Visibility = Visibility.Visible;
            UpdateFileCountText();
        }
    }
}
