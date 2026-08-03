using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ServicePoiPointTypes = FlowEngineLib.Node.POI.POIPointTypes;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalRealPoiParameters
    {
        public required PoiParam Poi { get; init; }
        public PoiFilterParam? Filter { get; init; }
        public PoiReviseParam? Revise { get; init; }
        public int SourceMasterId { get; init; } = -1;
    }

    internal static class LocalRealPoiInputResolver
    {
        public static LocalRealPoiParameters Resolve(
            int inputMasterId,
            int inputResultType,
            string imageInputName,
            string poiTemplateName,
            string poiFilterTemplateName,
            string poiReviseTemplateName,
            ServicePoiPointTypes poiType,
            float poiWidth,
            float poiHeight)
        {
            int sourceMasterId = -1;
            PoiParam poi;
            if (inputMasterId > 0)
            {
                if (inputResultType is (int)CVCommCore.CVResultType.Camera_Img
                    or (int)CVCommCore.CVResultType.Algorithm_Calibration)
                {
                    throw new InvalidOperationException($"IN_POI 接收到的是图像结果：MasterId={inputMasterId}，ResultType={inputResultType}。当前两条输入线可能接反；图像应连接 {imageInputName}，关注点布点应连接 IN_POI。");
                }
                sourceMasterId = inputMasterId;
                poi = BuildPoiFromInput(inputMasterId, inputResultType, poiType);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(poiTemplateName)) throw new InvalidOperationException("IN_POI 没有有效的布点结果，请选择备用 POI 模板。");
                poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, poiTemplateName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 模板：{poiTemplateName}");
            }

            ApplyPoiTypeOverride(poi, poiType, poiWidth, poiHeight);
            PoiFilterParam? filter = string.IsNullOrWhiteSpace(poiFilterTemplateName)
                ? null
                : TemplatePoiFilterParam.Params.FirstOrDefault(item => string.Equals(item.Key, poiFilterTemplateName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 过滤模板：{poiFilterTemplateName}");
            PoiReviseParam? revise = string.IsNullOrWhiteSpace(poiReviseTemplateName)
                ? null
                : TemplatePoiReviseParam.Params.FirstOrDefault(item => string.Equals(item.Key, poiReviseTemplateName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 修正模板：{poiReviseTemplateName}");
            return new LocalRealPoiParameters
            {
                Poi = poi,
                Filter = filter,
                Revise = revise,
                SourceMasterId = sourceMasterId
            };
        }

        private static PoiParam BuildPoiFromInput(int masterId, int masterResultType, ServicePoiPointTypes poiType)
        {
            List<PoiPointResultModel> details = PoiPointResultDao.Instance.GetAllByPid(masterId);
            PoiParam poi = new() { Id = masterId, Name = $"IN_POI#{masterId}" };
            foreach (PoiPointResultModel detail in details)
            {
                poi.PoiPoints.Add(new PoiPoint
                {
                    Id = detail.PoiId ?? detail.Id,
                    Name = string.IsNullOrWhiteSpace(detail.PoiName) ? (detail.PoiId ?? detail.Id).ToString() : detail.PoiName,
                    PointType = ToGraphicType(ResolvePointType(detail.PoiType, poiType)),
                    PixX = detail.PoiX ?? 0,
                    PixY = detail.PoiY ?? 0,
                    PixWidth = Math.Max(detail.PoiWidth ?? 1, 1),
                    PixHeight = Math.Max(detail.PoiHeight ?? 1, 1)
                });
            }
            if (poi.PoiPoints.Count > 0) return poi;

            List<PoiCieFileModel> files = PoiCieFileDao.Instance.GetAllByPid(masterId);
            foreach (PoiCieFileModel file in files)
            {
                if (string.IsNullOrWhiteSpace(file.FileUrl) || !File.Exists(file.FileUrl)) continue;
                POIPointInfo? pointInfo = ViewHandleBuildPoiFile.ReadPOIPointFromCSV(file.FileUrl);
                if (pointInfo?.Positions == null || pointInfo.HeaderInfo == null) continue;
                int pointId = poi.PoiPoints.Count + 1;
                foreach (POIPointPosition position in pointInfo.Positions)
                {
                    poi.PoiPoints.Add(new PoiPoint
                    {
                        Id = pointId,
                        Name = pointId.ToString(),
                        PointType = ToGraphicType(ResolvePointType(pointInfo.HeaderInfo.PointType, poiType)),
                        PixX = position.PixelX,
                        PixY = position.PixelY,
                        PixWidth = Math.Max(pointInfo.HeaderInfo.Width, 1),
                        PixHeight = Math.Max(pointInfo.HeaderInfo.Height, 1)
                    });
                    pointId++;
                }
                poi.PoiConfig.AreaRectRow = pointInfo.HeaderInfo.Rows;
                poi.PoiConfig.AreaRectCol = pointInfo.HeaderInfo.Cols;
            }
            if (poi.PoiPoints.Count == 0)
            {
                throw new InvalidOperationException($"IN_POI 无法加载布点数据：MasterId={masterId}，ResultType={masterResultType}；数据库明细和布点文件均为空。");
            }
            return poi;
        }

        private static void ApplyPoiTypeOverride(PoiParam poi, ServicePoiPointTypes poiType, float poiWidth, float poiHeight)
        {
            if (poiType == ServicePoiPointTypes.None) return;
            if (poiType == ServicePoiPointTypes.SubPixel)
            {
                throw new NotSupportedException("本地实时 POI 暂不支持亚像素类型，请使用服务实时关注点算法。");
            }

            GraphicTypes graphicType = ToGraphicType(ToCorePointType(poiType));
            foreach (PoiPoint point in poi.PoiPoints)
            {
                point.PointType = graphicType;
                if (poiType is ServicePoiPointTypes.SolidPoint or ServicePoiPointTypes.SolidPoint_KB)
                {
                    point.PixWidth = 1;
                    point.PixHeight = 1;
                }
                else
                {
                    point.PixWidth = poiWidth;
                    point.PixHeight = poiHeight;
                }
            }
        }

        private static POIPointTypes ResolvePointType(POIPointTypes sourceType, ServicePoiPointTypes poiType)
            => sourceType == POIPointTypes.None && poiType != ServicePoiPointTypes.None ? ToCorePointType(poiType) : sourceType;

        private static POIPointTypes ToCorePointType(ServicePoiPointTypes pointType)
        {
            return pointType switch
            {
                ServicePoiPointTypes.SolidPoint_KB => POIPointTypes.SolidPoint_KB,
                ServicePoiPointTypes.SolidPoint => POIPointTypes.SolidPoint,
                ServicePoiPointTypes.Circle => POIPointTypes.Circle,
                ServicePoiPointTypes.Rect => POIPointTypes.Rect,
                _ => POIPointTypes.None
            };
        }

        private static GraphicTypes ToGraphicType(POIPointTypes pointType)
        {
            return pointType switch
            {
                POIPointTypes.SolidPoint_KB or POIPointTypes.SolidPoint => GraphicTypes.Point,
                POIPointTypes.Circle => GraphicTypes.Circle,
                POIPointTypes.Rect or POIPointTypes.LTRect => GraphicTypes.Rect,
                _ => throw new NotSupportedException($"本地实时 POI 暂不支持上游布点形状：{pointType}")
            };
        }
    }

    internal sealed class LocalRealPoiNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.POI_XYZ;
        public int CieMasterId { get; init; }
        public int PoiSourceMasterId { get; init; }
        public string PoiTemplateName { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public int TotalTime { get; init; }
        public object? POIResult { get; init; }
    }

    [STNode("Flow_CustomNodes", "实时 POI")]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIFilterTempName), typeof(FlowPoiFilterTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIReviseTempName), typeof(FlowPoiReviseTemplateEditor))]
    public sealed class LocalRealPoiNode : LocalFlowNodeBase
    {
        private static readonly string[] InputPortNames = { "IN_CIE", "IN_POI" };
        private string poiTempName = string.Empty;
        private string poiFilterTempName = string.Empty;
        private string poiReviseTempName = string.Empty;
        private ServicePoiPointTypes poiType;
        private float poiWidth = 10;
        private float poiHeight = 10;

        [Category("实时 POI")]
        [STNodeProperty("POI 模板", "IN_POI 没有布点结果时使用的备用 POI 模板", true)]
        public string POITempName { get => poiTempName; set { poiTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("实时 POI")]
        [STNodeProperty("POI 过滤", "可选的 POI 过滤模板", true)]
        public string POIFilterTempName { get => poiFilterTempName; set { poiFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("实时 POI")]
        [STNodeProperty("POI 修正", "可选的 POI 修正模板", true)]
        public string POIReviseTempName { get => poiReviseTempName; set { poiReviseTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("实时 POI")]
        [STNodeProperty("POI 类型", "与服务实时关注点算法一致；None 使用上游布点结果中的类型", true)]
        public ServicePoiPointTypes POIType
        {
            get => poiType;
            set
            {
                poiType = value;
                if (poiType == ServicePoiPointTypes.Circle) poiHeight = poiWidth;
                OnPropertyChanged();
                OnPropertyChanged(nameof(POIHeight));
            }
        }

        [Category("实时 POI")]
        [STNodeProperty("POI 宽度", "POI 类型为圆或矩形时覆盖上游布点宽度", true)]
        public float POIWidth
        {
            get => poiWidth;
            set
            {
                poiWidth = NormalizePoiSize(value);
                if (POIType == ServicePoiPointTypes.Circle) poiHeight = poiWidth;
                OnPropertyChanged();
                OnPropertyChanged(nameof(POIHeight));
            }
        }

        [Category("实时 POI")]
        [STNodeProperty("POI 高度", "POI 类型为圆或矩形时覆盖上游布点高度", true)]
        public float POIHeight
        {
            get => poiHeight;
            set
            {
                poiHeight = NormalizePoiSize(value);
                if (POIType == ServicePoiPointTypes.Circle) poiWidth = poiHeight;
                OnPropertyChanged();
                OnPropertyChanged(nameof(POIWidth));
            }
        }

        public LocalRealPoiNode() : base("实时 POI", "LocalRealPOI", "Real_POI", 60000, InputPortNames)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            if (!action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) || currentFrame == null)
            {
                throw new InvalidOperationException("IN_CIE 没有可用的本地 CIE 内存帧。");
            }
            _ = TryGetInputMasterResult(action, 1, out int poiInputMasterId, out int poiInputResultType, out _);
            LocalRealPoiParameters parameters = LocalRealPoiInputResolver.Resolve(
                poiInputMasterId,
                poiInputResultType,
                InputPortNames[0],
                POITempName,
                POIFilterTempName,
                POIReviseTempName,
                POIType,
                POIWidth,
                POIHeight);

            Stopwatch stopwatch = Stopwatch.StartNew();
            LocalPoiResultSet result;
            using (LocalFlowFrameLease frame = currentFrame.Acquire())
            {
                result = LocalPoiCalculator.Calculate(frame, parameters.Poi, parameters.Filter, parameters.Revise);
            }
            stopwatch.Stop();
            int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
            ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(currentFrame.Metadata.Channels);
            int masterId = -1;
            try
            {
                masterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    parameters.Poi.Id,
                    parameters.Poi.Name,
                    currentFrame.CvCieFilePath,
                    string.IsNullOrWhiteSpace(DeviceCode) ? currentFrame.Metadata.DeviceCode : DeviceCode,
                    ZIndex,
                    totalTime,
                    new
                    {
                        CieMasterId = currentFrame.MasterId,
                        POISourceMasterId = parameters.SourceMasterId > 0 ? (int?)parameters.SourceMasterId : null,
                        CalibrationTemplate = currentFrame.Metadata.CalibrationTemplate,
                        POITemplate = parameters.Poi.Name,
                        POIFilterTemplate = parameters.Filter?.Name,
                        POIReviseTemplate = parameters.Revise?.Name,
                        FlipMode = currentFrame.Metadata.FlipMode.ToString(),
                        FlipApplied = currentFrame.IsCieFlipApplied,
                        ImageRead = false,
                        MemoryOnly = string.IsNullOrWhiteSpace(currentFrame.CvCieFilePath)
                    });
                LocalPoiCalculator.SaveDetails(masterId, result);

                action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(currentFrame.FrameId), result);
                action.Data["LocalPoiCount"] = result.Points.Count;
                action.Data["LocalPoiSourceMasterId"] = parameters.SourceMasterId;
                action.MasterValue(null, masterId, (int)resultType);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalRealPoiNodeResultData
                    {
                        FrameId = currentFrame.FrameId.ToString("N"),
                        MasterId = masterId,
                        MasterResultType = (int)resultType,
                        CieMasterId = currentFrame.MasterId,
                        PoiSourceMasterId = parameters.SourceMasterId,
                        PoiTemplateName = result.TemplateName,
                        PointCount = result.Points.Count,
                        TotalTime = totalTime,
                        POIResult = result.Points
                    }
                };
            }
            catch
            {
                LocalPoiCalculator.DeleteDetails(masterId);
                LocalFlowResultPersistence.DeleteAlgorithmResult(masterId);
                throw;
            }
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = operatorCode,
                action.SerialNumber,
                POITempName,
                POIFilterTempName,
                POIReviseTempName,
                POIType,
                POIWidth,
                POIHeight,
                InputMode = "CurrentFrame",
                InputPorts = InputPortNames
            });
        }

        private static float NormalizePoiSize(float value)
        {
            if (value <= 0) return 1;
            int size = checked((int)Math.Ceiling(value));
            return size % 2 == 0 ? size : size + 1;
        }
    }
}
