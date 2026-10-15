using ColorVision.Engine.Services.Devices.Camera.Templates.AutoFocus;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Base;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed class LocalCameraMotorExecution : FlowLocalExecution
{
    private readonly DeviceCamera device;
    private readonly Func<CancellationToken, object?> operation;
    private readonly string serialNumber;
    private CancellationToken stop;
    private object? result;
    private bool commandReleased;

    public override bool UseNodeTimeout => false;

    private static DeviceCamera? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>()
        .FirstOrDefault(camera => string.Equals(camera.Code, code, StringComparison.Ordinal));

    internal static bool UsesLocalCamera(string deviceCode) => Find(deviceCode)?.CameraBackend.OpensLocally == true;

    internal static FlowLocalExecution? Create(CVMQTTRequest request) => CreateForDevice(Find(request.DeviceCode), request);

    internal static FlowLocalExecution? CreateForDevice(DeviceCamera? device, CVMQTTRequest request)
        => device?.CameraBackend.OpensLocally == true ? new LocalCameraMotorExecution(device, request) : null;

    private LocalCameraMotorExecution(DeviceCamera device, CVMQTTRequest request)
    {
        this.device = device;
        serialNumber = request.SerialNumber;
        switch (request.EventName)
        {
            case "Move" when request.Data is FocusPosData move:
                int position = move.nPosition;
                bool absolute = move.bAbs;
                operation = _ => device.ExecuteLocalMotor(motor => motor.Move(position, absolute));
                break;
            case "MoveDiaphragm" when request.Data is FocusApertureData aperture:
                float value = aperture.dPosition;
                operation = _ => device.ExecuteLocalMotor(motor => { motor.Aperture(value); return null; });
                break;
            case "AutoFocus" when request.Data is AutoFocusData focus:
                string? name = focus.AutoFocusTemplate?.Name;
                AutoFocusParam template = TemplateAutoFocus.Params.FirstOrDefault(item => !string.IsNullOrWhiteSpace(name)
                    && string.Equals(item.Key, name, StringComparison.Ordinal))?.Value
                    ?? throw new InvalidOperationException($"找不到自动对焦模板：{name}");
                LocalAutoFocusSettings settings = LocalAutoFocusSettings.FromTemplate(template, device.PhyCamera?.Config.MotorConfig ?? device.Config.MotorConfig);
                float exposure = (float)(device.Config.IsExpThree ? device.DisplayConfig.ExpTimeR : device.DisplayConfig.ExpTime);
                float gain = device.DisplayConfig.Gain;
                operation = token => device.ExecuteLocalAutoFocus(settings, exposure, gain, token);
                break;
            default:
                throw new InvalidOperationException("本地相机电机请求格式无效。");
        }
        device.CameraBackend.BeginLocalCommand();
    }

    public override void Bind(CVStartCFC action) => stop = action.RuntimeResources.StopToken;

    public override void Execute()
    {
        try
        {
            stop.ThrowIfCancellationRequested();
            if (device.LocalCameraSession.IsOpen && device.LocalCameraSession.OpenedMode == TakeImageMode.Live)
                throw new InvalidOperationException("请先停止本地预览再操作电机。");
            device.EnsureLocalMeasurementConnected(autoConnect: true);
            stop.ThrowIfCancellationRequested();
            result = operation(stop) ?? new { Message = "ok" };
            stop.ThrowIfCancellationRequested();
        }
        finally { ReleaseCommand(); }
    }

    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
            throw new OperationCanceledException("流程已停止，本地电机结果不再交接。");
        if (!string.Equals(action.SerialNumber, serialNumber, StringComparison.Ordinal))
            throw new InvalidOperationException("本地电机结果与当前流程批次不匹配。");
        return result ?? throw new InvalidOperationException("本地相机电机操作尚未完成。");
    }

    private void ReleaseCommand()
    {
        if (commandReleased) return;
        commandReleased = true;
        device.CameraBackend.EndLocalCommand();
    }

    public override void Dispose() => ReleaseCommand();
}
