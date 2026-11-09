using ColorVision.ImageEditor.Abstractions;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".tif|.tiff")]
    public record class Opentif(EditorContext EditorContext) : IAsyncImageOpen, IImageOpenContentLifetime
    {
        private const int ConversionBufferPixelCount = 256 * 1024;
        private readonly object _decodeSync = new();
        private Task _decodeTail = Task.CompletedTask;
        private long _latestRequestId;
        private readonly object _requestSync = new();
        private CancellationTokenSource? _pendingRequest;

        public static int GetChannelCount(BitmapSource source)
        {
            PixelFormat format = source.Format;

            if (format == PixelFormats.Bgr24)
            {
                return 3; // BGR
            }
            else if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32)
            {
                return 4; // BGRA
            }
            else if (format == PixelFormats.Gray8)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Gray16)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Gray32Float)
            {
                return 1; // 灰度
            }
            else if (format == PixelFormats.Rgb24)
            {
                return 3; // RGB
            }
            else if (format == PixelFormats.Rgb48)
            {
                return 3; // RGB
            }
            else if (format == PixelFormats.Rgba64)
            {
                return 4; // RGBA 16位
            }
            else
            {
                throw new NotSupportedException("Unsupported pixel format");
            }
        }

        public static WriteableBitmap ConvertGray32FloatToBitmapSource(BitmapSource bitmapSource)
        {
            if (bitmapSource.Format != PixelFormats.Gray32Float)
                throw new ArgumentException("Bitmap source must use Gray32Float.", nameof(bitmapSource));

            WriteableBitmap writeableBitmap = new(
                bitmapSource.PixelWidth,
                bitmapSource.PixelHeight,
                96,
                96,
                PixelFormats.Gray16,
                null);
            writeableBitmap.Lock();
            try
            {
                CopyGray32FloatToGray16(bitmapSource, writeableBitmap.BackBuffer, writeableBitmap.BackBufferStride, default);
                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, writeableBitmap.PixelWidth, writeableBitmap.PixelHeight));
            }
            finally { writeableBitmap.Unlock(); }
            return writeableBitmap;
        }

        public async void OpenImage(EditorContext context, string? filePath)
        {
            try { await OpenImageAsync(context, filePath); }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Legacy fire-and-forget callers have no completion channel. */ }
        }

        public Task ReleaseContentAsync(bool reuseBuffers)
        {
            Interlocked.Increment(ref _latestRequestId);
            lock (_requestSync) _pendingRequest?.Cancel();
            return _decodeTail;
        }

        public async Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            if (!File.Exists(filePath)) throw new FileNotFoundException("Image file does not exist.", filePath);

            string requestedFilePath = filePath;
            long requestId = Interlocked.Increment(ref _latestRequestId);

            FileInfo fileInfo = new(requestedFilePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, requestedFilePath, nameof(Opentif), "打开器接收到的源文件路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(Opentif), "当前文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(Opentif), "当前文件大小（字节）");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, fileInfo.CreationTime, nameof(Opentif), "当前文件创建时间");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, fileInfo.LastWriteTime, nameof(Opentif), "当前文件修改时间");

            using CancellationTokenSource requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_requestSync)
            {
                _pendingRequest?.Cancel();
                _pendingRequest = requestCancellation;
            }
            cancellationToken = requestCancellation.Token;

            TaskCompletionSource decodeTurn = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task previousDecode;
            lock (_decodeSync)
            {
                previousDecode = _decodeTail;
                _decodeTail = decodeTurn.Task;
            }

            await previousDecode;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);

                DecodedImage decodedImage = await Task.Run(() => DecodeImage(requestedFilePath), cancellationToken);

                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);

                BitmapSource source = decodedImage.BitmapSource;

                // Providers may await or cancel before publication; keep the display staging private.
                // 测量型 Gray32Float 通常不是可直接显示的归一化范围；Gray16 仅作为显示代理，
                // 测量和无损导出仍持有原始浮点样本，不能从此显示代理反推。
                bool normalizeFloat = source.Format == PixelFormats.Gray32Float && TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen;
                WriteableBitmap writeableBitmap = await PrepareDisplayBitmapAsync(source, 96, 96, normalizeFloat, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);

                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, writeableBitmap.PixelWidth, nameof(Opentif), "位图像素宽度");
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, writeableBitmap.PixelHeight, nameof(Opentif), "位图像素高度");
                ApplyMetadata(context, decodedImage.Metadata);

                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);
                await context.ImageView.PublishDecodedImageContentAsync(
                    new DecodedImageContent(decodedImage.Metadata.ColorVisionParameters == null && source.Format != PixelFormats.Gray32Float ? writeableBitmap : source, decodedImage.Metadata.ColorVisionParameters, requestedFilePath),
                    writeableBitmap, cancellationToken);
                context.ImageView.UpdateZoomAndScale();
            }
            finally
            {
                lock (_requestSync)
                {
                    if (ReferenceEquals(_pendingRequest, requestCancellation)) _pendingRequest = null;
                }
                decodeTurn.TrySetResult();
            }
        }

        private bool IsCurrentRequest(EditorContext context, string requestedFilePath, long requestId)
        {
            if (requestId != Volatile.Read(ref _latestRequestId))
                return false;

            string? activeFilePath = context.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            return string.Equals(activeFilePath, requestedFilePath, StringComparison.OrdinalIgnoreCase);
        }

        private static DecodedImage DecodeImage(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                throw new InvalidDataException($"TIFF file contains no image frames: {filePath}");

            BitmapFrame frame = decoder.Frames[0];
            ImageMetadata metadata = ReadMetadata(frame.Metadata as BitmapMetadata);
            if (frame.CanFreeze && !frame.IsFrozen)
                frame.Freeze();
            return new DecodedImage(frame, metadata);
        }

        internal static async Task<WriteableBitmap> PrepareDisplayBitmapAsync(BitmapSource source, double dpiX, double dpiY,
            bool normalizeFloat, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bounds = new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight);
            WriteableBitmap target = new(bounds.Width, bounds.Height, dpiX, dpiY,
                normalizeFloat ? PixelFormats.Gray16 : source.Format, normalizeFloat ? null : source.Palette);
            // Only the UI thread touches the mutable bitmap. The worker fills its
            // locked, unpublished buffer and must finish before it can be unlocked.
            target.Lock();
            try
            {
                IntPtr buffer = target.BackBuffer;
                int stride = target.BackBufferStride;
                await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (normalizeFloat)
                        CopyGray32FloatToGray16(source, buffer, stride, cancellationToken);
                    else
                        CopyDisplayPixels(source, bounds, buffer, stride, cancellationToken);
                }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                target.AddDirtyRect(bounds);
            }
            finally { target.Unlock(); }
            return target;
        }

        private static unsafe void CopyDisplayPixels(BitmapSource source, Int32Rect bounds, IntPtr buffer, int stride,
            CancellationToken cancellationToken)
        {
            const int CopyBlockBytes = 8 * 1024 * 1024;
            int rowsPerBlock = Math.Max(1, CopyBlockBytes / stride);
            for (int top = 0; top < bounds.Height;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int rows = Math.Min(rowsPerBlock, bounds.Height - top);
                Int32Rect block = new(0, top, bounds.Width, rows);
                source.CopyPixels(block, (IntPtr)((byte*)buffer + (long)top * stride), checked(stride * rows), stride);
                top += rows;
            }
        }

        private static unsafe void CopyGray32FloatToGray16(BitmapSource source, IntPtr target, int targetStride, CancellationToken cancellationToken)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int rowsPerChunk = Math.Max(1, Math.Min(height, ConversionBufferPixelCount / Math.Max(1, width)));
            int bufferLength = checked(width * rowsPerChunk);
            int sourceStride = checked(width * sizeof(float));
            float[] floatPixels = ArrayPool<float>.Shared.Rent(bufferLength);

            try
            {
                float min = float.PositiveInfinity;
                float max = float.NegativeInfinity;

                for (int top = 0; top < height; top += rowsPerChunk)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int rowCount = Math.Min(rowsPerChunk, height - top);
                    int pixelCount = checked(width * rowCount);
                    source.CopyPixels(new Int32Rect(0, top, width, rowCount), floatPixels, sourceStride, 0);
                    AccumulateFiniteRange(floatPixels.AsSpan(0, pixelCount), ref min, ref max);
                }

                double range = (double)max - min;
                bool hasRange = range > 0 && double.IsFinite(range);

                for (int top = 0; top < height; top += rowsPerChunk)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int rowCount = Math.Min(rowsPerChunk, height - top);
                    if (hasRange)
                        source.CopyPixels(new Int32Rect(0, top, width, rowCount), floatPixels, sourceStride, 0);

                    for (int row = 0; row < rowCount; row++)
                    {
                        Span<ushort> output = new((byte*)target + (long)(top + row) * targetStride, width);
                        if (hasRange) NormalizeRow(floatPixels.AsSpan(row * width, width), output, min, range);
                        else output.Clear();
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(floatPixels);
            }
        }

        private static void AccumulateFiniteRange(ReadOnlySpan<float> samples, ref float min, ref float max)
        {
            int index = 0;
            if (Avx2.IsSupported && samples.Length >= Vector256<float>.Count)
            {
                Vector256<float> positiveInfinity = Vector256.Create(float.PositiveInfinity);
                Vector256<float> negativeInfinity = Vector256.Create(float.NegativeInfinity);
                Vector256<float> minimum = positiveInfinity;
                Vector256<float> maximum = negativeInfinity;
                ref float first = ref MemoryMarshal.GetReference(samples);
                for (; index <= samples.Length - Vector256<float>.Count; index += Vector256<float>.Count)
                {
                    Vector256<float> values = Vector256.LoadUnsafe(ref first, (nuint)index);
                    Vector256<float> finite = Vector256.IsFinite(values);
                    minimum = Vector256.MinNative(minimum, Vector256.ConditionalSelect(finite, values, positiveInfinity));
                    maximum = Vector256.MaxNative(maximum, Vector256.ConditionalSelect(finite, values, negativeInfinity));
                }
                for (int lane = 0; lane < Vector256<float>.Count; lane++)
                {
                    min = MathF.Min(min, minimum.GetElement(lane));
                    max = MathF.Max(max, maximum.GetElement(lane));
                }
            }

            for (; index < samples.Length; index++)
            {
                float value = samples[index];
                if (!float.IsFinite(value)) continue;
                if (value < min) min = value;
                if (value > max) max = value;
            }
        }

        private static void NormalizeRow(ReadOnlySpan<float> input, Span<ushort> output, float min, double range)
        {
            int x = 0;
            if (Avx2.IsSupported && Sse41.IsSupported && input.Length >= Vector256<float>.Count)
            {
                Vector256<float> replacement = Vector256.Create(min);
                Vector256<double> minimum = Vector256.Create((double)min);
                Vector256<double> span = Vector256.Create(range);
                ref float firstInput = ref MemoryMarshal.GetReference(input);
                ref ushort firstOutput = ref MemoryMarshal.GetReference(output);
                for (; x <= input.Length - Vector256<float>.Count; x += Vector256<float>.Count)
                {
                    Vector256<float> values = Vector256.LoadUnsafe(ref firstInput, (nuint)x);
                    values = Vector256.ConditionalSelect(Vector256.IsFinite(values), values, replacement);
                    Vector128<int> lower = QuantizeGray16(values.GetLower(), minimum, span);
                    Vector128<int> upper = QuantizeGray16(values.GetUpper(), minimum, span);
                    Sse41.PackUnsignedSaturate(lower, upper).StoreUnsafe(ref firstOutput, (nuint)x);
                }
            }

            for (; x < input.Length; x++)
            {
                float value = input[x];
                output[x] = float.IsFinite(value)
                    ? (ushort)(Math.Clamp(((double)value - min) / range, 0, 1) * ushort.MaxValue)
                    : (ushort)0;
            }
        }

        private static Vector128<int> QuantizeGray16(Vector128<float> values, Vector256<double> minimum, Vector256<double> range)
        {
            // Keep the scalar formula's double precision and truncation at Gray16 boundaries.
            Vector256<double> normalized = Avx.Divide(Avx.Subtract(Avx.ConvertToVector256Double(values), minimum), range);
            normalized = Avx.Min(Avx.Max(normalized, Vector256<double>.Zero), Vector256.Create(1d));
            return Avx.ConvertToVector128Int32WithTruncation(Avx.Multiply(normalized, Vector256.Create((double)ushort.MaxValue)));
        }

        private static ImageMetadata ReadMetadata(BitmapMetadata? metadata)
        {
            if (metadata == null)
                return ImageMetadata.Empty;

            ColorVisionTiffParameters.TryRead(metadata, out ColorVisionTiffParameters? parameters);
            try
            {
                return new ImageMetadata(
                    metadata.CameraModel,
                    metadata.CameraManufacturer,
                    metadata.DateTaken,
                    metadata.ApplicationName,
                    metadata.Title,
                    metadata.Subject,
                    parameters);
            }
            catch
            {
                return new ImageMetadata(null, null, null, null, null, null, parameters);
            }
        }

        private static void ApplyMetadata(EditorContext context, ImageMetadata metadata)
        {
            if (metadata.CameraModel != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.CameraModel, metadata.CameraModel, nameof(Opentif), "EXIF 相机型号");
            if (metadata.CameraManufacturer != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.CameraManufacturer, metadata.CameraManufacturer, nameof(Opentif), "EXIF 相机厂商");
            if (metadata.DateTaken != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.DateTaken, metadata.DateTaken, nameof(Opentif), "EXIF 拍摄时间");
            if (metadata.ApplicationName != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ApplicationName, metadata.ApplicationName, nameof(Opentif), "EXIF 应用程序名");
            if (metadata.Title != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageTitle, metadata.Title, nameof(Opentif), "EXIF 标题");
            if (metadata.Subject != null)
                context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageSubject, metadata.Subject, nameof(Opentif), "EXIF 主题");
            if (metadata.ColorVisionParameters != null)
                ApplyColorVisionMetadata(context.Config, metadata.ColorVisionParameters);
        }

        internal static void ApplyColorVisionMetadata(ImageViewConfig config, ColorVisionTiffParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(config);
            ArgumentNullException.ThrowIfNull(parameters);

            const string owner = nameof(Opentif);
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionSchema, parameters.Schema, owner, "ColorVision TIFF 参数架构版本");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionSourceType, parameters.SourceType, owner, "导出源文件类型");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionExportedChannel, parameters.ExportedChannel, owner, "导出的源图或测量通道");
            if (parameters.InputFileName != null)
                config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionInputFileName, parameters.InputFileName, owner, "参数所属的输入文件名");
            if (parameters.AssociatedSourceFileName != null)
                config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionAssociatedSourceFileName, parameters.AssociatedSourceFileName, owner, "CVCIE 关联源文件名");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionFileVersion, parameters.FileVersion, owner, "CVRAW/CVSRC/CVCIE 文件格式版本");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionRows, parameters.Rows, owner, "源文件图像行数");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionCols, parameters.Cols, owner, "源文件图像列数");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionBpp, parameters.Bpp, owner, "源文件单通道位深");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionSourceChannels, parameters.SourceChannels, owner, "源文件通道数");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionNdPort, parameters.NdPort, owner, "源文件 NDPort");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionGain, parameters.Gain, owner, "采集增益");
            config.SetImageMetadata(ImageViewPropertyKeys.ColorVisionExposure, parameters.Exposure, owner, "各源通道曝光参数");
            config.SetImageMetadata("ColorVision.PixelRole", parameters.PixelRole ?? "record-only", owner, "像素用途：校正输入、已计算测量通道或显示记录；参数本身不代表可测量能力");
            if (parameters.PixelLayout != null)
                config.SetImageMetadata("ColorVision.PixelLayout", parameters.PixelLayout, owner, "导出样本的存储布局");
            if (parameters.PixelSha256 != null)
                config.SetImageMetadata("ColorVision.PixelSha256", parameters.PixelSha256, owner, "与校正记录绑定的原始样本摘要");
            config.SetImageMetadata("ColorVision.PixelChannels", parameters.PixelChannels, owner, "导出样本通道数");
            if (parameters.MeasurementChannel != null)
                config.SetImageMetadata("ColorVision.MeasurementChannel", parameters.MeasurementChannel, owner, "已计算的实际测量通道身份");
            if (parameters.Calibration != null)
                config.SetImageMetadata("ColorVision.Calibration", parameters.Calibration.ToString(Formatting.None), owner, "实际执行校正参数的只读记录（包含矩阵、曝光等）；能否重放由当前内容能力验证");
            if (parameters.Acquisition != null)
            {
                config.SetImageMetadata("ColorVision.Acquisition", JsonConvert.SerializeObject(parameters.Acquisition), owner, "原始相机及采集记录");
                if (parameters.Acquisition.OpticalCenter is { } center && double.IsFinite(center.X) && double.IsFinite(center.Y))
                {
                    config.SetImageMetadata("ColorVision.Camera.OpticalCenterX", center.X, owner, "采集时的光心 X，未翻转图像像素坐标");
                    config.SetImageMetadata("ColorVision.Camera.OpticalCenterY", center.Y, owner, "采集时的光心 Y，未翻转图像像素坐标");
                }
                if (parameters.Acquisition.Camera is { } camera)
                {
                    // CameraModel/CameraManufacturer select ruler calibration profiles. Provenance
                    // must remain informational and must not change the existing drawing calibration.
                    if (camera.Model != null) config.SetImageMetadata("ColorVision.Camera.Model", camera.Model, owner);
                    if (camera.Manufacturer != null) config.SetImageMetadata("ColorVision.Camera.Manufacturer", camera.Manufacturer, owner);
                    if (camera.CameraId != null) config.SetImageMetadata("ColorVision.Camera.Id", camera.CameraId, owner);
                    if (camera.DeviceName != null) config.SetImageMetadata("ColorVision.Camera.Name", camera.DeviceName, owner);
                    if (camera.DeviceCode != null) config.SetImageMetadata("ColorVision.Camera.DeviceCode", camera.DeviceCode, owner);
                }
            }
            if (parameters.CalibrationSummary != null)
                config.SetImageMetadata("ColorVision.CalibrationSummary", JsonConvert.SerializeObject(parameters.CalibrationSummary), owner, "校正摘要及来源，不包含校正矩阵");
            if (parameters.RenderedOutput != null)
                config.SetImageMetadata("ColorVision.RenderedOutput", JsonConvert.SerializeObject(parameters.RenderedOutput), owner, "渲染输出的尺寸与像素格式");
        }

        private sealed record DecodedImage(BitmapSource BitmapSource, ImageMetadata Metadata);

        private sealed record ImageMetadata(
            string? CameraModel,
            string? CameraManufacturer,
            string? DateTaken,
            string? ApplicationName,
            string? Title,
            string? Subject,
            ColorVisionTiffParameters? ColorVisionParameters)
        {
            public static ImageMetadata Empty { get; } = new(null, null, null, null, null, null, null);
        }
    }
}
