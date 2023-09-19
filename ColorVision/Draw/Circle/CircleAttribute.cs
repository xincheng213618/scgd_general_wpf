using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class CircleAttribute : DrawBaseAttribute
    {
        private Brush _Brush;

        [Category("DrawingVisualCircle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private Point _Center;

        [Category("DrawingVisualCircle"), DisplayName("点")]
        public Point Center { get => _Center; set { if (_Center.Equals(value)) return; _Center = value; NotifyPropertyChanged(); } }

        private double _Radius;

        [Category("DrawingVisualCircle"), DisplayName("半径")]
        public double Radius { get => _Radius; set { _Radius = value; NotifyPropertyChanged(); } }
    }



}
