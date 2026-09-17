using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Core
{
    public enum FileFusionMode { Auto, CPU, GPU, GPUAsync }

    /// <summary>A frozen, managed snapshot; no native allocation escapes the execution boundary.</summary>
    public sealed record FileFusionResult(
        BitmapSource Image,
        IReadOnlyList<string> Files,
        FileFusionMode ActualMode,
        long ValidationMs,
        long NativeMs,
        long ConvertMs,
        long TotalMs);

    /// <summary>Shared file-stack execution for interactive tools and local Flow nodes.</summary>
    public sealed class FileFusion
    {
        internal delegate int NativeFusion(string json, FileFusionMode mode, out HImage image);
        private static readonly SemaphoreSlim ExecutionGate = new(1, 1);
        private readonly NativeFusion executeNative;
        private readonly Func<bool> cudaAvailable;
        public static FileFusion Default { get; } = new(ExecuteNative, () => ImageCompute.UseCuda);

        internal FileFusion(NativeFusion executeNative, Func<bool> cudaAvailable)
        {
            this.executeNative = executeNative;
            this.cudaAvailable = cudaAvailable;
        }

        public static bool IsSupportedFile(string path) => Path.GetExtension(path).ToLowerInvariant()
            is ".bmp" or ".jpeg" or ".jpg" or ".png" or ".tif" or ".tiff";

        public static string[] GetFolderFiles(string directory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            return Directory.EnumerateFiles(Path.GetFullPath(directory))
                .Where(IsSupportedFile)
                .OrderBy(path => Path.GetFileName(path), Comparer<string>.Create(CompareFileNames))
                .ToArray();
        }

        private static int CompareFileNames(string? left, string? right) => StrCmpLogicalW(left ?? "", right ?? "");

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string left, string right);

        public Task<FileFusionResult> ExecuteAsync(IEnumerable<string> files, FileFusionMode mode = FileFusionMode.Auto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(files);
            string[] snapshot = files.ToArray();
            return Task.Run(() => Execute(snapshot, mode, cancellationToken), cancellationToken);
        }

        public FileFusionResult Execute(IEnumerable<string> files, FileFusionMode mode = FileFusionMode.Auto, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(files);
            if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            string[] snapshot = files.Select(path =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);
                return Path.GetFullPath(path);
            }).ToArray();
            if (snapshot.Length < 2) throw new ArgumentException("景深融合至少需要两张图片。", nameof(files));
            if (snapshot.Distinct(StringComparer.OrdinalIgnoreCase).Count() != snapshot.Length)
                throw new ArgumentException("景深融合列表包含重复文件。", nameof(files));

            var total = Stopwatch.StartNew();
            ExecutionGate.Wait(cancellationToken);
            List<FileStream> inputLocks = new();
            HImage output = default;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileFusionMode actualMode = ResolveMode(mode, snapshot.Length);
                var stage = Stopwatch.StartNew();
                int width = 0, height = 0, channels = 0;
                foreach (string path in snapshot)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsSupportedFile(path)) throw new NotSupportedException($"不支持的融合文件类型：{path}");
                    // Keep read-only handles until native decoding finishes: validated files cannot
                    // be replaced or written while the legacy path-based ABI reads them again.
                    var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    inputLocks.Add(stream);
                    BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
                    BitmapFrame frame = decoder.Frames[0];
                    int frameChannels = frame.Format == PixelFormats.Gray8 ? 1
                        : frame.Format == PixelFormats.Bgr24 || frame.Format == PixelFormats.Rgb24 ? 3 : 0;
                    if (frameChannels == 0)
                        throw new NotSupportedException($"景深融合仅支持 8-bit 灰度或 RGB/BGR 图片：{path}（{frame.Format}）。");
                    if (width != 0 && (frame.PixelWidth != width || frame.PixelHeight != height || frameChannels != channels))
                        throw new ArgumentException($"融合图片的尺寸和通道数必须一致：{path}", nameof(files));
                    width = frame.PixelWidth;
                    height = frame.PixelHeight;
                    channels = frameChannels;
                }
                long validationMs = stage.ElapsedMilliseconds;
                cancellationToken.ThrowIfCancellationRequested();
                stage.Restart();
                int code = executeNative(JsonSerializer.Serialize(snapshot), actualMode, out output);
                long nativeMs = stage.ElapsedMilliseconds;
                cancellationToken.ThrowIfCancellationRequested();
                if (code != 0) throw new InvalidOperationException($"景深融合失败，模式 {actualMode}，错误码 {code}。");
                if (output.pData == IntPtr.Zero || output.cols != width || output.rows != height || output.depth != 8 || output.channels != channels)
                    throw new InvalidOperationException("景深融合返回了无效的输出图像。");
                stage.Restart();
                WriteableBitmap image = output.ToWriteableBitmap();
                image.Freeze();
                long convertMs = stage.ElapsedMilliseconds;
                cancellationToken.ThrowIfCancellationRequested();
                return new FileFusionResult(image, Array.AsReadOnly(snapshot), actualMode, validationMs, nativeMs, convertMs, total.ElapsedMilliseconds);
            }
            finally
            {
                output.Dispose();
                foreach (FileStream stream in inputLocks) stream.Dispose();
                ExecutionGate.Release();
            }
        }

        private FileFusionMode ResolveMode(FileFusionMode requested, int count)
        {
            if (requested == FileFusionMode.CPU) return requested;
            if (requested == FileFusionMode.Auto) return count >= 5 && cudaAvailable() ? FileFusionMode.GPU : FileFusionMode.CPU;
            if (count < 5) throw new NotSupportedException("GPU 景深融合至少需要五张图片；2–4 张图片请选择 CPU 或自动模式。");
            if (!cudaAvailable()) throw new NotSupportedException("CUDA 不可用，请选择 CPU 或自动模式。");
            return requested;
        }

        private static int ExecuteNative(string json, FileFusionMode mode, out HImage image) => mode switch
        {
            FileFusionMode.CPU => OpenCVMediaHelper.M_Fusion(json, out image),
            FileFusionMode.GPU => OpenCVCuda.CM_Fusion(json, out image),
            FileFusionMode.GPUAsync => OpenCVCuda.CM_Fusion_Async(json, out image),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
