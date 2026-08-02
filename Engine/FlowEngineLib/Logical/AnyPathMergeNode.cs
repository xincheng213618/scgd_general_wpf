using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System.Drawing;

namespace FlowEngineLib.Logical;

[STNode("/01 运算", "接收 A/B 任意一条路径的有效结果并继续执行")]
public sealed class AnyPathMergeNode : CVCommonNode
{
	private STNodeOption output = STNodeOption.Empty;

	public AnyPathMergeNode()
		: base("任一路汇合", "AnyPathMerge", "MG1", "DEV01")
	{
		AutoSize = false;
		Width = StandardNodeWidth;
		Height = StandardNodeMinHeight;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		TitleColor = Color.FromArgb(200, Color.Goldenrod);
		STNodeOption inputA = InputOptions.Add("IN_A", typeof(CVStartCFC), bSingle: true);
		STNodeOption inputB = InputOptions.Add("IN_B", typeof(CVStartCFC), bSingle: true);
		output = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		inputA.DataTransfer += Input_DataTransfer;
		inputB.DataTransfer += Input_DataTransfer;
	}

	private void Input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected && e.TargetOption.Data is CVStartCFC start)
		{
			output.TransferData(start);
		}
	}
}
