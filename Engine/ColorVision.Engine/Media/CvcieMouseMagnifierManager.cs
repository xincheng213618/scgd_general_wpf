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
        private readonly IImageMouseInfoProvider _mouseInfoProvider;
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
            if (!editorContext.TryGetService<IImageMouseInfoProvider>(out IImageMouseInfoProvider? mouseInfoProvider) || mouseInfoProvider == null)
            {
                throw new InvalidOperationException($"{nameof(CvcieMouseMagnifierManager)} requires {nameof(IImageMouseInfoProvider)}.");
            }

            _mouseInfoProvider = mouseInfoProvider;
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
                    _mouseInfoProvider.MouseMoveColorHandler += HandleMouseMoveColor;
                    Image.MouseEnter += MouseEnter;
                    Image.MouseLeave += MouseLeave;
                }
                else
                {
                    _mouseInfoProvider.MouseMoveColorHandler -= HandleMouseMoveColor;
                    Image.MouseEnter -= MouseEnter;
                    Image.MouseLeave -= MouseLeave;
                }
            }
        }
        private bool _isChecked;

        private void HandleMouseMoveColor(object sender, ImageInfo imageInfo)
        {
            if (!IsChecked)
            {
                return;
            }

            TryRenderOverlay(imageInfo);
        }

        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(false);

        private bool TryRenderOverlay(ImageInfo imageInfo)
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
                        _ = ConvertXYZ.CM_GetYCircle(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dYVal, radius);
                        DrawProbeOverlay(imageInfo, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, radius);
                        DrawProbeOverlay(imageInfo, BuildPrimaryText(imageInfo, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                case MagnigifierType.Rect:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYRect(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dYVal, rectWidth, rectHeight);
                        DrawProbeOverlay(imageInfo, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rectWidth, rectHeight);
                        DrawProbeOverlay(imageInfo, BuildPrimaryText(imageInfo, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void DrawProbeOverlay(ImageInfo imageInfo, string text1, string text2, MagnigifierType magnigifierType, double radius, double rectWidth, double rectHeight)
        {
            Point actPoint = imageInfo.ActPoint;

            if (Image.Source is not BitmapSource)
            {
                return;
            }

            using DrawingContext dc = DrawVisualImage.RenderOpen();

            if (magnigifierType == MagnigifierType.Circle)
            {
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Point(actPoint.X, actPoint.Y), radius, radius);
                dc.DrawEllipse(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11), new Point(actPoint.X, actPoint.Y), radius, radius);
            }
            else if (magnigifierType == MagnigifierType.Rect)
            {
                double rectWidthValue = Math.Max(1, rectWidth);
                double rectHeightValue = Math.Max(1, rectHeight);
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.Black, 2 / ZoomboxSub.ContentMatrix.M11), new Rect(actPoint.X - rectWidthValue / 2, actPoint.Y - rectHeightValue / 2, rectWidthValue, rectHeightValue));
                dc.DrawRectangle(Brushes.Transparent, new Pen(Brushes.White, 1 / ZoomboxSub.ContentMatrix.M11), new Rect(actPoint.X - rectWidthValue / 2, actPoint.Y - rectHeightValue / 2, rectWidthValue, rectHeightValue));
            }

            var transform = new MatrixTransform(1 / ZoomboxSub.ContentMatrix.M11, ZoomboxSub.ContentMatrix.M12, ZoomboxSub.ContentMatrix.M21, 1 / ZoomboxSub.ContentMatrix.M22, (1 - 1 / ZoomboxSub.ContentMatrix.M11) * actPoint.X, (1 - 1 / ZoomboxSub.ContentMatrix.M22) * actPoint.Y);
            dc.PushTransform(transform);

            double x1 = actPoint.X + 1;
            double y1 = actPoint.Y + 26;
            double width = 128;
            double height = 0;

            dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, 60));

            Brush brush = Brushes.White;
            FontFamily fontFamily = new("Arial");
            double fontSize = 10;
            FormattedText formattedText = new($"R:{imageInfo.R}  G:{imageInfo.G}  B:{imageInfo.B}", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
            FormattedText formattedTex1 = new($"({imageInfo.X},{imageInfo.Y})", System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 18));
            FormattedText formattedTex4 = new(text1, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedTex4, new Point(x1 + 5, y1 + height + 31));
            FormattedText formattedTex5 = new(text2, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), fontSize, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
            dc.DrawText(formattedTex5, new Point(x1 + 5, y1 + height + 44));

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

        private string BuildPrimaryText(ImageInfo imageInfo, float dXVal, float dYVal, float dZVal)
        {
            string text = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
            if (!_showDateFilePath())
            {
                return text;
            }

            (int pointIndex, int listIndex) = _findNearbyPoints(imageInfo.X, imageInfo.Y);
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