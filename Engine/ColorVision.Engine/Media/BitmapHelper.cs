using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// Helper for copying/restoring WriteableBitmap pixel data (copy-on-write pattern)
    /// </summary>
    public static class BitmapHelper
    {
        /// <summary>
        /// Copy all pixel data from WriteableBitmap to a byte array
        /// </summary>
        public static byte[] CopyPixels(WriteableBitmap bitmap)
        {
            int stride = bitmap.BackBufferStride;
            int size = stride * bitmap.PixelHeight;
            byte[] pixels = new byte[size];

            bitmap.Lock();
            try
            {
                Marshal.Copy(bitmap.BackBuffer, pixels, 0, size);
            }
            finally
            {
                bitmap.Unlock();
            }
            return pixels;
        }

        /// <summary>
        /// Restore pixel data from byte array back to WriteableBitmap
        /// </summary>
        public static void RestorePixels(WriteableBitmap bitmap, byte[] pixels)
        {
            bitmap.Lock();
            try
            {
                Marshal.Copy(pixels, 0, bitmap.BackBuffer, pixels.Length);
                bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
            }
            finally
            {
                bitmap.Unlock();
            }
        }
    }
}
