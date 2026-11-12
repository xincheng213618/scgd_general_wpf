using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Services.POI;

/// <summary>A run-owned point template. Only its handle travels through flow messages.</summary>
internal sealed class LocalFlowPoiPointSet
{
    internal const string HandlePrefix = "local-poi-points:";
    private readonly LightBeadCenter[] centers;
    private readonly PoiShape shape;
    private readonly double width;
    private readonly double height;
    private readonly CVImageFlipMode flip;
    internal LightBeadsInspectionResult? Inspection { get; }
    private readonly LightBeadInspectionPoint[]? inspectedPoints;

    internal int ImageWidth { get; }
    internal int ImageHeight { get; }
    internal int Count => centers.Length;

    internal LocalFlowPoiPointSet(IReadOnlyList<LightBeadCenter> centers, LocalFrameMetadata image, PoiShape shape, double width, double height, LightBeadsInspectionResult? inspection = null)
    {
        ArgumentNullException.ThrowIfNull(centers);
        if (centers.Count == 0) throw new InvalidOperationException("灯珠定位没有找到可测量的亮点，请检查阈值、尺寸和搜索区域。");
        this.centers = centers.ToArray();
        this.shape = shape;
        this.width = width;
        this.height = height;
        ImageWidth = image.Width;
        ImageHeight = image.Height;
        flip = image.FlipMode;
        Inspection = inspection;
        inspectedPoints = inspection?.Points.Where(point => point.Status != LightBeadStatus.Unknown).ToArray();
    }

    internal string Publish(CVStartCFC action)
    {
        string handle = HandlePrefix + Guid.NewGuid().ToString("N");
        action.SetRuntimeResource(handle, this);
        action.MasterValue(handle, 0, (int)ViewResultAlgType.BuildPOI);
        return handle;
    }

    internal static bool TryResolve(CVStartCFC action, string? handle, out LocalFlowPoiPointSet? points)
    {
        points = null;
        if (handle?.StartsWith(HandlePrefix, StringComparison.Ordinal) != true) return false;
        if (!action.RuntimeResources.TryGet(handle, out points) || points == null)
            throw new InvalidOperationException("本地 POI 点集已过期或不属于本次流程；请重新执行上游灯珠定位。");
        return true;
    }

    internal PoiParam CreateTemplate(LocalFlowFrame frame)
    {
        if (frame.Metadata.Width != ImageWidth || frame.Metadata.Height != ImageHeight || frame.Metadata.FlipMode != flip)
            throw new InvalidOperationException("灯珠点集与测量图像的尺寸或镜像方向不同，请在完成图像校正后定位，或提供同方向图像。");
        PoiParam template = new() { Id = -1, Name = "LocalLightBeads", Width = ImageWidth, Height = ImageHeight };
        for (int index = 0; index < centers.Length; index++)
            template.PoiPoints.Add(new PoiPoint
            {
                Id = index + 1,
                Name = inspectedPoints == null ? "LED" + (index + 1).ToString("D5", CultureInfo.InvariantCulture) : $"R{inspectedPoints[index].Row}C{inspectedPoints[index].Column}",
                PointType = shape, PixX = inspectedPoints?[index].X ?? centers[index].X, PixY = inspectedPoints?[index].Y ?? centers[index].Y,
                PixWidth = width, PixHeight = height
            });
        return template;
    }
}
