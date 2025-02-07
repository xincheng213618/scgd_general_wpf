using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System.Windows.Interop;
using System.Linq;

namespace ColorVision.Common.Utilities
{

    public static partial class ImageUtils
    {

        public static void DrawHistogram(int[] histogram, Color color, DrawingContext drawingContext, double width, double height)
        {
            double max = histogram.Max();
            double scale = height / max;

            Pen pen = new Pen(new SolidColorBrush(color), 1);

            for (int i = 0; i < histogram.Length; i++)
            {
                double x = i * (width / 256);
                double y = height - (histogram[i] * scale);
                drawingContext.DrawLine(pen, new Point(x, height), new Point(x, y));
            }
        }

        public static (int[] c1, int[] c2, int[] c3) RenderHistogram(BitmapSource bitmapSource)
        {
            int width = bitmapSource.PixelWidth;
            int height = bitmapSource.PixelHeight;
            int stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[height * stride];
            bitmapSource.CopyPixels(pixelData, stride, 0);

            int[] Channel1 = new int[256];
            int[] Channel2 = new int[256];
            int[] Channel3 = new int[256];

            if (bitmapSource.Format == PixelFormats.Gray8)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    byte gray = pixelData[i];
                    Channel1[gray]++;
                    Channel2[gray]++;
                    Channel3[gray]++;
                }
            }
            if (bitmapSource.Format == PixelFormats.Gray16)
            {
                for (int i = 0; i < pixelData.Length; i+=2)
                {
                    ushort gray = BitConverter.ToUInt16(pixelData, i);
                    Channel1[gray >> 8]++;
                }
            }
            else if (bitmapSource.Format == PixelFormats.Bgr24)
            {
                for (int i = 0; i < pixelData.Length; i += 3)
                {
                    byte blue = pixelData[i];
                    byte green = pixelData[i + 1];
                    byte red = pixelData[i + 2];

                    Channel1[red]++;
                    Channel2[green]++;
                    Channel3[blue]++;
                }
            }else if (bitmapSource.Format == PixelFormats.Bgra32 || bitmapSource.Format == PixelFormats.Bgr32)
            {
                for (int i = 0; i < pixelData.Length; i += 4)
                {
                    byte blue = pixelData[i];
                    byte green = pixelData[i + 1];
                    byte red = pixelData[i + 2];

                    Channel1[red]++;
                    Channel2[green]++;
                    Channel3[blue]++;
                }
            }
            else if (bitmapSource.Format == PixelFormats.Rgb48)
            {
                for (int i = 0; i < pixelData.Length; i += 6)
                {
                    ushort red = BitConverter.ToUInt16(pixelData, i);
                    ushort green = BitConverter.ToUInt16(pixelData, i + 2);
                    ushort blue = BitConverter.ToUInt16(pixelData, i + 4);

                    // Map 16-bit values to 8-bit range
                    Channel1[red >> 8]++;
                    Channel2[green >> 8]++;
                    Channel3[blue >> 8]++;
                }
            }

