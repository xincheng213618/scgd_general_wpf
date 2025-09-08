using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonProperties : BaseProperties
    {
        [Browsable(false)]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen;


        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush;

        public List<Point> Points { get; set; }

    }



}
