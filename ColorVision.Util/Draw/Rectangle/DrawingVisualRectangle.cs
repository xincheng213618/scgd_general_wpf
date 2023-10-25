#pragma warning disable CA1711,CA2211
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{


    public class DrawingVisualRectangle : DrawingVisualBase, IDrawingVisual, IRectangle
    {
        public RectangleAttribute Attribute { get; set; }
        public DrawBaseAttribute GetAttribute() => Attribute;
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }


        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualRectangle()
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

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
