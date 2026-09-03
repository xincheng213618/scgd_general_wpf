using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates.POI;
using FlowEngineLib.Algorithm;
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
        public bool UseROI { get; init; }
        public int? RoiOffsetX { get; init; }
        public int? RoiOffsetY { get; init; }
    }

    internal readonly record struct LocalPoiRoiAdjustment(PoiParam Poi, int OffsetX, int OffsetY);

    internal static class LocalPoiRoiCoordinateTransformer
    {
        public static LocalPoiRoiAdjustment Transform(PoiParam source, PhyCameraCfg cameraConfig, LocalFrameMetadata frameMetadata)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(cameraConfig);
            ArgumentNullException.ThrowIfNull(frameMetadata);
            bool hasRoiValues = cameraConfig.PointX != 0
                || cameraConfig.PointY != 0
                || cameraConfig.Width != 0
                || cameraConfig.Height != 0;
            if (!cameraConfig.IsRoiConfigured && hasRoiValues)
            {
                throw new InvalidOperationException($"物理相机 ROI 配置不完整：ROI=({cameraConfig.PointX},{cameraConfig.PointY},{cameraConfig.Width},{cameraConfig.Height})。");
            }
            if (cameraConfig.IsRoiConfigured
                && (cameraConfig.PointX < 0 || cameraConfig.PointY < 0
                    || (long)cameraConfig.PointX + cameraConfig.Width > cameraConfig.SensorWidth
                    || (long)cameraConfig.PointY + cameraConfig.Height > cameraConfig.SensorHeight))
            {
                throw new InvalidOperationException($"物理相机 ROI 超出传感器范围：ROI=({cameraConfig.PointX},{cameraConfig.PointY},{cameraConfig.Width},{cameraConfig.Height})，Sensor={cameraConfig.SensorWidth}x{cameraConfig.SensorHeight}。");
            }
            if (cameraConfig.IsRoiConfigured
                && (frameMetadata.Width != cameraConfig.Width || frameMetadata.Height != cameraConfig.Height))
            {
                throw new InvalidOperationException($"已启用“使用 ROI”，但当前图像尺寸 {frameMetadata.Width}x{frameMetadata.Height} 与物理相机 ROI {cameraConfig.Width}x{cameraConfig.Height} 不一致。全幅或历史图像请关闭此选项。");
            }
            if (source.PoiPoints.Count == 0 && source.Id > 0)
            {
                PoiParam.LoadPoiDetailFromDB(source);
            }
            if (source.PoiPoints.Count == 0)
            {
                throw new InvalidOperationException($"POI 模板没有关注点：{source.Name}");
            }

            LocalFrameMirrorService.ValidateFlipMode(frameMetadata.FlipMode);
            int offsetX = cameraConfig.IsRoiConfigured
                ? frameMetadata.FlipMode is CVImageFlipMode.Y or CVImageFlipMode.XY
                    ? checked(cameraConfig.SensorWidth - cameraConfig.PointX - cameraConfig.Width)
                    : cameraConfig.PointX
                : 0;
            int offsetY = cameraConfig.IsRoiConfigured
                ? frameMetadata.FlipMode is CVImageFlipMode.X or CVImageFlipMode.XY
                    ? checked(cameraConfig.SensorHeight - cameraConfig.PointY - cameraConfig.Height)
                    : cameraConfig.PointY
                : 0;
            PoiParam transformed = new()
            {
                Id = source.Id,
                Name = source.Name,
                Type = source.Type,
                Width = source.Width,
                Height = source.Height,
                CfgJson = source.CfgJson,
                LeftTopX = Offset(source.LeftTopX, offsetX),
                LeftTopY = Offset(source.LeftTopY, offsetY),
                RightTopX = Offset(source.RightTopX, offsetX),
                RightTopY = Offset(source.RightTopY, offsetY),
                RightBottomX = Offset(source.RightBottomX, offsetX),
                RightBottomY = Offset(source.RightBottomY, offsetY),
                LeftBottomX = Offset(source.LeftBottomX, offsetX),
                LeftBottomY = Offset(source.LeftBottomY, offsetY)
            };
            foreach (PoiPoint point in source.PoiPoints)
            {
                transformed.PoiPoints.Add(new PoiPoint
                {
                    Id = point.Id,
                    Pid = point.Pid,
                    Name = point.Name,
                    PointType = point.PointType,
                    PixX = point.PixX - offsetX,
                    PixY = point.PixY - offsetY,
                    PixWidth = point.PixWidth,
                    PixHeight = point.PixHeight
                });
            }
            return new LocalPoiRoiAdjustment(transformed, offsetX, offsetY);
        }

        private static int? Offset(int? value, int offset) => value.HasValue ? checked(value.Value - offset) : null;
    }

    [STNode("Flow_CustomNodes", "本地校正+实时 POI")]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
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
        private bool useROI;

        [Category("本地校正")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("备用图像文件", "上游没有本地内存帧时读取此文件；有上游帧时忽略", true)]
        public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("实时 POI")]
        [STNodeProperty("POI 模板", "校正后直接在 CIE 内存上计算的 POI 模板", true)]
        public string POITempName { get => poiTempName; set { poiTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        // Kept only so existing serialized node payloads can still be opened.
        public string POIFilterTempName { get => poiFilterTempName; set { poiFilterTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Browsable(false)]
        // Kept only so existing serialized node payloads can still be opened.
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

        [Category("高级")]
        [STNodeProperty("使用 ROI", "输入 POI 为全幅坐标且当前图像来自物理相机 ROI 时，将 POI 临时转换为 ROI 图像坐标；全幅或历史图像保持关闭", true)]
        public bool UseROI { get => useROI; set { useROI = value; OnPropertyChanged(); } }

        public LocalCalibrationRealPoiNode() : base("本地校正+实时 POI", "LocalCalibrationRealPOI", "Real_POI", InputPortNames)
        {
        }

        private protected override string SourceImageFilePath => ImageFilePath;

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            _ = TryGetInputMasterResult(action, 1, out int poiInputMasterId, out int poiInputResultType, out _);
            LocalRealPoiParameters parameters = LocalRealPoiInputResolver.Resolve(
                poiInputMasterId,
                poiInputResultType,
                InputPortNames[0],
                POITempName,
                POIType,
                POIWidth,
                POIHeight);
            using LocalCalibrationExecution execution = ExecuteCalibration(action);
            if (!execution.Frame.HasCie)
            {
                throw new InvalidOperationException("实时 POI 需要 CIE 数据，请在校正模板中选择一个亮度或颜色校正文件。");
            }
            LocalPoiRoiAdjustment? roiAdjustment = null;
            PoiParam calculationPoi = parameters.Poi;
            if (UseROI)
            {
                DeviceCamera device = ResolveDevice(execution.Frame.Metadata.DeviceCode);
                PhyCameraCfg cameraConfig = device.PhyCamera?.Config?.CameraCfg
                    ?? throw new InvalidOperationException("已启用“使用 ROI”，但当前相机没有关联物理相机配置。");
                roiAdjustment = LocalPoiRoiCoordinateTransformer.Transform(parameters.Poi, cameraConfig, execution.Frame.Metadata);
                calculationPoi = roiAdjustment.Value.Poi;
            }
            int calibrationMasterId = -1;
            int poiMasterId = -1;
            try
            {
                MeasureResultImgModel persistedCalibration = SaveCalibrationResult(action, execution);
                calibrationMasterId = persistedCalibration.Id;
                execution.Frame.MasterId = calibrationMasterId;

                Stopwatch stopwatch = Stopwatch.StartNew();
                LocalPoiResultSet result;
                using (LocalFlowFrameLease frame = execution.Frame.Acquire())
                {
                    result = LocalPoiCalculator.Calculate(frame, calculationPoi);
                }
                stopwatch.Stop();
                int poiTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(execution.Frame.Metadata.Channels);
                string algorithmDeviceCode = GetFirstAvailableDeviceCode<DeviceAlgorithm>();
                poiMasterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    parameters.Poi.Id,
                    parameters.Poi.Name,
                    execution.Frame.CvCieFilePath,
                    algorithmDeviceCode,
                    ZIndex,
                    poiTime,
                    new
                    {
                        CieMasterId = calibrationMasterId,
                        POISourceMasterId = parameters.SourceMasterId > 0 ? (int?)parameters.SourceMasterId : null,
                        CalibrationTemplate = execution.Calibration?.Name ?? execution.Frame.Metadata.CalibrationTemplate,
                        POITemplate = parameters.Poi.Name,
                        UseROI = UseROI,
                        RoiOffsetX = roiAdjustment?.OffsetX,
                        RoiOffsetY = roiAdjustment?.OffsetY,
                        MemoryOnly = string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath)
                    });
                LocalPoiCalculator.SaveDetails(poiMasterId, result);

                action.RuntimeResources.Set(LocalFlowFrameRuntime.GetPoiResultResourceKey(execution.Frame.FrameId), result);
                action.Data["LocalPoiCount"] = result.Points.Count;
                action.Data["LocalCalibrationMasterId"] = calibrationMasterId;
                action.MasterValue(null, poiMasterId, (int)resultType);
                execution.TransferFrameTo(action);
                (string calibrationRoute, string calibrationDeviceCode) = ResolveCalibrationResultTarget(persistedCalibration.DeviceCode ?? string.Empty);
                ResultMessageBus.Default.PublishPersisted(calibrationRoute, ResultKinds.Image, calibrationDeviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, calibrationMasterId, (int)ViewResultAlgType.Calibration);
                ResultMessageBus.Default.PublishPersisted(ResultRoutes.Algorithm, ResultKinds.Algorithm, algorithmDeviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, poiMasterId, (int)resultType);
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
                        FlipMode = execution.Frame.Metadata.FlipMode.ToString(),
                        FlipApplied = execution.Frame.IsFlipApplied,
                        LoadedFromFile = execution.LoadedFromFile,
                        Calibrated = execution.Calibrated,
                        HasRaw = execution.Frame.HasRaw,
                        HasCie = execution.Frame.HasCie,
                        CvRawFilePath = NullIfEmpty(execution.Frame.CvRawFilePath),
                        HasCieFile = !string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath),
                        CvCieFilePath = NullIfEmpty(execution.Frame.CvCieFilePath),
                        PoiTemplateName = result.TemplateName,
                        PointCount = result.Points.Count,
                        POIResult = result.Points,
                        UseROI = UseROI,
                        RoiOffsetX = roiAdjustment?.OffsetX,
                        RoiOffsetY = roiAdjustment?.OffsetY
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
                EventName = OperatorCode,
                action.SerialNumber,
                ImageFilePath,
                CalibTempName,
                POITempName,
                POIType,
                POIWidth,
                POIHeight,
                UseROI,
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
