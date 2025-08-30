using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVCircle : DrawingVisualBase<CircleProperties>, IDrawingVisual,ICircle, ISelectVisual
    {
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircle() 
        {
            Attribute = new CircleProperties();
            Attribute.PropertyChanged += (s, e) => Render();
        }

        private TextAttribute TextAttribute = new();

        public bool IsDrawing { get; set; }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            if (IsDrawing)
            {
                string Text = Attribute.Center.X.ToString("F0") + "," + Attribute.Center.Y.ToString("F0");
                FormattedText formattedText = new(Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                
                dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
                dc.DrawText(formattedText, Attribute.Center);
                FormattedText RadiusText = new(Attribute.Radius.ToString("F2"), CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(RadiusText,  new Point(Attribute.Radius + Attribute.Center.X, Attribute.Center.Y));
            }
            else
            {
                dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
            }

            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Center.X - formattedText.Width / 2, Attribute.Center.Y - formattedText.Height / 2));
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
