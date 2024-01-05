#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Algorithm.Views
{
    public class PoiResultCIEYData : PoiResultData
    {
        public double Y { get { return _Y; } set { _Y = value; NotifyPropertyChanged(); } }

        private double _Y;

        public PoiResultCIEYData(POIPoint point, POIDataCIEY data)
        {
            this.Point = point;
            this.Y = data.Y;
        }
    }
}
