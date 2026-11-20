using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Calibration;
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
        public string ImageDeviceCode { get; init; } = string.Empty;
        public (DeviceCalibration Device, string Code)? CalibrationOwner { get; init; }

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
        private bool allowAcceleration;

        [Category("本地校正")]
        [STNodeProperty("校正模板", "用于处理 RAW 图像的相机校正模板", true)]
        [PropertyEditorType(typeof(CalibrationTemplatePropertiesEditor))]
        public string CalibTempName { get => calibTempName; set { calibTempName = value ?? string.Empty; OnPropertyChanged(); } }

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

        private protected LocalCalibrationExecution ExecuteCalibration(CVStartCFC action, bool useInputColor)
            => ExecuteCalibrationCore(action, useInputColor, LocalFlowResultPersistence.ResolveEndpoint(action));

        private protected LocalCalibrationExecution ExecuteCalibrationCore(CVStartCFC action, bool useInputColor,
            MySqlConnectedEndpoint? endpoint)
        {
            LocalFlowFrame sourceFrame;
            bool ownsSourceFrame;
            bool loadedFromFile;
            if (action.TryGetCurrentFrame(out LocalFlowFrame? currentFrame) && currentFrame != null)
            {
                ValidateImageInputPath(currentFrame.CvCieFilePath);
                FlowNodeTiming.Skip("OpenImage");
                sourceFrame = currentFrame;
                ownsSourceFrame = false;
                loadedFromFile = false;
            }
            else
            {
                string sourceFilePath = ResolveInputImageFilePath(action, 0, endpoint);
                if (string.IsNullOrWhiteSpace(sourceFilePath))
                {
                    throw new InvalidOperationException("输入端没有本地图像内存帧，也没有可读取的图像结果。请连接相机取图或“加载图片”节点。");
                }
                ValidateImageInputPath(sourceFilePath);
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
                string imageDeviceCode = ResolveDeviceCode(sourceFrame.Metadata.DeviceCode);
                var camera = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
                    .FirstOrDefault(device => string.Equals(device.Code, imageDeviceCode, StringComparison.Ordinal));
                var calibrationDevice = camera == null ? LocalCalibrationFlowRouting.Find(imageDeviceCode) : camera.PhyCamera?.DeviceCalibration;
                var calibrationOwner = action.PersistResults ? CaptureCalibrationOwner(calibrationDevice, endpoint) : null;
                bool canReuseExistingCie = sourceFrame.HasCie
                    && (!sourceFrame.HasRaw
                        || string.IsNullOrWhiteSpace(CalibTempName)
                        || string.Equals(sourceFrame.Metadata.CalibrationTemplate, CalibTempName, StringComparison.Ordinal));
                bool canReuseCalibratedRaw = !sourceFrame.HasCie && sourceFrame.HasRaw
                    && sourceFrame.Metadata.IsMirrorReady && sourceFrame.ColorCalibration?.CanReplay == true
                    && (string.IsNullOrWhiteSpace(CalibTempName)
                        || string.Equals(sourceFrame.Metadata.CalibrationTemplate, CalibTempName, StringComparison.Ordinal));
                if (useInputColor || canReuseExistingCie || canReuseCalibratedRaw)
                {
                    if (useInputColor)
                    {
                        if (!sourceFrame.HasCie && (!sourceFrame.HasRaw || sourceFrame.ColorCalibration?.CanReplay != true))
                            throw new InvalidOperationException("上游图像没有可用的 CIE 或可回放的 RAW 色度参数。请在取图节点配置亮度或颜色校正，或在本节点选择校正模板。");
                        if (loadedFromFile && TryGetInputMasterResult(action, 0, out int imageMasterId, out _, out _))
                            sourceFrame.MasterId = imageMasterId;
                    }
                    else if (sourceFrame.HasRaw && (AllowAcceleration || !sourceFrame.HasCie))
                    {
                        LocalFrameCalibrationService.ReuseColorCalibration(sourceFrame, AllowAcceleration);
                    }
                    outputFrame = sourceFrame;
                    ownsOutputFrame = ownsSourceFrame;
                    ownsSourceFrame = false;
                    if (!outputFrame.IsFlipApplied)
                    {
                        throw new InvalidOperationException("The reusable frame has a pending mirror operation and was published before its orientation was finalized.");
                    }
                }
                else if (sourceFrame.HasRaw)
                {
                    DeviceCamera device = camera ?? calibrationDevice?.PhyCamera?.DeviceCamera
                        ?? throw new InvalidOperationException(string.IsNullOrWhiteSpace(imageDeviceCode)
                            ? "校正 RAW 内存前必须选择本地相机设备。" : $"找不到本地相机设备：{imageDeviceCode}");
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
                        LocalCalibrationRoi.ResolveForFrame(sourceFrame.Metadata, device.PhyCamera?.Config?.CameraCfg),
                        ResolveZeroExposureFallback(action, sourceFrame, endpoint),
                        AllowAcceleration);
                    outputFrame = sourceFrame;
                    ownsOutputFrame = ownsSourceFrame;
                    ownsSourceFrame = false;
                    calibrated = true;
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
                    checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)))
                {
                    ImageDeviceCode = imageDeviceCode,
                    CalibrationOwner = calibrationOwner
                };
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

        private protected MeasureResultImgModel SaveCalibrationResult(CVStartCFC action, LocalCalibrationExecution execution, MySqlConnectedEndpoint endpoint)
        {
            LocalFlowFrame frame = execution.Frame;
            int batchId = FlowNodeTiming.Run("ResolveBatch", () => LocalFlowResultPersistence.ResolveBatchId(action, endpoint));
            string? rawFilePath = NullIfEmpty(frame.CvRawFilePath);
            string? cieFilePath = NullIfEmpty(frame.CvCieFilePath);
            string? outputFilePath = cieFilePath ?? rawFilePath;
            CameraFileType? outputFileType = cieFilePath != null
                ? CameraFileType.CIEFile
                : rawFilePath != null ? CameraFileType.RawFile : null;

            MeasureResultImgModel model = new()
            {
                BatchId = batchId,
                ZIndex = ZIndex,
                NDPort = -1,
                Params = JsonConvert.SerializeObject(new
                {
                    SourceFrameId = execution.SourceFrameId,
                    SourceFile = NullIfEmpty(frame.Metadata.SourceFilePath),
                    execution.LoadedFromFile,
                    execution.Calibrated,
                    CalibrationDeviceCode = execution.CalibrationOwner?.Code,
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
                DeviceCode = execution.ImageDeviceCode,
                CreateDate = DateTime.Now
            };
            action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
            int masterId = FlowNodeTiming.Run("PersistResult", () => MeasureImgResultDao.InsertResult(endpoint, model));
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
                AllowAcceleration,
                InputMode = "CurrentFrameThenInputFile"
            });
        }

        private protected virtual void ValidateImageInputPath(string filePath) { }

        private protected virtual float[]? ResolveZeroExposureFallback(CVStartCFC action, LocalFlowFrame sourceFrame, MySqlConnectedEndpoint? endpoint) => null;

        private CalibrationParam ResolveCalibration(DeviceCamera device)
        {
            if (string.IsNullOrWhiteSpace(CalibTempName)) throw new InvalidOperationException("请选择校正模板。");
            return device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, CalibTempName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{CalibTempName}");
        }

        private string ResolveDeviceCode(string frameDeviceCode)
            => string.IsNullOrWhiteSpace(DeviceCode) ? frameDeviceCode : DeviceCode;

        private static (DeviceCalibration Device, string Code)? CaptureCalibrationOwner(DeviceCalibration? calibration, MySqlConnectedEndpoint? endpoint)
        {
            return endpoint?.IsCurrent == true && calibration is { IsDisposed: false } && ReferenceEquals(calibration.ResultDatabaseEndpoint, endpoint)
                && !string.IsNullOrWhiteSpace(calibration.Code) ? (calibration, calibration.Code) : null;
        }

        private protected void PublishCalibrationResult(CVStartCFC action, LocalCalibrationExecution execution, int masterId,
            string nodeId, MySqlConnectedEndpoint? endpoint)
        {
            var owner = execution.CalibrationOwner;
            ResultMessageBus.Default.PublishPersisted(owner.HasValue ? ResultRoutes.Calibration : ResultRoutes.Camera, ResultKinds.Image,
                owner?.Code ?? execution.ImageDeviceCode, OperatorCode, action.SerialNumber, nodeId, ZIndex, masterId,
                (int)ViewResultAlgType.Calibration, databaseEndpoint: endpoint, sourceDevice: owner?.Device);
        }

        protected static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    }

    [STNode("Flow_LocalDevices", "校正", CategoryOrder = 9900, NodeOrder = 30)]
    public sealed class LocalCalibrationNode : LocalCalibrationNodeBase
    {
        public LocalCalibrationNode() : base("校正", "LocalCalibration", "Calibration")
        {
        }

        protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
        {
            MySqlConnectedEndpoint? endpoint = LocalFlowResultPersistence.ResolveEndpoint(action);
            using LocalCalibrationExecution execution = ExecuteCalibrationCore(action, useInputColor: false, endpoint);
            action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
            MeasureResultImgModel? persistedResult = action.PersistResults
                ? SaveCalibrationResult(action, execution, endpoint ?? throw new InvalidOperationException("保存本地校正流程结果需要连接 MySQL。"))
                : null;
            int masterId = persistedResult?.Id ?? 0;
            execution.Frame.MasterId = masterId;
            action.MasterValue(null, masterId, (int)ViewResultAlgType.Calibration);
            execution.TransferFrameTo(action);
            if (persistedResult != null)
            {
                FlowNodeTiming.Run("PublishResult", () => PublishCalibrationResult(action, execution, masterId, ExecutionNodeId, endpoint));
            }
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
