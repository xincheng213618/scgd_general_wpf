using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    internal sealed class PixelValueOverlay : FrameworkElement
    {
        private const double MinPixelCellSize = 40;
        private const int MaxVisiblePixelCount = 2200;
        private static readonly Typeface OverlayTypeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

        private ImageView? _owner;

        public PixelValueOverlay()
        {
            IsHitTestVisible = false;
            Visibility = Visibility.Collapsed;
            SnapsToDevicePixels = true;
        }

        public void Attach(ImageView owner)
        {
            _owner = owner;
            Refresh();
        }

        public void Refresh()
        {
            bool shouldRender = TryGetRenderState(out _, out _, out _, out _, out _);
            Visibility = shouldRender ? Visibility.Visible : Visibility.Collapsed;

            if (shouldRender)
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (!TryGetRenderState(out BitmapSource? source, out Rect imageBounds, out double cellWidth, out double cellHeight, out Int32Rect visiblePixels) || source == null)
            {
                return;
            }

            if (source.Format == PixelFormats.Gray8 || source.Format == PixelFormats.Indexed8)
            {
                RenderGray8(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels);
                return;
            }

            if (source.Format == PixelFormats.Gray16)
            {
                RenderGray16(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels);
                return;
            }

            if (source.Format == PixelFormats.Gray32Float)
            {
                RenderGray32Float(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels);
            }
        }

        private bool TryGetRenderState(out BitmapSource? source, out Rect imageBounds, out double cellWidth, out double cellHeight, out Int32Rect visiblePixels)
        {
            DrawCanvas? targetCanvas = _owner?.ImageShow;
            source = GetCurrentBitmapSource();
            imageBounds = Rect.Empty;
            cellWidth = 0;
            cellHeight = 0;
            visiblePixels = Int32Rect.Empty;

            if (targetCanvas == null || source == null)
            {
                return false;
            }

            if (RenderOptions.GetBitmapScalingMode(targetCanvas) != BitmapScalingMode.NearestNeighbor)
            {
                return false;
            }

            if (!IsSupportedPixelFormat(source.Format))
            {
                return false;
            }

            if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            {
                return false;
            }

            try
            {
                GeneralTransform transform = targetCanvas.TransformToVisual(this);
                imageBounds = transform.TransformBounds(new Rect(0, 0, source.PixelWidth, source.PixelHeight));
            }
            catch (InvalidOperationException)
            {
                return false;
            }

            if (imageBounds.Width <= 0 || imageBounds.Height <= 0)
            {
                return false;
            }

            cellWidth = imageBounds.Width / source.PixelWidth;
            cellHeight = imageBounds.Height / source.PixelHeight;

            if (cellWidth < MinPixelCellSize || cellHeight < MinPixelCellSize)
            {
                return false;
            }

            Rect viewport = new(new Point(0, 0), RenderSize);
            Rect visibleBounds = Rect.Intersect(viewport, imageBounds);
            if (visibleBounds.IsEmpty)
            {
                return false;
            }

            int startX = Math.Max(0, Math.Min(source.PixelWidth - 1, (int)Math.Floor((visibleBounds.Left - imageBounds.Left) / cellWidth)));
            int startY = Math.Max(0, Math.Min(source.PixelHeight - 1, (int)Math.Floor((visibleBounds.Top - imageBounds.Top) / cellHeight)));
            int endX = Math.Max(0, Math.Min(source.PixelWidth - 1, (int)Math.Ceiling((visibleBounds.Right - imageBounds.Left) / cellWidth) - 1));
            int endY = Math.Max(0, Math.Min(source.PixelHeight - 1, (int)Math.Ceiling((visibleBounds.Bottom - imageBounds.Top) / cellHeight) - 1));

            if (endX < startX || endY < startY)
            {
                return false;
            }

            visiblePixels = new Int32Rect(startX, startY, endX - startX + 1, endY - startY + 1);
            return visiblePixels.Width * visiblePixels.Height <= MaxVisiblePixelCount;
        }

        private BitmapSource? GetCurrentBitmapSource()
        {
            if (_owner == null)
            {
                return null;
            }

            return _owner.ImageShow.Source as BitmapSource
                ?? _owner.FunctionImage as BitmapSource
                ?? _owner.ViewBitmapSource as BitmapSource;
        }

        private static bool IsSupportedPixelFormat(PixelFormat format)
        {
            return format == PixelFormats.Gray8 ||
                   format == PixelFormats.Indexed8 ||
                   format == PixelFormats.Gray16 ||
                   format == PixelFormats.Gray32Float;
        }

        private void RenderGray8(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels)
        {
            byte[] row = new byte[visiblePixels.Width];

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;
                source.CopyPixels(new Int32Rect(visiblePixels.X, y, visiblePixels.Width, 1), row, visiblePixels.Width, 0);

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    byte value = row[columnIndex];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, value.ToString(CultureInfo.InvariantCulture), value);
                }
            }
        }

        private void RenderGray16(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels)
        {
            ushort[] row = new ushort[visiblePixels.Width];

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;
                source.CopyPixels(new Int32Rect(visiblePixels.X, y, visiblePixels.Width, 1), row, visiblePixels.Width * 2, 0);

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    ushort value = row[columnIndex];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, value.ToString(CultureInfo.InvariantCulture), value / 257.0);
                }
            }
        }

        private void RenderGray32Float(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels)
        {
            float[] row = new float[visiblePixels.Width];

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;
                source.CopyPixels(new Int32Rect(visiblePixels.X, y, visiblePixels.Width, 1), row, visiblePixels.Width * 4, 0);

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    float value = row[columnIndex];
                    string text = FormatGray32Float(value);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    double brightness = value >= 0 && value <= 1
                        ? value * 255
                        : Math.Clamp(value, 0, 255);
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, text, brightness);
                }
            }
        }

        private void DrawCellText(DrawingContext drawingContext, Rect imageBounds, double cellWidth, double cellHeight, int pixelX, int pixelY, string text, double brightness)
        {
            Rect cellRect = new(
                imageBounds.Left + pixelX * cellWidth,
                imageBounds.Top + pixelY * cellHeight,
                cellWidth,
                cellHeight);

            double fontSize = Math.Max(8, Math.Min(cellRect.Width, cellRect.Height) * 0.42);
            Brush textBrush = brightness > 140 ? Brushes.Black : Brushes.White;
            Brush shadowBrush = brightness > 140 ? Brushes.White : Brushes.Black;

            FormattedText formattedText = CreateFormattedText(text, fontSize, textBrush);
            if (formattedText.Width > cellRect.Width - 2 || formattedText.Height > cellRect.Height - 2)
            {
                return;
            }

            Point origin = new(
                cellRect.Left + (cellRect.Width - formattedText.Width) / 2,
                cellRect.Top + (cellRect.Height - formattedText.Height) / 2);

            drawingContext.DrawText(CreateFormattedText(text, fontSize, shadowBrush), new Point(origin.X + 1, origin.Y + 1));
            drawingContext.DrawText(formattedText, origin);
        }

        private FormattedText CreateFormattedText(string text, double fontSize, Brush foreground)
        {
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                OverlayTypeface,
                fontSize,
                foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        private static string FormatGray32Float(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return string.Empty;
            }

            if (Math.Abs(value - MathF.Round(value)) < 0.001f)
            {
                return MathF.Round(value).ToString(CultureInfo.InvariantCulture);
            }

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}