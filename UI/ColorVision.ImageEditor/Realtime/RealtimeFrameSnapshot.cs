using System;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeFrameSnapshot
    {
        public RealtimeFrameSnapshot(byte[] pixelData, int width, int height, int stride, PixelFormat pixelFormat, DateTime timestampUtc)
        {
            PixelData = pixelData ?? throw new ArgumentNullException(nameof(pixelData));
            Width = width;
            Height = height;
            Stride = stride;
            PixelFormat = pixelFormat;
            TimestampUtc = timestampUtc;
        }

        public byte[] PixelData { get; }

        public int Width { get; }

        public int Height { get; }

        public int Stride { get; }

        public PixelFormat PixelFormat { get; }

        public DateTime TimestampUtc { get; }

        public BitmapSource ToBitmapSource()
        {
            BitmapSource bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormat, null, PixelData, Stride);
            if (bitmap.CanFreeze)
            {
                bitmap.Freeze();
            }

            return bitmap;
        }

        public void SaveRaw(string fileName)
        {
            File.WriteAllBytes(fileName, PixelData);
            File.WriteAllText(fileName + ".txt", CreateMetadataText());
        }

        public void SavePng(string fileName)
        {
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(ToBitmapSource()));

            using FileStream stream = new(fileName, FileMode.Create, FileAccess.Write);
            encoder.Save(stream);
        }

        private string CreateMetadataText()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Width={0}{5}Height={1}{5}Stride={2}{5}PixelFormat={3}{5}TimestampUtc={4:O}{5}",
                Width,
                Height,
                Stride,
                PixelFormat,
                TimestampUtc,
                Environment.NewLine);
        }
    }
}
