using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal interface ILocalSpectrumFilterWheelOperation : IDisposable
{
    void SetOpen(bool open);
}

// Borrow the bound device's existing channel; never open a second serial connection or close a shared wheel.
internal sealed class LocalSpectrumFilterWheel : ILocalSpectrumFilterWheelOperation
{
    private readonly DeviceCfwPort device;
    private readonly IDisposable reservation;
    private readonly int measurementPort;
    private readonly int darkPort;

    internal LocalSpectrumFilterWheel(ShutterConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.FilterWheelDeviceCode)) throw new InvalidOperationException("请选择用于校零的滤色轮设备。");
        device = ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>().FirstOrDefault(item => item.Code == config.FilterWheelDeviceCode)
            ?? throw new InvalidOperationException($"找不到校零滤色轮：{config.FilterWheelDeviceCode}。");
        measurementPort = config.MeasurementPort;
        darkPort = config.DarkPort;
        if (measurementPort < 0 || darkPort < 0 || measurementPort == darkPort)
            throw new InvalidOperationException("滤色轮测量位和校零位必须是不同的非负孔位索引。");
        var mapping = device.FilterWheelConfig.HoleMapping;
        if (mapping.Count(item => item.HoleIndex == measurementPort) != 1 || mapping.Count(item => item.HoleIndex == darkPort) != 1)
            throw new InvalidOperationException("测量位或校零位不在绑定滤色轮的唯一孔位映射中，请检查配置。");
        reservation = device.DService.ReserveForCalibration();
        try
        {
            if (device.DService.DeviceStatus == DeviceStatusType.Closed) WaitForReply(device.DService.Open(), "打开滤色轮");
            else if (device.DService.DeviceStatus != DeviceStatusType.Opened) throw new InvalidOperationException("校零滤色轮尚未连接或不可用。");
        }
        catch { reservation.Dispose(); throw; }
    }

    public void SetOpen(bool open)
    {
        int port = open ? measurementPort : darkPort;
        WaitForReply(device.DService.SetPort(port), $"设置滤色轮位置 {port}");
        MsgRecord read = device.DService.GetPort();
        WaitForReply(read, "读取滤色轮位置");
        object? payload = read.MsgReturn?.Data;
        JObject? data = payload == null ? null : payload as JObject ?? JObject.FromObject(payload);
        if (data?["Port"] is not JValue { Type: JTokenType.Integer } actual || actual.Value<long>() != port)
            throw new InvalidOperationException($"滤色轮未确认到达位置 {port}，停止本次操作。");
    }

    internal static void WaitForReply(MsgRecord record, string operation)
    {
        var completion = new TaskCompletionSource<MsgRecordState>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Complete(object? sender, MsgRecordState state)
        {
            if (state is MsgRecordState.Success or MsgRecordState.Fail or MsgRecordState.Timeout) completion.TrySetResult(state);
        }
        record.MsgRecordStateChanged += Complete;
        try
        {
            Complete(record, record.MsgRecordState);
            if (record is LocalCfwPortRecord) completion.Task.GetAwaiter().GetResult();
            else if (!completion.Task.Wait(TimeSpan.FromSeconds(31))) throw new TimeoutException($"{operation}等待回执超时。");
            if (completion.Task.Result != MsgRecordState.Success)
                throw new InvalidOperationException($"{operation}失败：{record.MsgReturn?.Message ?? completion.Task.Result.ToString()}");
        }
        finally { record.MsgRecordStateChanged -= Complete; }
    }

    public void Dispose() => reservation.Dispose();
}
