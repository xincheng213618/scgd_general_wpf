using ColorVision.Engine.Services.Devices.Sensor.Local;
using FlowEngineLib.Base;
using FlowEngineLib.Node.PG;
using FlowEngineLib.PG;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.PG.Local;

internal sealed class LocalPGFlowExecution : FlowLocalExecution
{
    private readonly DevicePG device;
    private readonly string operation;
    private readonly object? parameters;
    private readonly LocalPGConnection config;
    private readonly int maxTime;
    private CancellationTokenSource? deadline;
    private CancellationToken stop;
    private object? result;
    internal static bool Supports(CVBaseServerNode node) => node.NodeType == "PG" && node is PGNode or PGLoopNode or PGGECSNode or PGGECS_KeyNode or PGGECS_DemuraNode;
    private static DevicePG? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DevicePG>().FirstOrDefault(device => device.Code == code);

    internal static void Register() => RegisterBackend(nameof(LocalPGFlowExecution),
        node => Supports(node) && Find(node.DeviceCode)?.PGBackend.OpensLocally == true,
        (node, request) =>
        {
            if (!Supports(node) || Find(request.DeviceCode) is not { } device) return null!;
            lock (device.PGBackend.Sync)
            {
                if (!device.PGBackend.OpensLocally)
                {
                    LocalCommunicationOwnership.EnsureAvailable(device, false, device.LocalEndpoint);
                    device.PGBackend.EnsureServiceAvailable();
                    return null!;
                }
            }
            return new LocalPGFlowExecution(device, request.EventName, request.Data, node.MaxTime);
        });

    internal LocalPGFlowExecution(DevicePG device, string operation, object? parameters, int maxTime)
    {
        if (operation is not ("SetParam" or "ExecCmd")) throw new NotSupportedException($"本地 PG 流程不支持 {operation}。");
        this.device = device; this.operation = operation; this.parameters = parameters;
        this.maxTime = maxTime;
        config = device.SnapshotLocalPGConfig();
    }
    public override void Bind(CVStartCFC action)
    {
        deadline = CancellationTokenSource.CreateLinkedTokenSource(action.RuntimeResources.StopToken);
        if (maxTime > 0) deadline.CancelAfter(maxTime);
        stop = deadline.Token;
    }
    public override void Execute()
    {
        try { stop.ThrowIfCancellationRequested(); result = device.ExecuteLocalPGAsync(operation, parameters, config, stop, true).GetAwaiter().GetResult(); }
        catch (Exception ex) { device.PublishLocalPGResult(null, ex); throw; }
    }
    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃本地 PG 结果。");
        action.Data["LocalPG"] = result!;
        device.PublishLocalPGResult(result);
        return result ?? new { Message = "ok" };
    }
    public override void Dispose() { result = null; deadline?.Dispose(); }
}
