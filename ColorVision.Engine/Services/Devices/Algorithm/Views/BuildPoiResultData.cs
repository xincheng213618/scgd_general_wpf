#pragma  warning disable CA1708,CS8602,CS8604,CS8629
using ColorVision.Engine.Services.Devices.Algorithm.Dao;
using CVCommCore.CVAlgorithm;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    public class BuildPoiResultData : PoiResultData
    {
        public BuildPoiResultData(POIPointResultModel detail)
        {
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, (POIPointTypes)detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
        }
    }
}
