using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{


    public class DrawingVisualDatumCircle : DrawingVisualBase<CircleAttribute>, IDrawingVisualDatum, ICircle
    {
        public DrawBaseAttribute BaseAttribute => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public DrawingVisualDatumCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Red;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;

            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged && e.PropertyName!="ID")
                    Render();
            };
        }

        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



}
