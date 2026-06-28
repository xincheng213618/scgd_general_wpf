using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal sealed class CrossGuideOverlayVisual : DrawingVisual
    {
        private static readonly Brush StandardBrush = new SolidColorBrush(Color.FromArgb(230, 255, 48, 48));
        private static readonly Brush XAxisBrush = new SolidColorBrush(Color.FromArgb(240, 80, 255, 120));
        private static readonly Brush YAxisBrush = new SolidColorBrush(Color.FromArgb(240, 80, 210, 255));
        private static readonly Brush ExtensionBrush = new SolidColorBrush(Color.FromArgb(150, 185, 145, 255));
        private static readonly Brush LinkBrush = new SolidColorBrush(Color.FromArgb(210, 255, 210, 64));
        private static readonly Brush RotationBrush = new SolidColorBrush(Color.FromArgb(240, 255, 220, 64));
        private static readonly Brush PassBrush = new SolidColorBrush(Color.FromArgb(230, 80, 255, 120));
        private static readonly Brush FailBrush = new SolidColorBrush(Color.FromArgb(230, 255, 80, 80));
        private static readonly Brush TextBackgroundBrush = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0));

        private VideoCrossGuideResult? _result;
        private RealtimeCameraMetrics? _metrics;
        private bool _isAttached;

        public void Attach()
        {
            if (_isAttached) return;

            _isAttached = true;
            RequestRender();
        }

        public void Detach()
        {
            if (!_isAttached) return;

            _isAttached = false;
            _result = null;
            RequestRender();
        }

        public void Update(VideoCrossGuideResult result, RealtimeCameraMetrics metrics)
        {
            _result = result;
            _metrics = metrics;
            RequestRender();
        }

        public void Clear()
        {
            _result = null;
            RequestRender();
        }

        private static Pen CreatePen(Brush brush, double thickness)
        {
            return new Pen(brush, thickness);
        }

        private static Pen CreateDashedPen(Brush brush, double thickness)
        {
            return new Pen(brush, thickness) { DashStyle = DashStyles.Dash };
        }

        private void RequestRender()
        {
            if (Dispatcher.CheckAccess())
            {
                RenderCore();
                return;
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderCore));
        }

        private void RenderCore()
        {
            using DrawingContext dc = RenderOpen();
            if (!_isAttached || _result == null) return;

            VideoCrossGuideResult result = _result.Value;
            if (result.FrameWidth <= 0 || result.FrameHeight <= 0) return;

            double lineThickness = GetLineThickness(result);
            double markerHalfSize = GetMarkerHalfSize(result);
            Pen standardPen = CreatePen(StandardBrush, lineThickness);
            Pen xAxisPen = CreatePen(XAxisBrush, lineThickness * 1.25);
            Pen yAxisPen = CreatePen(YAxisBrush, lineThickness * 1.25);
            Pen extensionPen = CreateDashedPen(ExtensionBrush, Math.Max(1, lineThickness * 0.65));
            Pen linkPen = CreatePen(LinkBrush, Math.Max(1, lineThickness * 0.7));
            Pen rotationPen = CreatePen(RotationBrush, lineThickness * 1.4);
            Pen roiPen = CreateDashedPen(XAxisBrush, lineThickness);

            if (result.Roi.Width > 0 && result.Roi.Height > 0 && (result.Roi.Width < result.FrameWidth || result.Roi.Height < result.FrameHeight))
                dc.DrawRectangle(null, roiPen, result.Roi);

            DrawCross(dc, result.StandardCenter, result.FrameWidth, result.FrameHeight, standardPen, markerHalfSize);

            if (result.Found)
            {
                if (result.XAxisFound)
                    DrawExtendedLine(dc, result.XAxisStart, result.XAxisEnd, result.FrameWidth, result.FrameHeight, extensionPen);
                else
                    DrawExtendedAngleLine(dc, result.CrossCenter, result.RotationZDeg, result.FrameWidth, result.FrameHeight, extensionPen);

                if (result.YAxisFound)
                    DrawExtendedLine(dc, result.YAxisStart, result.YAxisEnd, result.FrameWidth, result.FrameHeight, extensionPen);
                else
                    DrawExtendedAngleLine(dc, result.CrossCenter, result.RotationZDeg + 90, result.FrameWidth, result.FrameHeight, extensionPen);

                if (result.XAxisFound)
                    dc.DrawLine(xAxisPen, result.XAxisStart, result.XAxisEnd);
                else
                    DrawAngleLine(dc, result.CrossCenter, result.RotationZDeg, GetFallbackAxisHalfLength(result), xAxisPen);

                if (result.YAxisFound)
                    dc.DrawLine(yAxisPen, result.YAxisStart, result.YAxisEnd);
                else
                    DrawAngleLine(dc, result.CrossCenter, result.RotationZDeg + 90, GetFallbackAxisHalfLength(result), yAxisPen);

                DrawRotatedCenterMarker(dc, result.CrossCenter, result.RotationZDeg, rotationPen, markerHalfSize * 1.35);
                dc.DrawLine(linkPen, result.StandardCenter, result.CrossCenter);
                dc.DrawEllipse(null, xAxisPen, result.CrossCenter, markerHalfSize * 0.45, markerHalfSize * 0.45);
            }

            DrawStatus(dc, result);
        }

        private static double GetLineThickness(VideoCrossGuideResult result)
            => Math.Clamp(Math.Min(result.FrameWidth, result.FrameHeight) * 0.0012, 3, 12);

        private static double GetMarkerHalfSize(VideoCrossGuideResult result)
            => Math.Clamp(Math.Min(result.FrameWidth, result.FrameHeight) * 0.008, 18, 80);

        private static double GetFallbackAxisHalfLength(VideoCrossGuideResult result)
            => Math.Clamp(Math.Min(result.FrameWidth, result.FrameHeight) * 0.045, 70, 320);

        private static void DrawCross(DrawingContext dc, Point center, double width, double height, Pen pen, double markerHalfSize)
        {
            if (center.X < 0 || center.Y < 0 || center.X > width || center.Y > height) return;

            dc.DrawLine(pen, new Point(0, center.Y), new Point(width, center.Y));
            dc.DrawLine(pen, new Point(center.X, 0), new Point(center.X, height));
            dc.DrawLine(pen, new Point(center.X - markerHalfSize, center.Y - markerHalfSize), new Point(center.X + markerHalfSize, center.Y + markerHalfSize));
            dc.DrawLine(pen, new Point(center.X - markerHalfSize, center.Y + markerHalfSize), new Point(center.X + markerHalfSize, center.Y - markerHalfSize));
        }

        private static void DrawRotatedCenterMarker(DrawingContext dc, Point center, double angleDeg, Pen pen, double markerHalfSize)
        {
            if (center.X < 0 || center.Y < 0) return;

            DrawAngleLine(dc, center, angleDeg + 45, markerHalfSize, pen);
            DrawAngleLine(dc, center, angleDeg - 45, markerHalfSize, pen);
        }

        private static void DrawAngleLine(DrawingContext dc, Point center, double angleDeg, double halfLength, Pen pen)
        {
            double angleRad = angleDeg * Math.PI / 180.0;
            double dx = Math.Cos(angleRad) * halfLength;
            double dy = Math.Sin(angleRad) * halfLength;
            dc.DrawLine(pen, new Point(center.X - dx, center.Y - dy), new Point(center.X + dx, center.Y + dy));
        }

        private static void DrawExtendedAngleLine(DrawingContext dc, Point center, double angleDeg, double width, double height, Pen pen)
        {
            double angleRad = angleDeg * Math.PI / 180.0;
            Point start = new(center.X - Math.Cos(angleRad), center.Y - Math.Sin(angleRad));
            Point end = new(center.X + Math.Cos(angleRad), center.Y + Math.Sin(angleRad));
            DrawExtendedLine(dc, start, end, width, height, pen);
        }

        private static void DrawExtendedLine(DrawingContext dc, Point start, Point end, double width, double height, Pen pen)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001) return;

            double dirX = dx / length;
            double dirY = dy / length;
            List<Point> points = new(4);
            AddLineEdgeIntersection(points, start, dirX, dirY, 0, true, width, height);
            AddLineEdgeIntersection(points, start, dirX, dirY, width, true, width, height);
            AddLineEdgeIntersection(points, start, dirX, dirY, 0, false, width, height);
            AddLineEdgeIntersection(points, start, dirX, dirY, height, false, width, height);

            if (points.Count < 2) return;

            Point a = points[0];
            Point b = points[1];
            double maxDistance = DistanceSquared(a, b);
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    double distance = DistanceSquared(points[i], points[j]);
                    if (distance <= maxDistance) continue;

                    maxDistance = distance;
                    a = points[i];
                    b = points[j];
                }
            }

            dc.DrawLine(pen, a, b);
        }

        private static void AddLineEdgeIntersection(List<Point> points, Point origin, double dirX, double dirY, double edgeValue, bool isVerticalEdge, double width, double height)
        {
            double denominator = isVerticalEdge ? dirX : dirY;
            if (Math.Abs(denominator) < 0.0001) return;

            double t = (edgeValue - (isVerticalEdge ? origin.X : origin.Y)) / denominator;
            Point point = new(origin.X + dirX * t, origin.Y + dirY * t);
            if (point.X < -0.5 || point.X > width + 0.5 || point.Y < -0.5 || point.Y > height + 0.5) return;

            point.X = Math.Clamp(point.X, 0, width);
            point.Y = Math.Clamp(point.Y, 0, height);
            foreach (Point existing in points)
            {
                if (DistanceSquared(existing, point) < 0.25) return;
            }

            points.Add(point);
        }

        private static double DistanceSquared(Point a, Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private void DrawStatus(DrawingContext dc, VideoCrossGuideResult result)
        {
            string text = BuildStatusText(result);

            Brush textBrush = result.Found && result.IsPass ? PassBrush : FailBrush;
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                GetStatusFontSize(result),
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Point origin = GetStatusOrigin(result, formattedText);
            Rect background = new(origin.X - 4, origin.Y - 2, formattedText.Width + 8, formattedText.Height + 4);
            dc.DrawRoundedRectangle(TextBackgroundBrush, null, background, 3, 3);
            dc.DrawText(formattedText, origin);
        }

        private static double GetStatusFontSize(VideoCrossGuideResult result)
            => Math.Clamp(Math.Min(result.FrameWidth, result.FrameHeight) * 0.022, 36, 180);

        private string BuildStatusText(VideoCrossGuideResult result)
        {
            string metricsText = string.Empty;
            if (_metrics.HasValue)
            {
                RealtimeCameraMetrics metrics = _metrics.Value;
                metricsText = metrics.Articulation.HasValue
                    ? string.Format(CultureInfo.InvariantCulture, "fps:{0:F1}  清晰度:{1:F5}", metrics.Fps, metrics.Articulation.Value)
                    : string.Format(CultureInfo.InvariantCulture, "fps:{0:F1}", metrics.Fps);
            }

            string resultText = result.Found
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "dx(center):{0:F2}px  dy(center):{1:F2}px  d:{2:F2}px  {3}{4}Rotation:{5:+0.00;-0.00;0.00}deg  XRotation:{6:+0.00;-0.00;0.00}deg  YRotation:{7:+0.00;-0.00;0.00}deg",
                    result.OffsetX,
                    result.OffsetY,
                    result.Distance,
                    result.IsPass ? "PASS" : "NG",
                    Environment.NewLine,
                    result.RotationZDeg,
                    result.XRotationDeg,
                    result.YRotationDeg)
                : result.Message;

            return string.IsNullOrWhiteSpace(metricsText)
                ? resultText
                : metricsText + Environment.NewLine + resultText;
        }

        private static Point GetStatusOrigin(VideoCrossGuideResult result, FormattedText formattedText)
        {
            if (result.Roi.Width > 0 && result.Roi.Height > 0)
            {
                double x = Math.Max(0, Math.Min(result.Roi.Left, result.FrameWidth - formattedText.Width - 8));
                double y = result.Roi.Top - formattedText.Height - 14;
                if (y >= 0) return new Point(x, y);

                y = Math.Min(result.Roi.Bottom + 6, Math.Max(0, result.FrameHeight - formattedText.Height - 4));
                return new Point(x, y);
            }

            return new Point(6, 6);
        }
    }
}
