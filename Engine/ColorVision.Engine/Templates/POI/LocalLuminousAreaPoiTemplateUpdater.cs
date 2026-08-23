using ColorVision.Core;
using ColorVision.Database;
using ColorVision.ImageEditor;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using ServicePoiPointType = CVCommCore.CVAlgorithm.POIPointTypes;

namespace ColorVision.Engine.Templates.POI
{
    internal enum LocalLuminousAreaPoiTemplateShape
    {
        Rectangle,
        LeftTopRectangle,
        PolygonFour
    }

    internal sealed class LocalLuminousAreaPoiTemplateUpdate
    {
        public required LocalLuminousAreaPoiTemplateShape Shape { get; init; }
        public required IReadOnlyList<PoiDetailModel> Details { get; init; }
    }

    internal static class LocalLuminousAreaPoiTemplateUpdater
    {
        // These values are the persisted CVCommCore.CVAlgorithm.POIPointTypes contract
        // used by cvwindowsservice, not the similarly named ImageEditor GraphicTypes values.
        internal const int RectPointType = (int)ServicePoiPointType.Rect;
        internal const int LeftTopRectPointType = (int)ServicePoiPointType.LTRect;
        internal const int PolygonFourPointType = (int)ServicePoiPointType.PolygonFour;

        public static LocalLuminousAreaPoiTemplateShape Update(
            string templateName,
            IReadOnlyList<LuminousAreaPoint> corners)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException("POI 保存模板名称不能为空。", nameof(templateName));

            using SqlSugarClient db = MySqlControl.CreateDbClient();
            db.Ado.BeginTran();
            try
            {
                string normalizedName = templateName.Trim();
                // Match the legacy service views: soft-deleted rows are excluded,
                // while is_enable does not change which saved template rows are updated.
                PoiMasterModel? master = db.Queryable<PoiMasterModel>()
                    .Where(item => item.Name == normalizedName && item.TenantId == 0 && item.IsDelete == false)
                    .OrderBy(item => item.Id)
                    .First();
                if (master == null)
                    throw new InvalidOperationException($"找不到 POI 保存模板：{normalizedName}");

                List<PoiDetailModel> sourceDetails = db.Queryable<PoiDetailModel>()
                    .Where(item => item.Pid == master.Id && item.IsDelete == false)
                    .OrderBy(item => item.Id)
                    .ToList();
                LocalLuminousAreaPoiTemplateUpdate update = BuildUpdate(sourceDetails, corners, normalizedName);

                int affectedRows = db.Updateable(update.Details.ToList())
                    .UpdateColumns(item => new { item.Type, item.PixX, item.PixY, item.PixWidth, item.PixHeight })
                    .ExecuteCommand();
                if (affectedRows < 1)
                    throw new InvalidOperationException($"POI 保存模板“{normalizedName}”没有更新任何明细。");
                db.Ado.CommitTran();
                return update.Shape;
            }
            catch (Exception updateException)
            {
                try
                {
                    db.Ado.RollbackTran();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException("更新 POI 保存模板失败，且数据库事务回滚失败。", updateException, rollbackException);
                }
                throw;
            }
        }

