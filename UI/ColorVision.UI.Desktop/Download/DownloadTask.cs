using ColorVision.Common.MVVM;

namespace ColorVision.UI.Desktop.Download
{
    public class DownloadTask : ViewModelBase
    {

        public int Id { get => _Id; set { _Id = value; OnPropertyChanged(); } }
        private int _Id;

        public string Url { get => _Url; set { _Url = value; OnPropertyChanged(); } }
        private string _Url = string.Empty;

        public string FileName { get => _FileName; set { _FileName = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileIconSource)); } }
        private string _FileName = string.Empty;

        public System.Windows.Media.ImageSource? FileIconSource
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(_FileName))
                    {
                        string ext = System.IO.Path.GetExtension(_FileName);
                        if (!string.IsNullOrEmpty(ext))
                            return ColorVision.Common.NativeMethods.FileIcon.GetFileIconImageSource("file" + ext);
                    }
                }
                catch (Exception ex)
                {
                    log4net.LogManager.GetLogger(nameof(DownloadTask)).Debug($"Failed to get file icon for {_FileName}: {ex.Message}");
                }
                return null;
            }
        }

        public string SavePath { get => _SavePath; set { _SavePath = value; OnPropertyChanged(); } }
        private string _SavePath = string.Empty;

        public DownloadStatus Status { get => _Status; set { _Status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(IsActiveDownloading)); OnPropertyChanged(nameof(IsCompleted)); OnPropertyChanged(nameof(IsPaused)); OnPropertyChanged(nameof(IsWaitingOrFailed)); OnPropertyChanged(nameof(FileSizeDisplayText)); } }
        private DownloadStatus _Status;

        public bool IsDownloading => Status == DownloadStatus.Downloading || Status == DownloadStatus.Waiting;
        public bool IsActiveDownloading => Status == DownloadStatus.Downloading;
        public bool IsCompleted => Status == DownloadStatus.Completed || Status == DownloadStatus.FileDeleted;
        public bool IsPaused => Status == DownloadStatus.Paused;
        public bool IsWaitingOrFailed => Status == DownloadStatus.Waiting || Status == DownloadStatus.Failed;

        public string StatusText => Status switch
        {
            DownloadStatus.Waiting => Properties.Resources.Waiting,
            DownloadStatus.Downloading => Properties.Resources.Downloading,
            DownloadStatus.Completed => Properties.Resources.Completed,
            DownloadStatus.Failed => Properties.Resources.Failed,
            DownloadStatus.Paused => Properties.Resources.Paused,
            DownloadStatus.FileDeleted => Properties.Resources.FileDeleted,
            _ => Status.ToString()
        };

        public int ProgressValue { get => _ProgressValue; set { _ProgressValue = value; OnPropertyChanged(); } }
        private int _ProgressValue;

        public long TotalBytes { get => _TotalBytes; set { _TotalBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalBytesText)); OnPropertyChanged(nameof(FileSizeDisplayText)); } }
        private long _TotalBytes;

        public long DownloadedBytes { get => _DownloadedBytes; set { _DownloadedBytes = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadedBytesText)); OnPropertyChanged(nameof(FileSizeDisplayText)); } }
        private long _DownloadedBytes;

        public string SpeedText { get => _SpeedText; set { _SpeedText = value; OnPropertyChanged(); } }
        private string _SpeedText = string.Empty;

        public string? ErrorMessage { get => _ErrorMessage; set { _ErrorMessage = value; OnPropertyChanged(); } }
        private string? _ErrorMessage;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; OnPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;

        public string TotalBytesText => FormatBytes(TotalBytes);
        public string DownloadedBytesText => FormatBytes(DownloadedBytes);

        public string FileSizeDisplayText
        {
            get
            {
                if (Status == DownloadStatus.Completed || Status == DownloadStatus.FileDeleted)
                    return FormatBytes(TotalBytes);
                if (IsDownloading && TotalBytes > 0)
                    return $"{FormatBytes(DownloadedBytes)} / {FormatBytes(TotalBytes)}";
                if (TotalBytes > 0)
                    return FormatBytes(TotalBytes);
                return string.Empty;
            }
        }

        /// <summary>
        /// The aria2c GID for this download (used with JSON-RPC)
        /// </summary>
        public string? Gid { get; set; }

        public CancellationTokenSource? CancellationTokenSource { get; set; }

        /// <summary>
        /// Per-task completion callback. When set, the global ShowCompletedNotification is skipped for this task.
        /// </summary>
        public Action<DownloadTask>? OnCompletedCallback { get; set; }

        /// <summary>
        /// HTTP authorization (user:password) for authenticated downloads. Persisted for resume/retry.
        /// </summary>
        public string? Authorization { get; set; }

        /// <summary>
        /// Local source file reused for this task. Used to support copy retry/cancel.
        /// </summary>
        public string? LocalReuseSourcePath { get; set; }

        /// <summary>
        /// Whether the local reuse source must be revalidated against the remote resource before copy.
        /// </summary>
        public bool LocalReuseRequiresRemoteValidation { get; set; }

        public static string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F2} {sizes[order]}";
        }

        public static string FormatSpeed(long bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "0 B/s";
            if (bytesPerSecond < 1024) return $"{bytesPerSecond} B/s";
            if (bytesPerSecond < 1024 * 1024) return $"{bytesPerSecond / 1024.0:F1} KB/s";
            if (bytesPerSecond < 1024L * 1024 * 1024) return $"{bytesPerSecond / 1024.0 / 1024.0:F2} MB/s";
            return $"{bytesPerSecond / 1024.0 / 1024.0 / 1024.0:F2} GB/s";
        }
    }
}
