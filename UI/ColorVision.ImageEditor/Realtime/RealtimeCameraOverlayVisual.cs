using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Settings;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeCameraOverlayVisual : DrawingVisual
    {
        private readonly DefaultRealtimeCameraConfig _config;
        private bool _isAttached;
        private bool _isRoiVisible = true;
        private string _statusText = string.Empty;

        public RealtimeCameraOverlayVisual(DefaultRealtimeCameraConfig? config = null)
        {
            _config = config ?? DefaultRealtimeCameraConfig.Current;
        }

        public bool IsRoiVisible
        {
            get => _isRoiVisible;
            set
            {
                if (_isRoiVisible == value) return;
                _isRoiVisible = value;
                RequestRender();
            }
        }

        public void Attach()
        {
            if (_isAttached) return;

            _config.RectangleTextProperties.PropertyChanged += OverlayConfigChanged;
            _isAttached = true;
            RequestRender();
        }

        public void Detach()
        {
            if (!_isAttached) return;

            _config.RectangleTextProperties.PropertyChanged -= OverlayConfigChanged;
            _isAttached = false;
            RequestClear();
        }

        public Rect GetProcessingRoi(int width, int height)
        {
            Rect rect = _config.RectangleTextProperties.Rect;
            return rect.Width <= 0 || rect.Height <= 0 ? new Rect(0, 0, width, height) : rect;
        }

        public void UpdateMetrics(double fps, double? articulation)
        {
            string statusText = articulation.HasValue
                ? string.Format(CultureInfo.InvariantCulture, "fps:{0:F1} 清晰度:{1:F5}", fps, articulation.Value)
                : string.Format(CultureInfo.InvariantCulture, "fps:{0:F1}", fps);
            if (string.Equals(_statusText, statusText, StringComparison.Ordinal)) return;

            _statusText = statusText;
            RequestRender();
        }

        public void ResetMetrics()
        {
            if (_statusText.Length == 0) return;

            _statusText = string.Empty;
            RequestRender();
        }

        private void OverlayConfigChanged(object? sender, PropertyChangedEventArgs e) => RequestRender();

        private void RequestClear()
        {
            if (Dispatcher.CheckAccess()) { ClearCore(); return; }
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(ClearCore));
        }

        private void RequestRender()
        {
            if (Dispatcher.CheckAccess()) { RenderCore(); return; }
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            _ = Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderCore));
        }

        private void ClearCore()
        {
            using DrawingContext dc = RenderOpen();
        }

        private void RenderCore()
        {
            using DrawingContext dc = RenderOpen();
            RectangleTextProperties rectangle = _config.RectangleTextProperties;
            DrawRoi(dc, rectangle);
            DrawStatus(dc, rectangle);
        }

        private void DrawRoi(DrawingContext dc, RectangleTextProperties rectangle)
        {
            if (!_isRoiVisible || rectangle.Rect.Width <= 0 || rectangle.Rect.Height <= 0) return;
            dc.DrawRectangle(rectangle.Brush, rectangle.Pen, rectangle.Rect);
        }

        private void DrawStatus(DrawingContext dc, RectangleTextProperties rectangle)
        {
            if (!rectangle.IsShowText || string.IsNullOrWhiteSpace(_statusText)) return;

            FormattedText formattedText = new(
                _statusText,
                CultureInfo.CurrentCulture,
                rectangle.FlowDirection,
                new Typeface(rectangle.FontFamily, rectangle.FontStyle, rectangle.FontWeight, rectangle.FontStretch),
                Math.Max(rectangle.FontSize, 1),
                rectangle.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            dc.DrawText(formattedText, GetStatusOrigin(rectangle.Rect, formattedText, _isRoiVisible));
        }

        private static Point GetStatusOrigin(Rect rect, FormattedText formattedText, bool anchorToRoi)
        {
            if (!anchorToRoi || rect.Width <= 0 || rect.Height <= 0) return new Point();

            double x = Math.Max(0, rect.Right - formattedText.Width);
            double y = rect.Top - formattedText.Height;
            if (y < 0) y = rect.Top;
            return new Point(x, y);
        }
    }
}
