using FlowEngineLib.MQTT;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("全局", CategoryOrder = 0)]
[System.Obsolete("Value display Hub is retained only for loading existing flows.")]
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
		string text = ((!(e.TargetOption.Data.GetType() == typeof(MQActionEvent))) ? JsonConvert.SerializeObject(e.TargetOption.Data, Formatting.None) : ((MQActionEvent)e.TargetOption.Data).Message);
		SetOptionText(sender, text ?? "");
	}
}
