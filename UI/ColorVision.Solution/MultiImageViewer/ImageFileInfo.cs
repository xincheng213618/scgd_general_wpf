using ColorVision.Common.MVVM;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
        [Display(Name = "Sol_FileInfo_Path", ResourceType = typeof(Properties.Resources))]
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
        [Display(Name = "Sol_FileInfo_Name", ResourceType = typeof(Properties.Resources))]
        public string FileName => Path.GetFileName(_filePath);

        /// <summary>
        /// 文件所在目录
        /// </summary>
        [Display(Name = "Sol_FileInfo_Dir", ResourceType = typeof(Properties.Resources))]
        public string Directory => Path.GetDirectoryName(_filePath) ?? string.Empty;

        /// <summary>
        /// 文件扩展名
        /// </summary>
        [Display(Name = "Sol_FileInfo_Ext", ResourceType = typeof(Properties.Resources))]
        public string FileExtension => Path.GetExtension(_filePath).ToLowerInvariant();

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        [Display(Name = "Sol_FileInfo_FileSize", ResourceType = typeof(Properties.Resources))]
        public long FileSize
        {
            get => _fileSize;
            private set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); }
        }
        private long _fileSize;

        /// <summary>
        /// 文件大小显示
        /// </summary>
        [Display(Name = "Sol_FileInfo_Size", ResourceType = typeof(Properties.Resources))]
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
        [Display(Name = "Sol_FileInfo_Modified", ResourceType = typeof(Properties.Resources))]
        public DateTime LastModified
        {
            get => _lastModified;
            private set { _lastModified = value; OnPropertyChanged(); }
        }
        private DateTime _lastModified;

        /// <summary>
        /// 原始图像宽度
        /// </summary>
        [Display(Name = "Sol_FileInfo_Width", ResourceType = typeof(Properties.Resources))]
        public int ImageWidth
        {
            get => _imageWidth;
            set { _imageWidth = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImageSizeDisplay)); }
        }
        private int _imageWidth;

        /// <summary>
        /// 原始图像高度
        /// </summary>
        [Display(Name = "Sol_FileInfo_Height", ResourceType = typeof(Properties.Resources))]
        public int ImageHeight
        {
            get => _imageHeight;
            set { _imageHeight = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImageSizeDisplay)); }
        }
        private int _imageHeight;

        /// <summary>
        /// 图像尺寸显示
        /// </summary>
        [Display(Name = "Sol_FileInfo_Resolution", ResourceType = typeof(Properties.Resources))]
        public string ImageSizeDisplay => _imageWidth > 0 && _imageHeight > 0
            ? $"{_imageWidth} × {_imageHeight}"
            : Properties.Resources.Sol_FileInfo_Unknown;

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
        [Display(Name = "Sol_FileInfo_Exists", ResourceType = typeof(Properties.Resources))]
        public bool FileExists
        {
            get => _fileExists;
            private set { _fileExists = value; OnPropertyChanged(); }
        }
        private bool _fileExists;

        /// <summary>
        /// 图像信息提示（用于ToolTip）
        /// </summary>
        public string ImageInfoTooltip =>
            $"{Properties.Resources.Sol_FileInfo_FileNameLabel} {FileName}\n" +
            $"{Properties.Resources.Sol_FileInfo_PathLabel} {FilePath}\n" +
            $"{Properties.Resources.Sol_FileInfo_SizeLabel} {FileSizeDisplay}\n" +
            $"{Properties.Resources.Sol_FileInfo_ResLabel} {ImageSizeDisplay}\n" +
            $"{Properties.Resources.Sol_FileInfo_ModLabel} {LastModified:yyyy-MM-dd HH:mm:ss}";

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
                    // 使用ProcessStartInfo避免命令注入，并验证路径
                    var fullPath = Path.GetFullPath(_filePath);
                    if (!fullPath.Contains("..") && File.Exists(fullPath))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{fullPath}\"",
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(startInfo);
                    }
                }
                else if (System.IO.Directory.Exists(Directory))
                {
                    var fullDir = Path.GetFullPath(Directory);
                    if (!fullDir.Contains("..") && System.IO.Directory.Exists(fullDir))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = fullDir,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(startInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OpenInExplorer Error: {ex.Message}");
            }
        }
    }
}
