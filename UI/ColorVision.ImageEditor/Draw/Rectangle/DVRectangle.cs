using System.Globalization;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DVRectangle : DrawingVisualBase<RectangleProperties>, IDrawingVisual, IRectangle, ILayoutScaleDrawingVisual
    {
        private bool _deferAttributeRender;

        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVRectangle()
        {
            Attribute = new RectangleProperties();
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }
        public DVRectangle(RectangleProperties rectangleProperties)
        {
            Attribute = rectangleProperties;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(RectangleProperties.Pen))
                LayoutBasePenThickness = null;

            if (!_deferAttributeRender)
                Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                ApplyLayoutScaleCore(context, Pen, value => Pen = value);
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }
        }


        private TextAttribute TextAttribute = new();

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (Attribute.Rect.IsEmpty || !ShapeGeometry.IsFinite(Attribute.Rect))
                return;

            dc.PushTransform(RegionGeometry.Transform(Attribute));
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
            dc.Pop();

            if (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                formattedText.TextAlignment = Attribute.IsMeasurementMessage ? TextAlignment.Center : TextAlignment.Left;
                Point position = Attribute.IsMeasurementMessage
                    ? new Point(Attribute.Rect.X + Attribute.Rect.Width / 2, Attribute.Rect.Y + Attribute.Rect.Height / 2 - formattedText.Height / 2)
                    : new Point(Attribute.Rect.X + formattedText.Width / 2 + Attribute.Rect.Width / 2, Attribute.Rect.Y + Attribute.Rect.Height / 2);
                dc.DrawText(formattedText, position);
            }
        }
        public override Rect GetRect() => RegionGeometry.Bounds(Attribute);

        public override void SetRect(Rect rect)
        {
            if (!rect.IsEmpty && !ShapeGeometry.IsFinite(rect))
                return;

            rect = RegionGeometry.ResizeLocalBounds(Attribute, rect);

            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Rect = rect;
            }
            finally
            {
                _deferAttributeRender = wasDeferred;
            }

            if (!wasDeferred)
                Render();
        }
    }



}
