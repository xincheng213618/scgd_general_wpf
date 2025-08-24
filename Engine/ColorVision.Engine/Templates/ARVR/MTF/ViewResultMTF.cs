#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;

namespace ColorVision.Engine.Templates.MTF
{
    public class ViewResultMTF : PoiResultData, IViewResult
    {
        public double Articulation { get { return _Articulation; } set { _Articulation = value; OnPropertyChanged(); } }

        private double _Articulation;

        public ViewResultMTF(POIPoint point, double articulation)
        {
            Point = point;
            Articulation = articulation;
        }
        public PoiPointResultModel AlgResultMTFModel { get; set; }

        public ViewResultMTF(PoiPointResultModel detail)
        {
            AlgResultMTFModel = detail;
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            var temp = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.MTFResultData>(detail.Value);
            Articulation = temp.Articulation;
        }
    }
}
