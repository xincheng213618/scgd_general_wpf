using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows;
using System.IO;

namespace ColorVision.Common.Utilities
{
    /// <summary>
    /// 图像操作的一些静态方法
    /// </summary>
    public static class ImageUtil
    {
        private static BrushConverter brushConverter = new();

        public static Brush? ConvertFromString(string colorCode)
        {
            return (Brush)brushConverter.ConvertFromString(colorCode);
        }

        private static BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream memory = new())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapImage = new();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }

        public static bool SaveImageSourceToFile(ImageSource imageSource, string filePath)
        {
            if (imageSource is BitmapSource bitmapSource)
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using var fileStream = new FileStream(filePath, FileMode.Create);
                encoder.Save(fileStream);
                return true;
            }
            return false;
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



        public static WriteableBitmap BitmapToWriteableBitmap(System.Drawing.Bitmap src)
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

        public static PixelFormat CoverFormat(System.Drawing.Bitmap src)
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
