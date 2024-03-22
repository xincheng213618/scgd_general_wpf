using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Common.Extension
{
    public static class BitmapExtensions
    {

        public static int ToInt32(this double num) => Convert.ToInt32(num);


        public static Color GetPixelColor(this BitmapSource bitmapImage, int x, int y)
        {
            if (x < 0 || x >= bitmapImage.PixelWidth || y < 0 || y >= bitmapImage.PixelHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(bitmapImage));
            }
            byte[] pixelData;
            CroppedBitmap croppedBitmap;
            if (bitmapImage is WriteableBitmap writeableBitmap && writeableBitmap.Format.ToString() == "Rgb48")
            {
                // For RGB48 format, each channel is 16 bits, so we need 6 bytes per pixel
                pixelData = new byte[6];
                croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));

                // Stride is the number of bytes per pixel row, for RGB48 it's 6 bytes per pixel
                croppedBitmap.CopyPixels(pixelData, 6, 0);

                // Convert the 16-bit channel data to 8-bit by taking the most significant byte
                // This is a simplification and may result in some loss of color precision
                byte red = pixelData[0];
                byte green = pixelData[2];
                byte blue = pixelData[4];

                // There is no alpha channel in RGB48, so we assume it's fully opaque (255)
                return Color.FromArgb(255, red, green, blue);
            }

            Color color = Colors.Black;
            pixelData = new byte[4];
            croppedBitmap = new CroppedBitmap(bitmapImage, new Int32Rect(x, y, 1, 1));
            croppedBitmap.CopyPixels(pixelData, 4, 0);
            color = Color.FromArgb(pixelData[3], pixelData[2], pixelData[1], pixelData[0]);
            return color;
        }

        public static string ToHex(this Color color) => "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");

        public static Color GetPixelColor(System.Drawing.Bitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).ToMediaColor();

        public static Color ToMediaColor(this System.Drawing.Color color) => Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}
