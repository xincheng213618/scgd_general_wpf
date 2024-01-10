#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.MVVM;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Device.Algorithm.Views
{
    public class PoiResultData : ViewModelBase
    {
        public POIPoint Point { get { return POIPoint; } set { POIPoint = value; NotifyPropertyChanged(); } }

        public string Name { get { return POIPoint.Name; } }
        public string PixelPos { get { return string.Format("{0},{1}", POIPoint.PixelX, POIPoint.PixelY); } }
        public string PixelSize { get { return string.Format("{0},{1}", POIPoint.Width, POIPoint.Height); } }

        public string Shapes { get { return string.Format("{0}", POIPoint.PointType == 0 ? "圆形" : "矩形"); } }

        protected POIPoint POIPoint { get; set; }
    }
}
