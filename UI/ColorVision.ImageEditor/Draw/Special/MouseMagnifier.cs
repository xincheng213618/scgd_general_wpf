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
        private EditorContext EditorContext { get; }
        private readonly ImageMouseInfoProvider _mouseInfoProvider;

        public MouseMagnifierManager(EditorContext editorContext)
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
                FormattedText? formattedText2 = string.IsNullOrWhiteSpace(pixelSample.ColorimetryText)
                    ? null
                    : new FormattedText(pixelSample.ColorimetryText, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 9, brush, VisualTreeHelper.GetDpi(DrawVisualImage).PixelsPerDip);
                double width = Math.Max(Math.Max(formattedText.Width, formattedTex1.Width), formattedText2?.Width ?? 0) + 10;
                double panelHeight = formattedText2 == null ? 30 : 43;

                dc.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AA000000")), new Pen(Brushes.White, 0), new Rect(x1 - 1, y1 + height + 1, width + 2, panelHeight));

                dc.DrawText(formattedText, new Point(x1 + 5, y1 + height + 5));
                dc.DrawText(formattedTex1, new Point(x1 + 5, y1 + height + 18));
                if (formattedText2 != null)
                {
                    dc.DrawText(formattedText2, new Point(x1 + 5, y1 + height + 31));
                }
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
                    Image.AddVisualCommand(DrawVisualImage);
            }
            else
            {
                if (Image.ContainsVisual(DrawVisualImage))
                    Image.RemoveVisualCommand(DrawVisualImage);
            }
        }
    }
}
