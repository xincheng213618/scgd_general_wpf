using ColorVision.ImageEditor.Abstractions;
using Newtonsoft.Json;
using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
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
            CopyGray32FloatToGray16(bitmapSource, writeableBitmap);
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
                WriteableBitmap? currentBitmap = decodedImage.Metadata.ColorVisionParameters == null ? context.ImageView.ViewBitmapSource as WriteableBitmap : null;
                WriteableBitmap writeableBitmap;

                // 测量型 Gray32Float 通常不是可直接显示的归一化范围；Gray16 仅作为显示代理，
                // 测量和无损导出仍持有原始浮点样本，不能从此显示代理反推。
                if (source.Format == PixelFormats.Gray32Float && TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen)
                {
                    writeableBitmap = GetReusableBitmap(currentBitmap, source.PixelWidth, source.PixelHeight, PixelFormats.Gray16, null);
                    CopyGray32FloatToGray16(source, writeableBitmap);
                }
                else
                {
                    writeableBitmap = GetReusableBitmap(currentBitmap, source.PixelWidth, source.PixelHeight, source.Format, source.Palette);
                    CopyPixels(source, writeableBitmap);
                }

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

        private static WriteableBitmap GetReusableBitmap(
            WriteableBitmap? currentBitmap,
            int width,
            int height,
            PixelFormat pixelFormat,
            BitmapPalette? palette)
        {
            if (currentBitmap != null && !currentBitmap.IsFrozen &&
                currentBitmap.PixelWidth == width &&
                currentBitmap.PixelHeight == height &&
                currentBitmap.Format == pixelFormat &&
                Math.Abs(currentBitmap.DpiX - 96) <= 0.01 &&
                Math.Abs(currentBitmap.DpiY - 96) <= 0.01 &&
                PalettesEqual(currentBitmap.Palette, palette))
            {
                return currentBitmap;
            }

            return new WriteableBitmap(width, height, 96, 96, pixelFormat, palette);
        }

        private static void CopyPixels(BitmapSource source, WriteableBitmap target)
        {
            target.Lock();
            try
            {
                int bufferSize = checked(target.BackBufferStride * target.PixelHeight);
                source.CopyPixels(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), target.BackBuffer, bufferSize, target.BackBufferStride);
                target.AddDirtyRect(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight));
            }
            finally
            {
                target.Unlock();
            }
        }

        private static void CopyGray32FloatToGray16(BitmapSource source, WriteableBitmap target)
        {
            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int rowsPerChunk = Math.Max(1, Math.Min(height, ConversionBufferPixelCount / Math.Max(1, width)));
            int bufferLength = checked(width * rowsPerChunk);
            int sourceStride = checked(width * sizeof(float));
            int targetStride = checked(width * sizeof(ushort));
            float[] floatPixels = ArrayPool<float>.Shared.Rent(bufferLength);
            ushort[] ushortPixels = ArrayPool<ushort>.Shared.Rent(bufferLength);

            try
            {
                bool hasFiniteValue = false;
                float min = float.MaxValue;
                float max = float.MinValue;

                for (int top = 0; top < height; top += rowsPerChunk)
                {
                    int rowCount = Math.Min(rowsPerChunk, height - top);
                    int pixelCount = checked(width * rowCount);
                    source.CopyPixels(new Int32Rect(0, top, width, rowCount), floatPixels, sourceStride, 0);

                    for (int index = 0; index < pixelCount; index++)
                    {
                        float value = floatPixels[index];
                        if (!float.IsFinite(value))
                            continue;

                        hasFiniteValue = true;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }

                double range = hasFiniteValue ? (double)max - min : 0;
                bool hasRange = range > 0 && double.IsFinite(range);

                for (int top = 0; top < height; top += rowsPerChunk)
                {
                    int rowCount = Math.Min(rowsPerChunk, height - top);
                    int pixelCount = checked(width * rowCount);
                    source.CopyPixels(new Int32Rect(0, top, width, rowCount), floatPixels, sourceStride, 0);

                    for (int index = 0; index < pixelCount; index++)
                    {
                        float value = floatPixels[index];
                        if (!float.IsFinite(value) || !hasRange)
                        {
                            ushortPixels[index] = 0;
                        }
                        else
                        {
                            double normalized = Math.Clamp(((double)value - min) / range, 0, 1);
                            ushortPixels[index] = (ushort)(normalized * ushort.MaxValue);
                        }
                    }

                    target.WritePixels(new Int32Rect(0, top, width, rowCount), ushortPixels, targetStride, 0);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(floatPixels);
                ArrayPool<ushort>.Shared.Return(ushortPixels);
            }
        }

        private static bool PalettesEqual(BitmapPalette? left, BitmapPalette? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null || left.Colors.Count != right.Colors.Count)
                return false;

            for (int index = 0; index < left.Colors.Count; index++)
            {
                if (left.Colors[index] != right.Colors[index])
                    return false;
            }
            return true;
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
