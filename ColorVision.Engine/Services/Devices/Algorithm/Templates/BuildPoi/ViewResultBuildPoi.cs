#pragma warning disable CS8629
using ColorVision.Engine.Services.Devices.Algorithm.Templates.POI;
using CVCommCore.CVAlgorithm;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.BuildPoi
{
    public class ViewResultBuildPoi : PoiResultData, IViewResult
    {
        public ViewResultBuildPoi(PoiPointResultModel detail)
        {
            Point = new POIPoint(detail.PoiId ?? -1, -1, detail.PoiName, detail.PoiType, (int)detail.PoiX, (int)detail.PoiY, (int)detail.PoiWidth, (int)detail.PoiHeight);
        }
    }
}
