using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.POI;

internal static class LocalNinePointPoiCalibration
{
    internal static PoiParam ApplyIfEnabled(bool enabled, PoiParam source,
        Func<IReadOnlyList<LuminousAreaPoint>, IReadOnlyList<NinePointBlockCalibrationPoint>> calibrate)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!enabled || source.PoiPoints.Count != 9) return source;
        ArgumentNullException.ThrowIfNull(calibrate);

        LuminousAreaPoint[] seeds = source.PoiPoints.Select(point => point.PointType == PoiShape.LeftTopRect
            ? new LuminousAreaPoint(point.PixX + point.PixWidth / 2, point.PixY + point.PixHeight / 2)
            : new LuminousAreaPoint(point.PixX, point.PixY)).ToArray();
        IReadOnlyList<NinePointBlockCalibrationPoint> refined = calibrate(seeds);
        if (refined.Count != seeds.Length)
            throw new InvalidOperationException("9点校准返回的点数与输入不一致。");
        for (int index = 0; index < seeds.Length; index++)
        {
            NinePointBlockCalibrationPoint point = refined[index];
            if (point.Index != index || point.Seed != seeds[index]
                || !double.IsFinite(point.Center.X) || !double.IsFinite(point.Center.Y))
                throw new InvalidOperationException("9点校准返回了无效坐标或点对应关系。");
        }
        if (!refined.Any(point => point.Success)) return source;

        // Only this measurement owns the corrected copy. The RE result and shared template stay intact.
        PoiParam result = new()
        {
            Id = source.Id, Name = source.Name, Type = source.Type,
            Width = source.Width, Height = source.Height, CfgJson = source.CfgJson,
            LeftTopX = source.LeftTopX, LeftTopY = source.LeftTopY,
            RightTopX = source.RightTopX, RightTopY = source.RightTopY,
            RightBottomX = source.RightBottomX, RightBottomY = source.RightBottomY,
            LeftBottomX = source.LeftBottomX, LeftBottomY = source.LeftBottomY
        };
        for (int index = 0; index < seeds.Length; index++)
        {
            PoiPoint original = source.PoiPoints[index];
            LuminousAreaPoint center = refined[index].Success ? refined[index].Center : seeds[index];
            result.PoiPoints.Add(new PoiPoint(original.Id, original.Pid, original.Name, original.PointType,
                original.PixX + center.X - seeds[index].X, original.PixY + center.Y - seeds[index].Y,
                original.PixWidth, original.PixHeight) { RegionShapeName = original.RegionShapeName });
        }
        return result;
    }
}
