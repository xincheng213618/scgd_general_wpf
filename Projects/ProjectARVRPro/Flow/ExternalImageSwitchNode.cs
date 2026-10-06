using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Services;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;
using System.ComponentModel;

namespace ProjectARVRPro.Flow;

[STNode("", "ExternalSwitch_Title")]
public sealed class ExternalImageSwitchNode : LocalFlowNodeBase
{
    private int timeoutMs = 5000;
    private int delayMs;
    private bool requireMatchingMsgId;
    private readonly ExternalImageSwitchService service;
    private readonly Func<string> productSerialNumber;

    static ExternalImageSwitchNode() => Lang.RegisterResourceManager(Properties.Resources.ResourceManager);

    public ExternalImageSwitchNode() : this(ExternalImageSwitchService.Instance, () => ProjectARVRProConfig.Instance.SN ?? string.Empty) { }

    internal ExternalImageSwitchNode(ExternalImageSwitchService service, Func<string> productSerialNumber)
        : base(DisplayText.Get("ExternalSwitch_Title"), "ExternalImageSwitch", ExternalImageSwitchService.RequestEvent)
    {
        this.service = service;
        this.productSerialNumber = productSerialNumber;
    }

    [Category("ExternalSwitch_Title")]
    [STNodeProperty("ExternalSwitch_TimeoutLabel", "ExternalSwitch_TimeoutDescription", true)]
    public int TimeoutMs { get => timeoutMs; set { timeoutMs = value; OnPropertyChanged(); } }

    [Category("ExternalSwitch_Title")]
    [STNodeProperty("ExternalSwitch_DelayLabel", "ExternalSwitch_DelayDescription", true)]
    public int DelayMs { get => delayMs; set { delayMs = value; OnPropertyChanged(); } }

    [Category("ExternalSwitch_Title")]
    [STNodeProperty("ExternalSwitch_MatchLabel", "ExternalSwitch_MatchDescription", true)]
    public bool RequireMatchingMsgId { get => requireMatchingMsgId; set { requireMatchingMsgId = value; OnPropertyChanged(); } }

    public override string OnGetDrawTitle() => Title is "外部切图" or "External image switch" or "外部切圖"
        ? DisplayText.Get("ExternalSwitch_Title") : base.OnGetDrawTitle();

    protected override string GetCompactSummaryValue() => DisplayText.Format($"ExternalSwitch_Summary: {TimeoutMs}");

    protected override LocalNodeExecutionResult ExecuteLocal(CVStartCFC action)
    {
        action.RuntimeResources.StopToken.ThrowIfCancellationRequested();
        // The flow serial number identifies a measurement run; the wire protocol uses the product SN.
        ExternalImageSwitchResult result = service.ExecuteAsync(
            productSerialNumber(), TimeoutMs, DelayMs, RequireMatchingMsgId,
            action.RuntimeResources.StopToken).GetAwaiter().GetResult();
        return new LocalNodeExecutionResult { Message = DisplayText.Get("ExternalSwitch_Completed"), Data = result };
    }

    protected override string BuildRunPayload(CVStartCFC action) => JsonConvert.SerializeObject(new
    {
        EventName = ExternalImageSwitchService.RequestEvent, FlowRunId = action.SerialNumber,
        TimeoutMs, DelayMs, RequireMatchingMsgId
    });
}
