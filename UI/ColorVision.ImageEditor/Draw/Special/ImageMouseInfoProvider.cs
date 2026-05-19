using ColorVision.Common.Utilities;
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
            if (_pixelSampleChanged == null || Image.Source is not BitmapSource bitmap)
            {
                return;
            }

            if (Image.ActualWidth <= 0 || Image.ActualHeight <= 0)
            {
                return;
            }

            Point point = e.GetPosition(Image);
            Point viewPosition = new(point.X, point.Y);
            point.X = point.X / Image.ActualWidth * bitmap.PixelWidth;
            point.Y = point.Y / Image.ActualHeight * bitmap.PixelHeight;

            int pixelX = point.X.ToInt32();
            int pixelY = point.Y.ToInt32();
            if (pixelX < 0 || pixelX >= bitmap.PixelWidth || pixelY < 0 || pixelY >= bitmap.PixelHeight)
            {
                return;
            }

            _pixelSampleChanged?.Invoke(this, CreatePixelSample(bitmap, viewPosition, pixelX, pixelY));
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

            if (format == PixelFormats.Rgb48 || format == PixelFormats.Rgba64)
            {
                return (true, BitConverter.ToUInt16(pixelData, 0), BitConverter.ToUInt16(pixelData, 2), BitConverter.ToUInt16(pixelData, 4));
            }

            return (false, 0, 0, 0);
        }

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
                return Color.FromRgb(ToByte(red), ToByte(green), ToByte(blue));
            }

            if (format == PixelFormats.Gray8 || format == PixelFormats.Indexed8)
            {
                byte value = pixelData[0];
                return Color.FromRgb(value, value, value);
            }

            if (format == PixelFormats.Gray16)
            {
                byte value = ToByte(BitConverter.ToUInt16(pixelData, 0));
                return Color.FromRgb(value, value, value);
            }

            if (format == PixelFormats.Gray32Float)
            {
                byte value = ToByte(BitConverter.ToSingle(pixelData, 0));
                return Color.FromRgb(value, value, value);
            }

            return Colors.Black;
        }

        private static byte ToByte(int value)
        {
            if (value <= 255)
            {
                return (byte)Math.Clamp(value, 0, 255);
            }

            return (byte)Math.Clamp(value / 257, 0, 255);
        }

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