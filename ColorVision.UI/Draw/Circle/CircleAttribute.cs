using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class CircleAttribute : DrawBaseAttribute
    {
        [Category("DrawingVisual"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }
        private Pen _Pen;

        [Category("Circle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush;

        [Category("Circle"), DisplayName("点")]
        public Point Center { get => _Center; set { if (_Center.Equals(value)) return; _Center = value; NotifyPropertyChanged(); } }
        private Point _Center;

        [Category("Circle"), DisplayName("半径")]
        public double Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
        private double _Radius;

    }





}
