#pragma warning disable CA1711,CA2211
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Draw
{
    public class PolygonAttribute : DrawBaseAttribute
    {
        private Brush _Brush;

        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }

        private List<Point> _Points;
        public List<Point> Points { get => _Points; set { _Points = value; NotifyPropertyChanged(); } }
    }



}
