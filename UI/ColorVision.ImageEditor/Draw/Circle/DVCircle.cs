using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVCircle : DrawingVisualBase<CircleProperties>, IDrawingVisual,ICircle, ISelectVisual, ILayoutScaleDrawingVisual
    {
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircle() 
        {
            Attribute = new CircleProperties();
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVCircle(CircleProperties attribute)
        {
            Attribute = attribute;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public void ApplyLayoutScale(DrawingVisualScaleContext context)
        {
            ApplyLayoutScaleCore(context, Pen, value => Pen = value);
        }


        private TextAttribute TextAttribute = new();

        public bool IsDrawing { get; set; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);

            if (IsDrawing || !string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                Typeface typeface = new(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch);
                double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
                if (IsDrawing)
                {
                    string text = Attribute.Center.X.ToString("F0") + "," + Attribute.Center.Y.ToString("F0");
                    FormattedText formattedText = new(text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(formattedText, Attribute.Center);
                    FormattedText radiusText = new(Attribute.Radius.ToString("F2"), CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(radiusText, new Point(Attribute.Radius + Attribute.Center.X, Attribute.Center.Y));
                }

                if (!string.IsNullOrWhiteSpace(Attribute.Msg))
                {
                    FormattedText formattedText = new(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, typeface, TextAttribute.FontSize, TextAttribute.Brush, pixelsPerDip);
                    dc.DrawText(formattedText, new Point(Attribute.Center.X - formattedText.Width / 2, Attribute.Center.Y - formattedText.Height / 2));
                }
            }
        }

        public override Rect GetRect()
        {
            return new Rect(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - Attribute.Radius, Attribute.Radius *2, Attribute.Radius*2);
        }

        public override void SetRect(Rect rect)
        {
            Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Attribute.Radius = Math.Min(rect.Width, rect.Height) / 2;
            Render();
        }
    }

}
