using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using cvColorVision;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public enum MagnigifierType
    {
        Circle,
        Rect
    }


    internal sealed class CvcieMouseMagnifierManager : IEditorToggleToolBase, IDisposable
    {
        private readonly EditorContext _context;
        private readonly ImageMouseInfoProvider _mouseInfoProvider;
        private readonly Func<IntPtr> _getConvertHandle;
        private readonly Action _ensureBufferLoaded;
        private readonly Func<float[]?> _getExp;
        private readonly Func<bool> _showDateFilePath;
        private readonly Func<int, int, (int pointIndex, int listIndex)> _findNearbyPoints;
        private readonly Func<CvcieMouseProbeOptions> _getOptions;
        private DrawingVisual DrawVisualImage { get; } = new DrawingVisual();
        private Zoombox ZoomboxSub => _context.Zoombox;
        private DrawCanvas Image => _context.DrawCanvas;

        public CvcieMouseMagnifierManager(
            EditorContext editorContext,
            Func<IntPtr> getConvertHandle,
            Action ensureBufferLoaded,
            Func<float[]?> getExp,
            Func<bool> showDateFilePath,
            Func<int, int, (int pointIndex, int listIndex)> findNearbyPoints,
            Func<CvcieMouseProbeOptions> getOptions)
        {
            _context = editorContext;
            _mouseInfoProvider = editorContext.MouseInfoProvider;
            _getConvertHandle = getConvertHandle;
            _ensureBufferLoaded = ensureBufferLoaded;
            _getExp = getExp;
            _showDateFilePath = showDateFilePath;
            _findNearbyPoints = findNearbyPoints;
            _getOptions = getOptions;
            ToolBarLocal = ToolBarLocal.Top;
            Order = 0;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageMouse");
        }

        public override string? GuidId => nameof(MouseMagnifierManager);

        public override bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                {
                    return;
                }

                _isChecked = value;
                DrawVisualImageControl(_isChecked);
                if (value)
                {
                    _mouseInfoProvider.PixelSampleChanged += HandlePixelSampleChanged;
                    Image.MouseEnter += MouseEnter;
                    Image.MouseLeave += MouseLeave;
                }
                else
                {
                    _mouseInfoProvider.PixelSampleChanged -= HandlePixelSampleChanged;
                    Image.MouseEnter -= MouseEnter;
                    Image.MouseLeave -= MouseLeave;
                }
            }
        }
        private bool _isChecked;

        private void HandlePixelSampleChanged(object? sender, ImagePixelSample pixelSample)
        {
            if (!IsChecked)
            {
                return;
            }

            if (!TryRenderOverlay(pixelSample))
            {
                DrawDefaultOverlay(pixelSample);
            }
        }

        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(false);

        private bool TryRenderOverlay(ImagePixelSample pixelSample)
        {
            float[]? exp = _getExp();
            if (exp == null || exp.Length == 0)
            {
                return false;
            }

            _ensureBufferLoaded();

            CvcieMouseProbeOptions options = _getOptions();
            double radius = Math.Max(1, options.Radius);
            int rectWidth = Math.Max(1, options.RectWidth);
            int rectHeight = Math.Max(1, options.RectHeight);

            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0;
            float dy = 0;
            float du = 0;
            float dv = 0;

            switch (options.MagnigifierType)
            {
                case MagnigifierType.Circle:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYCircle(_getConvertHandle(), pixelSample.PixelX, pixelSample.PixelY, ref dYVal, radius);
                        DrawProbeOverlay(pixelSample, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(_getConvertHandle(), pixelSample.PixelX, pixelSample.PixelY, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, radius);
                        DrawProbeOverlay(pixelSample, BuildPrimaryText(pixelSample, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                case MagnigifierType.Rect:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYRect(_getConvertHandle(), pixelSample.PixelX, pixelSample.PixelY, ref dYVal, rectWidth, rectHeight);
                        DrawProbeOverlay(pixelSample, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(_getConvertHandle(), pixelSample.PixelX, pixelSample.PixelY, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rectWidth, rectHeight);
                        DrawProbeOverlay(pixelSample, BuildPrimaryText(pixelSample, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void DrawProbeOverlay(ImagePixelSample pixelSample, string text1, string text2, MagnigifierType magnigifierType, double radius, double rectWidth, double rectHeight)
        {
            Point viewPosition = pixelSample.ViewPosition;

            if (Image.Source is not BitmapSource)
            {
                return;
            }

            using DrawingContext dc = DrawVisualImage.RenderOpen();

            if (magnigifierType == MagnigifierType.Circle)
            {
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Point(viewPosition.X, viewPosition.Y), radius, radius);
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11), new Point(viewPosition.X, viewPosition.Y), radius, radius);
            }
            else if (magnigifierType == MagnigifierType.Rect)
            {
                double rectWidthValue = Math.Max(1, rectWidth);
                double rectHeightValue = Math.Max(1, rectHeight);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Rect(viewPosition.X - rectWidthValue / 2, viewPosition.Y - rectHeightValue / 2, rectWidthValue, rectHeightValue));
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11), new Rect(viewPosition.X - rectWidthValue / 2, viewPosition.Y - rectHeightValue / 2, rectWidthValue, rectHeightValue));
            }

            var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * viewPosition.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * viewPosition.Y);
            dc.PushTransform(transform);

            double x1 = viewPosition.X + 1;
            double y1 = viewPosition.Y + 26;
            double height = 0;

            Brush brush = Brushes.White;
            FontFamily fontFamily = new("Arial");
            double fontSize = 10;
            FormattedText formattedText = new(pixelSample.ValueText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            FormattedText formattedTex1 = new(pixelSample.CoordinateText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            FormattedText formattedTex4 = new(text1, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            FormattedText formattedTex5 = new(text2, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            double width = Math.Max(Math.Max(formattedText.Width, formattedTex1.Width), Math.Max(formattedTex4.Width, formattedTex5.Width)) + 10;

            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 60));

            dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
            dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 18));
            dc.DrawText(formattedTex4, new Point(x1 + 5, y1 + height + 31));
            dc.DrawText(formattedTex5, new Point(x1 + 5, y1 + height + 44));

            dc.Pop();
            if (DrawVisualImage.Effect is not DropShadowEffect)
            {
                DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };
            }
        }

        private void DrawDefaultOverlay(ImagePixelSample pixelSample)
        {
            Point viewPosition = pixelSample.ViewPosition;
            Point pixelPosition = pixelSample.PixelPosition;

            if (Image.Source is not BitmapSource bitmapImage || pixelPosition.X <= 60 || pixelPosition.X >= bitmapImage.PixelWidth - 60 || pixelPosition.Y <= 45 || pixelPosition.Y >= bitmapImage.PixelHeight - 45)
            {
                return;
            }

            using DrawingContext dc = DrawVisualImage.RenderOpen();
            var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * viewPosition.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * viewPosition.Y);
            dc.PushTransform(transform);

            double x1 = viewPosition.X;
            double y1 = viewPosition.Y + 20;
            double height = 0;

            x1++;
            y1++;
            height -= 2;

            Brush brush = Brushes.White;
            FontFamily fontFamily = new("Arial");
            double fontSize = 10;
            FormattedText formattedText = new(pixelSample.ValueText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            FormattedText formattedTex1 = new(pixelSample.CoordinateText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            double width = Math.Max(formattedText.Width, formattedTex1.Width) + 10;

            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 30));
            dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
            dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 18));
            dc.Pop();
            if (DrawVisualImage.Effect is not DropShadowEffect)
            {
                DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };
            }
        }

        private void DrawVisualImageControl(bool control)
        {
            if (control)
            {
                if (!Image.ContainsVisual(DrawVisualImage))
                {
                    Image.AddVisualCommand(DrawVisualImage);
                }
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                {
                    Image.RemoveVisualCommand(DrawVisualImage);
                }
            }
        }

        private string BuildPrimaryText(ImagePixelSample pixelSample, float dXVal, float dYVal, float dZVal)
        {
            string text = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
            if (!_showDateFilePath())
            {
                return text;
            }

            (int pointIndex, int listIndex) = _findNearbyPoints(pixelSample.PixelX, pixelSample.PixelY);
            if (pointIndex < 0 || listIndex < 0)
            {
                return text;
            }

            return $"{text},({pointIndex + 1},{listIndex + 1})";
        }

        public void Dispose()
        {
            IsChecked = false;
            GC.SuppressFinalize(this);
        }
    }
}