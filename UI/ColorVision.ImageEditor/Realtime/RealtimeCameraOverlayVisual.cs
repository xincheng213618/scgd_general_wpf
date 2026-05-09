using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Settings;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Realtime
{
    public sealed class RealtimeCameraOverlayVisual : DrawingVisual
    {
        private readonly DefaultRealtimeCameraConfig _config;
        private bool _isAttached;
        private string _statusText = string.Empty;

        public RealtimeCameraOverlayVisual(DefaultRealtimeCameraConfig? config = null)
        {
            _config = config ?? DefaultRealtimeCameraConfig.Current;
        }

        public void Attach()
        {
            if (_isAttached)
            {
                return;
            }

            _config.TextProperties.PropertyChanged += OverlayConfigChanged;
            _config.RectangleTextProperties.PropertyChanged += OverlayConfigChanged;
            _isAttached = true;
            Render();
        }

        public void Detach()
        {
            if (!_isAttached)
            {
                return;
            }

            _config.TextProperties.PropertyChanged -= OverlayConfigChanged;
            _config.RectangleTextProperties.PropertyChanged -= OverlayConfigChanged;
            _isAttached = false;
            Clear();
        }

        public Rect GetProcessingRoi(int width, int height)
        {
            Rect rect = _config.RectangleTextProperties.Rect;
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return new Rect(0, 0, width, height);
            }

            return rect;
        }

        public void UpdateMetrics(double articulation, double fps)
        {
            string statusText = string.Format(CultureInfo.InvariantCulture, "fps:{0:F1} Articulation: {1:F5}", fps, articulation);
            if (string.Equals(_statusText, statusText, StringComparison.Ordinal))
            {
                return;
            }

            _statusText = statusText;
            Render();
        }

        public void ResetMetrics()
        {
            if (_statusText.Length == 0)
            {
                return;
            }

            _statusText = string.Empty;
            Render();
        }

        private void OverlayConfigChanged(object? sender, PropertyChangedEventArgs e)
        {
            Render();
        }

        private void Clear()
        {
            using DrawingContext dc = RenderOpen();
        }

        private void Render()
        {
            using DrawingContext dc = RenderOpen();
            DrawRoi(dc);
            DrawStatus(dc);
        }

        private void DrawRoi(DrawingContext dc)
        {
            RectangleTextProperties rectangle = _config.RectangleTextProperties;
            if (rectangle.Rect.Width <= 0 || rectangle.Rect.Height <= 0)
            {
                return;
            }

            dc.DrawRectangle(rectangle.Brush, rectangle.Pen, rectangle.Rect);
        }

        private void DrawStatus(DrawingContext dc)
        {
            TextProperties text = _config.TextProperties;
            if (!text.IsShowText || string.IsNullOrWhiteSpace(_statusText))
            {
                return;
            }

            FormattedText formattedText = new(
                _statusText,
                CultureInfo.CurrentCulture,
                text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                Math.Max(text.FontSize, 1),
                text.Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            Rect backgroundRect = new(text.Position.X, text.Position.Y, formattedText.Width, formattedText.Height);
            if (text.Background != null && text.Background != Brushes.Transparent)
            {
                dc.DrawRectangle(text.Background, null, backgroundRect);
            }

            dc.DrawText(formattedText, text.Position);
        }
    }
}