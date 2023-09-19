#pragma warning disable CA1711,CA2211
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualDatumRectangle : DrawingVisualBase, IDrawingVisualDatum
    {
        public RectangleAttribute Attribute { get; set; }
        public DrawBaseAttribute GetAttribute() => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualDatumRectangle()
        {
            Attribute = new RectangleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged) Render();
            };
        }
        public int ID { get => Attribute.ID; set => Attribute.ID = value; }

        public void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
