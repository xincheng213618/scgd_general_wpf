using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class DrawingVisualDatumRectangle : DrawingVisualBase<RectangleAttribute>, IDrawingVisualDatum, IRectangle
    {
        public DrawBaseAttribute BaseAttribute => Attribute;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }

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

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
