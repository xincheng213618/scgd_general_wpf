using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal sealed class LocalSpectrumNdFlowExecution : FlowLocalExecution
{
    private readonly DeviceSpectrum device;
    private readonly LocalSpectrumOpticalScope optics;
    private readonly int port;
    private readonly bool reserved;
    private CancellationToken stop;
    private (int Port, LocalSpectrumNd.Calibration Calibration)? result;
    private int released;

    internal static bool Supports(CVBaseServerNode node) => node is FWNode { ModelType: FWModelType.Spectrum_FilterWheel } && node.NodeType == "Spectrum";
    private static DeviceSpectrum? Find(string code) => ServiceManager.Current?.DeviceServices.OfType<DeviceSpectrum>().FirstOrDefault(item => item.Code == code);
    internal static void Register() => RegisterBackend(nameof(LocalSpectrumNdFlowExecution),
        node => Supports(node) && Find(node.DeviceCode)?.UsesLocalNd == true,
        (node, request) => Supports(node) && Find(request.DeviceCode) is { UsesLocalNd: true } device
            ? new LocalSpectrumNdFlowExecution(device, request.EventName, request.Data) : null!);

    internal LocalSpectrumNdFlowExecution(DeviceSpectrum device, string operation, object data)
    {
        if (operation != "SetPort") throw new NotSupportedException($"本地光谱 ND 流程不支持 {operation}。");
        this.device = device;
        optics = device.SnapshotNdCommandOptics();
        port = JObject.FromObject(data).Value<int?>("PortNum") ?? throw new ArgumentException("光谱 ND 流程缺少孔位。");
        reserved = device.SpectrumBackend.OpensLocally;
        if (reserved) device.SpectrumBackend.BeginLocalCommand();
        else if (device.DService.DeviceStatus is DeviceStatusType.Busy or DeviceStatusType.SP_Continuous_Mode)
            throw new InvalidOperationException("光谱仪正在采集，请完成或停止后再切换 ND。");
    }

    public override void Bind(CVStartCFC action) => stop = action.RuntimeResources.StopToken;
    public override void Execute()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stop, reserved ? device.SpectrumBackend.LocalCancellationToken : CancellationToken.None);
        result = device.ExecuteLocalNd(optics, port, cancellation.Token);
    }
    public override object Complete(CVStartCFC action)
    {
        try
        {
            if (action.RuntimeResources.IsDisposed || action.IsDel || action.TryGetStopStatus(out _)) throw new OperationCanceledException("流程已停止，丢弃 ND 切位结果。");
            var confirmed = result ?? throw new InvalidOperationException("ND 没有返回确认孔位。");
            bool Apply() => device.ApplyLocalNdState(confirmed.Port, confirmed.Calibration, optics.ConfigurationTarget, optics);
            bool accepted = Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess() ? dispatcher.Invoke(Apply) : Apply();
            if (!accepted) throw new InvalidOperationException(DeviceSpectrum.NdConfigurationChangedMessage);
            return new JObject { ["Port"] = confirmed.Port };
        }
        finally { Release(); }
    }
    private void Release() { if (reserved && Interlocked.Exchange(ref released, 1) == 0) device.SpectrumBackend.EndLocalCommand(); }
    public override void Dispose() => Release();
}
