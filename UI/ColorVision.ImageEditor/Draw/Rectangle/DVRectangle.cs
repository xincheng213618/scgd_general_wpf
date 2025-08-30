using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{

    public class DVRectangle : DrawingVisualBase<RectangleProperties>, IDrawingVisual, IRectangle
    {
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; } = true;

        public DVRectangle()
        {
            Attribute = new RectangleProperties();
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged) Render();
            };
        }
        private TextAttribute TextAttribute = new();

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);

            if (!string.IsNullOrWhiteSpace(Attribute.Msg))
            {
                TextAttribute.FontSize = Attribute.Pen.Thickness * 10;
                FormattedText formattedText = new FormattedText(Attribute.Msg, CultureInfo.CurrentCulture, TextAttribute.FlowDirection, new Typeface(TextAttribute.FontFamily, TextAttribute.FontStyle, TextAttribute.FontWeight, TextAttribute.FontStretch), TextAttribute.FontSize, TextAttribute.Brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(formattedText, new Point(Attribute.Rect.X + formattedText.Width / 2 + Attribute.Rect.Width / 2, Attribute.Rect.Y + Attribute.Rect.Height / 2));
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
