using System.Windows;
using System.Windows.Media;
using System.ComponentModel;

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
            ObserveAttributeChanges(OnAttributePropertyChanged);
        }

        private void OnAttributePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (AutoAttributeChanged)
                Render();
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            if (Attribute.Rect.IsEmpty || !ShapeGeometry.IsFinite(Attribute.Rect))
                return;

            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
