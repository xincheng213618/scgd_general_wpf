#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Services.Devices.Algorithm.Dao;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    public class MTFResultData : PoiResultData
    {
        public double Articulation { get { return _Articulation; } set { _Articulation = value; NotifyPropertyChanged(); } }

        private double _Articulation;

        public MTFResultData(POIPoint point, double articulation)
        {
            Point = point;
            Articulation = articulation;
        }

        public MTFResultData(AlgResultMTFModel detail)
        {
            Point = new POIPoint(detail.PoiId??-1, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            var temp = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.MTFResultData>(detail.Value);
            Articulation = temp.Articulation;
        }
    }
}
