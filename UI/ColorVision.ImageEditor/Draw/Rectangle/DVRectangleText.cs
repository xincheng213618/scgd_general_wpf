using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class RectangleTextProperties : RectangleProperties, ITextProperties
    {
        [Browsable(false)]
        public TextAttribute TextAttribute { get; set; } = new TextAttribute();

        [Category("Attribute"), DisplayName("Text")]
        public string Text { get => TextAttribute.Text; set { TextAttribute.Text = value; OnPropertyChanged(); } }

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



    public class DVRectangleText : DrawingVisualBase<RectangleTextProperties>, IDrawingVisual,IRectangle
    {
        public TextAttribute TextAttribute { get => Attribute.TextAttribute; }

        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DVRectangleText()
        {
            Attribute = new RectangleTextProperties();
            Attribute.PropertyChanged += (s, e) => Render(); 
        }

        public DVRectangleText(RectangleTextProperties rectangleTextProperties)
        {
            Attribute = rectangleTextProperties;
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
            double size = 0;

            if (IsShowText)
            {
                Brush brush = Brushes.Red;
                double fontSize = Attribute.Pen.Thickness * 10;
                FormattedText formattedText = new(TextAttribute.Text, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, DefalutTextAttribute.Defalut.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                size = formattedText.Width / 2;
                dc.DrawText(formattedText, new Point(Attribute.Rect.X + Attribute.Rect.Width /2 - size, Attribute.Rect.Y+ Attribute.Rect.Height / 2 - formattedText.Height / 2));
            }
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);

            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, DefalutTextAttribute.Defalut.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Rect.X + size + Attribute.Rect.Width / 2 + Attribute.Pen.Thickness, Attribute.Rect.Y + Attribute.Rect.Height / 2 - formattedText.Height / 2));
            }
        }
        public override Rect GetRect()
        {
            return Rect;
        }
        public override void SetRect(Rect rect)
        {
            Rect = rect;
            Render();
        }
    }



}
