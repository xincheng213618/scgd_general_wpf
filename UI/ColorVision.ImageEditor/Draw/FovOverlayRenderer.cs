using ColorVision.Core;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    /// <summary>
    /// Renders the shared FOV evidence overlay for both ImageView calculations and project results.
    /// </summary>
    public static class FovOverlayRenderer
    {
        public static void Render(ImageProcessingContext imageContext, DrawEditorContext drawContext, FovMeasurement measurement)
        {
            ArgumentNullException.ThrowIfNull(imageContext);
            ArgumentNullException.ThrowIfNull(drawContext);
            ArgumentNullException.ThrowIfNull(measurement);
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(measurement.Corners, out string geometryError))
                throw new ArgumentException($"FOV overlay corners are invalid: {geometryError}", nameof(measurement));

            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FovTag);
            double scaleX = GetPixelToDipScale(imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double scaleY = GetPixelToDipScale(imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            Point Position(LuminousAreaPoint point) => new(point.X * scaleX, point.Y * scaleY);
            Point[] corners = measurement.Corners.Select(Position).ToArray();
            Point left = Midpoint(corners[0], corners[3]);
            Point right = Midpoint(corners[1], corners[2]);
            Point up = Midpoint(corners[0], corners[1]);
            Point down = Midpoint(corners[3], corners[2]);
            Point center = new(corners.Average(point => point.X), corners.Average(point => point.Y));
            double zoom = AlgorithmResultOverlay.GetZoom(drawContext);

            AlgorithmResultOverlay.AddPolygon(
                drawContext,
                corners,
                new Pen(Brushes.DeepSkyBlue, 2 / zoom),
                AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.AddLine(drawContext, left, right, new Pen(Brushes.Orange, 1.5 / zoom), AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.AddLine(drawContext, up, down, new Pen(Brushes.LimeGreen, 1.5 / zoom), AlgorithmResultOverlay.FovTag);
            Pen diagonalPen = new(Brushes.MediumPurple, 1 / zoom) { DashStyle = DashStyles.Dash };
            AlgorithmResultOverlay.AddLine(drawContext, corners[3], corners[1], diagonalPen, AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.AddLine(drawContext, corners[0], corners[2], diagonalPen.CloneCurrentValue(), AlgorithmResultOverlay.FovTag);

            string[] names = ["LT", "RT", "RB", "LB"];
            for (int index = 0; index < corners.Length; index++)
                AlgorithmResultOverlay.AddLabel(drawContext, corners[index], names[index], Brushes.DeepSkyBlue, AlgorithmResultOverlay.FovTag, scaleRadiusWithFontSize: true);
            AlgorithmResultOverlay.AddLabel(
                drawContext,
                Interpolate(left, right, 0.25),
                $"H {measurement.DirectionalHorizontalFovDegrees:F4}°",
                Brushes.Orange,
                AlgorithmResultOverlay.FovTag,
                scaleRadiusWithFontSize: true);
            AlgorithmResultOverlay.AddLabel(
                drawContext,
                Interpolate(up, down, 0.25),
                $"V {measurement.DirectionalVerticalFovDegrees:F4}°",
                Brushes.LimeGreen,
                AlgorithmResultOverlay.FovTag,
                scaleRadiusWithFontSize: true);
            AlgorithmResultOverlay.AddLabel(
                drawContext,
                center,
                $"D {measurement.DiagonalFovDegrees:F4}°",
                Brushes.MediumPurple,
                AlgorithmResultOverlay.FovTag,
                scaleRadiusWithFontSize: true);
        }

        private static Point Midpoint(Point first, Point second) =>
            new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

        private static Point Interpolate(Point start, Point end, double amount) =>
            new(start.X + (end.X - start.X) * amount, start.Y + (end.Y - start.Y) * amount);

        private static double GetPixelToDipScale(double dpi) =>
            double.IsFinite(dpi) && dpi > 0 ? 96 / dpi : 1;
    }
}
