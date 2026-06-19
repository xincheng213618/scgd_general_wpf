using ColorVision.ImageEditor.Abstractions;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    [FileExtension(".tif|.tiff")]
    public record class Opentif(EditorContext EditorContext) : IImageOpen
    {
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
            // 确保图像数据已加载
            bitmapSource.Freeze();

            // 获取图像的宽度和高度
            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;

            // 创建一个新的32位浮点数组来存储像素数据
            float[] floatPixels = new float[width * height];

            // 从BitmapSource中读取像素数据
            bitmapSource.CopyPixels(floatPixels, width * 4, 0);

            // 创建一个新的16位整数数组来存储转换后的像素值
            ushort[] ushortPixels = new ushort[width * height];

            // 1. 遍历一遍，找出实际的最大值和最小值
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < floatPixels.Length; i++)
            {
                float v = floatPixels[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            // 计算极差，防止全纯色图导致除以0
            float range = max - min;
            if (range <= 0) range = 1f;

            // 2. 将浮点值根据最大最小值动态映射到 0-65535 范围的16位整数
            for (int i = 0; i < floatPixels.Length; i++)
            {
                // 归一化到 0.0 - 1.0 之间
                float normalized = (floatPixels[i] - min) / range;

                // 映射到 16 位整数 (0 - 65535)
                ushortPixels[i] = (ushort)(normalized * 65535f);
            }

            // 创建一个新的WriteableBitmap对象
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Gray16, null);

            // 写入像素数据
            int stride = width * 2; // 每行像素数据的字节数（16位，即2字节）
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), ushortPixels, stride, 0);

            return writeableBitmap;
        }

        public async void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // Get file metadata
            FileInfo fileInfo = new FileInfo(filePath);
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, filePath, nameof(Opentif), "打开器接收到的源文件路径");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, fileInfo.Name, nameof(Opentif), "当前文件名");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSize, fileInfo.Length, nameof(Opentif), "当前文件大小（字节）");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileCreationTime, fileInfo.CreationTime, nameof(Opentif), "当前文件创建时间");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.FileModifiedTime, fileInfo.LastWriteTime, nameof(Opentif), "当前文件修改时间");

            WriteableBitmap? writeableBitmap = null;
            BitmapMetadata? metadata = null;
            BitmapSource source = null;
            await Task.Run(() =>
            {
                try
                {
                    using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var decoder = new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                    if (decoder.Frames.Count > 0)
                    {
                        source = decoder.Frames[0];
                        metadata = source.Metadata as BitmapMetadata;

                        // 检查 DPI 是否为 96，允许微小误差
                        if (Math.Abs(source.DpiX - 96.0) > 0.01 || Math.Abs(source.DpiY - 96.0) > 0.01)
                        {
                            // 计算 stride (每行字节数)
                            int stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;

                            // 创建缓冲区并复制像素数据
                            byte[] pixels = new byte[source.PixelHeight * stride];
                            source.CopyPixels(pixels, stride, 0);

                            // 使用相同的像素数据创建新的 BitmapSource，但指定 96 DPI
                            source = BitmapSource.Create(
                                source.PixelWidth,
                                source.PixelHeight,
                                96, // DpiX
                                96, // DpiY
                                source.Format,
                                source.Palette,
                                pixels,
                                stride);
                        }

                        source.Freeze();
                    }
                }
                catch (Exception)
                {
                    return;
                }

            });

            if (source == null) return;

            // Gray32Float TIFF 可按打开器配置决定是否先归一化转换为 Gray16。
            if (source.Format == PixelFormats.Gray32Float)
            {
                if (TifOpenConfig.Current.ConvertGray32FloatToGray16OnOpen)
                {
                    writeableBitmap = ConvertGray32FloatToBitmapSource(source);
                }
                else
                {
                    writeableBitmap = new WriteableBitmap(source);
                }
            }
            else
            {
                writeableBitmap = new WriteableBitmap(source);
            }

            if (writeableBitmap == null) return;

            // Add image dimensions
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, writeableBitmap.PixelWidth, nameof(Opentif), "位图像素宽度");
            context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, writeableBitmap.PixelHeight, nameof(Opentif), "位图像素高度");

            // Add EXIF metadata if available
            if (metadata != null)
            {
                try
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
                catch
                {
                    // Silently ignore metadata extraction errors
                }
            }

            context.ImageView.SetImageSource(writeableBitmap);
            context.ImageView.UpdateZoomAndScale();
        }
    }
}
