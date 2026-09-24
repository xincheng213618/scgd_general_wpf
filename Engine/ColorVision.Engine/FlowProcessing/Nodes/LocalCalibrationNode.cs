using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Database;
using FlowEngineLib.Base;
using MQTTMessageLib.Camera;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    internal class LocalCalibrationNodeResultData
    {
        public string FrameId { get; init; } = string.Empty;
        public int MasterId { get; init; }
        public int MasterResultType { get; init; } = (int)ViewResultAlgType.Calibration;
        public int TotalTime { get; init; }
        public string FlipMode { get; init; } = "None";
        public bool FlipApplied { get; init; }
        public bool Calibrated { get; init; }
        public bool HasRaw { get; init; }
        public bool HasCie { get; init; }
        public string? CvRawFilePath { get; init; }
        public bool HasCieFile { get; init; }
        public string? CvCieFilePath { get; init; }
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

    public abstract class LocalCalibrationNodeBase : LocalDeviceFlowNodeBase
    {
        private string calibTempName = string.Empty;
        private bool saveFiles;
        private bool allowAcceleration;

        [Category("本地校正")]
        [STNodeProperty("校正模板", "对 RAW 指针执行的相机校正模板；CVCIE 输入会直接透传", true)]
        [PropertyEditorType(typeof(CalibrationTemplatePropertiesEditor))]
        public string CalibTempName { get => calibTempName; set { calibTempName = value ?? string.Empty; OnPropertyChanged(); } }

        [Category("本地校正")]
        [PropertyVisibility(nameof(AllowAcceleration), true)]
        [STNodeProperty("保存 CIE 文件", "默认关闭；保存完整 CVCIE 文件。色度参数仍默认写入已有 CVRAW，不受此选项影响；加速模式下不保存 CIE。", true)]
        public bool SaveFiles { get => saveFiles; set { saveFiles = value; OnPropertyChanged(); } }

        [Category("本地校正")]
        [STNodeProperty("允许加速", "开启后保留 RAW 和色度校正参数，不生成整幅 CIE 内存。本地 POI 按关注点区域计算；需要完整 CIE 的下游应关闭此项。", true)]
        public bool AllowAcceleration { get => allowAcceleration; set { allowAcceleration = value; OnPropertyChanged(); } }

        [JsonIgnore]
        [CommandDisplay("校正缓存", Order = -100)]
        [Description("查看已缓存的校正文件、内存占用，并可释放本机校正缓存")]
        public RelayCommand OpenLocalCalibrationCacheManagerCommand { get; }

        protected LocalCalibrationNodeBase(string title, string nodeType, string operatorName, params string[] inputNames)
            : base(title, nodeType, operatorName, inputNames)
        {
            OpenLocalCalibrationCacheManagerCommand = new RelayCommand(_ => LocalCalibrationCacheManagerWindow.OpenWindow());
            SelectFirstAvailableDevice<DeviceCamera>();
        }

        protected override string GetCompactSummaryValue() => CompactValueOrDash(CalibTempName);

        private protected LocalCalibrationExecution ExecuteCalibration(CVStartCFC action)
        {
            LocalFlowFrame sourceFrame;
            bool ownsSourceFrame;
            bool loadedFromFile;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
            {
                FlowNodeTiming.Skip("OpenImage");
                sourceFrame = currentFrame;
                ownsSourceFrame = false;
                loadedFromFile = false;
            }
            else
            {
                string sourceFilePath = ResolveInputImageFilePath(action, 0, SourceImageFilePath);
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    throw new InvalidOperationException("输入端没有本地图像内存帧，也没有可读取的图像结果。请连接相机取图或图像节点。");
                }
                sourceFrame = FlowNodeTiming.Run("OpenImage", () => LocalFrameFileService.Load(sourceFilePath));
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
                bool canReuseExistingCie = sourceFrame.HasCie
                    && (!sourceFrame.HasRaw
                        || string.IsNullOrWhiteSpace(CalibTempName)
                        || string.Equals(sourceFrame.Metadata.CalibrationTemplate, CalibTempName, StringComparison.Ordinal));
                bool canReuseCalibratedRaw = !sourceFrame.HasCie && sourceFrame.HasRaw
                    && sourceFrame.Metadata.IsMirrorReady && sourceFrame.ColorCalibration?.CanReplay == true
                    && (string.IsNullOrWhiteSpace(CalibTempName)
                        || string.Equals(sourceFrame.Metadata.CalibrationTemplate, CalibTempName, StringComparison.Ordinal));
                if (canReuseExistingCie || canReuseCalibratedRaw)
                {
                    if (AllowAcceleration || !sourceFrame.HasCie)
                        LocalFrameCalibrationService.ReuseColorCalibration(sourceFrame, AllowAcceleration);
                    outputFrame = sourceFrame;
                    ownsOutputFrame = ownsSourceFrame;
                    ownsSourceFrame = false;
                    if (!outputFrame.IsFlipApplied)
                    {
                        throw new InvalidOperationException("The reusable CIE frame has a pending mirror operation and was published before its orientation was finalized.");
                    }
                    if (SaveFiles && !AllowAcceleration && string.IsNullOrWhiteSpace(outputFrame.CvCieFilePath))
                    {
                        DeviceCamera device = ResolveDevice(sourceFrame.Metadata.DeviceCode);
                        LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code, includeRaw: false);
                    }
                }
                else if (sourceFrame.HasRaw)
                {
                    DeviceCamera device = ResolveDevice(sourceFrame.Metadata.DeviceCode);
                    calibration = ResolveCalibration(device);
                    if (!device.TryGetCalibrationTemplateFiles(calibration, out IReadOnlyList<DeviceCameraCalibrationFile> calibrationFiles, out string? errorMessage))
                    {
                        throw new InvalidOperationException(errorMessage ?? "校正模板无效。");
                    }
                    LocalFrameCalibrationService.CalibrateInPlace(
                        sourceFrame,
                        device.LocalCalibrationCacheManager,
                        calibrationFiles,
                        calibration.Name,
                        LocalCalibrationRoi.Resolve(device.PhyCamera?.Config?.CameraCfg, sourceFrame.Metadata.Width, sourceFrame.Metadata.Height),
                        ResolveZeroExposureFallback(action, sourceFrame),
                        AllowAcceleration);
                    outputFrame = sourceFrame;
                    ownsOutputFrame = ownsSourceFrame;
                    ownsSourceFrame = false;
                    calibrated = true;
                    if (SaveFiles && !AllowAcceleration && outputFrame.HasCie)
                    {
                        LocalFrameFileService.SaveCapture(outputFrame, device.Config.FileServerCfg.DataBasePath, device.Code, includeRaw: false);
                    }
                }
                else
                {
                    throw new InvalidOperationException("当前本地帧既没有 RAW 内存，也没有 CIE 内存。");
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

        private protected MeasureResultImgModel SaveCalibrationResult(CVStartCFC action, LocalCalibrationExecution execution)
        {
            LocalFlowFrame frame = execution.Frame;
            MeasureBatchModel batch = FlowNodeTiming.Run("ResolveBatch", () => BatchResultMasterDao.Instance.GetByNameOrCode(action.SerialNumber))
                ?? throw new InvalidOperationException($"找不到流程批次：{action.SerialNumber}");
            string? rawFilePath = NullIfEmpty(frame.CvRawFilePath);
            string? cieFilePath = NullIfEmpty(frame.CvCieFilePath);
            string? outputFilePath = cieFilePath ?? rawFilePath;
            CameraFileType? outputFileType = cieFilePath != null
                ? CameraFileType.CIEFile
                : rawFilePath != null ? CameraFileType.RawFile : null;
            if (SaveFiles && !AllowAcceleration && frame.HasCie && cieFilePath == null)
            {
                throw new InvalidOperationException("已启用“保存 CIE 文件”，但本地校正没有生成 CVCIE 文件。");
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
                    MemoryOnly = outputFilePath == null,
                    OutputBuffer = frame.HasCie ? "CIE" : "RAW",
                    AllowAcceleration,
                    frame.Metadata.Width,
                    frame.Metadata.Height,
                    frame.Metadata.SourceBpp,
                    frame.Metadata.CieBpp,
                    frame.Metadata.Channels,
                    frame.Metadata.Gain,
                    Exposure = frame.Metadata.Exposure,
                    FlipMode = frame.Metadata.FlipMode.ToString(),
                    FlipApplied = frame.IsFlipApplied,
                    Calibration = new { ID = execution.Calibration?.Id ?? -1, Name = execution.Calibration?.Name ?? frame.Metadata.CalibrationTemplate }
                }),
                RawFile = outputFilePath == null ? null : Path.GetFileName(outputFilePath),
                FileUrl = outputFilePath,
                FileType = outputFileType.HasValue ? (sbyte)outputFileType.Value : null,
                ImgFrameInfo = JsonConvert.SerializeObject(new
                {
                    bpp = frame.HasCie ? frame.Metadata.CieBpp : frame.Metadata.SourceBpp,
                    width = frame.Metadata.Width,
                    height = frame.Metadata.Height,
                    channels = frame.Metadata.Channels,
                    flipMode = frame.Metadata.FlipMode.ToString(),
                    flipApplied = frame.IsFlipApplied
                }),
                ResultCode = 0,
                Result = "ok",
                TotalTime = execution.TotalTime,
                DeviceCode = ResolveDeviceCode(frame.Metadata.DeviceCode),
                CreateDate = DateTime.Now
            };
            int masterId = FlowNodeTiming.Run("PersistResult", () => MeasureImgResultDao.Instance.SaveAndReturnId(model));
            if (masterId <= 0) throw new InvalidOperationException("保存本地校正图像结果失败。");
            model.Id = masterId;
            return model;
        }

        protected override string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new
            {
                ServiceName = NodeName,
                DeviceCode,
                EventName = OperatorCode,
                action.SerialNumber,
                CalibTempName,
                SaveFiles,
                AllowAcceleration,
                InputMode = "CurrentFrameThenInputFile"
            });
        }

        private protected virtual string SourceImageFilePath => string.Empty;

        private protected virtual float[]? ResolveZeroExposureFallback(CVStartCFC action, LocalFlowFrame sourceFrame) => null;

        private protected DeviceCamera ResolveDevice(string frameDeviceCode)
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

        private protected (string Route, string DeviceCode) ResolveCalibrationResultTarget(string cameraDeviceCode)
        {
            DeviceCamera? camera = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
                .FirstOrDefault(item => string.Equals(item.Code, cameraDeviceCode, StringComparison.Ordinal));
            string? calibrationDeviceCode = camera?.PhyCamera?.DeviceCalibration?.Code;
            return string.IsNullOrWhiteSpace(calibrationDeviceCode)
                ? (ResultRoutes.Camera, cameraDeviceCode)
                : (ResultRoutes.Calibration, calibrationDeviceCode);
        }

        protected static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [STNode("Flow_CustomNodes", "校正")]
    public sealed class LocalCalibrationNode : LocalCalibrationNodeBase
    {
        public LocalCalibrationNode() : base("校正", "LocalCalibration", "Calibration")
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            using LocalCalibrationExecution execution = ExecuteCalibration(action);
            MeasureResultImgModel persistedResult = SaveCalibrationResult(action, execution);
            int masterId = persistedResult.Id;
            execution.Frame.MasterId = masterId;
            action.MasterValue(null, masterId, (int)ViewResultAlgType.Calibration);
            execution.TransferFrameTo(action);
            (string route, string deviceCode) = ResolveCalibrationResultTarget(persistedResult.DeviceCode ?? string.Empty);
            FlowNodeTiming.Run("PublishResult", () => ResultMessageBus.Default.PublishPersisted(route, ResultKinds.Image, deviceCode, OperatorCode, action.SerialNumber, NodeID, ZIndex, masterId, (int)ViewResultAlgType.Calibration));
            return new LocalNodeExecutionResult
            {
                Data = new LocalCalibrationNodeResultData
                {
                    FrameId = execution.Frame.FrameId.ToString("N"),
                    MasterId = masterId,
                    TotalTime = execution.TotalTime,
                    FlipMode = execution.Frame.Metadata.FlipMode.ToString(),
                    FlipApplied = execution.Frame.IsFlipApplied,
                    Calibrated = execution.Calibrated,
                    HasRaw = execution.Frame.HasRaw,
                    HasCie = execution.Frame.HasCie,
                    CvRawFilePath = NullIfEmpty(execution.Frame.CvRawFilePath),
                    HasCieFile = !string.IsNullOrWhiteSpace(execution.Frame.CvCieFilePath),
                    CvCieFilePath = NullIfEmpty(execution.Frame.CvCieFilePath)
                }
            };
        }
    }

}
