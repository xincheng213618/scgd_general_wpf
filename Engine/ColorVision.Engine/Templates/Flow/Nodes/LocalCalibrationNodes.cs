using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.Engine.Templates.POI.POIFilters;
using ColorVision.Engine.Templates.POI.POIRevise;
using ColorVision.Database;
using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ServicePoiPointTypes = FlowEngineLib.Node.POI.POIPointTypes;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    internal class LocalCalibrationNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.Calibration;
        public int TotalTime { get; init; }
        public bool LoadedFromFile { get; init; }
        public bool Calibrated { get; init; }
        public bool HasCieFile { get; init; }
        public string? CvCieFilePath { get; init; }
    }

    internal sealed class LocalCalibrationRealPoiNodeResultData : LocalCalibrationNodeResultData
    {
        public int CalibrationMasterId { get; init; }
        public int PoiMasterId { get; init; }
        public string PoiTemplateName { get; init; } = string.Empty;
        public int PointCount { get; init; }
        public object? POIResult { get; init; }
    }

    internal sealed class LocalCalibrationExecution : IDisposable
    {
        private bool ownsFrame;

        public LocalCalibrationExecution(
            LocalFlowFrame frame,
            bool ownsFrame,
            CalibrationParam? calibration,
            string sourceFrameId,
            bool loadedFromFile,
            bool calibrated,
            int totalTime)
        {
            Frame = frame;
            this.ownsFrame = ownsFrame;
            Calibration = calibration;
            SourceFrameId = sourceFrameId;
            LoadedFromFile = loadedFromFile;
            Calibrated = calibrated;
            TotalTime = totalTime;
        }

        public LocalFlowFrame Frame { get; }
        public CalibrationParam? Calibration { get; }
        public string SourceFrameId { get; }
        public bool LoadedFromFile { get; }
        public bool Calibrated { get; }
        public int TotalTime { get; }

        public void TransferFrameTo(CVStartCFC action)
        {
            action.SetCurrentFrame(Frame);
            ownsFrame = false;
        }

        public void Dispose()
        {
            if (ownsFrame)
            {
                Frame.Dispose();
                ownsFrame = false;
            }
        }
    }

    public abstract class LocalCalibrationNodeBase : LocalFlowNodeBase
    {
        private string imageFilePath = string.Empty;
        private string calibTempName = string.Empty;
        private bool saveFiles;

        [Category("本地校正")]
        [PropertyEditorType(typeof(TextSelectFilePropertiesEditor))]
        [STNodeProperty("备用图像文件", "上游没有本地内存帧时读取此文件；有上游帧时忽略", true)]
        public string ImageFilePath { get => imageFilePath; set { imageFilePath = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地校正")]
        [STNodeProperty("校正模板", "对 RAW 指针执行的相机校正模板；CVCIE 输入会直接透传", true)]
        public string CalibTempName { get => calibTempName; set { calibTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地校正")]
        [STNodeProperty("保存 CVCIE", "默认关闭；关闭时校正结果只保留在流程内存中", true)]
        public bool SaveFiles { get => saveFiles; set { saveFiles = value; OnPropertyChanged(); } }

        protected LocalCalibrationNodeBase(string title, string nodeType, string operatorName, int timeoutMs, params string[] inputNames)
            : base(title, nodeType, operatorName, timeoutMs, inputNames)
        {
        }

        private protected LocalCalibrationExecution ExecuteCalibration(CVStartCFC action)
        {
            LocalFlowFrame sourceFrame;
            bool ownsSourceFrame;
            bool loadedFromFile;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
            {
                sourceFrame = currentFrame;
                ownsSourceFrame = false;
                loadedFromFile = false;
            }
            else
            {
                string sourceFilePath = ResolveSourceFilePath(action);
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    throw new InvalidOperationException("IN_IMG 没有本地图像内存帧，也没有可读取的图像结果，请配置备用图像文件。");
                }
                sourceFrame = LocalFrameFileService.Load(sourceFilePath);
                ownsSourceFrame = true;
                loadedFromFile = true;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            LocalFlowFrame? outputFrame = null;
            bool ownsOutputFrame = false;
            CalibrationParam? calibration = null;
            string sourceFrameId = sourceFrame.FrameId.ToString("N");
            bool calibrated = false;
            try
            {
                using (LocalFlowFrameLease source = sourceFrame.Acquire())
                {
                    if (source.HasRaw)
                    {
                        DeviceCamera device = ResolveDevice(source.Metadata.DeviceCode);
                        calibration = ResolveCalibration(device);
                        if (!device.TryGetCalibrationTemplateFiles(calibration, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? errorMessage))
                        {
                            throw new InvalidOperationException(errorMessage ?? "校正模板无效。");
                        }
                        outputFrame = LocalFrameCalibrationService.Calibrate(source, calibrationFiles, calibration.Name);
                        ownsOutputFrame = true;
                        calibrated = true;
                        if (SaveFiles)
                        {
                            LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code);
                        }
                    }
                    else if (source.HasCie)
                    {
                        outputFrame = sourceFrame;
                        ownsOutputFrame = ownsSourceFrame;
                        ownsSourceFrame = false;
                        if (SaveFiles && string.IsNullOrWhiteSpace(outputFrame.CvCieFilePath))
                        {
                            DeviceCamera device = ResolveDevice(source.Metadata.DeviceCode);
                            LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code);
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("当前本地帧既没有 RAW 内存，也没有 CIE 内存。");
                    }
                }

                stopwatch.Stop();
                LocalFlowFrame completedFrame = outputFrame ?? throw new InvalidOperationException("本地校正没有生成输出帧。");
                ownsOutputFrame = false;
                return new LocalCalibrationExecution(
                    completedFrame,
                    completedFrame != sourceFrame || loadedFromFile,
                    calibration,
                    sourceFrameId,
                    loadedFromFile,
                    calibrated,
                    checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)));
            }
            catch
            {
                if (ownsOutputFrame) outputFrame?.Dispose();
                throw;
            }
            finally
            {
                if (ownsSourceFrame) sourceFrame.Dispose();
            }
        }

        private protected int SaveCalibrationResult(CVStartCFC action, LocalCalibrationExecution execution)
        {
            LocalFlowFrame frame = execution.Frame;
            MeasureBatchModel batch = BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber)
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            string? cieFilePath = NullIfEmpty(frame.CvCieFilePath);
            if (SaveFiles && cieFilePath == null)
            {
                throw new InvalidOperationException("已启用“保存 CVCIE”，但本地校正没有生成 CVCIE 文件。");
            }

            MeasureResultImgModel model = new()
            {
                BatchId = batch.Id,
                ZIndex = ZIndex,
                NDPort = -1,
                Params = JsonConvert.SerializeObject(new
                {
                    SourceFrameId = execution.SourceFrameId,
                    SourceFile = NullIfEmpty(frame.Metadata.SourceFilePath),
                    execution.LoadedFromFile,
                    execution.Calibrated,
                    MemoryOnly = string.IsNullOrWhiteSpace(frame.CvCieFilePath),
                    frame.Metadata.Width,
                    frame.Metadata.Height,
                    frame.Metadata.SourceBpp,
                    frame.Metadata.CieBpp,
                    frame.Metadata.Channels,
                    frame.Metadata.Gain,
                    Exposure = frame.Metadata.Exposure,
                    Calibration = new { ID = execution.Calibration?.Id ?? -1, Name = execution.Calibration?.Name ?? frame.Metadata.CalibrationTemplate }
                }),
                RawFile = cieFilePath == null ? null : Path.GetFileName(cieFilePath),
                FileUrl = cieFilePath,
                FileType = cieFilePath == null ? null : (sbyte)CameraFileType.CIEFile,
                ImgFrameInfo = JsonConvert.SerializeObject(new
                {
                    bpp = frame.Metadata.CieBpp,
                    width = frame.Metadata.Width,
                    height = frame.Metadata.Height,
                    channels = frame.Metadata.Channels
                }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = execution.TotalTime,
                DeviceCode = ResolveDeviceCode(frame.Metadata.DeviceCode),
                CreateDate = DateTime.Now
            };
            int masterId = MeasureImgResultDao.Instance.SaveAndReturnId(model);
            if (masterId <= 0) throw new InvalidOperationException("保存本地校正图像结果失败。");
            return masterId;
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
                SaveFiles,
                InputPriority = "CurrentFrameThenFile"
            });
        }

        private DeviceCamera ResolveDevice(string frameDeviceCode)
        {
            string deviceCode = ResolveDeviceCode(frameDeviceCode);
            if (string.IsNullOrWhiteSpace(deviceCode)) throw new InvalidOperationException("校正 RAW 内存前必须选择本地相机设备。");
            return ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(camera => string.Equals(camera.Code, deviceCode, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"找不到本地相机设备：{deviceCode}");
        }

        private CalibrationParam ResolveCalibration(DeviceCamera device)
        {
            if (string.IsNullOrWhiteSpace(CalibTempName)) throw new InvalidOperationException("请选择校正模板。");
            return device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, CalibTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{CalibTempName}");
        }

        private string ResolveDeviceCode(string frameDeviceCode)
            => string.IsNullOrWhiteSpace(DeviceCode) ? frameDeviceCode : DeviceCode;

        private string ResolveSourceFilePath(CVStartCFC action)
        {
            if (!string.IsNullOrWhiteSpace(ImageFilePath))
            {
                return Path.GetFullPath(ImageFilePath.Trim());
            }
            if (!TryGetInputMasterResult(action, 0, out int masterId, out int masterResultType, out _) || masterId <= 0)
            {
                return string.Empty;
            }
            if (masterResultType is not (int)CVCommCore.CVResultType.Camera_Img
                and not (int)CVCommCore.CVResultType.Algorithm_Calibration)
            {
                throw new InvalidOperationException($"IN_IMG 接收到的不是图像结果：MasterId={masterId}，ResultType={masterResultType}。请将图像节点连接到 IN_IMG。");
            }

            MeasureResultImgModel? imageResult = MeasureImgResultDao.Instance.GetById(masterId);
            if (imageResult == null) return string.Empty;
            string? firstCandidate = null;
            foreach (string? candidate in new[] { imageResult.RawFile, imageResult.FileUrl })
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                firstCandidate ??= candidate;
                string fullPath = Path.GetFullPath(candidate);
                if (File.Exists(fullPath)) return fullPath;
            }
            return string.IsNullOrWhiteSpace(firstCandidate) ? string.Empty : Path.GetFullPath(firstCandidate);
        }

        protected static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [STNode("Flow_CustomNodes", "本地校正")]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    public sealed class LocalCalibrationNode : LocalCalibrationNodeBase
    {
        public LocalCalibrationNode() : base("本地校正", "LocalCalibration", "Calibration", 120000)
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            using LocalCalibrationExecution execution = ExecuteCalibration(action);
            int masterId = -1;
            try
            {
                masterId = SaveCalibrationResult(action, execution);
                execution.Frame.MasterId = masterId;
                action.MasterValue(null, masterId, (int)ViewResultAlgType.Calibration);
                execution.TransferFrameTo(action);
                return new LocalNodeExecutionResult
                {
                    Data = new LocalCalibrationNodeResultData
                    {
                        FrameId = execution.Frame.FrameId.ToString("N"),
                        MasterId = masterId,
                        TotalTime = execution.TotalTime,
                        LoadedFromFile = execution.LoadedFromFile,
                        Calibrated = execution.Calibrated,
                        HasCieFile = !string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath),
                        CvCieFilePath = NullIfEmpty(execution.Frame.CvCieFilePath)
                    }
                };
            }
            catch
            {
                throw;
            }
        }
    }

    [STNode("Flow_CustomNodes", "本地校正+实时 POI")]
    [FlowNodePropertyEditorAttribute(nameof(CalibTempName), typeof(FlowCalibrationTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POITempName), typeof(FlowPoiTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIFilterTempName), typeof(FlowPoiFilterTemplateEditor))]
    [FlowNodePropertyEditorAttribute(nameof(POIReviseTempName), typeof(FlowPoiReviseTemplateEditor))]
    public sealed class LocalCalibrationRealPoiNode : LocalCalibrationNodeBase
    {
        private static readonly string[] InputPortNames = { "IN_IMG", "IN_POI" };
        private string poiTempName = string.Empty;
        private string poiFilterTempName = string.Empty;
        private string poiReviseTempName = string.Empty;
        private ServicePoiPointTypes poiType;
        private float poiWidth = 10;
        private float poiHeight = 10;

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

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            ResolvePoiTemplates(action, out PoiParam poi, out PoiFilterParam? filter, out PoiReviseParam? revise, out int poiSourceMasterId);
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
                    result = LocalPoiCalculator.Calculate(frame, poi, filter, revise);
                }
                stopwatch.Stop();
                int poiTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                ViewResultAlgType resultType = LocalPoiCalculator.ResolveResultType(execution.Frame.Metadata.Channels);
                poiMasterId = LocalFlowResultPersistence.SaveAlgorithmResult(
                    action,
                    resultType,
                    poi.Id,
                    poi.Name,
                    execution.Frame.CvCieFilePath,
                    string.IsNullOrWhiteSpace(DeviceCode) ? execution.Frame.Metadata.DeviceCode : DeviceCode,
                    ZIndex,
                    poiTime,
                    new
                    {
                        CieMasterId = calibrationMasterId,
                        POISourceMasterId = poiSourceMasterId > 0 ? (int?)poiSourceMasterId : null,
                        CalibrationTemplate = execution.Calibration?.Name ?? execution.Frame.Metadata.CalibrationTemplate,
                        POITemplate = poi.Name,
                        POIFilterTemplate = filter?.Name,
                        POIReviseTemplate = revise?.Name,
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

        private void ResolvePoiTemplates(
            CVStartCFC action,
            out PoiParam poi,
            out PoiFilterParam? filter,
            out PoiReviseParam? revise,
            out int poiSourceMasterId)
        {
            poiSourceMasterId = -1;
            if (TryGetInputMasterResult(action, 1, out int inputMasterId, out int inputResultType, out _) && inputMasterId > 0)
            {
                if (inputResultType is (int)CVCommCore.CVResultType.Camera_Img
                    or (int)CVCommCore.CVResultType.Algorithm_Calibration)
                {
                    throw new InvalidOperationException($"IN_POI 接收到的是图像结果：MasterId={inputMasterId}，ResultType={inputResultType}。当前两条输入线可能接反；图像应连接 IN_IMG，关注点布点应连接 IN_POI。");
                }
                poiSourceMasterId = inputMasterId;
                poi = BuildPoiFromInput(inputMasterId, inputResultType);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(POITempName)) throw new InvalidOperationException("IN_POI 没有有效的布点结果，请选择备用 POI 模板。");
                poi = TemplatePoi.Params.FirstOrDefault(item => string.Equals(item.Key, POITempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 模板：{POITempName}");
            }
            ApplyPoiTypeOverride(poi);
            filter = string.IsNullOrWhiteSpace(POIFilterTempName)
                ? null
                : TemplatePoiFilterParam.Params.FirstOrDefault(item => string.Equals(item.Key, POIFilterTempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 过滤模板：{POIFilterTempName}");
            revise = string.IsNullOrWhiteSpace(POIReviseTempName)
                ? null
                : TemplatePoiReviseParam.Params.FirstOrDefault(item => string.Equals(item.Key, POIReviseTempName, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到 POI 修正模板：{POIReviseTempName}");
        }

        private PoiParam BuildPoiFromInput(int masterId, int masterResultType)
        {
            List<PoiPointResultModel> details = PoiPointResultDao.Instance.GetAllByPid(masterId);
            PoiParam poi = new() { Id = masterId, Name = $"IN_POI#{masterId}" };
            foreach (PoiPointResultModel detail in details)
            {
                poi.PoiPoints.Add(new PoiPoint
                {
                    Id = detail.PoiId ?? detail.Id,
                    Name = string.IsNullOrWhiteSpace(detail.PoiName) ? (detail.PoiId ?? detail.Id).ToString() : detail.PoiName,
                    PointType = ToGraphicType(ResolvePointType(detail.PoiType)),
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
                        PointType = ToGraphicType(ResolvePointType(pointInfo.HeaderInfo.PointType)),
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

        private void ApplyPoiTypeOverride(PoiParam poi)
        {
            if (POIType == ServicePoiPointTypes.None) return;
            if (POIType == ServicePoiPointTypes.SubPixel)
            {
                throw new NotSupportedException("本地实时 POI 暂不支持亚像素类型，请使用服务实时关注点算法。");
            }

            GraphicTypes graphicType = ToGraphicType(ToCorePointType(POIType));
            foreach (PoiPoint point in poi.PoiPoints)
            {
                point.PointType = graphicType;
                if (POIType is ServicePoiPointTypes.SolidPoint or ServicePoiPointTypes.SolidPoint_KB)
                {
                    point.PixWidth = 1;
                    point.PixHeight = 1;
                }
                else
                {
                    point.PixWidth = POIWidth;
                    point.PixHeight = POIHeight;
                }
            }
        }

        private POIPointTypes ResolvePointType(POIPointTypes sourceType)
            => sourceType == POIPointTypes.None && POIType != ServicePoiPointTypes.None ? ToCorePointType(POIType) : sourceType;

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

        private static float NormalizePoiSize(float value)
        {
            if (value <= 0) return 1;
            int size = checked((int)Math.Ceiling(value));
            return size % 2 == 0 ? size : size + 1;
        }

        private static GraphicTypes ToGraphicType(CVCommCore.CVAlgorithm.POIPointTypes pointType)
        {
            return pointType switch
            {
                CVCommCore.CVAlgorithm.POIPointTypes.SolidPoint_KB or CVCommCore.CVAlgorithm.POIPointTypes.SolidPoint => GraphicTypes.Point,
                CVCommCore.CVAlgorithm.POIPointTypes.Circle => GraphicTypes.Circle,
                CVCommCore.CVAlgorithm.POIPointTypes.Rect or CVCommCore.CVAlgorithm.POIPointTypes.LTRect => GraphicTypes.Rect,
                _ => throw new NotSupportedException($"本地实时 POI 暂不支持上游布点形状：{pointType}")
            };
        }
    }
}
