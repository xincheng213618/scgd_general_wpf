using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.Templates.POI;

internal static class PoiImageTemplateCapture
{
    internal static PoiParam Capture(ImageView view, PoiParam? selected)
    {
        var snapshot = selected == null ? new PoiParam { Id = -1 }
            : JsonConvert.DeserializeObject<PoiParam>(JsonConvert.SerializeObject(selected))!;
        if (ImageUtils.TryGetImageSize(view.ImageShow.Source, out int width, out int height))
        {
            snapshot.Width = width;
            snapshot.Height = height;
            snapshot.PoiConfig.BackgroundFilePath = File.Exists(view.Config.FilePath) ? view.Config.FilePath : string.Empty;
        }
        if (snapshot.Width <= 0 || snapshot.Height <= 0) throw new InvalidOperationException("请先打开图像，再保存 POI 模板。");
        if (snapshot.PoiPoints.Any(x => x.PointType is PoiShape.Polygon or PoiShape.Quadrilateral))
            throw new InvalidOperationException("此模板包含当前图像工具栏不能完整保存的形状，请通过 POI 管理编辑。");

        snapshot.PoiPoints.Clear();
        var imageBounds = new System.Windows.Rect(0, 0, snapshot.Width, snapshot.Height);
        foreach (var visual in view.EditorContext.DrawingVisualLists)
        {
            var properties = visual.BaseAttribute;
            PoiPoint? point = null;
            if (properties is RectangleProperties rectangle)
            {
                if (rectangle.Rotation % 180 != 0) throw new InvalidOperationException("POI 模板暂不支持旋转矩形，请先将旋转角度设为 0°。");
                if (rectangle.Rect.IsEmpty || !imageBounds.Contains(rectangle.Rect)) throw new InvalidOperationException("有矩形超出图像范围，请调整后再保存。");
                bool leftTop = (properties.Tag as PoiPoint)?.PointType == PoiShape.LeftTopRect;
                point = new PoiPoint
                {
                    PointType = leftTop ? PoiShape.LeftTopRect : PoiShape.Rect,
                    PixX = rectangle.Rect.X + (leftTop ? 0 : rectangle.Rect.Width / 2),
                    PixY = rectangle.Rect.Y + (leftTop ? 0 : rectangle.Rect.Height / 2),
                    PixWidth = rectangle.Rect.Width, PixHeight = rectangle.Rect.Height,
                    Name = properties is RectangleTextProperties text ? text.Text : properties.Name
                };
            }
            else if (properties is CircleProperties circle)
            {
                if (circle.Radius != circle.RadiusY) throw new InvalidOperationException("POI 模板暂不支持椭圆，请先改为圆形。");
                if (!imageBounds.Contains(RegionGeometry.Bounds(circle))) throw new InvalidOperationException("有圆形超出图像范围，请调整后再保存。");
                var original = properties.Tag as PoiPoint;
                bool isPoint = original?.PointType is PoiShape.Point or PoiShape.LegacySolidPoint;
                point = new PoiPoint
                {
                    PointType = isPoint ? original!.PointType : PoiShape.Circle,
                    PixX = circle.Center.X, PixY = circle.Center.Y,
                    PixWidth = isPoint ? original!.PixWidth : circle.Radius * 2,
                    PixHeight = isPoint ? original!.PixHeight : circle.Radius * 2,
                    Name = properties is CircleTextProperties text ? text.Text : original?.Name ?? properties.Name
                };
            }
            else if (properties is PolygonProperties)
                throw new InvalidOperationException("POI 模板暂不支持保存此多边形，请通过 POI 管理编辑。");
            if (point == null) continue; // Rulers and annotations are not POI regions.
            point.Id = snapshot.PoiPoints.Count + 1;
            if (string.IsNullOrWhiteSpace(point.Name)) point.Name = $"P_{point.Id}";
            snapshot.PoiPoints.Add(point);
        }
        if (snapshot.PoiPoints.Count == 0) throw new InvalidOperationException("请先绘制要保存的 POI 区域。");
        snapshot.Storage = selected?.Storage;
        snapshot.DetailsLoaded = true;
        return snapshot;
    }
}
