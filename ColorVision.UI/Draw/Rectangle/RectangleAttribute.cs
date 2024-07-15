using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.UI.Draw
{
    public class RectangleAttribute : DrawBaseAttribute
    {

        [Category("RectangleAttribute"), DisplayName("笔刷")]
        public Pen Pen { get => _Pen; set { _Pen = value; NotifyPropertyChanged(); } }
        private Pen _Pen;

        [Category("RectangleAttribute"), DisplayName("颜色")]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush;

        [Category("RectangleAttribute"), DisplayName("矩形")]
        public Rect Rect { get => _Rect; set { _Rect = value; NotifyPropertyChanged(); } }
        private Rect _Rect;
    }



}
