using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Linq;
using ColorVision.Engine.FlowProcessing.Diagnostics;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal interface ILocalLvCameraServices
{
    LocalCameraCaptureResult Capture(LocalCameraCaptureRequest request);
    MeasureResultImgModel Save(CVStartCFC action, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture);
    void Publish(CVStartCFC action, string nodeId, int zIndex, LocalCameraCaptureRequest request, LocalCameraCaptureResult capture, MeasureResultImgModel model);
}

internal sealed class LocalLvCameraServices : ILocalLvCameraServices
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

internal sealed class LocalLvCameraExecution : FlowLocalExecution
{
    private readonly DeviceCamera device;
    private readonly LocalCameraCaptureRequest captureRequest;
    private readonly ILocalLvCameraServices services;
    private readonly string serialNumber;
    private readonly string nodeId;
    private readonly int zIndex;
    private LocalCameraCaptureResult? capture;
    private bool frameTransferred;
    private bool disposed;
    private bool commandReleased;
    private readonly FlowNodeTiming timing = new();

    internal static FlowLocalExecution? Create(CVMQTTRequest request)
    {
        DeviceCamera? device = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
            .FirstOrDefault(camera => string.Equals(camera.Code, request.DeviceCode, StringComparison.Ordinal));
        return CreateForDevice(device, request, new LocalLvCameraServices());
    }

    internal static FlowLocalExecution? CreateForDevice(DeviceCamera? device, CVMQTTRequest request, ILocalLvCameraServices services)
    {
        if (device == null || !device.CameraBackend.OpensLocally) return null;
        // Reuse the current owner, or select the next-open preference when closed; keep this request on that backend.
        return new LocalLvCameraExecution(device, request, services);
    }

    private LocalLvCameraExecution(DeviceCamera device, CVMQTTRequest request, ILocalLvCameraServices services)
    {
        this.device = device;
        this.services = services;
        serialNumber = request.SerialNumber;
        nodeId = request.DeviceNodeCode;
        zIndex = request.ZIndex;
        if (request.EventName != "GetData" || request.Data is not CameraData parameters)
            throw new InvalidOperationException("本地 L/BV 相机请求格式无效。");
        captureRequest = BuildCaptureRequest(device, parameters);
        device.CameraBackend.BeginLocalCommand();
    }

    internal static LocalCameraCaptureRequest BuildCaptureRequest(DeviceCamera device, CameraData parameters)
    {
        if (parameters.ExpTime == null || parameters.ExpTime.Length != 1 || !float.IsFinite(parameters.ExpTime[0]) || parameters.ExpTime[0] <= 0)
            throw new InvalidOperationException("L/BV 相机曝光时间必须为一个大于 0 的有限值。");
        if (!float.IsFinite(parameters.Gain) || parameters.Gain < 0 || parameters.AvgCount < 1)
            throw new InvalidOperationException("增益必须为非负有限值，平均次数至少为 1。");
        LocalFrameMirrorService.ValidateFlipMode(parameters.FlipMode);
        string calibrationName = parameters.Calibration?.Name ?? string.Empty;
        CalibrationParam? calibration = string.IsNullOrWhiteSpace(calibrationName) ? null
            : device.PhyCamera?.CalibrationParams.FirstOrDefault(item => string.Equals(item.Key, calibrationName, StringComparison.Ordinal))?.Value
                ?? throw new InvalidOperationException($"找不到校正模板：{calibrationName}");
        CameraRunParam cameraParameters = new() { Gain = parameters.Gain, AvgCount = parameters.AvgCount };
        cameraParameters.SetAllExposure(parameters.ExpTime[0]);
        if (CalibrationGroupGainResolver.TryResolve(calibration, device.PhyCamera?.VisualChildren.OfType<GroupResource>() ?? Enumerable.Empty<GroupResource>(), out float calibrationGain, out _))
            cameraParameters.Gain = calibrationGain;
        // POI, Filter and Revise are deliberately not resolved or executed for local L/BV forwarding.
        return new LocalCameraCaptureRequest
        {
            Device = device, CameraParameters = cameraParameters, Calibration = calibration,
            FlipMode = parameters.FlipMode, IsAutoExposure = false,
            SaveFiles = device.DisplayConfig.SaveLocalCaptureFiles
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
        MeasureResultImgModel model = FlowNodeTiming.Run("PersistResult", () => services.Save(action, zIndex, captureRequest, capture));
        if (model.Id <= 0) throw new InvalidOperationException("保存本地相机结果记录失败。");
        LocalFlowFrame frame = capture.Frame;
        frame.MasterId = model.Id;
        action.SetCurrentFrame(frame);
        frameTransferred = true;
        action.MasterValue(null, model.Id, 100);
        FlowNodeTiming.Run("PublishResult", () => services.Publish(action, nodeId, zIndex, captureRequest, capture, model));
        return new
        {
            MasterId = model.Id, MasterResultType = 100, MasterValue = (string?)null,
            FrameId = frame.FrameId.ToString("N"), frame.HasRaw, frame.HasCie,
            frame.CvRawFilePath, frame.CvCieFilePath,
            TotalTime = capture.TotalTimeMs, CaptureTime = capture.CaptureTimeMs,
            CalibrationTime = capture.CalibrationTimeMs, SaveTime = capture.SaveTimeMs,
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
