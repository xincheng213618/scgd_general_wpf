using ColorVision.Engine.Services.Results;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Global;
using FlowEngineLib.Node.SMU;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal sealed class LocalSmuFlowExecution : FlowLocalExecution
{
    private readonly DeviceSMU device;
    private readonly LocalSmuConnection config;
    private readonly LocalSmuParameters parameters;
    private readonly string operation, nodeId;
    private readonly int zIndex, waitTime;
    private readonly bool checkLimits;
    private CancellationToken stop;
    private LocalSmuCapture? capture;
    private bool completed;

    internal static bool Supports(CVBaseServerNode node) => node.NodeType == "SMU"
        && node is SMUBaseNode or SMUSweepNode or SMUSweepModelNode or SMUReaderNode or PhyDeviceControlNode;
    private static DeviceSMU? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceSMU>().FirstOrDefault(device => device.Code == code);
    internal static void Register() => RegisterBackend(nameof(LocalSmuFlowExecution),
        node => Supports(node) && Find(node.DeviceCode)?.SmuBackend.OpensLocally == true,
        (node, request) =>
        {
            if (!Supports(node)) return null!;
            var device = Find(request.DeviceCode);
            if (device == null) return null!;
            lock (device.SmuBackend.Sync)
            {
                if (!device.SmuBackend.OpensLocally)
                {
                    device.EnsureOtherSmuBackends(false, LocalSmuConnection.From(device.Config));
                    device.SmuBackend.EnsureServiceAvailable();
                    return null!;
                }
            }
            return new LocalSmuFlowExecution(device, request.EventName, request.Data, request.ZIndex, request.DeviceNodeCode);
        });

    internal LocalSmuFlowExecution(DeviceSMU device, string operation, object? data, int zIndex, string nodeId)
    {
        this.device = device; this.operation = operation; this.zIndex = zIndex; this.nodeId = nodeId;
        if (operation is not ("Open" or "Reopen" or "Close" or "CloseOutput" or "GetData" or "ModelGetData" or "Scan" or "SMU.MeasureResult"))
            throw new NotSupportedException($"本地源表流程暂不支持 {operation}。");
        var json = data == null ? new JObject() : JObject.FromObject(data);
        config = LocalSmuConnection.From(device.Config);
        parameters = device.ResolveLocalSmuParameters(operation, json);
        checkLimits = device.DisplayConfig.IsUseLimitSigned;
        waitTime = operation == "SMU.MeasureResult" ? json.Value<int?>("WaitTime") ?? 0 : 0;
        if (waitTime < 0) throw new ArgumentException("源表读取等待时间不能为负数。");
    }
    public override void Bind(CVStartCFC action) => stop = action.RuntimeResources.StopToken;
    public override void Execute()
    {
        stop.ThrowIfCancellationRequested();
        // Legacy GetData nodes perform step measurements; the manual Ignite action uses MeasureData.
        capture = device.ExecuteLocalSmuAsync(operation is "GetData" or "ModelGetData" ? "StepData" : operation,
            config, parameters, true, checkLimits, stop).GetAwaiter().GetResult();
        if (waitTime != 0) Task.Delay(waitTime, stop).GetAwaiter().GetResult();
    }
    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _))
            throw new OperationCanceledException("流程已停止，丢弃本地源表结果。");
        if (capture == null) { completed = true; return new { Message = "ok" }; }
        int id = LocalSmuResultService.Save(capture, device.Code, action, zIndex);
        action.MasterValue(null, id, LocalSmuResultService.ResultType);
        action.Data["LocalSmu"] = capture;
        if (!capture.IsScan) action.Data["SMUResult"] = new SMUResultData((FlowEngineLib.SMUChannelType)(int)capture.Parameters.Channel,
            capture.V, capture.I, id, LocalSmuResultService.ResultType);
        device.PublishLocalSmu(capture, id);
        if (id > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Smu, ResultKinds.Smu, device.Code, operation,
            action.SerialNumber, nodeId, zIndex, id, LocalSmuResultService.ResultType);
        completed = true;
        var result = LocalSmuResultService.Response(capture, id);
        if (operation == "ModelGetData")
        {
            result["ResultData"] = new JObject { ["V"] = capture.V, ["I"] = capture.I };
            result["ScanRequestParam"] = JObject.FromObject(parameters);
        }
        return result;
    }
    public override void Dispose()
    {
        if (!completed && capture != null && !capture.Parameters.IsCloseOutput)
        {
            try { device.LocalSession.CloseOutputAsync(capture.Parameters.Channel, expectedGeneration: capture.Generation).GetAwaiter().GetResult(); }
            catch (Exception ex) { MQTTServiceBase.log.Error(ex); }
        }
        capture = null;
    }
}
