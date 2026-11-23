using ColorVision.Database;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Results;
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
    private readonly LocalSpectrumOpticalScope optics;
    private readonly string operation;
    private readonly LocalSpectrumLicenseSource? licenseSource;
    private LocalSpectrumCapture? capture;
    private CancellationToken flowStopToken;
    private MySqlConnectedEndpoint? resultDatabase;
    private int released;
    private static DeviceSpectrum? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault(d => d.Code == code);

    internal static void Register()
    {
        LocalSpectrumNdFlowExecution.Register();
        RegisterBackend(nameof(LocalSpectrumFlowExecution), node => (node is SpectrumNode or SpectrumEQENode) && Find(node.DeviceCode)?.SpectrumBackend.OpensLocally == true, (node, request) =>
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
            LocalSpectrumParameters parameters = ReadLegacyParameters(request);
            return new LocalSpectrumFlowExecution(device, parameters, request.EventName, true, request.ZIndex, request.DeviceNodeCode);
        });
    }

    internal static LocalSpectrumParameters ReadLegacyParameters(CVMQTTRequest request)
    {
        var data = JObject.FromObject(request.Data);
        var parameters = data.ToObject<LocalSpectrumParameters>()!;
        parameters.Eqe = request.EventName == "EQE.GetData";
        if (parameters.Eqe) parameters.ApplySmuData(data["SMUData"]);
        return parameters;
    }

    private readonly int zIndex;
    private readonly string nodeId;
    internal LocalSpectrumFlowExecution(DeviceSpectrum device, LocalSpectrumParameters parameters, string operation, bool autoConnect, int zIndex, string nodeId,
        LocalSpectrumOpticalScope? optics = null, LocalSpectrumLicenseSource? licenseSource = null)
    {
        this.device = device; this.parameters = parameters; this.operation = operation;
        this.zIndex = zIndex; this.nodeId = nodeId;
        if (operation is not ("GetData" or "EQE.GetData" or "InitDark")) throw new NotSupportedException($"本地光谱流程不支持 {operation}。");
        parameters.Validate();
        this.optics = optics ?? device.SnapshotLocalOptics(operation);
        this.licenseSource = autoConnect ? licenseSource ?? device.CaptureOpenLicense(this.optics) : null;
        device.SpectrumBackend.BeginLocalCommand();
    }

    public override void Bind(CVStartCFC action)
    {
        flowStopToken = action.RuntimeResources.StopToken;
        if (action.PersistResults && operation != "InitDark")
        {
            resultDatabase = LocalFlowResultPersistence.ResolveEndpoint(action);
            if (resultDatabase != null && device.SysResourceModel.Id > 0 && !ReferenceEquals(device.ResultDatabaseEndpoint, resultDatabase))
                throw new InvalidOperationException("光谱仪与流程批次所属的结果库不一致，请重新启动流程。");
        }
    }

    public override void Execute()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(device.SpectrumBackend.LocalCancellationToken, flowStopToken);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            device.EnsureLocalSpectrumConnected(optics, licenseSource);
            if (operation == "InitDark") device.LocalSession.Dark(parameters, optics.Configuration, opticalScope: optics);
            else capture = device.CaptureLocalSpectrum(parameters, optics, cancellation.Token);
        }
        finally { Release(); }
    }

    public override object Complete(CVStartCFC action)
    {
        if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃本地光谱结果。");
        if (operation == "InitDark") return new { Message = "校零完成" };
        if (capture == null) throw new InvalidOperationException("本地光谱仪未返回结果。");
        var model = LocalSpectrumResultService.SaveFlow(capture, parameters, action, zIndex, resultDatabase);
        action.MasterValue(null, model.Id, 300);
        action.Data["LocalSpectrum"] = capture;
        device.PublishLocalSpectrum(capture, optics, resultDatabase);
        if (model.Id > 0) ResultMessageBus.Default.PublishPersisted(ResultRoutes.Spectrum, ResultKinds.Spectrum, capture.DeviceCode, operation,
            action.SerialNumber, nodeId, zIndex, model.Id, 300, resultDatabase);
        return new { MasterId = model.Id, MasterResultType = 300, capture.IntegralTime, capture.Data, capture.EqeData };
    }

    private void Release() { if (Interlocked.Exchange(ref released, 1) == 0) device.SpectrumBackend.EndLocalCommand(); }
    public override void Dispose() { capture = null; Release(); }
}
