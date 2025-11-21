using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Tif
{
    public class TiffReader
    {
        public static BitmapSource ReadTiff(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found", filePath);

            // 使用 BitmapDecoder 读取 TIFF 图像
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                TiffBitmapDecoder decoder = new TiffBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
             
                BitmapSource bitmapSource = decoder.Frames[0];

                // 检查 DPI 是否为 96，允许微小误差
                if (Math.Abs(bitmapSource.DpiX - 96.0) > 0.01 || Math.Abs(bitmapSource.DpiY - 96.0) > 0.01)
                {
                    // 计算 stride (每行字节数)
                    int stride = (bitmapSource.PixelWidth * bitmapSource.Format.BitsPerPixel + 7) / 8;

                    // 创建缓冲区并复制像素数据
                    byte[] pixels = new byte[bitmapSource.PixelHeight * stride];
                    bitmapSource.CopyPixels(pixels, stride, 0);

                    // 使用相同的像素数据创建新的 BitmapSource，但指定 96 DPI
                    bitmapSource = BitmapSource.Create(
                        bitmapSource.PixelWidth,
                        bitmapSource.PixelHeight,
                        96, // DpiX
                        96, // DpiY
                        bitmapSource.Format,
                        bitmapSource.Palette,
                        pixels,
                        stride);
                }

                // 确保图像数据已加载
                bitmapSource.Freeze();
                return bitmapSource;
            }
        }
    }
}
