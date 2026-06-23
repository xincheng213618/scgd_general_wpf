using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Video
{
    internal sealed class CrossGuideOverlayVisual : DrawingVisual
    {
        private static readonly Pen StandardPen = new(new SolidColorBrush(Color.FromArgb(210, 255, 48, 48)), 1);
        private static readonly Pen CrossPen = new(new SolidColorBrush(Color.FromArgb(230, 80, 255, 120)), 1.5);
        private static readonly Pen LinkPen = new(new SolidColorBrush(Color.FromArgb(180, 255, 210, 64)), 1);
        private static readonly Pen RoiPen = CreateDashedPen(Color.FromArgb(160, 80, 180, 255));
        private static readonly Brush PassBrush = new SolidColorBrush(Color.FromArgb(230, 80, 255, 120));
        private static readonly Brush FailBrush = new SolidColorBrush(Color.FromArgb(230, 255, 80, 80));
        private static readonly Brush TextBackgroundBrush = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0));

        private VideoCrossGuideResult? _result;
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

        public void Update(VideoCrossGuideResult result)
        {
            _result = result;
            RequestRender();
        }

        public void Clear()
        {
            _result = null;
            RequestRender();
        }

        private static Pen CreateDashedPen(Color color)
        {
            Pen pen = new(new SolidColorBrush(color), 1) { DashStyle = DashStyles.Dash };
            return pen;
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

            if (result.Roi.Width > 0 && result.Roi.Height > 0 && (result.Roi.Width < result.FrameWidth || result.Roi.Height < result.FrameHeight))
                dc.DrawRectangle(null, RoiPen, result.Roi);

            DrawCross(dc, result.StandardCenter, result.FrameWidth, result.FrameHeight, StandardPen, 14);

            if (result.Found)
            {
                DrawCross(dc, result.CrossCenter, result.FrameWidth, result.FrameHeight, CrossPen, 20);
                dc.DrawLine(LinkPen, result.StandardCenter, result.CrossCenter);
                dc.DrawEllipse(null, CrossPen, result.CrossCenter, 6, 6);
            }

            DrawStatus(dc, result);
        }

        private static void DrawCross(DrawingContext dc, Point center, double width, double height, Pen pen, double markerHalfSize)
        {
            if (center.X < 0 || center.Y < 0 || center.X > width || center.Y > height) return;

            dc.DrawLine(pen, new Point(0, center.Y), new Point(width, center.Y));
            dc.DrawLine(pen, new Point(center.X, 0), new Point(center.X, height));
            dc.DrawLine(pen, new Point(center.X - markerHalfSize, center.Y - markerHalfSize), new Point(center.X + markerHalfSize, center.Y + markerHalfSize));
            dc.DrawLine(pen, new Point(center.X - markerHalfSize, center.Y + markerHalfSize), new Point(center.X + markerHalfSize, center.Y - markerHalfSize));
        }

        private void DrawStatus(DrawingContext dc, VideoCrossGuideResult result)
        {
            string text = result.Found
                ? string.Format(CultureInfo.InvariantCulture, "dx:{0:F2}px  dy:{1:F2}px  d:{2:F2}px  {3}", result.OffsetX, result.OffsetY, result.Distance, result.IsPass ? "PASS" : "NG")
                : result.Message;

            Brush textBrush = result.Found && result.IsPass ? PassBrush : FailBrush;
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                16,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Point origin = GetStatusOrigin(result, formattedText);
            Rect background = new(origin.X - 4, origin.Y - 2, formattedText.Width + 8, formattedText.Height + 4);
            dc.DrawRoundedRectangle(TextBackgroundBrush, null, background, 3, 3);
            dc.DrawText(formattedText, origin);
        }

        private static Point GetStatusOrigin(VideoCrossGuideResult result, FormattedText formattedText)
        {
            if (result.Roi.Width > 0 && result.Roi.Height > 0)
            {
                double x = Math.Max(0, Math.Min(result.Roi.Left, result.FrameWidth - formattedText.Width - 8));
                double y = result.Roi.Top - formattedText.Height - 6;
                if (y >= 0) return new Point(x, y);

                y = Math.Min(result.Roi.Bottom + 6, Math.Max(0, result.FrameHeight - formattedText.Height - 4));
                return new Point(x, y);
            }

            return new Point(6, 6);
        }
    }
}
