#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Conoscope.Core;
using Conoscope.Presentation;

namespace Conoscope
{
    internal readonly record struct PolarPlotPoint(double AngleDegrees, double Radius);

    internal sealed class ConoscopePolarPlot : FrameworkElement
    {
        private static readonly Typeface PlotTypeface = new Typeface("Segoe UI");

        private IReadOnlyList<PolarPlotPoint> points = Array.Empty<PolarPlotPoint>();
        private Brush? seriesBrush;
        private string radialAxisLabel = string.Empty;
        private double radialMaximum = 1;
        private bool closePath;
        private readonly MenuItem saveImageMenuItem;
        private readonly MenuItem copyToClipboardMenuItem;
        private readonly MenuItem autoscaleMenuItem;
        private readonly MenuItem openInNewWindowMenuItem;

        public ConoscopePolarPlot()
        {
            saveImageMenuItem = new MenuItem { Header = "Save Image" };
            saveImageMenuItem.Click += SaveImageMenuItem_Click;

            copyToClipboardMenuItem = new MenuItem { Header = "Copy to Clipboard" };
            copyToClipboardMenuItem.Click += CopyToClipboardMenuItem_Click;

            autoscaleMenuItem = new MenuItem { Header = "Autoscale" };
            autoscaleMenuItem.Click += AutoscaleMenuItem_Click;

            openInNewWindowMenuItem = new MenuItem { Header = "Open in New Window" };
            openInNewWindowMenuItem.Click += OpenInNewWindowMenuItem_Click;

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(saveImageMenuItem);
            ContextMenu.Items.Add(copyToClipboardMenuItem);
            ContextMenu.Items.Add(autoscaleMenuItem);
            ContextMenu.Items.Add(new Separator());
            ContextMenu.Items.Add(openInNewWindowMenuItem);

            ContextMenuOpening += ConoscopePolarPlot_ContextMenuOpening;
        }

        public void UpdatePlot(IReadOnlyList<PolarPlotPoint>? newPoints, Brush? strokeBrush, string? axisLabel, double maxRadius, bool shouldClosePath)
        {
            points = newPoints ?? Array.Empty<PolarPlotPoint>();
            seriesBrush = strokeBrush;
            radialAxisLabel = axisLabel ?? string.Empty;
            radialMaximum = double.IsFinite(maxRadius) && maxRadius > 0 ? maxRadius : 1;
            closePath = shouldClosePath;
            InvalidateVisual();
        }

        public void Clear()
        {
            UpdatePlot(Array.Empty<PolarPlotPoint>(), null, string.Empty, 1, false);
        }

        public void RefreshTheme() => InvalidateVisual();

        private void ConoscopePolarPlot_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            bool hasSurface = ActualWidth > 0 && ActualHeight > 0;
            bool hasData = points.Any(point => double.IsFinite(point.AngleDegrees) && double.IsFinite(point.Radius));

            saveImageMenuItem.IsEnabled = hasSurface;
            copyToClipboardMenuItem.IsEnabled = hasSurface;
            openInNewWindowMenuItem.IsEnabled = hasSurface;
            autoscaleMenuItem.IsEnabled = hasData;
        }

        private void SaveImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RenderTargetBitmap snapshot = CreateSnapshot();
                SaveFileDialog dialog = new SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|Bitmap Image (*.bmp)|*.bmp|JPEG Image (*.jpg;*.jpeg)|*.jpg;*.jpeg",
                    DefaultExt = "png",
                    FileName = $"PolarPlot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                    RestoreDirectory = true
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                BitmapEncoder encoder = CreateBitmapEncoder(Path.GetExtension(dialog.FileName));
                encoder.Frames.Add(BitmapFrame.Create(snapshot));

                using FileStream stream = File.Create(dialog.FileName);
                encoder.Save(stream);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyToClipboardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetImage(CreateSnapshot());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AutoscaleMenuItem_Click(object sender, RoutedEventArgs e)
        {
            radialMaximum = GetNiceRadialMaximum(points.Select(point => point.Radius));
            InvalidateVisual();
        }

        private void OpenInNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                RenderTargetBitmap snapshot = CreateSnapshot();
                Image image = new Image
                {
                    Source = snapshot,
                    Stretch = Stretch.None,
                    SnapsToDevicePixels = true
                };

