using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal interface ILocalSpectrumFilterWheelOperation : IDisposable
{
    void SetTransmitting(bool transmitting);
    void RestoreTransmitting() => SetTransmitting(true);
}

// Borrow the bound device's existing channel; never open a second serial connection or close a shared wheel.
internal sealed class LocalSpectrumFilterWheel : ILocalSpectrumFilterWheelOperation
{
    private readonly DeviceCfwPort device;
    private readonly MQTTCfwPort service;
    private readonly LocalSpectrumOpticalScope? scope;
    private readonly LocalSpectrumWheelBinding? binding;
    private readonly IDisposable reservation;
    private readonly int measurementPort;
    private readonly int darkPort;

    internal LocalSpectrumFilterWheel(ShutterConfig config, LocalSpectrumOpticalScope? scope = null)
    {
        this.scope = scope;
        scope?.RequireShutterCurrent();
        binding = scope?.Shutter;
        if (scope != null && binding == null) throw new InvalidOperationException("未捕获校零滤色轮，请重新选择设备。");
        device = binding?.Device ?? LocalSpectrumOpticalScope.FindWheel(ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>() ?? [], config.FilterWheelDeviceCode);
        service = binding?.Service ?? device.DService;
        measurementPort = config.MeasurementPort;
        darkPort = config.DarkPort;
        if (measurementPort < 0 || darkPort < 0 || measurementPort == darkPort)
            throw new InvalidOperationException("滤色轮测量位和校零位必须是不同的非负孔位索引。");
        var mapping = binding?.Holes.Select(item => item.Index) ?? device.FilterWheelConfig.HoleMapping.Select(item => item.HoleIndex);
        int[] holes = mapping.ToArray();
        if (holes.Count(index => index == measurementPort) != 1 || holes.Count(index => index == darkPort) != 1)
            throw new InvalidOperationException("测量位或校零位不在绑定滤色轮的唯一孔位映射中，请检查配置。");
        reservation = service.ReserveForCalibration();
        try
        {
            if (service.DeviceStatus == DeviceStatusType.Closed)
                Execute("Open", null, "打开滤色轮", restore: false);
            else if (service.DeviceStatus != DeviceStatusType.Opened) throw new InvalidOperationException("校零滤色轮尚未连接或不可用。");
        }
        catch { reservation.Dispose(); throw; }
    }

    public void SetTransmitting(bool transmitting) => Move(transmitting ? measurementPort : darkPort, restore: false);

    public void RestoreTransmitting() => Move(measurementPort, restore: true);

    private void Move(int port, bool restore)
    {
        Execute("SetPort", port, $"设置滤色轮位置 {port}", restore);
        if (Execute("GetPort", null, "读取滤色轮位置", restore) != port)
            throw new InvalidOperationException($"滤色轮未确认到达位置 {port}，停止本次操作。");
        if (scope?.IsShutterCurrent != false)
            device.UpdateLocalPort(port, scope == null ? null : () => scope.IsShutterCurrent);
    }

    private int? Execute(string command, int? port, string description, bool restore)
    {
        if (!restore) scope?.RequireShutterCurrent();
        if (binding?.UsesLocal == true)
            return binding.ExecuteLocal(command, port, autoConnect: command == "Open");

        MsgRecord record = binding?.RemoteRoute is { } route
            ? service.SendCalibrationCommand(route, command, port)
            : service.SendCalibrationCommand(command, port);
        WaitForReply(record, description);
        if (command != "GetPort") return null;
        object? payload = record.MsgReturn?.Data;
        JObject? data = payload == null ? null : payload as JObject ?? JObject.FromObject(payload);
        return data?["Port"] is JValue { Type: JTokenType.Integer } actual && actual.Value<long>() is >= 0 and <= int.MaxValue
            ? actual.Value<int>() : null;
    }

    internal static void WaitForReply(MsgRecord record, string operation)
    {
        MsgRecordState state = record.MsgRecordState;
        try
        {
            if (record.WorkerCompletion is { } completion)
                state = record is LocalCfwPortRecord
                    ? completion.GetAwaiter().GetResult()
                    : completion.WaitAsync(TimeSpan.FromSeconds(31)).GetAwaiter().GetResult();
        }
        catch (TimeoutException error)
        {
            throw new TimeoutException($"{operation}等待回执超时。", error);
        }
        if (state != MsgRecordState.Success)
            throw new InvalidOperationException($"{operation}失败：{record.MsgReturn?.Message ?? state.ToString()}");
    }

    public void Dispose() => reservation.Dispose();
}
