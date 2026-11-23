using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Draw.Special
{
    /// <summary>
    /// 后续优化调整，成为不同的图像格式显示不同的参数
    /// </summary>
    public class MouseMagnifierManager : IEditorToggleToolBase
    {
        private static readonly Typeface LabelTypeface = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        private static readonly SolidColorBrush BackgroundBrush = new(Color.FromArgb(0xAA, 0, 0, 0));
        private static readonly Pen BackgroundPen = new(Brushes.White, 0);

        static MouseMagnifierManager()
        {
            BackgroundBrush.Freeze();
            BackgroundPen.Freeze();
        }

        private DrawEditorContext EditorContext { get; }
        private readonly ImageMouseInfoProvider _mouseInfoProvider;
        
        public MouseMagnifierManager(DrawEditorContext editorContext)
        {
            EditorContext = editorContext;
            _mouseInfoProvider = editorContext.MouseInfoProvider;
            ToolBarLocal = ToolBarLocal.Top;
            Order = 0;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageMouse");
        }

        public override string? GuidId => nameof(MouseMagnifierManager);

        private Zoombox ZoomboxSub => EditorContext.Zoombox;
        private DrawCanvas Image => EditorContext.DrawCanvas;

        private DrawingVisual DrawVisualImage { get; } = new DrawingVisual();

        public override bool IsChecked
        {
            get => _IsChecked; set
            {
                if (_IsChecked == value) return;
                _IsChecked = value;
                DrawVisualImageControl(_IsChecked);
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
        private bool _IsChecked;

        private void DrawImage(ImagePixelSample pixelSample)
        {
            Point viewPosition = pixelSample.ViewPosition;
            Point pixelPosition = pixelSample.PixelPosition;

            if (Image.Source is BitmapSource bitmapImage && pixelPosition.X > 60 && pixelPosition.X < bitmapImage.PixelWidth - 60 && pixelPosition.Y > 45 && pixelPosition.Y < bitmapImage.PixelHeight - 45)
            {
                using DrawingContext dc = DrawVisualImage.RenderOpen();
                Matrix matrix = ZoomboxSub.ContentMatrix;
                var transform = new MatrixTransform(1 / matrix.M11, matrix.M12, matrix.M21, 1 / matrix.M22, (1 - 1 / matrix.M11) * viewPosition.X, (1 - 1 / matrix.M22) * viewPosition.Y);
                dc.PushTransform(transform);

                double x1 = viewPosition.X;
                double y1 = viewPosition.Y + 20;
                double height = 0;

                x1++;
                y1++;
                height -= 2;

                Brush brush = Brushes.White;
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                double pixelsPerDip = VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip;
                double fontSize = 10;
                FormattedText formattedText = new(pixelSample.ValueText, culture, FlowDirection.LeftToRight, LabelTypeface, fontSize, brush, pixelsPerDip);
                FormattedText formattedTex1 = new(pixelSample.CoordinateText, culture, FlowDirection.LeftToRight, LabelTypeface, fontSize, brush, pixelsPerDip);
                double width = Math.Max(formattedText.Width, formattedTex1.Width) + 10;

                dc.DrawRectangle(BackgroundBrush, BackgroundPen, new Rect(x1 - 1, y1 + height + 1, width + 2, 30));

                dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 18));
                dc.Pop();
                if (DrawVisualImage.Effect is not DropShadowEffect)
                    DrawVisualImage.Effect = new DropShadowEffect() { Opacity = 0.5 };

            }
        }

        private void HandlePixelSampleChanged(object? sender, ImagePixelSample pixelSample)
        {
            if (!IsChecked)
            {
                return;
            }

            DrawImage(pixelSample);
        }

        public void MouseEnter(object sender, MouseEventArgs e) => DrawVisualImageControl(true);

        public void MouseLeave(object sender, MouseEventArgs e) => DrawVisualImageControl(false);

        public void DrawVisualImageControl(bool Control)
        {
            if (Control)
            {
                if (!Image.ContainsVisual(DrawVisualImage))
                    Image.AddOverlayVisual(DrawVisualImage);
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                    Image.RemoveOverlayVisual(DrawVisualImage);
            }
        }
    }
}
