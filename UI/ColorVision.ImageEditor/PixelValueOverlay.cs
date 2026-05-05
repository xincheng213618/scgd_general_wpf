using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ColorVision.ImageEditor.Settings;

namespace ColorVision.ImageEditor
{
    internal sealed class PixelValueOverlay : FrameworkElement
    {
        private const double MinTextFontSize = 6;
        private const int MaxFormattedTextCacheEntries = 8192;
        private const double TextHorizontalPadding = 4;
        private const double TextVerticalPadding = 2;
        private static readonly Typeface OverlayTypeface = new(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        private static readonly Brush OverlayTextBrush = CreateFrozenBrush(Color.FromArgb(255, 255, 255, 255));
        private static readonly Brush OverlayBackgroundBrush = CreateFrozenBrush(Color.FromArgb(144, 0, 0, 0));

        private readonly DefaultImageViewDisplayConfig _displayDefaults = DefaultImageViewDisplayConfig.Current;
        private readonly Dictionary<FormattedTextCacheKey, CachedFormattedText> _formattedTextCache = new();
        private DrawingGroup? _cachedDrawing;
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
            if (!TryGetRenderState(out BitmapSource? source, out Rect imageBounds, out double cellWidth, out double cellHeight, out Int32Rect visiblePixels) || source == null)
            {
                _cachedDrawing = null;
                Visibility = Visibility.Collapsed;
                InvalidateVisual();
                return;
            }

            _cachedDrawing = BuildOverlayDrawing(source, imageBounds, cellWidth, cellHeight, visiblePixels);
            Visibility = Visibility.Visible;
            InvalidateVisual();
        }

