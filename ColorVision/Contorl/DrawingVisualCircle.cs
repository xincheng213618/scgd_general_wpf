#pragma warning disable CA1711
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ColorVision.MVVM;

namespace ColorVision
{

    public class CircleAttribute : DrawAttributeBase
    {
        private int _ID;
        [Category("DrawingVisualCircle"), DisplayName("序号")]
        public int ID { get => _ID; set { _ID = value; NotifyPropertyChanged(); } }

        private Brush _Brush;

        [Category("DrawingVisualCircle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [Category("DrawingVisualCircle"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private Point _Center;

        [Category("DrawingVisualCircle"), DisplayName("点")]
        public Point Center { get => _Center; set { if (_Center.Equals(value)) return;  _Center = value; NotifyPropertyChanged(); } }

        private double _Radius;

        [Category("DrawingVisualCircle"), DisplayName("弧度")]
        public double Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
    }

    public class RectangleAttribute : DrawAttributeBase
    {
        private Brush _Brush;

        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [Category("RectangleAttribute"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private Rect _Rect;

        [Category("RectangleAttribute"), DisplayName("矩形")]
        public Rect Rect { get => _Rect; set { _Rect = value; NotifyPropertyChanged(); } }
    }

    public partial class DrawAttributeBase : ViewModelBase
    {
        private Point _Start;
        public virtual Point Start { get => _Start; set { _Start = value; NotifyPropertyChanged(); } }

        public bool IsCheck { get; set; } = true;
    }

    public class DrawingVisualCircle : DrawingVisual, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public CircleAttribute Attribute { get; set; }

        public bool AutoAttributeChanged { get; set; } = true;

        private static int No;
        public DrawingVisualCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.ID = No++;
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 2);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s, e) =>
            {
                if (AutoAttributeChanged)
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

        public Point Center { get => Attribute.Center; set => Attribute.Center =value; }

        public double CenterX { get => Attribute.Center.X; set => Attribute.Center = new Point(value, Attribute.Center.Y); }
        public double CenterY { get => Attribute.Center.Y; set => Attribute.Center = new Point(Attribute.Center.X, value); }

        public double Radius { get => Attribute.Radius; set => Attribute.Radius = value; }

        public int ID { get => Attribute.ID; set => Attribute.ID = value; }



        public void Render()
        {
            using DrawingContext dc = RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }




    public class DrawingVisualRectangle : DrawingVisual
    {
        public RectangleAttribute Attribute { get; set; }

        public bool AutoAttributeChanged { get; set; } = true;

        public DrawingVisualRectangle()
        {
            Attribute = new RectangleAttribute();
            Attribute.Brush = Brushes.Transparent;
            Attribute.Pen = new Pen(Brushes.Red, 1);
            Attribute.Rect = new Rect(50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) => 
            {
                if (AutoAttributeChanged) Render();
            };
        }

        public void Render()
        {
            using DrawingContext dc = RenderOpen();


            //RotateTransform form = new RotateTransform(50, Attribute.Rect.Left, Attribute.Rect.Top);
            //dc.PushTransform(form);
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }



    }



}
