using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class RectangleProperties : RegionProperties ,IRectangle
    {

        [DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; OnPropertyChanged(); } }
        private Pen _Pen = new Pen(Brushes.Red, 1);

        [DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; OnPropertyChanged(); } }
        private Brush _Brush = DefaultBrush;

        [DisplayName("矩形")]
        public Rect Rect { get => _Rect; set { _Rect = value; InvalidateMeasurementMessage(); OnPropertyChanged(); } }
        private Rect _Rect = new Rect(50, 50, 100, 100);
    }



}
