#pragma warning disable CA1711,CA2211
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class CircleTextAttribute: CircleAttribute
    {
        [Category("Circle"), DisplayName("TextAttribute")]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        [Category("TextAttribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; 
                NotifyPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontSize")]
        public double FontSize { get => TextAttribute.FontSize; set { TextAttribute.FontSize = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("Brush")]
        public Brush Foreground { get => TextAttribute.Brush; set { TextAttribute.Brush = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontFamily")]
        public FontFamily FontFamily { get => TextAttribute.FontFamily; set { TextAttribute.FontFamily = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FontStyle")]
        public FontStyle FontStyle { get => TextAttribute.FontStyle; set { TextAttribute.FontStyle = value; NotifyPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontWeight")]
        public FontWeight FontWeight { get => TextAttribute.FontWeight; set { TextAttribute.FontWeight = value; NotifyPropertyChanged(); } }
        [Category("TextAttribute"), DisplayName("FontStretch")]
        public FontStretch FontStretch { get => TextAttribute.FontStretch; set { TextAttribute.FontStretch = value; NotifyPropertyChanged(); } }

        [Category("TextAttribute"), DisplayName("FlowDirection")]
        public FlowDirection FlowDirection { get => TextAttribute.FlowDirection; set { TextAttribute.FlowDirection = value; NotifyPropertyChanged(); } }
    }



    public class DrawingVisualCircleWord : DrawingVisualBase<CircleTextAttribute>, IDrawingVisual,ICircle
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }
        public bool AutoAttributeChanged { get; set; } = true;

        public DrawBaseAttribute GetAttribute() => Attribute;
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public DrawingVisualCircleWord()
        {
            Attribute = new CircleTextAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s, e) =>
            { 
                if (AutoAttributeChanged && e.PropertyName != "ID")
                {
                    Render();
                }
            };
        }



        public override void Render()
        {
            TextAttribute.Text =  string.IsNullOrWhiteSpace(TextAttribute.Text)? "Point_" + Attribute.ID.ToString(): TextAttribute.Text;
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            using DrawingContext dc = RenderOpen();

            FormattedText formattedText = new FormattedText(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawText(formattedText, new Point(Attribute.Center.X - Attribute.Radius, Attribute.Center.Y - TextAttribute.FontSize));
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



}