                Window previewWindow = new Window
                {
                    Title = string.IsNullOrWhiteSpace(radialAxisLabel) ? "Polar Plot" : $"Polar Plot - {radialAxisLabel}",
                    Owner = Window.GetWindow(this),
                    Content = new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = image
                    },
                    Width = Math.Min(snapshot.Width + 32, SystemParameters.WorkArea.Width * 0.9),
                    Height = Math.Min(snapshot.Height + 40, SystemParameters.WorkArea.Height * 0.9),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                previewWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open preview window: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = double.IsInfinity(availableSize.Width) ? 240 : availableSize.Width;
            double height = double.IsInfinity(availableSize.Height) ? 240 : availableSize.Height;
            return new Size(width, height);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            ConoscopePlotTheme theme = ConoscopePlotTheme.Resolve(this);
            drawingContext.DrawRectangle(theme.Background, null, new Rect(0, 0, ActualWidth, ActualHeight));

            const double sidePadding = 46;
            const double topPadding = 28;
            const double bottomPadding = 28;

            Rect plotBounds = new Rect(
                sidePadding,
                topPadding,
                Math.Max(0, ActualWidth - sidePadding * 2),
                Math.Max(0, ActualHeight - topPadding - bottomPadding));

            if (plotBounds.Width <= 0 || plotBounds.Height <= 0)
            {
                return;
            }

            Point center = new Point(plotBounds.Left + plotBounds.Width / 2.0, plotBounds.Top + plotBounds.Height / 2.0);
            double plotRadius = Math.Max(0, Math.Min(plotBounds.Width, plotBounds.Height) / 2.0);
            if (plotRadius <= 0)
            {
                return;
            }

