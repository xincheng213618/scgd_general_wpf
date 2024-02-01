#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Services.Devices.Algorithm.Dao;
using MQTTMessageLib.Algorithm;

namespace ColorVision.Services.Devices.Algorithm.Views
{
    public class BuildPoiResultData : PoiResultData
    {
        public BuildPoiResultData(AlgResultMTFModel detail)
        {
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
        }
    }
}