        internal static LocalLuminousAreaPoiTemplateUpdate BuildUpdate(
            IReadOnlyList<PoiDetailModel> sourceDetails,
            IReadOnlyList<LuminousAreaPoint> corners,
            string templateName = "POI")
        {
            ArgumentNullException.ThrowIfNull(sourceDetails);
            ValidateCorners(corners);

            if (sourceDetails.Count == 1)
            {
                PoiDetailModel source = sourceDetails[0];
                int storedPointType = (int)source.Type;
                if (storedPointType is not (RectPointType or LeftTopRectPointType))
                    throw CreateShapeException(templateName, sourceDetails);

                int left = checked((int)Math.Floor(corners.Min(point => point.X)));
                int top = checked((int)Math.Floor(corners.Min(point => point.Y)));
                int right = checked((int)Math.Floor(corners.Max(point => point.X)));
                int bottom = checked((int)Math.Floor(corners.Max(point => point.Y)));
                int width = checked(right - left + 1);
                int height = checked(bottom - top + 1);
                if (width <= 0 || height <= 0)
                    throw new InvalidOperationException("发光区四角点无法生成有效的 POI 外接矩形。");

                bool isLeftTop = storedPointType == LeftTopRectPointType;
                PoiDetailModel updated = Clone(source);
                updated.Type = (GraphicTypes)storedPointType;
                updated.PixX = isLeftTop ? left : checked(left + width / 2);
                updated.PixY = isLeftTop ? top : checked(top + height / 2);
                updated.PixWidth = width;
                updated.PixHeight = height;
                return new LocalLuminousAreaPoiTemplateUpdate
                {
                    Shape = isLeftTop
                        ? LocalLuminousAreaPoiTemplateShape.LeftTopRectangle
                        : LocalLuminousAreaPoiTemplateShape.Rectangle,
                    Details = [updated]
                };
            }

            // cvwindowsservice identifies a four-point template by its first row and
            // rewrites every row as PolygonFour (3). Current WPF templates may use 2;
            // accept either first-row representation and normalize all four rows.
            if (sourceDetails.Count == 4
                && (int)sourceDetails[0].Type is PolygonFourPointType or LeftTopRectPointType)
            {
                List<PoiDetailModel> updated = new(sourceDetails.Count);
                for (int index = 0; index < sourceDetails.Count; index++)
                {
                    PoiDetailModel detail = Clone(sourceDetails[index]);
                    LuminousAreaPoint corner = corners[index];
                    detail.Type = (GraphicTypes)PolygonFourPointType;
                    detail.PixX = checked((int)corner.X);
                    detail.PixY = checked((int)corner.Y);
                    detail.PixWidth = 0;
                    detail.PixHeight = 0;
                    updated.Add(detail);
                }
                return new LocalLuminousAreaPoiTemplateUpdate
                {
                    Shape = LocalLuminousAreaPoiTemplateShape.PolygonFour,
                    Details = updated
                };
            }

            throw CreateShapeException(templateName, sourceDetails);
        }

        private static void ValidateCorners(IReadOnlyList<LuminousAreaPoint> corners)
        {
            ArgumentNullException.ThrowIfNull(corners);
            if (corners.Count != 4)
                throw new ArgumentException("更新 POI 保存模板需要 LT、RT、RB、LB 四个角点。", nameof(corners));
            for (int index = 0; index < corners.Count; index++)
            {
                LuminousAreaPoint corner = corners[index];
                if (!double.IsFinite(corner.X) || !double.IsFinite(corner.Y))
                {
                    throw new ArgumentException("更新 POI 保存模板的角点必须按 LT、RT、RB、LB 排列且坐标有效。", nameof(corners));
                }
            }
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(corners, out string geometryError))
                throw new ArgumentException($"更新 POI 保存模板的四角点无效：{geometryError}", nameof(corners));
        }

        private static InvalidOperationException CreateShapeException(
            string templateName,
            IReadOnlyList<PoiDetailModel> details)
        {
            string pointTypes = string.Join(",", details.Select(detail => ((int)detail.Type).ToString()));
            return new InvalidOperationException(
                $"POI 保存模板“{templateName}”必须包含 1 个 Rect/LTRect，或 4 个 PolygonFour；当前明细数 {details.Count}，类型 [{pointTypes}]。");
        }

        private static PoiDetailModel Clone(PoiDetailModel source) => new()
        {
            Id = source.Id,
            Pid = source.Pid,
            Name = source.Name,
            Type = source.Type,
            PixX = source.PixX,
            PixY = source.PixY,
            PixWidth = source.PixWidth,
            PixHeight = source.PixHeight,
            IsEnable = source.IsEnable,
            IsDelete = source.IsDelete,
            Remark = source.Remark
        };
    }
}
