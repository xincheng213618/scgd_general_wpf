using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class DrawingVisualRectangle : DrawingVisualBase<RectangleAttribute>, IDrawingVisual, IRectangle
    {
        public BaseProperties BaseAttribute => Attribute;
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualRectangle()
        {
            Version = "矩形";
            Attribute = new RectangleAttribute();
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
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
