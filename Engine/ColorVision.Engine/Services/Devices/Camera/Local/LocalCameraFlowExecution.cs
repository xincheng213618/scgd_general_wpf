using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Linq;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.FileIO;
using System.Diagnostics;
using cvColorVision;
using FlowEngineLib.Algorithm;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal interface ILocalCameraFlowServices
{
    LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request);
    MeasureResultImgModel Save(CVStartCFC action, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture);
    void Publish(CVStartCFC action, string nodeId, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture, MeasureResultImgModel model);
}

internal sealed class LocalCameraFlowServices : ILocalCameraFlowServices
{
    public LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request)
    {
        FlowNodeTiming.Run("ConnectCamera", () => request.Device.EnsureLocalMeasurementConnected(autoConnect: true));
        return LocalCameraCaptureService.Capture(request);
    }

    public MeasureResultImgModel Save(CVStartCFC action, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture)
        => LocalCameraResultService.SaveFlowModel(action, zIndex, capture.Frame, capture, request.CameraParameters, request.Calibration, request.IsAutoExposure);

    public void Publish(CVStartCFC action, string nodeId, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture, MeasureResultImgModel model)
    {
        request.Device.PublishLocalPreview(capture.Frame, model, forceDisplay: false);
        ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image, request.Device.Code, "GetData", action.SerialNumber, nodeId, zIndex, model.Id, 100);
    }
}

internal sealed class LocalCameraFlowExecution : FlowLocalExecution
{
    private readonly DeviceCamera device;
    private readonly LocalCameraCaptureRequest captureRequest;
    private readonly ILocalCameraFlowServices services;
    private readonly string serialNumber;
    private readonly string nodeId;
    private readonly int zIndex;
    private readonly CVFileSaveMode saveMode;
    private LocalCameraCaptureResult? capture;
    private bool frameTransferred;
    private bool disposed;
    private bool commandReleased;
    private readonly FlowNodeTiming timing = new();

    public override bool UseNodeTimeout => false;

    internal static FlowLocalExecution? Create(CVMQTTRequest request, CVFileSaveMode saveMode)
    {
        DeviceCamera? device = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
            .FirstOrDefault(camera => string.Equals(camera.Code, request.DeviceCode, StringComparison.Ordinal));
        return CreateForDevice(device, request, new LocalCameraFlowServices(), saveMode);
    }

    internal static FlowLocalExecution? CreateForDevice(DeviceCamera? device, CVMQTTRequest request, ILocalCameraFlowServices services, CVFileSaveMode saveMode)
    {
        if (device == null || !device.CameraBackend.OpensLocally) return null;
        // Reuse the current owner, or select the next-open preference when closed; keep this request on that backend.
        return new LocalCameraFlowExecution(device, request, services, saveMode);
    }

    private LocalCameraFlowExecution(DeviceCamera device, CVMQTTRequest request, ILocalCameraFlowServices services, CVFileSaveMode saveMode)
    {
        this.device = device;
        this.services = services;
        this.saveMode = saveMode;
        serialNumber = request.SerialNumber;
        nodeId = request.DeviceNodeCode;
        zIndex = request.ZIndex;
        if (request.EventName != "GetData" || request.Data is not CameraData parameters)
            throw new InvalidOperationException("本地相机请求格式无效。");
        captureRequest = BuildCaptureRequest(device, parameters);
        device.CameraBackend.BeginLocalCommand();
    }

