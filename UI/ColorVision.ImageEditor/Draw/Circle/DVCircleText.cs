using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class CircleTextProperties: CircleProperties,ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();
        public bool IsShowText { get; set; } = true;

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value;  OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("Brush")]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontFamily")]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontStyle")]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontWeight")]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; OnPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontStretch")]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; OnPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FlowDirection")]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; OnPropertyChanged(); } }
    }



    public class DVCircleText : DrawingVisualBase<CircleTextProperties>, IDrawingVisual,ICircle
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVCircleText()
        {
            Attribute = new CircleTextProperties();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public DVCircleText(CircleTextProperties circleTextProperties)
        {
            Attribute = circleTextProperties;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            Attribute.PropertyChanged += (s, e) => Render();
        }



        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.RadiusY);

            double size = 0;
            if (Attribute.IsShowText)
            {
                FormattedText formattedText = new(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                size = formattedText.Width / 2;
                dc.DrawText(formattedText, new Point(Attribute.Center.X - size, Attribute.Center.Y - formattedText.Height / 2));
            }

            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Center.X + size +Radius/2, Attribute.Center.Y - formattedText.Height / 2));
            }
        }

        public override Rect GetRect()
        {
            return new Rect(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - Attribute.RadiusY, Attribute.Radius * 2, Attribute.RadiusY * 2);
        }
        public override void SetRect(Rect rect)
        {
            Attribute.Center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            Attribute.Radius = rect.Width / 2;
            Attribute.RadiusY = rect.Height / 2;
            Render();
        }
    }



}
