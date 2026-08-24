using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVCircle : DrawingVisualBase<CircleProperties>, IDrawingVisual,ICircle, ISelectVisual, ILayoutScaleDrawingVisual
    {
        private bool _deferAttributeRender;

        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircle() 
        {
            Attribute = new CircleProperties();
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        public DVCircle(CircleProperties attribute)
        {
            Attribute = attribute;
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(CircleProperties.Pen))
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

        public bool IsDrawing { get; set; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (!ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.Radius, out Rect bounds))
                return;

            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, bounds.Width / 2, bounds.Height / 2);

            if (IsDrawing || (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg)))
            {
                Typeface typeface = new(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch);
                double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                if (IsDrawing)
                {
                    string text = Attribute.Center.X.ToString("F0") + "," + Attribute.Center.Y.ToString("F0");
                    FormattedText formattedText = new(text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(formattedText, Attribute.Center);
                    FormattedText radiusText = new(Attribute.Radius.ToString("F2"), CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(radiusText, new Point(bounds.Right, Attribute.Center.Y));
                }

                if (IsMessageVisible && !string.IsNullOrWhiteSpace(Attribute.Msg))
                {
                    FormattedText formattedText = new(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(formattedText, new Point(Attribute.Center.X - formattedText.Width / 2, Attribute.Center.Y - formattedText.Height / 2));
                }
            }
        }

        public override Rect GetRect()
        {
            return ShapeGeometry.TryGetEllipseBounds(Attribute.Center, Attribute.Radius, Attribute.Radius, out Rect bounds)
                ? bounds
                : Rect.Empty;
        }

        public override void SetRect(Rect rect)
        {
            if (!ShapeGeometry.IsFinite(rect))
                return;

            bool wasDeferred = _deferAttributeRender;
            _deferAttributeRender = true;
            try
            {
                Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                Attribute.Radius = Math.Min(rect.Width, rect.Height) / 2;
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
