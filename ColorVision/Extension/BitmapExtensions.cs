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
using System.Runtime.CompilerServices;

namespace ColorVision.Extension
{
    public static class BitmapExtensions
    {

        public static int ToInt32(this double num) => Convert.ToInt32(num);


        public static Color GetPixelColor(this BitmapImage bitmapImage, int x, int y)
        {
            if (x < 0 || x >= bitmapImage.PixelWidth || y < 0 || y >= bitmapImage.PixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(bitmapImage));
            }
            Color color = Colors.Black;
            byte[] pixelData = new byte[4];
            CroppedBitmap croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));
            croppedBitmap.CopyPixels(pixelData, 4, 0);
            color = Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]);
            return color;
        }

        public static string ToHex(this Color color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        public static Color GetPixelColor(System.Drawing.Bitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).ToMedia();

        public static Color ToMedia(this System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
