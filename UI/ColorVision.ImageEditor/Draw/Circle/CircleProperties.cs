using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class CircleProperties : RegionProperties,ICircle
    {

        [Browsable(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Red, 2);

        [Category("Circle"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush = DefaultBrush;

        [Category("Circle"), DisplayName("圆心")]
        public Point Center { get => _Center; set { if (_Center.Equals(value)) return; _Center = value; InvalidateMeasurementMessage(); OnPropertyChanged(); } }
        private Point _Center = new Point(50, 50);

        [Browsable(false)]
        public double Radius { get => _Radius; set { _Radius = value; _RadiusY = value; InvalidateMeasurementMessage(); OnPropertyChanged(); OnPropertyChanged(nameof(RadiusY)); OnPropertyChanged(nameof(RadiusX)); } }
        private double _Radius = 30;

        [Category("几何"), DisplayName("半轴 X（px）")]
        [Newtonsoft.Json.JsonIgnore]
        public double RadiusX
        {
            get => _Radius;
            set { if (!double.IsFinite(value) || value <= 0) return; _Radius = value; InvalidateMeasurementMessage(); OnPropertyChanged(); OnPropertyChanged(nameof(Radius)); }
        }

        [Category("Circle"), DisplayName("半径Y")]
        public double RadiusY { get => _RadiusY; set { _RadiusY = value; InvalidateMeasurementMessage(); OnPropertyChanged(); } }
        private double _RadiusY = 30;  
    }
}
