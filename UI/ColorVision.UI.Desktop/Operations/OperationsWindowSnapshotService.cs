using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Interop;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsWindowSnapshotResult
    {
        public string SnapshotId { get; init; } = string.Empty;
        public string FilePath { get; init; } = string.Empty;
        public string Sha256 { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
        public byte[] Data { get; init; } = [];
    }

    public enum OperationsWindowSnapshotLookupStatus
    {
        Available,
        InvalidId,
        NotFound,
        Expired,
        TooLarge,
        UnsupportedFormat,
        ReadFailed,
    }

    public sealed class OperationsWindowSnapshotService
    {
        public const int MaximumDownloadBytes = 1536 * 1024;
        public const string EvidencePrefix = "window-snapshot:";
        public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);

        private const uint PrintWindowRenderFullContent = 0x00000002;
        private readonly object _syncRoot = new();
        private readonly string _directory;
        private readonly Func<DateTimeOffset> _clock;
        private readonly Func<byte[]> _captureProvider;

        public OperationsWindowSnapshotService(
            string? directory = null,
            Func<DateTimeOffset>? clock = null,
            Func<byte[]>? captureProvider = null)
        {
            _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ColorVision", "Operations", "window-snapshots");
            _clock = clock ?? (() => DateTimeOffset.UtcNow);
            _captureProvider = captureProvider ?? CaptureMainWindowJpeg;
        }

        public OperationsWindowSnapshotResult Create()
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(_directory);
                PruneExpiredNoLock();
                byte[] data = _captureProvider();
                ValidateCapturedData(data);

                string snapshotId = Guid.NewGuid().ToString("N");
                string path = ResolvePath(snapshotId);
                DateTimeOffset createdAt = _clock();
                File.WriteAllBytes(path, data);
                File.SetLastWriteTimeUtc(path, createdAt.UtcDateTime);
                return CreateResult(snapshotId, path, data, createdAt, includeData: false);
            }
        }

        public OperationsWindowSnapshotLookupStatus TryTake(string snapshotId, out OperationsWindowSnapshotResult? result)
        {
            result = null;
            if (!IsValidId(snapshotId))
                return OperationsWindowSnapshotLookupStatus.InvalidId;

            lock (_syncRoot)
            {
                string path;
                try
                {
                    path = ResolvePath(snapshotId);
                }
                catch (InvalidOperationException)
                {
                    return OperationsWindowSnapshotLookupStatus.InvalidId;
                }
                if (!File.Exists(path))
                    return OperationsWindowSnapshotLookupStatus.NotFound;

                try
                {
                    FileInfo info = new(path);
                    DateTimeOffset createdAt = new(info.LastWriteTimeUtc);
                    if (_clock() > createdAt.Add(DownloadLifetime))
                    {
                        File.Delete(path);
                        return OperationsWindowSnapshotLookupStatus.Expired;
                    }
                    if (info.Length is <= 0 or > MaximumDownloadBytes)
                        return OperationsWindowSnapshotLookupStatus.TooLarge;

                    byte[] data = File.ReadAllBytes(path);
                    if (!IsJpeg(data))
                        return OperationsWindowSnapshotLookupStatus.UnsupportedFormat;

                    OperationsWindowSnapshotResult available = CreateResult(
                        snapshotId, path, data, createdAt, includeData: true);
                    File.Delete(path);
                    result = available;
                    return OperationsWindowSnapshotLookupStatus.Available;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return OperationsWindowSnapshotLookupStatus.ReadFailed;
                }
            }
        }

        public static bool TryGetSnapshotId(string? evidenceId, out string snapshotId)
        {
            snapshotId = string.Empty;
            if (string.IsNullOrWhiteSpace(evidenceId)
                || !evidenceId.StartsWith(EvidencePrefix, StringComparison.Ordinal))
                return false;
            string value = evidenceId[EvidencePrefix.Length..];
            if (!IsValidId(value))
                return false;
            snapshotId = value;
            return true;
        }

        private static OperationsWindowSnapshotResult CreateResult(
            string snapshotId,
            string path,
            byte[] data,
            DateTimeOffset createdAt,
            bool includeData)
        {
            return new OperationsWindowSnapshotResult
            {
                SnapshotId = snapshotId,
                FilePath = path,
                Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant(),
                SizeBytes = data.LongLength,
                CreatedAt = createdAt,
                ExpiresAt = createdAt.Add(DownloadLifetime),
                Data = includeData ? data : [],
            };
        }

        private void PruneExpiredNoLock()
        {
            DateTimeOffset cutoff = _clock().Subtract(DownloadLifetime);
            foreach (string path in Directory.EnumerateFiles(_directory, "colorvision-window-*.jpg", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff.UtcDateTime)
                        File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // A later create or download can retry cleanup without blocking the requested capture.
                }
            }
        }

        private string ResolvePath(string snapshotId)
        {
            string directory = Path.GetFullPath(_directory);
            string path = Path.GetFullPath(Path.Combine(directory, $"colorvision-window-{snapshotId}.jpg"));
            if (!string.Equals(Path.GetDirectoryName(path), directory, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("invalid_window_snapshot_id");
            return path;
        }

        private static void ValidateCapturedData(byte[] data)
        {
            if (data.Length is <= 0 or > MaximumDownloadBytes)
                throw new InvalidOperationException("window_snapshot_size_rejected");
            if (!IsJpeg(data))
                throw new InvalidOperationException("window_snapshot_format_rejected");
        }

        private static bool IsValidId(string value) => value.Length == 32 && value.All(char.IsAsciiHexDigit);

        private static bool IsJpeg(byte[] data) => data.Length >= 4
            && data[0] == 0xff && data[1] == 0xd8
            && data[^2] == 0xff && data[^1] == 0xd9;

        private static byte[] CaptureMainWindowJpeg()
        {
            var dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("当前没有可用的 WPF 调度器。");
            return dispatcher.CheckAccess()
                ? CaptureMainWindowJpegOnUiThread()
                : dispatcher.Invoke(CaptureMainWindowJpegOnUiThread);
        }

        private static byte[] CaptureMainWindowJpegOnUiThread()
        {
            Window window = Application.Current?.MainWindow
                ?? throw new InvalidOperationException("当前没有主窗口。");
            if (!window.IsVisible || window.WindowState == WindowState.Minimized)
                throw new InvalidOperationException("请先显示 ColorVision 主窗口，再确认采集快照。");

            nint handle = new WindowInteropHelper(window).EnsureHandle();
            if (!GetWindowRect(handle, out NativeRect rect))
                throw new InvalidOperationException("无法读取 ColorVision 主窗口边界。");
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width is <= 0 or > 16384 || height is <= 0 or > 16384)
                throw new InvalidOperationException("ColorVision 主窗口尺寸无效。");

            using Bitmap captured = new(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(captured))
            {
                nint hdc = graphics.GetHdc();
                try
                {
                    if (!PrintWindow(handle, hdc, PrintWindowRenderFullContent))
                        throw new InvalidOperationException("Windows 未能渲染 ColorVision 主窗口。");
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            using Bitmap bounded = ResizeToMaximumDimension(captured, 1280);
            foreach (long quality in new long[] { 82, 68, 52, 40 })
            {
                byte[] data = EncodeJpeg(bounded, quality);
                if (data.Length <= MaximumDownloadBytes)
                    return data;
            }
            throw new InvalidOperationException("window_snapshot_size_rejected");
        }

        private static Bitmap ResizeToMaximumDimension(Bitmap source, int maximumDimension)
        {
            double scale = Math.Min(1d, maximumDimension / (double)Math.Max(source.Width, source.Height));
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            Bitmap target = new(width, height, PixelFormat.Format24bppRgb);
            target.SetResolution(source.HorizontalResolution, source.VerticalResolution);
            using Graphics graphics = Graphics.FromImage(target);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.DrawImage(source, 0, 0, width, height);
            return target;
        }

        private static byte[] EncodeJpeg(Bitmap bitmap, long quality)
        {
            ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders()
                .First(item => item.FormatID == ImageFormat.Jpeg.Guid);
            using EncoderParameters parameters = new(1);
            parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            using MemoryStream stream = new();
            bitmap.Save(stream, codec, parameters);
            return stream.ToArray();
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(nint hwnd, nint hdcBlt, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(nint hwnd, out NativeRect rect);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
