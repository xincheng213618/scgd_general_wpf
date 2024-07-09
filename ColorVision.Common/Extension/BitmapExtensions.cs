using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Common.Extension
{
    public static class BitmapExtensions
    {

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
    }
}
