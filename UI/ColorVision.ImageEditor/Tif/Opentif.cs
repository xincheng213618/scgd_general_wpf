using ColorVision.ImageEditor.Abstractions;
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
    public record class Opentif(EditorContext EditorContext) : IImageOpen
    {
        private const int ConversionBufferPixelCount = 256 * 1024;
        private readonly object _decodeSync = new();
        private Task _decodeTail = Task.CompletedTask;
        private long _latestRequestId;

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
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            string requestedFilePath = filePath;
            long requestId = Interlocked.Increment(ref _latestRequestId);

            FileInfo fileInfo = new(requestedFilePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, requestedFilePath, nameof(Opentif), "打开器接收到的源文件路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(Opentif), "当前文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(Opentif), "当前文件大小（字节）");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, fileInfo.CreationTime, nameof(Opentif), "当前文件创建时间");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, fileInfo.LastWriteTime, nameof(Opentif), "当前文件修改时间");

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
                if (!IsCurrentRequest(context, requestedFilePath, requestId))
                    return;

                DecodedImage? decodedImage;
                try
                {
                    decodedImage = await Task.Run(() => DecodeImage(requestedFilePath));
                }
                catch
                {
                    return;
                }

                if (decodedImage == null || !IsCurrentRequest(context, requestedFilePath, requestId))
                    return;

                BitmapSource source = decodedImage.BitmapSource;
                WriteableBitmap? currentBitmap = context.ImageView.ViewBitmapSource as WriteableBitmap;
                WriteableBitmap writeableBitmap;

                // 测量型 Gray32Float 通常不是可直接显示的归一化范围；Gray16 仅作为显示代理，
                // 原始浮点帧在分块映射完成后即可释放，避免长期保留两份大图。
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

                context.ImageView.SetImageSource(writeableBitmap);
                context.ImageView.UpdateZoomAndScale();
            }
            finally
            {
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

        private static DecodedImage? DecodeImage(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            TiffBitmapDecoder decoder = new(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0)
                return null;

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
            if (currentBitmap != null &&
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

            try
            {
                return new ImageMetadata(
                    metadata.CameraModel,
                    metadata.CameraManufacturer,
                    metadata.DateTaken,
                    metadata.ApplicationName,
                    metadata.Title,
                    metadata.Subject);
            }
            catch
            {
                return ImageMetadata.Empty;
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
        }

        private sealed record DecodedImage(BitmapSource BitmapSource, ImageMetadata Metadata);

        private sealed record ImageMetadata(
            string? CameraModel,
            string? CameraManufacturer,
            string? DateTaken,
            string? ApplicationName,
            string? Title,
            string? Subject)
        {
            public static ImageMetadata Empty { get; } = new(null, null, null, null, null, null);
        }
    }
}
