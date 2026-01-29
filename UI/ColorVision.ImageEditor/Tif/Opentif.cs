using ColorVision.ImageEditor.Abstractions;
using System;
using System.Collections.Generic;
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

            // 将浮点值转换为0-65535范围的16位整数
            for (int i = 0; i < floatPixels.Length; i++)
            {
                float v = floatPixels[i];
                if (v < 0) v = 0; if (v > 1) v = 1;
                ushortPixels[i] = (ushort)(v * 65535);
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
            context.Config.AddProperties("FileSource", filePath);
            context.Config.AddProperties("FileName", fileInfo.Name);
            context.Config.AddProperties("FileSize", fileInfo.Length);
            context.Config.AddProperties("FileCreationTime", fileInfo.CreationTime);
            context.Config.AddProperties("FileModifiedTime", fileInfo.LastWriteTime);

            WriteableBitmap? writeableBitmap = null;
            BitmapMetadata? metadata = null;
            BitmapSource source = null;
           await Task.Run(() =>
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
                    // 这里将处理过（或原始）的 source 转换为 WriteableBitmap
                }
            });

            writeableBitmap = new WriteableBitmap(source);
            if (writeableBitmap == null) return;

            // Add image dimensions
            context.Config.AddProperties("ImageWidth", writeableBitmap.PixelWidth);
            context.Config.AddProperties("ImageHeight", writeableBitmap.PixelHeight);

            // Add EXIF metadata if available
            if (metadata != null)
            {
                try
                {
                    if (metadata.CameraModel != null)
                        context.Config.AddProperties("CameraModel", metadata.CameraModel);
                    if (metadata.CameraManufacturer != null)
                        context.Config.AddProperties("CameraManufacturer", metadata.CameraManufacturer);
                    if (metadata.DateTaken != null)
                        context.Config.AddProperties("DateTaken", metadata.DateTaken);
                    if (metadata.ApplicationName != null)
                        context.Config.AddProperties("ApplicationName", metadata.ApplicationName);
                    if (metadata.Title != null)
                        context.Config.AddProperties("ImageTitle", metadata.Title);
                    if (metadata.Subject != null)
                        context.Config.AddProperties("ImageSubject", metadata.Subject);
                }
                catch
                {
                    // Silently ignore metadata extraction errors
                }
            }

            context.ImageView.SetImageSource(writeableBitmap);
            context.ImageView.ComboBoxLayers.SelectedIndex = 0;
            context.ImageView.ComboBoxLayers.ItemsSource = new List<string>() { "Src", "R", "G", "B" };
            context.ImageView.AddSelectionChangedHandler(context.ImageView.ComboBoxLayersSelectionChanged);
            context.ImageView.UpdateZoomAndScale();
        }
    }
}