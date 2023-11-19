#pragma warning disable CA1711,CA2211
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    [CircleAttribute(ID =1)]
    public class DrawingVisualCircle : DrawingVisualBase<CircleAttribute>, IDrawingVisual,ICircle
    {
        public DrawBaseAttribute GetAttribute() => Attribute;
        public bool AutoAttributeChanged { get; set; }
        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }
        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }
        public Pen Pen { get => Attribute.Pen; set => Attribute.Pen = value; }

        public DrawingVisualCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s,e)=> { if (AutoAttributeChanged && e.PropertyName != "ID") Render(); };
        }


        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



    public class BeingDrawingVisualCircle : DrawingVisualCircle
    {
        public BeingDrawingVisualCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s, e) => { if (AutoAttributeChanged && e.PropertyName != "ID") Render(); };
        }


        public override void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
            dc.DrawText();


        }
    }
}
