using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Global;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal sealed class LocalSensorFlowExecution : FlowLocalExecution
{
    private readonly DeviceSensor device;
    private readonly string operation;
    private readonly JObject parameters;
    private readonly LocalSensorConnectionConfig config;
    private CancellationToken stop;
    private object? result;
    internal static bool Supports(CVBaseServerNode node) => node.NodeType == "Sensor"
        && node is CommonSensorNode or TempCommonSensorNode or RealCommonSensorNode or PhyDeviceControlNode;
    private static DeviceSensor? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceSensor>().FirstOrDefault(device => device.Code == code);

    internal static void Register() => RegisterBackend(nameof(LocalSensorFlowExecution),
        node => Supports(node) && Find(node.DeviceCode)?.SensorBackend.OpensLocally == true,
        (node, request) =>
        {
            if (!Supports(node)) return null!;
            var device = Find(request.DeviceCode);
            if (device == null) return null!;
            lock (device.SensorBackend.Sync)
            {
                if (!device.SensorBackend.OpensLocally)
                {
                    device.EnsureOtherSensorBackends(false, LocalSensorSession.EndpointKey(device.Config));
                    device.SensorBackend.EnsureServiceAvailable();
                    return null!;
                }
            }
            return new LocalSensorFlowExecution(device, request.EventName, request.Data);
        });

    internal LocalSensorFlowExecution(DeviceSensor device, string operation, object? parameters)
    {
        this.device = device; this.operation = operation;
        this.parameters = parameters == null ? new JObject() : JObject.FromObject(parameters);
        config = device.SnapshotLocalSensorConfig();
    }
    public override bool UseNodeTimeout => false;
    public override void Bind(CVStartCFC action) => stop = action.RuntimeResources.StopToken;
    public override void Execute()
    {
        try
        {
            stop.ThrowIfCancellationRequested();
            var commands = operation == "ExecCmd" ? device.ResolveLocalSensorCommands(parameters, config) : null;
            result = device.ExecuteLocalSensorAsync(operation, commands, config, stop).GetAwaiter().GetResult();
        }
        catch (Exception ex) { device.PublishLocalSensorResult(null, ex); throw; }
    }
    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃本地传感器结果。");
        action.Data["LocalSensor"] = result!;
        device.PublishLocalSensorResult(result);
        return result ?? new { Message = "ok" };
    }
    public override void Dispose() => result = null;
}