    internal static LocalCameraCaptureRequest BuildCaptureRequest(DeviceCamera device, CameraData parameters)
    {
        bool isCv = parameters is CVCameraData;
        int exposureCount = isCv ? 3 : 1;
        if (parameters.ExpTime == null || parameters.ExpTime.Length != exposureCount || parameters.ExpTime.Any(value => !float.IsFinite(value) || value <= 0))
            throw new InvalidOperationException(isCv ? "CV 相机 R/G/B 曝光时间必须为三个大于 0 的有限值。" : "L/BV 相机曝光时间必须为一个大于 0 的有限值。");
        if (parameters is CVCameraData cv)
        {
            if (device.Config.CameraMode != CameraMode.CV_MODE || device.Config.Channel != ImageChannel.Three)
                throw new InvalidOperationException("本地 CV 相机节点需要配置为 CV 模式的三通道相机。");
            if (cv.CV2LVChannel != (int)CV2LVChannelMode.None)
                throw new NotSupportedException("本地 CV 相机节点暂不支持 CV2LV 单通道模式，请将 CV2LVChannel 设置为 None。");
        }
        if (!float.IsFinite(parameters.Gain) || parameters.Gain < 0 || parameters.AvgCount < 1)
            throw new InvalidOperationException("增益必须为非负有限值，平均次数至少为 1。");
        LocalFrameMirrorService.ValidateFlipMode(parameters.FlipMode);
        string calibrationName = parameters.Calibration?.Name ?? string.Empty;
        CalibrationParam? calibration = string.IsNullOrWhiteSpace(calibrationName) ? null
            : device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, calibrationName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{calibrationName}");
        CameraRunParam cameraParameters = new() { Gain = parameters.Gain, AvgCount = parameters.AvgCount };
        cameraParameters.SetAllExposure(parameters.ExpTime[0]);
        if (isCv)
        {
            cameraParameters.ExpTimeR = parameters.ExpTime[0];
            cameraParameters.ExpTimeG = parameters.ExpTime[1];
            cameraParameters.ExpTimeB = parameters.ExpTime[2];
        }
        if (CalibrationGroupGainResolver.TryResolve(calibration, device.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>(), out float calibrationGain, out _))
            cameraParameters.Gain = calibrationGain;
        // POI, Filter and Revise are deliberately not resolved or executed for local camera forwarding.
        return new LocalCameraCaptureRequest
        {
            Device = device, CameraParameters = cameraParameters, Calibration = calibration,
            FlipMode = parameters.FlipMode, IsAutoExposure = false,
            SaveFiles = false
        };
    }

    public override void Execute()
    {
        using var activation = timing.Activate();
        try { capture = services.Capture(captureRequest); }
        finally { ReleaseCommand(); }
    }

    private void ReleaseCommand()
    {
        if (commandReleased) return;
        commandReleased = true;
        device.CameraBackend.EndLocalCommand();
    }

    public override object Complete(CVStartCFC action)
    {
        using var activation = timing.Activate();
        if (capture == null) throw new InvalidOperationException("本地相机未返回取图结果。");
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
            throw new OperationCanceledException("流程已停止，本地取图结果不再交接。");
        if (!string.Equals(action.SerialNumber, serialNumber, StringComparison.Ordinal))
            throw new InvalidOperationException("本地取图结果与当前流程批次不匹配。");
        LocalFlowFrame frame = capture.Frame;
        frame.CvRawFilePath = LocalFrameFileService.CreateCapturePath(device.Config.FileServerCfg.DataBasePath, device.Code, frame.Metadata);
        MeasureResultImgModel model = FlowNodeTiming.Run("PersistResult", () => services.Save(action, zIndex, captureRequest, capture));
        if (model.Id <= 0) throw new InvalidOperationException("保存本地相机结果记录失败。");
        Stopwatch saveTimer = Stopwatch.StartNew();
        LocalFrameFileService.SaveCapture(frame, frame.CvRawFilePath, saveMode);
        int saveTime = checked((int)Math.Min(saveTimer.ElapsedMilliseconds, int.MaxValue));
        frame.MasterId = model.Id;
        action.SetCurrentFrame(frame);
        frameTransferred = true;
        action.MasterValue(null, model.Id, 100);
        FlowNodeTiming.Run("PublishResult", () => services.Publish(action, nodeId, zIndex, captureRequest, capture, model));
        using LocalFlowFrameLease diagnosticLease = frame.Acquire();
        return new
        {
            MasterId = model.Id, MasterResultType = 100, MasterValue = (string?)null,
            FrameId = frame.FrameId.ToString("N"), frame.HasRaw, frame.HasCie,
            Backend = "Local", DeviceCode = device.Code,
            frame.Metadata.Width, frame.Metadata.Height, frame.Metadata.SourceBpp, frame.Metadata.Channels,
            frame.Metadata.Exposure, frame.Metadata.Gain,
            AverageCount = captureRequest.CameraParameters?.AvgCount ?? 1,
            RawBytes = diagnosticLease.RawLength, CieBytes = diagnosticLease.CieLength,
            capture.CalibrationBackend,
            frame.CvRawFilePath, frame.CvCieFilePath,
            TotalTime = checked(capture.TotalTimeMs + saveTime), CaptureTime = capture.CaptureTimeMs,
            CalibrationTime = capture.CalibrationTimeMs, SaveTime = saveTime,
            SaveMode = saveMode.ToString(),
            Timing = timing.Finish()
        };
    }

    public override void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!frameTransferred) capture?.Frame.Dispose();
        ReleaseCommand();
    }
}
