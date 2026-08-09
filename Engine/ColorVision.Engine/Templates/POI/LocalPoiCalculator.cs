using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI
{
    internal sealed class LocalPoiPointResult
    {
        public int PoiId { get; init; }
        public string Name { get; init; } = string.Empty;
        public POIPointTypes PointType { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public required IPOIResultData Value { get; init; }
    }

    internal sealed class LocalPoiResultSet
    {
        public string FrameId { get; init; } = string.Empty;
        public string TemplateName { get; init; } = string.Empty;
        public List<LocalPoiPointResult> Points { get; init; } = new();
    }

    /// <summary>
    /// Organizes the local-flow POI calculation and result persistence. Native interop lives in PoiMeasurementService.
    /// </summary>
    internal static class LocalPoiCalculator
    {
        public static ViewResultAlgType ResolveResultType(int channels)
            => channels == 1 ? ViewResultAlgType.POI_Y : ViewResultAlgType.POI_XYZ;

        public static LocalPoiResultSet Calculate(LocalFlowFrameLease frame, PoiParam poi)
        {
            ValidateCieFrame(frame);
            ArgumentNullException.ThrowIfNull(poi);
            if (poi.PoiPoints.Count == 0 && poi.Id > 0) PoiParam.LoadPoiDetailFromDB(poi);
            if (poi.PoiPoints.Count == 0) throw new InvalidOperationException($"POI 模板没有关注点：{poi.Name}");

            PoiMeasurementPoint[] requests = new PoiMeasurementPoint[poi.PoiPoints.Count];
            (int X, int Y, int Width, int Height, POIPointTypes Type)[] definitions =
                new (int, int, int, int, POIPointTypes)[poi.PoiPoints.Count];
            for (int index = 0; index < poi.PoiPoints.Count; index++)
            {
                definitions[index] = ResolvePoint(poi.PoiPoints[index]);
                (int x, int y, int width, int height, POIPointTypes type) = definitions[index];
                requests[index] = new PoiMeasurementPoint(x, y, width, height, ToMeasurementShape(type));
            }

            PoiMeasurementResult[] measurements = PoiMeasurementService.Calculate(
                frame.CiePointer,
                frame.CieLength,
                frame.Metadata.Width,
                frame.Metadata.Height,
                frame.Metadata.CieBpp,
                frame.Metadata.Channels,
                requests);

            LocalPoiResultSet result = new()
            {
                FrameId = frame.FrameId.ToString("N"),
                TemplateName = poi.Name
            };
            for (int index = 0; index < measurements.Length; index++)
            {
                PoiPoint point = poi.PoiPoints[index];
                (int x, int y, int width, int height, POIPointTypes type) = definitions[index];
                PoiMeasurementResult measurement = measurements[index];
                IPOIResultData value = frame.Metadata.Channels == 1
                    ? new POIResultDataCIEY(measurement.Y)
                    : new POIResultDataCIExyuv(
                        measurement.Cct,
                        measurement.Wave,
                        measurement.X,
                        measurement.Y,
                        measurement.Z,
                        measurement.ChromaX,
                        measurement.ChromaY,
                        measurement.U,
                        measurement.V);
                result.Points.Add(new LocalPoiPointResult
                {
                    PoiId = point.Id,
                    Name = point.Name ?? point.Id.ToString(),
                    PointType = type,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    Value = value
                });
            }
            return result;
        }

        public static void SaveDetails(int masterId, LocalPoiResultSet result)
        {
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId), "POI 结果主表 ID 无效。");
            List<PoiPointResultModel> details = new(result.Points.Count);
            foreach (LocalPoiPointResult point in result.Points)
            {
                details.Add(new PoiPointResultModel
                {
                    Pid = masterId,
                    PoiId = point.PoiId,
                    PoiName = point.Name,
                    PoiType = point.PointType,
                    PoiX = point.X,
                    PoiY = point.Y,
                    PoiWidth = point.Width,
                    PoiHeight = point.Height,
                    Value = JsonConvert.SerializeObject(point.Value)
                });
            }
            int inserted = PoiPointResultDao.Instance.BulkInsert(details);
            if (inserted != details.Count)
            {
                throw new InvalidOperationException($"保存 POI 明细失败：应写入 {details.Count} 条，实际写入 {inserted} 条。");
            }
        }

        public static void DeleteDetails(int masterId)
        {
            if (masterId > 0) _ = PoiPointResultDao.Instance.Delete(item => item.Pid == masterId);
        }

        private static void ValidateCieFrame(LocalFlowFrameLease frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (!frame.HasCie || frame.CiePointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("当前内存帧没有 CIE 数据，无法计算 POI。");
            }
            if (!frame.IsCieFlipApplied)
            {
                throw new InvalidOperationException("CIE 镜像操作必须在 POI 计算前完成。");
            }
        }

        internal static (int X, int Y, int Width, int Height, POIPointTypes Type) ResolvePoint(PoiPoint point)
        {
            int x = checked((int)point.PixX);
            int y = checked((int)point.PixY);
            int width = Math.Max(checked((int)point.PixWidth), 1);
            int height = Math.Max(checked((int)point.PixHeight), 1);
            POIPointTypes pointType = point.PointType switch
            {
                PoiShape.Point or PoiShape.LegacySolidPoint => POIPointTypes.SolidPoint,
                PoiShape.Circle => POIPointTypes.Circle,
                PoiShape.Rect => POIPointTypes.Rect,
                _ => throw new NotSupportedException($"本地 POI 暂不支持形状：{point.PointType}")
            };
            return (x, y, width, height, pointType);
        }

        private static PoiMeasurementShape ToMeasurementShape(POIPointTypes type)
        {
            return type switch
            {
                POIPointTypes.SolidPoint => PoiMeasurementShape.Point,
                POIPointTypes.Circle => PoiMeasurementShape.Circle,
                POIPointTypes.Rect => PoiMeasurementShape.Rect,
                _ => throw new NotSupportedException($"本地 POI 暂不支持类型：{type}")
            };
        }
    }
}
