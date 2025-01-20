using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVCircle : DrawingVisualBase<CircleProperties>, IDrawingVisual,ICircle
    {
        public bool AutoAttributeChanged { get; set; }
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircle()
        {
            Attribute = new CircleProperties();
            Attribute.Id = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s,e)=> { if (AutoAttributeChanged && e.PropertyName != "ID") Render(); };
        }

        private TextAttribute TextAttribute = new();

        public bool IsDrawing { get; set; }

        public override void Render()
        {
            if (IsDrawing)
            {
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
                string Text = Attribute.Center.X.ToString("F0") + "," + Attribute.Center.Y.ToString("F0");
                FormattedText formattedText = new(Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                
                using DrawingContext dc = RenderOpen();
                dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
                dc.DrawText(formattedText, Attribute.Center);

                FormattedText RadiusText = new(Attribute.Radius.ToString("F2"), CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(RadiusText,  new Point(Attribute.Radius + Attribute.Center.X, Attribute.Center.Y));
            }
            else
            {
                using DrawingContext dc = RenderOpen();
                dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
            }

        }
    }

}
