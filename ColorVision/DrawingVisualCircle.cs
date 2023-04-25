#pragma warning disable CA1711
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ColorVision.MVVM;

namespace ColorVision
{

    public class CircleAttribute: ViewModelBase
    {
        private Brush _Brush;

        [CategoryAttribute("DrawingVisualCircle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [CategoryAttribute("DrawingVisualCircle"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }

        private Point _Center;

        [CategoryAttribute("DrawingVisualCircle"), DisplayName("点")]
        public Point Center { get=> _Center; set { _Center = value; NotifyPropertyChanged(); } }

        private double _Radius;

        [CategoryAttribute("DrawingVisualCircle"), DisplayName("弧度")]   
        public double Radius { get=>_Radius; set { _Radius = value; NotifyPropertyChanged(); } }
    }


    public class DrawingVisualCircle: DrawingVisual
    {
        public CircleAttribute Attribute { get; set; } 
        public DrawingVisualCircle()
        {
            Attribute = new CircleAttribute();
            Attribute.Brush = Brushes.Blue;
            Attribute.Pen = new Pen(Brushes.Black, 1);
            Attribute.Center = new Point(50, 50);
            Attribute.Radius = 30;
            Attribute.PropertyChanged += (s,e) => Render();
        }

        public void Render()
        {
            using DrawingContext dc = this.RenderOpen();
            dc.DrawEllipse(Attribute.Brush, Attribute.Pen, Attribute.Center, Attribute.Radius, Attribute.Radius);
        }
    }

    public class RectangleAttribute : ViewModelBase
    {
        private Brush _Brush;

        [CategoryAttribute("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Pen _Pen;

        [CategoryAttribute("RectangleAttribute"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }


        private Rect _Rect;

        [CategoryAttribute("RectangleAttribute"), DisplayName("弧度")]
        public Rect Rect { get => _Rect; set { _Rect = value; NotifyPropertyChanged(); } }


    }


    public class DrawingVisualRectangle : DrawingVisual
    {
        public RectangleAttribute Attribute { get; set; }
        public DrawingVisualRectangle()
        {
            Attribute = new RectangleAttribute();
            Attribute.Brush = Brushes.Blue;
            Attribute.Pen = new Pen(Brushes.Black, 1);
            Attribute.Rect = new Rect (50, 50, 100, 100);
            Attribute.PropertyChanged += (s, e) => Render();
        }

        public void Render()
        {
            using DrawingContext dc = this.RenderOpen();
            dc.DrawRectangle(Attribute.Brush, Attribute.Pen, Attribute.Rect);
        }
    }



}