            DrawAxisLabel(drawingContext, plotBounds, theme);
            DrawGrid(drawingContext, center, plotRadius, theme);
            DrawSeries(drawingContext, center, plotRadius, theme);
        }

        private void DrawAxisLabel(DrawingContext drawingContext, Rect plotBounds, ConoscopePlotTheme theme)
        {
            if (string.IsNullOrWhiteSpace(radialAxisLabel))
            {
                return;
            }

            DrawText(drawingContext, radialAxisLabel, new Point(plotBounds.Left, 4), theme.Foreground, 12, centered: false);
        }

        private void DrawGrid(DrawingContext drawingContext, Point center, double plotRadius, ConoscopePlotTheme theme)
        {
            const int ringCount = 6;
            const double radialLabelAngle = 82.5;
            int angleStep = plotRadius >= 125 ? 15 : 30;
            Pen gridPen = CreatePen(theme.Grid, 0.75);
            Pen minorGridPen = CreatePen(theme.MinorGrid, 0.75);
            Pen outerGridPen = CreatePen(theme.Grid, 1.0);

            // Draw spokes before labels so the grid never cuts through a radial value.
            for (int angle = 0; angle < 360; angle += angleStep)
            {
                Point spokeEnd = ToScreenPoint(center, plotRadius, angle);
                drawingContext.DrawLine(angle % 30 == 0 ? gridPen : minorGridPen, center, spokeEnd);
                if (angle % 30 == 0)
                {
                    Point labelPoint = ToScreenPoint(center, plotRadius + 16, angle);
                    DrawText(drawingContext, $"{angle}°", labelPoint, theme.MutedForeground, 11, centered: true);
                }
            }

            for (int ringIndex = 1; ringIndex <= ringCount; ringIndex++)
            {
                double radius = plotRadius * ringIndex / ringCount;
                Pen pen = ringIndex == ringCount ? outerGridPen : gridPen;
                drawingContext.DrawEllipse(null, pen, center, radius, radius);

                double value = radialMaximum * ringIndex / ringCount;
                // Put radial values between the 75° and 90° spokes. Keeping them off the
                // horizontal axis prevents the outer value from colliding with the 90° label.
                Point labelPoint = ToScreenPoint(center, Math.Max(0, radius - 9), radialLabelAngle);
                DrawText(drawingContext, FormatTickValue(value), labelPoint, theme.MutedForeground, 11, centered: true, background: theme.Background);
            }

            DrawText(drawingContext, "0", new Point(center.X + 4, center.Y - 8), theme.MutedForeground, 11, centered: false, background: theme.Background);
        }

        private void DrawSeries(DrawingContext drawingContext, Point center, double plotRadius, ConoscopePlotTheme theme)
        {
            List<Point> screenPoints = points
                .Where(point => double.IsFinite(point.AngleDegrees) && double.IsFinite(point.Radius))
                .Select(point => new PolarPlotPoint(NormalizeAngle(point.AngleDegrees), Math.Max(0, point.Radius)))
                .Select(point => ToScreenPoint(center, plotRadius * Math.Clamp(point.Radius / radialMaximum, 0, 1), point.AngleDegrees))
                .ToList();

            if (screenPoints.Count == 0)
            {
                DrawText(drawingContext, "No data", center, theme.MutedForeground, 13, centered: true, background: theme.Background);
                return;
            }

            StreamGeometry geometry = new StreamGeometry();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(screenPoints[0], false, closePath);
                if (screenPoints.Count > 1)
                {
                    context.PolyLineTo(screenPoints.Skip(1).ToList(), true, false);
                }
            }

            geometry.Freeze();

            Pen seriesPen = new Pen(seriesBrush ?? ConoscopePlotTheme.GetChannelBrush(ExportChannel.Y, this), 2.0)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            if (seriesPen.CanFreeze)
            {
                seriesPen.Freeze();
            }

            drawingContext.DrawGeometry(null, seriesPen, geometry);
        }

        private void DrawText(DrawingContext drawingContext, string text, Point origin, Brush brush, double fontSize, bool centered, Brush? background = null)
        {
            FormattedText formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                PlotTypeface,
                fontSize,
                brush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Point drawPoint = centered
                ? new Point(origin.X - formattedText.Width / 2.0, origin.Y - formattedText.Height / 2.0)
                : origin;

            if (background != null)
            {
                drawingContext.DrawRectangle(background, null, new Rect(drawPoint.X - 2, drawPoint.Y, formattedText.Width + 4, formattedText.Height));
            }
            drawingContext.DrawText(formattedText, drawPoint);
        }

        private static Point ToScreenPoint(Point center, double radius, double angleDegrees)
        {
            // Use a compass-style polar display: 0° at the top, increasing clockwise.
            double radians = angleDegrees * Math.PI / 180.0;
            return new Point(
                center.X + radius * Math.Sin(radians),
                center.Y - radius * Math.Cos(radians));
        }

        private static double NormalizeAngle(double angleDegrees)
        {
            double normalized = angleDegrees % 360.0;
            return normalized < 0 ? normalized + 360.0 : normalized;
        }

        private static string FormatTickValue(double value)
        {
            if (Math.Abs(value) >= 1000)
            {
                double thousands = value / 1000.0;
                string format = Math.Abs(thousands - Math.Round(thousands)) < 0.05 ? "F0" : "F1";
                return $"{thousands.ToString(format, CultureInfo.InvariantCulture)}k";
            }

            if (Math.Abs(value) >= 100)
            {
                return value.ToString("F0", CultureInfo.InvariantCulture);
            }

            if (Math.Abs(value) >= 10)
            {
                return value.ToString("F1", CultureInfo.InvariantCulture);
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private RenderTargetBitmap CreateSnapshot()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0)
            {
                throw new InvalidOperationException("The polar plot is not ready to render.");
            }

            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));

            DrawingVisual snapshotVisual = new DrawingVisual();
            using (DrawingContext context = snapshotVisual.RenderOpen())
            {
                context.DrawRectangle(ConoscopePlotTheme.Resolve(this).Background, null, new Rect(0, 0, ActualWidth, ActualHeight));
                context.DrawRectangle(new VisualBrush(this), null, new Rect(0, 0, ActualWidth, ActualHeight));
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);

            bitmap.Render(snapshotVisual);
            return bitmap;
        }

        private static BitmapEncoder CreateBitmapEncoder(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".bmp" => new BmpBitmapEncoder(),
                ".jpg" => new JpegBitmapEncoder(),
                ".jpeg" => new JpegBitmapEncoder(),
                _ => new PngBitmapEncoder(),
            };
        }

        private static double GetNiceRadialMaximum(IEnumerable<double> values)
        {
            double maxValue = values
                .Where(value => double.IsFinite(value) && value > 0)
                .DefaultIfEmpty(0)
                .Max();

            if (maxValue <= 0)
            {
                return 1;
            }

            const int ringCount = 6;
            double rawStep = maxValue / ringCount;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
            double normalized = rawStep / magnitude;
            double niceNormalized = normalized <= 1 ? 1
                : normalized <= 1.5 ? 1.5
                : normalized <= 2 ? 2
                : normalized <= 2.5 ? 2.5
                : normalized <= 3 ? 3
                : normalized <= 4 ? 4
                : normalized <= 5 ? 5
                : 10;

            return niceNormalized * magnitude * ringCount;
        }

        private static Pen CreatePen(Brush brush, double thickness)
        {
            Pen pen = new Pen(brush, thickness);
            if (pen.CanFreeze)
            {
                pen.Freeze();
            }

            return pen;
        }
    }
}