            return (Channel1, Channel2, Channel3);
        }






        /// <summary>
        /// 对图标的扩展
        /// </summary>
        /// <param name="icon"></param>
        /// <returns></returns>
        public static ImageSource ToImageSource(this System.Drawing.Icon icon)
        {
            ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;
        }

        public static System.Drawing.Icon ToIcon(this ImageSource imageSource)
        {
            ArgumentNullException.ThrowIfNull(imageSource);
            var bitmapSource = imageSource as BitmapSource ?? throw new ArgumentException("ImageSource must be of type BitmapSource", nameof(imageSource));
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(memoryStream);

                using (var bitmap = new System.Drawing.Bitmap(memoryStream))
                {
                    IntPtr hIcon = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(hIcon);
                }
            }
        }
        public static BitmapSource ToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);

            var bitmapData = bitmap.LockBits(
                rect,
                System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                PixelFormats.Bgr24,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);

            return bitmapSource;
        }

        private static BitmapImage ToBitmapImage(System.Drawing.Bitmap bitmap)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream memory = new())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }

        public static BitmapImage CreateBitmapImage(byte[] imageData)
        {
            BitmapImage bitmapImage = new BitmapImage();
            using (MemoryStream ms = new MemoryStream(imageData))
            {
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ms;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Freeze the BitmapImage to make it cross-thread accessible
            }
            return bitmapImage;
        }

        public static WriteableBitmap ToWriteableBitmap(System.Drawing.Bitmap src)
        {
            var wb = CreateCompatibleWriteableBitmap(src);
            System.Drawing.Imaging.PixelFormat format = src.PixelFormat;
            if (wb == null)
            {
                wb = new WriteableBitmap(src.Width, src.Height, 0, 0, PixelFormats.Bgra32, null);
                format = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
            }
            BitmapCopyToWriteableBitmap(src, wb, new System.Drawing.Rectangle(0, 0, src.Width, src.Height), 0, 0, format);
            return wb;
        }
    }



    /// <summary>
    /// 图像操作的一些静态方法
    /// </summary>
    public static partial class ImageUtils
    {

        private static BrushConverter brushConverter = new();

        public static Brush? ConvertFromString(string colorCode)
        {
            return (Brush)brushConverter.ConvertFromString(colorCode);
        }


        public static int ToInt32(this double num) => Convert.ToInt32(num);


        public static Color GetPixelColor(this BitmapSource bitmapImage, int x, int y)
        {
            if (x < 0 || x >= bitmapImage.PixelWidth || y < 0 || y >= bitmapImage.PixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(bitmapImage));
            }

            PixelFormat format = bitmapImage.Format;
            int bitsPerPixel = format.BitsPerPixel;
            int bytesPerPixel = (bitsPerPixel + 7) / 8; // Round up to the nearest whole byte

            byte[] pixelData = new byte[bytesPerPixel];
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));
            croppedBitmap.CopyPixels(pixelData, bytesPerPixel, 0);

            // Handle different pixel formats
            switch (format)
            {
                case PixelFormat fmt when fmt == PixelFormats.Rgb48:
                    // For RGB48 format, each channel is 16 bits
                    ushort red16 = BitConverter.ToUInt16(pixelData, 0);
                    ushort green16 = BitConverter.ToUInt16(pixelData, 2);
                    ushort blue16 = BitConverter.ToUInt16(pixelData, 4);

                    // Scale down to 8 bits
                    byte red = (byte)(red16 >> 8);
                    byte green = (byte)(green16 >> 8);
                    byte blue = (byte)(blue16 >> 8);

                    // Return color with full opacity
                    return Color.FromArgb(255, red, green, blue);

                case PixelFormat fmt when fmt == PixelFormats.Bgr24:
                    // For BGR24 format, each channel is 8 bits
                    return Color.FromArgb(255, pixelData[2], pixelData[1], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Pbgra32:
                    // For BGRA32 and PBGRA32 formats, each channel is 8 bits
                    return Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]);
                case PixelFormat fmt when fmt == PixelFormats.Bgr32 || fmt == PixelFormats.Bgr32:
                    // For BGRA32 and PBGRA32 formats, each channel is 8 bits
                    return Color.FromArgb(255, pixelData[2], pixelData[1], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Gray8:
                    // For Gray8 format, single channel is 8 bits
                    return Color.FromArgb(255, pixelData[0], pixelData[0], pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Gray16:
                    // For Gray16 format, single channel is 16 bits
                    ushort gray16 = BitConverter.ToUInt16(pixelData, 0);
                    byte gray = (byte)(gray16 >> 8);
                    return Color.FromArgb(255, gray, gray, gray);

                default:
                    throw new NotSupportedException($"Pixel format {format} is not supported.");
            }
        }

        public static string ToHex(this Color color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        public static Color GetPixelColor(System.Drawing.Bitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).ToMediaColor();

        public static Color ToMediaColor(this System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);


        public static bool SaveImageSourceToFile(this ImageSource imageSource, string filePath)
        {
            ArgumentNullException.ThrowIfNull(imageSource);

            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            BitmapSource bitmapSource = null;

            if (imageSource is BitmapSource source)
            {
                bitmapSource = source;
            }
            else
            {
                // Attempt to convert ImageSource to BitmapSource
                try
                {
                    var drawingVisual = new DrawingVisual();
                    using (var drawingContext = drawingVisual.RenderOpen())
                    {
                        drawingContext.DrawImage(imageSource, new Rect(0, 0, imageSource.Width, imageSource.Height));
                    }

                    var renderTargetBitmap = new RenderTargetBitmap(
                        (int)imageSource.Width,
                        (int)imageSource.Height,
                        96, 96, PixelFormats.Pbgra32);
                    renderTargetBitmap.Render(drawingVisual);
                    bitmapSource = renderTargetBitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while converting ImageSource to BitmapSource: {ex.Message}");
                    return false;
                }
            }

            try
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
                encoder.Save(fileStream);
                return true;
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as necessary
                Console.WriteLine($"An error occurred while saving the image: {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 创建一个新的BitmapImage
        /// </summary>
        public static BitmapImage CreateSolidColorBitmap(int width, int height, Color color)
        {
            // 创建一个 WriteableBitmap，用于绘制纯色图像
            WriteableBitmap writeableBitmap = new(width, height, 96, 96, PixelFormats.Pbgra32, null);

            // 将所有像素设置为指定的颜色

            writeableBitmap.Lock();
            unsafe
            {
                byte* pBackBuffer = (byte*)writeableBitmap.BackBuffer;
                int stride = writeableBitmap.BackBufferStride;

                for (int y = 0; y < writeableBitmap.PixelHeight; y++)
                {
                    for (int x = 0; x < writeableBitmap.PixelWidth; x++)
                    {
                        pBackBuffer[y * stride + 4 * x] = color.B;     // 蓝色通道
                        pBackBuffer[y * stride + 4 * x + 1] = color.G; // 绿色通道
                        pBackBuffer[y * stride + 4 * x + 2] = color.R; // 红色通道
                        pBackBuffer[y * stride + 4 * x + 3] = color.A; // 透明度通道
                    }
                }
            }


            writeableBitmap.Unlock();

            BitmapImage bitmapImage = new();
            using (var stream = new System.IO.MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(writeableBitmap));
                encoder.Save(stream);
                stream.Position = 0;

                bitmapImage.BeginInit();
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            }
            return bitmapImage;
        }

        private static PixelFormat CoverFormat(System.Drawing.Bitmap src)
        {
            return src.PixelFormat switch
            {
                System.Drawing.Imaging.PixelFormat.DontCare => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Max => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Indexed => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Gdi => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppRgb555 => PixelFormats.Bgr555,
                System.Drawing.Imaging.PixelFormat.Format16bppRgb565 => PixelFormats.Bgr565,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb => PixelFormats.Bgr24,
                System.Drawing.Imaging.PixelFormat.Format32bppRgb => PixelFormats.Bgr32,
                System.Drawing.Imaging.PixelFormat.Format1bppIndexed => PixelFormats.Indexed1,
                System.Drawing.Imaging.PixelFormat.Format4bppIndexed => PixelFormats.Indexed4,
                System.Drawing.Imaging.PixelFormat.Format8bppIndexed => PixelFormats.Indexed8,
                System.Drawing.Imaging.PixelFormat.Alpha => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppArgb1555 => PixelFormats.Bgr101010,
                System.Drawing.Imaging.PixelFormat.PAlpha => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb => PixelFormats.Pbgra32,
                System.Drawing.Imaging.PixelFormat.Extended => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format16bppGrayScale => PixelFormats.Gray16,
                System.Drawing.Imaging.PixelFormat.Format48bppRgb => PixelFormats.Rgb48,
                System.Drawing.Imaging.PixelFormat.Format64bppPArgb => PixelFormats.Prgba64,
                System.Drawing.Imaging.PixelFormat.Canonical => throw new NotImplementedException(),
                System.Drawing.Imaging.PixelFormat.Format32bppArgb => PixelFormats.Bgra32,
                System.Drawing.Imaging.PixelFormat.Format64bppArgb => PixelFormats.Rgba64,
                _ => PixelFormats.Default,
            }; ;
        }

        //创建尺寸和格式与Bitmap兼容的WriteableBitmap
        public static WriteableBitmap CreateCompatibleWriteableBitmap(System.Drawing.Bitmap src)
        {
            return new WriteableBitmap(src.Width, src.Height, 0, 0, CoverFormat(src), null);
        }
        //将Bitmap数据写入WriteableBitmap中
        public static void BitmapCopyToWriteableBitmap(System.Drawing.Bitmap src, WriteableBitmap dst, System.Drawing.Rectangle srcRect, int destinationX, int destinationY, System.Drawing.Imaging.PixelFormat srcPixelFormat)
        {
            var data = src.LockBits(new System.Drawing.Rectangle(new System.Drawing.Point(0, 0), src.Size), System.Drawing.Imaging.ImageLockMode.ReadOnly, srcPixelFormat);
            dst.WritePixels(new Int32Rect(srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height), data.Scan0, data.Height * data.Stride, data.Stride, destinationX, destinationY);
            src.UnlockBits(data);
        }



    }
}
