using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Special
{
    public sealed class ImageMouseInfoProvider : IDisposable
    {
        private readonly DrawEditorContext _editorContext;
        private EventHandler<ImagePixelSample>? _pixelSampleChanged;

        private DrawCanvas Image => _editorContext.DrawCanvas;

        public ImageMouseInfoProvider(DrawEditorContext editorContext)
        {
            _editorContext = editorContext;
        }

        public event EventHandler<ImagePixelSample>? PixelSampleChanged
        {
            add
            {
                bool shouldAttach = _pixelSampleChanged == null;
                _pixelSampleChanged += value;
                if (shouldAttach)
                {
                    Image.MouseMove += HandleMouseMove;
                }
            }
            remove
            {
                _pixelSampleChanged -= value;
                if (_pixelSampleChanged == null)
                {
                    Image.MouseMove -= HandleMouseMove;
                }
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs e)
        {
            if (_pixelSampleChanged != null && SampleAt(e.GetPosition(Image)) is { } sample)
                _pixelSampleChanged.Invoke(this, sample);
        }

        internal ImagePixelSample? SampleAt(Point viewPosition)
        {
            if (Image.Source is not BitmapSource bitmap
                || !(viewPosition.X >= 0 && viewPosition.X < Image.ActualWidth
                    && viewPosition.Y >= 0 && viewPosition.Y < Image.ActualHeight))
                return null;

            int pixelX = Math.Min(bitmap.PixelWidth - 1, (int)(viewPosition.X / Image.ActualWidth * bitmap.PixelWidth));
            int pixelY = Math.Min(bitmap.PixelHeight - 1, (int)(viewPosition.Y / Image.ActualHeight * bitmap.PixelHeight));
            return CreatePixelSample(bitmap, viewPosition, pixelX, pixelY);
        }

        private static ImagePixelSample CreatePixelSample(BitmapSource bitmap, Point viewPosition, int pixelX, int pixelY)
        {
            byte[] rawPixel = ReadRawPixel(bitmap, pixelX, pixelY);
            PixelFormat format = bitmap.Format;
            (bool hasRgbChannels, int red, int green, int blue) = ReadRgbChannels(format, rawPixel);

            return new ImagePixelSample
            {
                ViewPosition = viewPosition,
                PixelX = pixelX,
                PixelY = pixelY,
                PixelFormat = format,
                ValueText = BuildSampleText(format, rawPixel, hasRgbChannels, red, green, blue),
                PreviewColor = BuildDisplayColor(format, rawPixel, hasRgbChannels, red, green, blue),
                HasRgbSourceChannels = hasRgbChannels,
                Rgb16 = IsRgb16(format) ? ((ushort)red, (ushort)green, (ushort)blue) : null,
            };
        }

        private static byte[] ReadRawPixel(BitmapSource bitmap, int pixelX, int pixelY)
        {
            int bytesPerPixel = Math.Max(1, (bitmap.Format.BitsPerPixel + 7) / 8);
            byte[] pixelData = new byte[bytesPerPixel];
            bitmap.CopyPixels(new Int32Rect(pixelX, pixelY, 1, 1), pixelData, bytesPerPixel, 0);
            return pixelData;
        }

        private static (bool hasRgbChannels, int red, int green, int blue) ReadRgbChannels(PixelFormat format, byte[] pixelData)
        {
            if (format == PixelFormats.Rgb24)
            {
                return (true, pixelData[0], pixelData[1], pixelData[2]);
            }

            if (format == PixelFormats.Bgr24)
            {
                return (true, pixelData[2], pixelData[1], pixelData[0]);
            }

            if (format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 || format == PixelFormats.Bgr32)
            {
                return (true, pixelData[2], pixelData[1], pixelData[0]);
            }

            if (IsRgb16(format))
            {
                return (true, BitConverter.ToUInt16(pixelData, 0), BitConverter.ToUInt16(pixelData, 2), BitConverter.ToUInt16(pixelData, 4));
            }

            return (false, 0, 0, 0);
        }

        private static bool IsRgb16(PixelFormat format) => format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64;

        private static string BuildSampleText(PixelFormat format, byte[] pixelData, bool hasRgbChannels, int red, int green, int blue)
        {
            if (hasRgbChannels)
            {
                return string.Format(CultureInfo.InvariantCulture, "R:{0}  G:{1}  B:{2}", red, green, blue);
            }

            if (format == PixelFormats.Gray8)
            {
                return $"Gray:{pixelData[0]}";
            }

            if (format == PixelFormats.Indexed8)
            {
                return $"Index:{pixelData[0]}";
            }

            if (format == PixelFormats.Gray16)
            {
                return $"Gray:{BitConverter.ToUInt16(pixelData, 0)}";
            }

            if (format == PixelFormats.Gray32Float)
            {
                return $"Gray:{FormatGray32Float(BitConverter.ToSingle(pixelData, 0))}";
            }

            return format.ToString();
        }

        private static string FormatGray32Float(float value)
        {
            if (float.IsNaN(value))
            {
                return "NaN";
            }

            if (float.IsPositiveInfinity(value))
            {
                return "+Inf";
            }

            if (float.IsNegativeInfinity(value))
            {
                return "-Inf";
            }

            if (Math.Abs(value - MathF.Round(value)) < 0.001f)
            {
                return MathF.Round(value).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static Color BuildDisplayColor(PixelFormat format, byte[] pixelData, bool hasRgbChannels, int red, int green, int blue)
        {
            if (hasRgbChannels)
            {
                return IsRgb16(format)
                    ? Color.FromRgb(Scale16BitToByte(red), Scale16BitToByte(green), Scale16BitToByte(blue))
                    : Color.FromRgb((byte)red, (byte)green, (byte)blue);
            }

            if (format == PixelFormats.Gray8 || format == PixelFormats.Indexed8)
            {
                byte value = pixelData[0];
                return Color.FromRgb(value, value, value);
            }

            if (format == PixelFormats.Gray16)
            {
                byte value = Scale16BitToByte(BitConverter.ToUInt16(pixelData, 0));
                return Color.FromRgb(value, value, value);
            }

            if (format == PixelFormats.Gray32Float)
            {
                byte value = ToByte(BitConverter.ToSingle(pixelData, 0));
                return Color.FromRgb(value, value, value);
            }

            return Colors.Black;
        }

        private static byte Scale16BitToByte(int value) => (byte)((value + 128) / 257);

        private static byte ToByte(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0;
            }

            float scaled = value <= 1f ? value * 255f : value;
            return (byte)Math.Clamp((int)MathF.Round(scaled), 0, 255);
        }

        public void Dispose()
        {
            Image.MouseMove -= HandleMouseMove;
            _pixelSampleChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
