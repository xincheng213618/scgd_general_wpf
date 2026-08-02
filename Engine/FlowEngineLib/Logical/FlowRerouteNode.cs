using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Drawing;

namespace FlowEngineLib.Logical;

[STNode("/01 运算", "整理长连线或交叉连线，不改变流程信号")]
public sealed class FlowRerouteNode : CVCommonNode
{
	private STNodeOption output = STNodeOption.Empty;

	public FlowRerouteNode()
		: base("流程中继", "FlowReroute", "RT1", "DEV01")
	{
		AutoSize = false;
		Width = 96;
		Height = 60;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		TitleColor = Color.FromArgb(200, Color.SlateGray);
		STNodeOption input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		output = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		input.DataTransfer += Input_DataTransfer;
	}

	protected override Size GetDefaultNodeSize(Graphics g) => new Size(96, 60);

	private void Input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		output.TransferData(e.Status == ConnectionStatus.Connected ? e.TargetOption.Data : null);
	}
}
