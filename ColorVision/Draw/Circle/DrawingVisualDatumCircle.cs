#pragma warning disable CA1711,CA2211
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class DrawingVisualDatumCircle : DrawingVisualBase, IDrawingVisualDatum
    {
        public CircleAttribute Attribute { get; set; }
        public DrawBaseAttribute GetAttribute() => Attribute;

        public bool AutoAttributeChanged { get; set; } = true;

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
                if (e.PropertyName == "Center")
                {
                    NotifyPropertyChanged(nameof(CenterX));
                    NotifyPropertyChanged(nameof(CenterY));
                }
                else if (e.PropertyName == "Radius")
                {
                    NotifyPropertyChanged(nameof(Radius));
                }
            };
        }

        public int ID { get => Attribute.ID; set => Attribute.ID = value; }

        public Point Center { get => Attribute.Center; set => Attribute.Center = value; }

        public double CenterX { get => Attribute.Center.X; set => Attribute.Center = new Point(value, Attribute.Center.Y); }
        public double CenterY { get => Attribute.Center.Y; set => Attribute.Center = new Point(Attribute.Center.X, value); }

        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }



        public virtual void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }



}
