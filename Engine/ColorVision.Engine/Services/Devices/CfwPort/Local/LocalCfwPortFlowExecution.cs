using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.CfwPort.Local;

internal sealed class LocalCfwPortFlowExecution : FlowLocalExecution
{
    private readonly DeviceCfwPort device;
    private readonly LocalCfwPortConnection config;
    private readonly int port;
    private CancellationToken stop;
    private int? actualPort;

    internal static bool Supports(CVBaseServerNode node) => node is FWNode && node.NodeType == "FilterWheel";
    private static DeviceCfwPort? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>().FirstOrDefault(item => item.Code == code);
    internal static void Register() => RegisterBackend(nameof(LocalCfwPortFlowExecution),
        node => Supports(node) && Find(node.DeviceCode)?.CfwBackend.OpensLocally == true,
        (node, request) => Supports(node) && Find(request.DeviceCode) is { } device && device.CfwBackend.OpensLocally
            ? new LocalCfwPortFlowExecution(device, request.EventName, request.Data) : null!);

    internal LocalCfwPortFlowExecution(DeviceCfwPort device, string operation, object? data)
    {
        if (operation != "SetPort") throw new NotSupportedException($"本地滤色轮流程不支持 {operation}。");
        this.device = device;
        config = LocalCfwPortConnection.From(device.Config);
        port = (data == null ? null : JObject.FromObject(data).Value<int?>("PortNum")) ?? throw new ArgumentException("滤色轮流程缺少孔位。");
    }
    public override void Bind(CVStartCFC action) => stop = action.RuntimeResources.StopToken;
    public override void Execute()
    {
        stop.ThrowIfCancellationRequested();
        actualPort = device.DService.ExecuteLocalAsync("SetPort", config, port, stop).GetAwaiter().GetResult();
    }
    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃滤色轮结果。");
        if (!actualPort.HasValue) throw new InvalidOperationException("滤色轮没有返回确认孔位。");
        device.UpdateLocalPort(actualPort.Value);
        return new JObject { ["Port"] = actualPort.Value };
    }
    public override void Dispose() { } // This is the shared device connection, also used by spectrum calibration.
}
