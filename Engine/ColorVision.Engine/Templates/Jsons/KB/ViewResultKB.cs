#pragma  warning disable CA1708,CS8602,CS8604,CS8629,CA1711,CS8601
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;

namespace  ColorVision.Engine.Templates.Jsons.KB

{
    public class ViewResultKB : PoiResultData, IViewResult
    {
        public POIResultDataCIEYEx POIResultDataCIEYEx { get; set; }

        public ViewResultKB()
        {
        }

        public PoiPointResultModel AlgResultMTFModel { get; set; }

        public ViewResultKB(PoiPointResultModel detail)
        {
            AlgResultMTFModel = detail;
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
            POIResultDataCIEYEx = JsonConvert.DeserializeObject<MQTTMessageLib.Algorithm.POIResultDataCIEYEx>(detail.Value);
        }
    }
}
