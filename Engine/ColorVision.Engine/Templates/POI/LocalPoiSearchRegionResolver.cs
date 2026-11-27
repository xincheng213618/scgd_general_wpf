using ColorVision.Core;
using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ColorVision.Engine.Templates.POI;

internal static class LocalPoiSearchRegionResolver
{
    // Read persisted service point types directly: editor GraphicTypes has different values.
    public static Int32Rect Load(string templateName, MySqlConnectedEndpoint? endpoint, PoiTemplateStorage? storage = null)
    {
        string name = templateName.Trim();
        if (name.Length == 0) throw new ArgumentException("搜索区域关注点模板名称不能为空。", nameof(templateName));
        storage ??= PoiTemplateStorage.Default;
        if (storage.IsLocal) return Resolve(LocalRegionDetails(storage.ReadLocalByName(name)), name);
        using SqlSugarClient db = storage.OpenTemplateDatabase(endpoint);
        PoiMasterModel? master = db.Queryable<PoiMasterModel>()
            .Where(item => item.Name == name && item.TenantId == 0 && item.IsDelete == false)
            .OrderBy(item => item.Id).First();
        if (master == null) throw new InvalidOperationException($"找不到搜索区域关注点模板：{name}");
        List<PoiDetailModel> details = db.Queryable<PoiDetailModel>()
            .Where(item => item.Pid == master.Id && item.IsDelete == false)
            .OrderBy(item => item.Id).ToList();
        return Resolve(details, name);
    }

    internal static PoiDetailModel[] LocalRegionDetails(PoiParam template)
    {
        var points = template.PoiPoints;
        int type;
        if (points.Count == 1 && points[0].PointType is PoiShape.Rect or PoiShape.LeftTopRect)
            type = (int)points[0].PointType.ToAlgorithmPoiShape();
        else if (points.Count == 4 && (points.All(point => point.PointType == PoiShape.Quadrilateral)
            || points.Select((point, index) => point.PointType == PoiShape.Point && point.Name == $"PointInt{index + 1}").All(matches => matches)))
            type = LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType;
        else
            throw Invalid(template.Name, "需要一个中心或左上角矩形，或按 LT、RT、RB、LB 保存的四边形角点或 PointInt1 至 PointInt4。");

        // SQLite stores editor shapes and floating-point pixels. Convert at the legacy geometry boundary only.
        return points.Select(point => new PoiDetailModel
        {
            Type = (ColorVision.ImageEditor.GraphicTypes)type, PixX = checked((int)point.PixX), PixY = checked((int)point.PixY),
            PixWidth = checked((int)point.PixWidth), PixHeight = checked((int)point.PixHeight)
        }).ToArray();
    }

    internal static Int32Rect Resolve(IReadOnlyList<PoiDetailModel> details, string templateName)
    {
        const int rect = LocalLuminousAreaPoiTemplateUpdater.RectPointType;
        const int leftTopRect = LocalLuminousAreaPoiTemplateUpdater.LeftTopRectPointType;
        const int polygon = LocalLuminousAreaPoiTemplateUpdater.PolygonFourPointType;
        if (details.Count == 1 && (int)details[0].Type is rect or leftTopRect)
        {
            PoiDetailModel point = details[0];
            if (point.PixX is not int x || point.PixY is not int y ||
                point.PixWidth is not int width || point.PixHeight is not int height || width <= 0 || height <= 0)
                throw Invalid(templateName, "矩形坐标和宽高必须有效，宽高必须大于零。");
            if ((int)point.Type == rect)
            {
                x = checked(x - width / 2);
                y = checked(y - height / 2);
            }
            return new Int32Rect(x, y, width, height);
        }
        if (details.Count == 4 && (int)details[0].Type is polygon or leftTopRect &&
            details.All(point => point.Type == details[0].Type && point.PixX.HasValue && point.PixY.HasValue))
        {
            LuminousAreaPoint[] corners = details.Select(point => new LuminousAreaPoint(point.PixX!.Value, point.PixY!.Value)).ToArray();
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(corners, out string error))
                throw Invalid(templateName, error);
            int left = details.Min(point => point.PixX!.Value);
            int top = details.Min(point => point.PixY!.Value);
            int right = details.Max(point => point.PixX!.Value);
            int bottom = details.Max(point => point.PixY!.Value);
            return new Int32Rect(left, top, checked(right - left + 1), checked(bottom - top + 1));
        }
        throw Invalid(templateName, "需要 1 个 Rect/LTRect 矩形，或按 LT、RT、RB、LB 顺序保存的 4 个 PolygonFour 角点。");
    }

    private static InvalidOperationException Invalid(string name, string reason) =>
        new($"搜索区域关注点模板“{name}”无效：{reason}");
}
