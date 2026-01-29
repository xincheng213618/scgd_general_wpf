using ColorVision.Common.MVVM;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Solution.MultiImageViewer
{
    /// <summary>
    /// 图像文件信息，用于文件列表显示
    /// </summary>
    public class ImageFileInfo : ViewModelBase
    {
        private string _filePath = string.Empty;
        private ImageSource? _thumbnail;
        private bool _isLoadingThumbnail;

        /// <summary>
        /// 文件完整路径
        /// </summary>
        [DisplayName("文件路径")]
        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(Directory));
                OnPropertyChanged(nameof(FileExtension));
                LoadFileInfo();
            }
        }

        /// <summary>
        /// 文件名（不含路径）
        /// </summary>
        [DisplayName("文件名")]
        public string FileName => Path.GetFileName(_filePath);

        /// <summary>
        /// 文件所在目录
        /// </summary>
        [DisplayName("目录")]
        public string Directory => Path.GetDirectoryName(_filePath) ?? string.Empty;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        [DisplayName("扩展名")]
        public string FileExtension => Path.GetExtension(_filePath).ToLowerInvariant();

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        [DisplayName("文件大小")]
        public long FileSize
        {
            get => _fileSize;
            private set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
        }
        private long _fileSize;

        /// <summary>
        /// 文件大小显示
        /// </summary>
        [DisplayName("大小")]
        public string FileSizeDisplay
        {
            get
            {
                if (_fileSize < 1024) return $"{_fileSize} B";
                if (_fileSize < 1024 * 1024) return $"{_fileSize / 1024.0:F1} KB";
                return $"{_fileSize / 1024.0 / 1024.0:F2} MB";
            }
        }

        /// <summary>
        /// 文件最后修改时间
        /// </summary>
        [DisplayName("修改时间")]
        public DateTime LastModified
        {
            get => _lastModified;
            private set { _lastModified = value; OnPropertyChanged(); }
        }
        private DateTime _lastModified;

        /// <summary>
        /// 原始图像宽度
        /// </summary>
        [DisplayName("宽度")]
        public int ImageWidth
        {
            get => _imageWidth;
            set { _imageWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImageSizeDisplay)); }
        }
        private int _imageWidth;

        /// <summary>
        /// 原始图像高度
        /// </summary>
        [DisplayName("高度")]
        public int ImageHeight
        {
            get => _imageHeight;
            set { _imageHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImageSizeDisplay)); }
        }
        private int _imageHeight;

        /// <summary>
        /// 图像尺寸显示
        /// </summary>
        [DisplayName("分辨率")]
        public string ImageSizeDisplay => _imageWidth > 0 && _imageHeight > 0
            ? $"{_imageWidth} × {_imageHeight}"
            : "未知";

        /// <summary>
        /// 缩略图
        /// </summary>
        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set { _thumbnail = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 是否正在加载缩略图
        /// </summary>
        public bool IsLoadingThumbnail
        {
            get => _isLoadingThumbnail;
            set { _isLoadingThumbnail = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 文件是否存在
        /// </summary>
        [DisplayName("存在")]
        public bool FileExists
        {
            get => _fileExists;
            private set { _fileExists = value; OnPropertyChanged(); }
        }
        private bool _fileExists;

        /// <summary>
        /// 图像信息提示（用于ToolTip）
        /// </summary>
        public string ImageInfoTooltip => $"文件名: {FileName}\n" +
            $"路径: {FilePath}\n" +
            $"大小: {FileSizeDisplay}\n" +
            $"分辨率: {ImageSizeDisplay}\n" +
            $"修改时间: {LastModified:yyyy-MM-dd HH:mm:ss}";

        public ImageFileInfo()
        {
        }

        public ImageFileInfo(string filePath)
        {
            FilePath = filePath;
        }

        private void LoadFileInfo()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    FileExists = true;
                    var fileInfo = new FileInfo(_filePath);
                    FileSize = fileInfo.Length;
                    LastModified = fileInfo.LastWriteTime;

                    // 尝试从缓存获取图像尺寸
                    var cached = ThumbnailCacheManager.Instance.GetCachedInfo(_filePath);
                    if (cached != null)
                    {
                        ImageWidth = cached.OriginalWidth;
                        ImageHeight = cached.OriginalHeight;
                    }
                }
                else
                {
                    FileExists = false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadFileInfo Error: {ex.Message}");
                FileExists = false;
            }
        }

        /// <summary>
        /// 异步加载缩略图
        /// </summary>
        public async System.Threading.Tasks.Task LoadThumbnailAsync(int thumbnailSize = 120, bool useCache = true)
        {
            if (IsLoadingThumbnail) return;

            IsLoadingThumbnail = true;
            try
            {
                if (useCache)
                {
                    var thumb = await ThumbnailCacheManager.Instance.GetOrCreateThumbnailAsync(_filePath, thumbnailSize);
                    if (thumb != null)
                    {
                        Thumbnail = thumb;

                        // 更新图像尺寸
                        var cached = ThumbnailCacheManager.Instance.GetCachedInfo(_filePath);
                        if (cached != null)
                        {
                            ImageWidth = cached.OriginalWidth;
                            ImageHeight = cached.OriginalHeight;
                        }
                    }
                }
                else
                {
                    // 直接创建缩略图，不使用缓存
                    await LoadThumbnailDirectAsync(thumbnailSize);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadThumbnailAsync Error: {ex.Message}");
            }
            finally
            {
                IsLoadingThumbnail = false;
            }
        }

        private async System.Threading.Tasks.Task LoadThumbnailDirectAsync(int thumbnailSize)
        {
            try
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    var frame = decoder.Frames[0];

                    ImageWidth = frame.PixelWidth;
                    ImageHeight = frame.PixelHeight;

                    double scale = Math.Min((double)thumbnailSize / ImageWidth, (double)thumbnailSize / ImageHeight);
                    scale = Math.Min(scale, 1.0);

                    var thumbnail = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                    thumbnail.Freeze();
                    Thumbnail = thumbnail;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadThumbnailDirectAsync Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 在资源管理器中打开文件所在目录
        /// </summary>
        public void OpenInExplorer()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_filePath}\"");
                }
                else if (System.IO.Directory.Exists(Directory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", Directory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenInExplorer Error: {ex.Message}");
            }
        }
    }
}
