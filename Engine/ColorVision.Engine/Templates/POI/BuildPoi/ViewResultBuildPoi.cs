#pragma warning disable CS8629
using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ColorVision.Engine.Templates.POI.BuildPoi
{
    public class ViewResultBuildPoi : PoiResultData, IViewResult
    {
        public ViewResultBuildPoi(PoiPointResultModel detail)
        {
            Point = new PoiPoint(detail.PoiId, -1, detail.PoiName, detail.PoiType.ToPoiShape(), detail.PoiX ?? 0, detail.PoiY ?? 0, detail.PoiWidth ?? 0, detail.PoiHeight ?? 0);
        }
    }
}
