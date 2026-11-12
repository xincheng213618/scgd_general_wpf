using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using MQTTMessageLib.Algorithm;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

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
        public required object Value { get; init; }
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
        internal static string ResultResourceKey(int masterId) => $"ColorVision.LocalPoi.Result.{masterId}";

        internal static (string Handle, string? CsvFile) StoreRuntimeResult(CVStartCFC action, LocalPoiResultSet result, ViewResultAlgType resultType, string resultDirectory)
        {
            action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
            string handle = "local-poi-result:" + Guid.NewGuid().ToString("N");
            string? file = ExportCsv(result, resultDirectory, action.RuntimeResources.StopToken);
            try
            {
                action.SetRuntimeResource(handle, result);
                action.SetRuntimeResource(LocalFlowFrameRuntime.PoiResultResourceKeyPrefix + result.FrameId, result);
                action.Data["LocalPoiCount"] = result.Points.Count;
                action.MasterValue(handle, 0, (int)resultType);
                return (handle, file);
            }
            catch
            {
                if (file != null) File.Delete(file);
                throw;
            }
        }

        private static string? ExportCsv(LocalPoiResultSet result, string directory, CancellationToken cancellation)
        {
            if (string.IsNullOrWhiteSpace(directory)) return null;
            if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("本地 POI 结果目录必须是绝对路径。", nameof(directory));
            cancellation.ThrowIfCancellationRequested();
            Directory.CreateDirectory(directory);
            string file = Path.Combine(directory, $"POI_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.csv");
            try
            {
                using var writer = new StreamWriter(new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
                writer.WriteLine("PoiId,Name,PixelX,PixelY,Width,Height,X,Y,Z,x,y,u,v,CCT,Wave");
                foreach (LocalPoiPointResult point in result.Points)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string name = "\"" + point.Name.Replace("\"", "\"\"") + "\"";
                    writer.Write(FormattableString.Invariant($"{point.PoiId},{name},{point.X},{point.Y},{point.Width},{point.Height},"));
                    if (point.Value is POIResultDataCIExyuv xyz)
                        writer.WriteLine(FormattableString.Invariant($"{xyz.X},{xyz.Y},{xyz.Z},{xyz.x},{xyz.y},{xyz.u},{xyz.v},{xyz.CCT},{xyz.Wave}"));
                    else if (point.Value is POIResultDataCIEY luminance)
                        writer.WriteLine("," + luminance.Y.ToString("R", CultureInfo.InvariantCulture) + ",,,,,,,");
                    else throw new InvalidOperationException("无法导出未知的 POI 测量类型。");
                }
                cancellation.ThrowIfCancellationRequested();
                return file;
            }
            catch { File.Delete(file); throw; }
        }

        public static ViewResultAlgType ResolveResultType(int channels)
            => channels == 1 ? ViewResultAlgType.POI_Y : ViewResultAlgType.POI_XYZ;

        public static LocalPoiResultSet Calculate(LocalFlowFrameLease frame, PoiParam poi)
        {
            ValidateFrame(frame);
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

            PoiMeasurementResult[] measurements = frame.HasCie ? PoiMeasurementService.Calculate(
                frame.CiePointer,
                frame.CieLength,
                frame.Metadata.Width,
                frame.Metadata.Height,
                frame.Metadata.CieBpp,
                frame.Metadata.Channels,
                requests) : PoiMeasurementService.CalculateCalibratedRaw(frame.RawPointer, frame.RawLength, frame.ColorCalibration!, requests);

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
                object value = frame.Metadata.Channels == 1
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

        private static void ValidateFrame(LocalFlowFrameLease frame)
        {
            ArgumentNullException.ThrowIfNull(frame);
            if (!frame.HasCie)
            {
                ColorCalibrationSnapshot? snapshot = frame.ColorCalibration;
                if (!frame.HasRaw || snapshot == null)
                    throw new InvalidOperationException("当前帧没有 CIE 或带色度校正参数的 RAW，无法计算 POI。请先执行亮度/颜色校正。");
                if (snapshot.Width != frame.Metadata.Width || snapshot.Height != frame.Metadata.Height
                    || snapshot.Channels != frame.Metadata.Channels || snapshot.RawBpp != frame.Metadata.SourceBpp)
                    throw new InvalidOperationException("RAW 色度校正参数与当前帧布局不一致。");
                if (!frame.IsRawFlipApplied) throw new InvalidOperationException("RAW 镜像操作必须在 POI 计算前完成。");
                return;
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
