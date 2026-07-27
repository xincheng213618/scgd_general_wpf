using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIGenCali;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.ImageEditor;
using ColorVision.Common.Utilities;
using CVCommCore.CVAlgorithm;
using cvColorVision;
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

    internal static class LocalPoiCalculator
    {
        public static ViewResultAlgType ResolveResultType(int channels)
        {
            return channels == 1 ? ViewResultAlgType.POI_Y : ViewResultAlgType.POI_XYZ;
        }

        public static LocalPoiResultSet Calculate(LocalFlowFrameLease frame, PoiParam poi, PoiFilterParam? filter, PoiReviseParam? revise)
        {
            if (!frame.HasCie) throw new InvalidOperationException("当前内存帧没有 CIE 数据，无法计算 POI。");
            if (poi.PoiPoints.Count == 0 && poi.Id > 0) PoiParam.LoadPoiDetailFromDB(poi);
            if (poi.PoiPoints.Count == 0) throw new InvalidOperationException($"POI 模板没有关注点：{poi.Name}");

            IntPtr convertHandle = Tool.GenerateRandomIntPtr();
            bool initialized = false;
            try
            {
                if (ConvertXYZ.CM_InitXYZ(convertHandle) == 0) throw new InvalidOperationException("初始化本地 CIE 计算上下文失败。");
                initialized = true;
                if (ConvertXYZ.CM_SetBufferXYZ(convertHandle, (uint)frame.Metadata.Width, (uint)frame.Metadata.Height, (uint)frame.Metadata.CieBpp, (uint)frame.Metadata.Channels, frame.CiePointer) == 0)
                {
                    throw new InvalidOperationException("设置本地 CIE 内存缓冲区失败。");
                }

                ApplyFilter(convertHandle, filter);
                ApplyRevise(convertHandle, revise);
                LocalPoiResultSet result = new() { FrameId = frame.FrameId.ToString("N"), TemplateName = poi.Name };
                foreach (PoiPoint point in poi.PoiPoints)
                {
                    result.Points.Add(CalculatePoint(convertHandle, frame.Metadata.Channels, point));
                }
                return result;
            }
            finally
            {
                if (initialized)
                {
                    _ = ConvertXYZ.CM_ReleaseBuffer(convertHandle);
                    _ = ConvertXYZ.CM_UnInitXYZ(convertHandle);
                }
            }
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
            if (inserted != details.Count) throw new InvalidOperationException($"保存 POI 明细失败：应写入 {details.Count} 条，实际写入 {inserted} 条。");
        }

        public static void DeleteDetails(int masterId)
        {
            if (masterId > 0) _ = PoiPointResultDao.Instance.Delete(item => item.Pid == masterId);
        }

        private static LocalPoiPointResult CalculatePoint(IntPtr handle, int channels, PoiPoint point)
        {
            int x = checked((int)point.PixX);
            int y = checked((int)point.PixY);
            int width = Math.Max(checked((int)point.PixWidth), 1);
            int height = Math.Max(checked((int)point.PixHeight), 1);
            POIPointTypes pointType = point.PointType switch
            {
                GraphicTypes.Point => POIPointTypes.SolidPoint,
                GraphicTypes.Circle => POIPointTypes.Circle,
                GraphicTypes.Rect => POIPointTypes.Rect,
                _ => throw new NotSupportedException($"本地 POI 暂不支持形状：{point.PointType}")
            };
            ValidateRegion(x, y, width, height, pointType, point.Name);

            IPOIResultData value = channels == 1
                ? CalculateLuminance(handle, x, y, width, height, pointType, point.Name)
                : CalculateColor(handle, x, y, width, height, pointType, point.Name);
            return new LocalPoiPointResult
            {
                PoiId = point.Id,
                Name = point.Name ?? point.Id.ToString(),
                PointType = pointType,
                X = x,
                Y = y,
                Width = width,
                Height = height,
                Value = value
            };
        }

        private static POIResultDataCIEY CalculateLuminance(IntPtr handle, int x, int y, int width, int height, POIPointTypes type, string name)
        {
            float luminance = 0;
            int success = type == POIPointTypes.Rect
                ? ConvertXYZ.CM_GetYRect(handle, x, y, ref luminance, width, height)
                : ConvertXYZ.CM_GetYCircle(handle, x, y, ref luminance, type == POIPointTypes.SolidPoint ? 1 : width / 2.0);
            if (success == 0) throw new InvalidOperationException($"计算 POI 亮度失败：{name}");
            return new POIResultDataCIEY(luminance);
        }

        private static POIResultDataCIExyuv CalculateColor(IntPtr handle, int x, int y, int width, int height, POIPointTypes type, string name)
        {
            float valueX = 0, valueY = 0, valueZ = 0, chromaX = 0, chromaY = 0, u = 0, v = 0, cct = 0, wave = 0;
            int success;
            if (type == POIPointTypes.Rect)
            {
                success = ConvertXYZ.CM_GetXYZxyuvRect(handle, x, y, ref valueX, ref valueY, ref valueZ, ref chromaX, ref chromaY, ref u, ref v, width, height);
                if (success != 0) success = ConvertXYZ.CM_GetxyuvCCTWaveRect(handle, x, y, ref chromaX, ref chromaY, ref u, ref v, ref cct, ref wave, width, height);
            }
            else
            {
                double radius = type == POIPointTypes.SolidPoint ? 1 : width / 2.0;
                success = ConvertXYZ.CM_GetXYZxyuvCircle(handle, x, y, ref valueX, ref valueY, ref valueZ, ref chromaX, ref chromaY, ref u, ref v, radius);
                if (success != 0) success = ConvertXYZ.CM_GetxyuvCCTWaveCircle(handle, x, y, ref chromaX, ref chromaY, ref u, ref v, ref cct, ref wave, radius);
            }
            if (success == 0) throw new InvalidOperationException($"计算 POI 色度失败：{name}");
            return new POIResultDataCIExyuv(cct, wave, valueX, valueY, valueZ, chromaX, chromaY, u, v);
        }

        private static void ApplyFilter(IntPtr handle, PoiFilterParam? filter)
        {
            _ = ConvertXYZ.CM_SetPercentFilter(handle, filter?.ThresholdUsePercent == true, filter?.MaxPercent ?? 0.2f);
            _ = ConvertXYZ.CM_SetFilter(handle, filter?.Enable == true, filter?.Threshold ?? 0);
            _ = ConvertXYZ.CM_SetFilterNoArea(handle, filter?.NoAreaEnable == true, filter?.Threshold ?? 0);
            _ = ConvertXYZ.CM_SetFilterXYZ(handle, filter?.XYZEnable == true, filter?.XYZType ?? 0, filter?.Threshold ?? 0);
        }

        private static void ApplyRevise(IntPtr handle, PoiReviseParam? revise)
        {
            bool enabled = revise != null && revise.GenCalibrationType != GenCalibrationType.None;
            float m = revise?.M ?? 1;
            float n = revise?.N ?? 1;
            float p = revise?.P ?? 1;
            _ = ConvertXYZ.CM_SetBymnp(handle, enabled, m, n, p);
        }

        private static void ValidateRegion(int x, int y, int width, int height, POIPointTypes type, string name)
        {
            if (x < 0 || y < 0 || width <= 0 || height <= 0)
            {
                throw new InvalidOperationException($"POI 区域无效：{name}");
            }
            if (type == POIPointTypes.Circle && width != height)
            {
                // Native POI uses width as the circle diameter; retaining this behavior is intentional.
            }
        }
    }
}