        private DrawingGroup BuildOverlayDrawing(BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels)
        {
            DrawingGroup drawingGroup = new();
            using (DrawingContext drawingContext = drawingGroup.Open())
            {
                double fontSize = Math.Max(8, Math.Min(cellWidth, cellHeight) * 0.42);
                OverlayTextCache textCache = new(this, fontSize);

                if (source.Format == PixelFormats.Gray8 || source.Format == PixelFormats.Indexed8)
                {
                    RenderGray8(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Gray16)
                {
                    RenderGray16(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Gray32Float)
                {
                    RenderGray32Float(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Bgr24)
                {
                    RenderBgr24(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Rgb24)
                {
                    RenderRgb24(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Bgr32 || source.Format == PixelFormats.Bgra32 || source.Format == PixelFormats.Pbgra32)
                {
                    RenderBgr32Like(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Rgb48)
                {
                    RenderRgb48(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
                else if (source.Format == PixelFormats.Rgba64)
                {
                    RenderRgba64(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, textCache);
                }
            }

            if (drawingGroup.CanFreeze)
            {
                drawingGroup.Freeze();
            }

            return drawingGroup;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_cachedDrawing == null)
            {
                return;
            }

            drawingContext.DrawDrawing(_cachedDrawing);
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

            if (cellWidth < _displayDefaults.PixelValueOverlayMinPixelCellSize || cellHeight < _displayDefaults.PixelValueOverlayMinPixelCellSize)
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
            return visiblePixels.Width * visiblePixels.Height <= _displayDefaults.PixelValueOverlayMaxVisiblePixelCount;
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
                   format == PixelFormats.Gray32Float ||
                   format == PixelFormats.Bgr24 ||
                   format == PixelFormats.Rgb24 ||
                   format == PixelFormats.Bgr32 ||
                   format == PixelFormats.Bgra32 ||
                   format == PixelFormats.Pbgra32 ||
                   format == PixelFormats.Rgb48 ||
                   format == PixelFormats.Rgba64;
        }

        private static void RenderGray8(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            byte[] pixels = new byte[visiblePixels.Width * visiblePixels.Height];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = rowIndex * visiblePixels.Width + columnIndex;
                    byte value = pixels[pixelIndex];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, value.ToString(CultureInfo.InvariantCulture), textCache);
                }
            }
        }

        private static void RenderGray16(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            ushort[] pixels = new ushort[visiblePixels.Width * visiblePixels.Height];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width * 2, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = rowIndex * visiblePixels.Width + columnIndex;
                    ushort value = pixels[pixelIndex];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, value.ToString(CultureInfo.InvariantCulture), textCache);
                }
            }
        }

        private static void RenderGray32Float(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            float[] pixels = new float[visiblePixels.Width * visiblePixels.Height];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width * 4, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = rowIndex * visiblePixels.Width + columnIndex;
                    float value = pixels[pixelIndex];
                    string text = FormatGray32Float(value);
                    if (string.IsNullOrEmpty(text))
                    {
                        continue;
                    }

                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, text, textCache);
                }
            }
        }

        private static void RenderBgr24(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            RenderColor8(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, 3, isRgbOrder: false, textCache);
        }

        private static void RenderRgb24(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            RenderColor8(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, 3, isRgbOrder: true, textCache);
        }

        private static void RenderBgr32Like(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            RenderColor8(drawingContext, source, imageBounds, cellWidth, cellHeight, visiblePixels, 4, isRgbOrder: false, textCache);
        }

        private static void RenderColor8(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, int bytesPerPixel, bool isRgbOrder, OverlayTextCache textCache)
        {
            byte[] pixels = new byte[visiblePixels.Width * visiblePixels.Height * bytesPerPixel];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width * bytesPerPixel, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = (rowIndex * visiblePixels.Width + columnIndex) * bytesPerPixel;
                    byte r = isRgbOrder ? pixels[pixelIndex] : pixels[pixelIndex + 2];
                    byte g = pixels[pixelIndex + 1];
                    byte b = isRgbOrder ? pixels[pixelIndex + 2] : pixels[pixelIndex];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, FormatRgbText(r, g, b), textCache);
                }
            }
        }

        private static void RenderRgb48(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            ushort[] pixels = new ushort[visiblePixels.Width * visiblePixels.Height * 3];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width * 6, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = (rowIndex * visiblePixels.Width + columnIndex) * 3;
                    ushort r = pixels[pixelIndex];
                    ushort g = pixels[pixelIndex + 1];
                    ushort b = pixels[pixelIndex + 2];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, FormatRgbText(r, g, b), textCache);
                }
            }
        }

        private static void RenderRgba64(DrawingContext drawingContext, BitmapSource source, Rect imageBounds, double cellWidth, double cellHeight, Int32Rect visiblePixels, OverlayTextCache textCache)
        {
            ushort[] pixels = new ushort[visiblePixels.Width * visiblePixels.Height * 4];
            source.CopyPixels(visiblePixels, pixels, visiblePixels.Width * 8, 0);

            for (int rowIndex = 0; rowIndex < visiblePixels.Height; rowIndex++)
            {
                int y = visiblePixels.Y + rowIndex;

                for (int columnIndex = 0; columnIndex < visiblePixels.Width; columnIndex++)
                {
                    int pixelIndex = (rowIndex * visiblePixels.Width + columnIndex) * 4;
                    ushort r = pixels[pixelIndex];
                    ushort g = pixels[pixelIndex + 1];
                    ushort b = pixels[pixelIndex + 2];
                    DrawCellText(drawingContext, imageBounds, cellWidth, cellHeight, visiblePixels.X + columnIndex, y, FormatRgbText(r, g, b), textCache);
                }
            }
        }

        private static void DrawCellText(DrawingContext drawingContext, Rect imageBounds, double cellWidth, double cellHeight, int pixelX, int pixelY, string text, OverlayTextCache textCache)
        {
            Rect cellRect = new(
                imageBounds.Left + pixelX * cellWidth,
                imageBounds.Top + pixelY * cellHeight,
                cellWidth,
                cellHeight);

            double availableWidth = Math.Max(0, cellRect.Width - TextHorizontalPadding);
            double availableHeight = Math.Max(0, cellRect.Height - TextVerticalPadding);
            CachedFormattedText? formattedText = textCache.GetBestFit(text, availableWidth, availableHeight);
            if (formattedText == null)
            {
                return;
            }

            Point origin = new(
                cellRect.Left + (cellRect.Width - formattedText.FormattedText.Width) / 2,
                cellRect.Top + (cellRect.Height - formattedText.FormattedText.Height) / 2);

            Rect backgroundRect = new(
                origin.X - TextHorizontalPadding / 2,
                origin.Y - TextVerticalPadding / 2,
                formattedText.FormattedText.Width + TextHorizontalPadding,
                formattedText.FormattedText.Height + TextVerticalPadding);

            drawingContext.DrawRoundedRectangle(OverlayBackgroundBrush, null, backgroundRect, 2, 2);
            drawingContext.DrawText(formattedText.FormattedText, origin);
        }

        private CachedFormattedText GetOrCreateFormattedText(string text, double fontSize, double pixelsPerDip)
        {
            double quantizedFontSize = QuantizeFontSize(fontSize);
            FormattedTextCacheKey key = new(text, ToFontSizeKey(quantizedFontSize), ToPixelsPerDipKey(pixelsPerDip));
            if (_formattedTextCache.TryGetValue(key, out CachedFormattedText? cached))
            {
                return cached;
            }

            if (_formattedTextCache.Count >= MaxFormattedTextCacheEntries)
            {
                _formattedTextCache.Clear();
            }

            FormattedText formattedText = new(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                OverlayTypeface,
                quantizedFontSize,
                OverlayTextBrush,
                pixelsPerDip);

            cached = new CachedFormattedText(formattedText, quantizedFontSize);
            _formattedTextCache[key] = cached;
            return cached;
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

        private static string FormatRgbText<T>(T r, T g, T b)
        {
            return string.Concat(
                Convert.ToString(r, CultureInfo.InvariantCulture),
                "\n",
                Convert.ToString(g, CultureInfo.InvariantCulture),
                "\n",
                Convert.ToString(b, CultureInfo.InvariantCulture));
        }

        private static double QuantizeFontSize(double fontSize)
        {
            double normalized = Math.Max(MinTextFontSize, fontSize);
            return Math.Round(normalized * 2, MidpointRounding.AwayFromZero) / 2.0;
        }

        private static int ToFontSizeKey(double fontSize)
        {
            return (int)Math.Round(fontSize * 2, MidpointRounding.AwayFromZero);
        }

        private static int ToPixelsPerDipKey(double pixelsPerDip)
        {
            return (int)Math.Round(pixelsPerDip * 1000, MidpointRounding.AwayFromZero);
        }

        private static SolidColorBrush CreateFrozenBrush(Color color)
        {
            SolidColorBrush brush = new(color);
            if (brush.CanFreeze)
            {
                brush.Freeze();
            }

            return brush;
        }

        private sealed class CachedFormattedText
        {
            public CachedFormattedText(FormattedText formattedText, double fontSize)
            {
                FormattedText = formattedText;
                FontSize = fontSize;
            }

            public FormattedText FormattedText { get; }

            public double FontSize { get; }
        }

        private readonly record struct FormattedTextCacheKey(string Text, int FontSizeKey, int PixelsPerDipKey);

        private sealed class OverlayTextCache
        {
            private readonly PixelValueOverlay _owner;
            private readonly double _baseFontSize;
            private readonly double _pixelsPerDip;

            public OverlayTextCache(PixelValueOverlay owner, double fontSize)
            {
                _owner = owner;
                _baseFontSize = fontSize;
                _pixelsPerDip = VisualTreeHelper.GetDpi(owner).PixelsPerDip;
            }

            public CachedFormattedText Get(string text, double fontSize)
            {
                return _owner.GetOrCreateFormattedText(text, fontSize, _pixelsPerDip);
            }

            public CachedFormattedText? GetBestFit(string text, double availableWidth, double availableHeight)
            {
                if (availableWidth <= 0 || availableHeight <= 0)
                {
                    return null;
                }

                CachedFormattedText candidate = Get(text, _baseFontSize);
                if (Fits(candidate.FormattedText, availableWidth, availableHeight))
                {
                    return candidate;
                }

                double widthScale = availableWidth / Math.Max(candidate.FormattedText.Width, 1);
                double heightScale = availableHeight / Math.Max(candidate.FormattedText.Height, 1);
                double scaledFontSize = QuantizeFontSize(_baseFontSize * Math.Min(widthScale, heightScale) * 0.96);
                if (scaledFontSize >= candidate.FontSize)
                {
                    return null;
                }

                CachedFormattedText fitted = Get(text, scaledFontSize);
                return Fits(fitted.FormattedText, availableWidth, availableHeight) ? fitted : null;
            }

            private static bool Fits(FormattedText formattedText, double availableWidth, double availableHeight)
            {
                return formattedText.Width <= availableWidth && formattedText.Height <= availableHeight;
            }
        }
    }
}