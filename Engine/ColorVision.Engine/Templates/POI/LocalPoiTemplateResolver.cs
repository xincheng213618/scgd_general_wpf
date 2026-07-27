using ColorVision.Database;
using ColorVision.Engine.Templates.POI.BuildPoi;
using System;
using System.Linq;

namespace ColorVision.Engine.Templates.POI
{
    internal static class LocalPoiTemplateResolver
    {
        public static ParamBuildPoi ResolveBuildPoiTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) throw new InvalidOperationException("请选择参数模板。");
            return TemplateBuildPoi.Params.FirstOrDefault(item => string.Equals(item.Key, templateName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到参数模板：{templateName}");
        }

        public static PoiParam ResolvePoiTemplate(string templateName, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(templateName)) throw new InvalidOperationException($"请选择{propertyName}。");
            PoiParam source = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, templateName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到 POI 模板：{templateName}");

            PoiMasterModel? master = source.Id > 0 ? PoiMasterDao.Instance.GetById(source.Id) : null;
            PoiParam template = master == null ? CloneTemplate(source) : new PoiParam(master);
            if (master != null) PoiParam.LoadPoiDetailFromDB(template);
            if (template.PoiPoints.Count == 0 && source.PoiPoints.Count > 0) CopyPoints(source, template);
            if (template.PoiPoints.Count == 0) throw new InvalidOperationException($"POI 模板没有关注点：{template.Name}");
            return template;
        }

        private static PoiParam CloneTemplate(PoiParam source)
        {
            PoiParam template = new()
            {
                Id = source.Id,
                Name = source.Name,
                Type = source.Type,
                Width = source.Width,
                Height = source.Height,
                LeftTopX = source.LeftTopX,
                LeftTopY = source.LeftTopY,
                RightTopX = source.RightTopX,
                RightTopY = source.RightTopY,
                RightBottomX = source.RightBottomX,
                RightBottomY = source.RightBottomY,
                LeftBottomX = source.LeftBottomX,
                LeftBottomY = source.LeftBottomY
            };
            CopyPoints(source, template);
            return template;
        }

        private static void CopyPoints(PoiParam source, PoiParam destination)
        {
            foreach (PoiPoint point in source.PoiPoints)
            {
                destination.PoiPoints.Add(new PoiPoint
                {
                    Id = point.Id,
                    Name = point.Name,
                    PointType = point.PointType,
                    PixX = point.PixX,
                    PixY = point.PixY,
                    PixWidth = point.PixWidth,
                    PixHeight = point.PixHeight
                });
            }
        }
    }
}
