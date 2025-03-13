using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DVDatumRectangle : DrawingVisualBase<RectangleProperties>, IDrawingVisualDatum, IRectangle
    {
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public Rect Rect { get => Attribute.Rect; set => Attribute.Rect = value; }

        public bool AutoAttributeChanged { get; set; } = true;

        public DVDatumRectangle()
        {
            Attribute = new RectangleProperties();
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
