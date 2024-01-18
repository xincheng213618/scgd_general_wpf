using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    public class PoiResultCIEYData : PoiResultData
    {
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }

        private double _Y;

        public PoiResultCIEYData(POIPoint point, POIDataCIEY data)
        {
            Point = point;
            Y = data.Y;
        }
    }
}
