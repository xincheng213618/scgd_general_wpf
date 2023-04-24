using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows;

namespace ColorVision
{
    public static class BitmapExtensions
    {

        public static int ToInt32(this double num) => Convert.ToInt32(num);


        public static Color GetPixelColor(this BitmapImage bitmapImage, int x, int y)
        {
            if (x < 0 || x >= bitmapImage.PixelWidth || y < 0 || y >= bitmapImage.PixelHeight)
            {
                throw new ArgumentOutOfRangeException("指定的坐标超出图像范围");
            }
            Color color = Colors.Black;
            byte[] pixelData = new byte[4];
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));
            croppedBitmap.CopyPixels(pixelData, 4, 0);
            color = Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]);
            return color;
        }

        public static string ToHex(this Color color)
        {
            return "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
        }


        //public static byte[] GetPngImageData(this BitmapImage bitmapImage)
        //{
        //    byte[] imageData = null;
        //    // 将 BitmapImage 转换为 Bitmap
        //    Bitmap bmp = new Bitmap(bitmapImage.PixelWidth, bitmapImage.PixelHeight, PixelFormat.Format32bppArgb);
        //    BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
        //    bitmapImage.CopyPixels(Int32Rect.Empty, bmpData.Scan0, bmpData.Height * bmpData.Stride, bmpData.Stride);
        //    bmp.UnlockBits(bmpData);

        //    // 将 Bitmap 转换为 MemoryStream，并使用 PngBitmapEncoder 进行编码
        //    using (MemoryStream ms = new MemoryStream())
        //    {
        //        bmp.Save(ms, ImageFormat.Png);
        //        imageData = ms.ToArray();
        //    }

        //    return imageData;
        //}


        //public static Color GetPixelColor(Bitmap bitmap, int x, int y)
        //{
        //    if (x < 0 || x >= bitmap.Width || y < 0 || y >= bitmap.Height)
        //    {
        //        throw new ArgumentOutOfRangeException("指定的坐标超出图像范围");
        //    }
        //    Color color = Color.FromArgb(0, 0, 0, 0);
        //    BitmapData bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly,
        //        PixelFormat.Format32bppArgb);
        //    IntPtr ptr = bmpData.Scan0;
        //    int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
        //    byte[] rgbValues = new byte[bytes];
        //    Marshal.Copy(ptr, rgbValues, 0, bytes);
        //    int offset = y * bmpData.Stride + x * 4;
        //    color = Color.FromArgb(rgbValues[offset + 3], rgbValues[offset + 2], rgbValues[offset + 1], rgbValues[offset]);
        //    bitmap.UnlockBits(bmpData);
        //    return color;
        //}
    }
}
