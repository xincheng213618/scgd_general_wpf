using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Results;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Spectrum;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal sealed class LocalSpectrumFlowExecution : FlowLocalExecution
{
    private readonly DeviceSpectrum device;
    private readonly LocalSpectrumParameters parameters;
    private readonly ConfigSpectrum config;
    private readonly string operation;
    private readonly bool autoConnect;
    private LocalSpectrumCapture? capture;
    private int released;
    private static DeviceSpectrum? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault(d => d.Code == code);

    internal static void Register()
    {
        CanExecuteLocally = node => (node is SpectrumNode or SpectrumEQENode) && Find(node.DeviceCode)?.SpectrumBackend.OpensLocally == true;
        CreateForNode = (node, request) =>
        {
            if (node is not (SpectrumNode or SpectrumEQENode)) return null!;
            DeviceSpectrum? device = Find(request.DeviceCode);
            if (device == null) return null!;
            lock (device.SpectrumBackend.Sync)
            {
                if (!device.SpectrumBackend.OpensLocally)
                {
                    device.EnsureOtherSpectrumBackends(false);
                    device.SpectrumBackend.EnsureServiceAvailable();
                    return null!;
                }
            }
            var data = JObject.FromObject(request.Data);
            LocalSpectrumParameters parameters = data.ToObject<LocalSpectrumParameters>()!;
            parameters.Eqe = request.EventName == "EQE.GetData";
            parameters.Voltage = data["SMUData"]?["V"]?.Value<float>() ?? 0;
            parameters.Current = data["SMUData"]?["I"]?.Value<float>() ?? 0;
            return new LocalSpectrumFlowExecution(device, parameters, request.EventName, true, request.ZIndex, request.DeviceNodeCode);
        };
    }

    private readonly int zIndex;
    private readonly string nodeId;
    internal LocalSpectrumFlowExecution(DeviceSpectrum device, LocalSpectrumParameters parameters, string operation, bool autoConnect, int zIndex, string nodeId)
    {
        this.device = device; this.parameters = parameters; this.operation = operation;
        this.autoConnect = autoConnect; this.zIndex = zIndex; this.nodeId = nodeId;
        if (operation is not ("GetData" or "EQE.GetData" or "InitDark")) throw new NotSupportedException($"本地光谱流程不支持 {operation}。");
        parameters.Validate();
        config = device.SnapshotLocalConfiguration();
        device.SpectrumBackend.BeginLocalCommand();
    }

    public override void Execute()
    {
        try
        {
            device.EnsureLocalSpectrumConnected(autoConnect, config);
            if (operation == "InitDark") device.LocalSession.Dark(parameters, config);
            else capture = device.CaptureLocalSpectrum(parameters, config, false);
        }
        finally { Release(); }
    }

    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃本地光谱结果。");
        if (operation == "InitDark") return new { Message = "校零完成" };
        if (capture == null) throw new InvalidOperationException("本地光谱仪未返回结果。");
        var model = LocalSpectrumResultService.Save(capture, parameters, action, zIndex);
        action.MasterValue(null, model.Id, 300);
        action.Data["LocalSpectrum"] = capture;
        device.PublishLocalSpectrum(capture);
        if (model.Id > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Spectrum, ResultKinds.Spectrum, device.Code, operation, action.SerialNumber, nodeId, zIndex, model.Id, 300);
        return new { MasterId = model.Id, MasterResultType = 300, capture.IntegralTime, capture.Data, capture.EqeData };
    }

    private void Release() { if (Interlocked.Exchange(ref released, 1) == 0) device.SpectrumBackend.EndLocalCommand(); }
    public override void Dispose() { capture = null; Release(); }
}
