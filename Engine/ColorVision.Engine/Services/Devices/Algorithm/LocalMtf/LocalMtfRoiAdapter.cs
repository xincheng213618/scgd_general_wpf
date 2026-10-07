using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor.Algorithms.Mtf;
using System;
using System.Collections.Generic;
using System.Linq;
namespace ColorVision.Engine.Services.Devices.Algorithm.LocalMtf;
internal static class LocalMtfRoiAdapter
{
    internal static MtfRoi[] BuildRegions(IEnumerable<PoiPoint> points, int width, int height)
    {
        MtfRoi[] regions = points.Select(point =>
        {
            if (point.PointType is not (PoiShape.Rect or PoiShape.LeftTopRect))
                throw new InvalidOperationException($"MTF 关注点必须是矩形：{point.Name}。");
            // The service POIPointOnly ABI stores floats and uses Convert.ToInt32 (ties to even).
            float x = (float)point.PixX, y = (float)point.PixY;
            float w = (float)point.PixWidth, h = (float)point.PixHeight;
            bool leftTop = point.PointType == PoiShape.LeftTopRect;
            return new MtfRoi(point.Name, Convert.ToInt32(leftTop ? x : x - w / 2f),
                Convert.ToInt32(leftTop ? y : y - h / 2f), Convert.ToInt32(w), Convert.ToInt32(h));
        }).ToArray();
        StripeMtfAnalyzer.ValidateRegions(regions, width, height);
        return regions;
    }

}
