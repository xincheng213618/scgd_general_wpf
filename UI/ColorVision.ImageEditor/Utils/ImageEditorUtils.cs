using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Utils
{
    /// <summary>
    /// 图像操作的一些静态方法
    /// </summary>
    internal static partial class ImageEditorUtils
    {
        public static (int R, int G, int B) GetPixelColor(this BitmapSource bitmapImage, int x, int y)
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

                    // Return color with full opacity
                    return ((int)red16,(int)green16,(int)blue16);
                case PixelFormat fmt when fmt == PixelFormats.Rgb24:
                    return ((int)pixelData[0], (int)pixelData[1], (int)pixelData[2]);

                case PixelFormat fmt when fmt == PixelFormats.Bgr24:

                    return ((int)pixelData[2], (int)pixelData[1], (int)pixelData[0]);
                case PixelFormat fmt when fmt == PixelFormats.Bgra32 || fmt == PixelFormats.Pbgra32:
                    return ((int)pixelData[2], (int)pixelData[1], (int)pixelData[0]);
                case PixelFormat fmt when fmt == PixelFormats.Bgr32 || fmt == PixelFormats.Bgr32:
                    return ((int)pixelData[2], (int)pixelData[1], (int)pixelData[0]);

                case PixelFormat fmt when fmt == PixelFormats.Gray8:
                    return ((int)pixelData[0], (int)pixelData[0], (int)pixelData[0]);
                case PixelFormat fmt when fmt == PixelFormats.Gray16:
                    // For Gray16 format, single channel is 16 bits
                    ushort gray16 = BitConverter.ToUInt16(pixelData, 0);
                    return ((int)gray16, (int)gray16, (int)gray16);
                case PixelFormat fmt when fmt == PixelFormats.Gray32Float:
                    // For Gray16 format, single channel is 16 bits
                    float gray32Float = BitConverter.ToSingle(pixelData, 0);
                    //byte gray1 = (byte)(Math.Clamp(gray32Float * 255.0f, 0, 255));
                   return ((int)gray32Float, (int)gray32Float, (int)gray32Float);
                default:
                    throw new NotSupportedException($"Pixel format {format} is not supported.");
            }
        }
    }
}
