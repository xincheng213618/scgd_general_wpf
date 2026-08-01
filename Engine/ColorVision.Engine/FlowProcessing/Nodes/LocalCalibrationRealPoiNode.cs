using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using ServicePoiPointTypes = FlowEngineLib.Node.POI.POIPointTypes;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal sealed class LocalCalibrationRealPoiNodeResultData : LocalCalibrationNodeResultData
    {
        public int CalibrationMasterId { get; init; }
        public int PoiMasterId { get; init; }
        public string PoiTemplateName { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public bool LoadedFromFile { get; init; }
        public object? POIResult { get; init; }
    }

    [STNode("Flow_CustomNodes", "本地校正+实时 POI")]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIFilterTempName), typeof(FlowPoiFilterTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIReviseTempName), typeof(FlowPoiReviseTemplateEditor))]
    public sealed class LocalCalibrationRealPoiNode : LocalCalibrationNodeBase
    {
        private static readonly string[] InputPortNames = { "IN_IMG", "IN_POI" };
        private string imageFilePath = string.Empty;
        private string poiTempName = string.Empty;
        private string poiFilterTempName = string.Empty;
        private string poiReviseTempName = string.Empty;
        private ServicePoiPointTypes poiType;
        private float poiWidth = 10;
        private float poiHeight = 10;

        [Category("本地校正")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("备用图像文件", "上游没有本地内存帧时读取此文件；有上游帧时忽略", true)]
        public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("实时 POI")]
        [STNodeProperty("POI 模板", "校正后直接在 CIE 内存上计算的 POI 模板", true)]
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

        public LocalCalibrationRealPoiNode() : base("本地校正+实时 POI", "LocalCalibrationRealPOI", "Real_POI", 120000, InputPortNames)
        {
        }

        private protected override bool SupportsFileFallback => true;

        private protected override string SourceImageFilePath => ImageFilePath;

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
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
            using LocalCalibrationExecution execution = ExecuteCalibration(action);
            int calibrationMasterId = -1;
            int poiMasterId = -1;
            try
            {
                calibrationMasterId = SaveCalibrationResult(action, execution);
                execution.Frame.MasterId = calibrationMasterId;

                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalPoiResultSet result;
                using (LocalFlowFrameLease frame = execution.Frame.Acquire())
                {
                    result = LocalPoiCalculator.Calculate(frame, parameters.Poi, parameters.Filter, parameters.Revise);
                }
                stopwatch.Stop();
                int poiTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(execution.Frame.Metadata.Channels);
                poiMasterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    parameters.Poi.Id,
                    parameters.Poi.Name,
                    execution.Frame.CvCieFilePath,
                    string.IsNullOrWhiteSpace(DeviceCode) ? execution.Frame.Metadata.DeviceCode : DeviceCode,
                    ZIndex,
                    poiTime,
                    new
                    {
                        CieMasterId = calibrationMasterId,
                        POISourceMasterId = parameters.SourceMasterId > 0 ? (int?)parameters.SourceMasterId : null,
                        CalibrationTemplate = execution.Calibration?.Name ?? execution.Frame.Metadata.CalibrationTemplate,
                        POITemplate = parameters.Poi.Name,
                        POIFilterTemplate = parameters.Filter?.Name,
                        POIReviseTemplate = parameters.Revise?.Name,
                        MemoryOnly = string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath)
                    });
                LocalPoiCalculator.SaveDetails(poiMasterId, result);

                action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(execution.Frame.FrameId), result);
                action.Data["LocalPoiCount"] = result.Points.Count;
                action.Data["LocalCalibrationMasterId"] = calibrationMasterId;
                action.MasterValue(null, poiMasterId, (int)resultType);
                execution.TransferFrameTo(action);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalCalibrationRealPoiNodeResultData
                    {
                        FrameId = execution.Frame.FrameId.ToString("N"),
                        MasterId = poiMasterId,
                        MasterResultType = (int)resultType,
                        CalibrationMasterId = calibrationMasterId,
                        PoiMasterId = poiMasterId,
                        TotalTime = execution.TotalTime + poiTime,
                        LoadedFromFile = execution.LoadedFromFile,
                        Calibrated = execution.Calibrated,
                        HasRaw = execution.Frame.HasRaw,
                        HasCie = execution.Frame.HasCie,
                        CvRawFilePath = NullIfEmpty(execution.Frame.CvRawFilePath),
                        HasCieFile = !string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath),
                        CvCieFilePath = NullIfEmpty(execution.Frame.CvCieFilePath),
                        PoiTemplateName = result.TemplateName,
                        PointCount = result.Points.Count,
                        POIResult = result.Points
                    }
                };
            }
            catch
            {
                LocalPoiCalculator.DeleteDetails(poiMasterId);
                LocalFlowResultPersistence.DeleteAlgorithmResult(poiMasterId);
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
                ImageFilePath,
                CalibTempName,
                POITempName,
                POIFilterTempName,
                POIReviseTempName,
                POIType,
                POIWidth,
                POIHeight,
                SaveFiles,
                InputPriority = "CurrentFrameThenFile",
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
