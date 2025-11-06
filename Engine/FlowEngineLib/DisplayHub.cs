using FlowEngineLib.MQTT;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/00 全局")]
public class DisplayHub : STNodeInHub
{
	public DisplayHub()
		: base(bSingle: true, "值显示HUB")
	{
	}

	protected override void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected || e.TargetOption.Data == null)
		{
			SetOptionText(sender, "--");
			return;
		}
		string text = ((!(e.TargetOption.Data.GetType() == typeof(MQActionEvent))) ? JsonConvert.SerializeObject(e.TargetOption.Data, (Formatting)0) : ((MQActionEvent)e.TargetOption.Data).Message);
		SetOptionText(sender, text ?? "");
	}
}
