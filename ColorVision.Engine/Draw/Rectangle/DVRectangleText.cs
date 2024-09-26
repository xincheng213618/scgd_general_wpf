using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine.Draw
{
    public class RectangleTextProperties : RectangleProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; NotifyPropertyChanged(); } }

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



    public class DVRectangleText : DrawingVisualBase<RectangleTextProperties>, IDrawingVisual,IRectangle
    {
        public BaseProperties BaseAttribute => Attribute;
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; } = true;

        public DVRectangleText()
        {
            Attribute = new RectangleTextProperties();
            Attribute.Id = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged) Render();
            };
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (IsShowText)
            {
                TextAttribute.Text = Attribute.Text;
                TextAttribute.Text = string.IsNullOrWhiteSpace(TextAttribute.Text) ? Attribute.Id.ToString() : TextAttribute.Text;
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;

                Brush brush = Brushes.Red;
                double fontSize = Attribute.Pen.Thickness * 10;
                FormattedText formattedText = new(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Rect.X - fontSize, Attribute.Rect.Y - fontSize - Attribute.Pen.Thickness));
            }
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
