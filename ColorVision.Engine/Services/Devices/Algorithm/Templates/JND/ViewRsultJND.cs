#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.JND
{
    public class ViewRsultJND : PoiResultData, IViewResult
    {
        public MQTTMessageLib.Algorithm.POIResultDataJND JND { get { return _JND; } set { _JND = value; NotifyPropertyChanged(); } }

        private MQTTMessageLib.Algorithm.POIResultDataJND _JND;

        public PoiPointResultModel AlgResultJNDModel { get; set; }

        public ViewRsultJND(PoiPointResultModel detail)
        {
            AlgResultJNDModel = detail;
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            JND = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.POIResultDataJND>(detail.Value);
        }
    }
}
