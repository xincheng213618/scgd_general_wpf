using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class PolygonAttribute : DrawBaseAttribute
    {
        [Category("DrawingVisual"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }
        private Pen _Pen;


        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush;

        public List<Point> Points { get; set; }

    }



}
