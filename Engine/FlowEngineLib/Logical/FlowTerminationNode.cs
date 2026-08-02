using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Drawing;

namespace FlowEngineLib.Logical;

public enum FlowTerminationStatus
{
	Completed,
	Failed,
	Canceled,
	OverTime
}

[STNode("/01 运算", "以指定状态立即结束当前流程")]
public sealed class FlowTerminationNode : CVCommonNode
{
	private FlowTerminationStatus terminationStatus;
	private string reason = string.Empty;

	[STNodeProperty("结束状态", "Completed、Failed、Canceled 或 OverTime", true)]
	public FlowTerminationStatus TerminationStatus
	{
		get => terminationStatus;
		set
		{
			terminationStatus = value;
			OnPropertyChanged();
			Invalidate();
		}
	}

	[STNodeProperty("原因", "写入流程结果的结束原因", true)]
	public string Reason
	{
		get => reason;
		set
		{
			reason = value ?? string.Empty;
			OnPropertyChanged();
		}
	}

	public FlowTerminationNode()
		: base("流程终止", "FlowTermination", "FT1", "DEV01")
	{
		terminationStatus = FlowTerminationStatus.Completed;
		reason = "流程主动结束";
		AutoSize = false;
		Width = StandardNodeWidth;
		Height = StandardNodeMinHeight;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		TitleColor = Color.FromArgb(200, Color.Firebrick);
		STNodeOption input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		input.DataTransfer += Input_DataTransfer;
	}

	protected override string GetCompactSummaryLabel() => "状态:";

	protected override string GetCompactSummaryValue() => TerminationStatus.ToString();

	private void Input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status != ConnectionStatus.Connected || e.TargetOption.Data is not CVStartCFC start)
		{
			return;
		}

		if (start.TryDoFinishing(() => ApplyTermination(start)))
		{
			start.FireFinished();
		}
	}

	private void ApplyTermination(CVStartCFC start)
	{
		start.Data ??= new System.Collections.Generic.Dictionary<string, object>();
		start.Data["TerminationNodeName"] = Title;
		start.Data["TerminationNodeId"] = NodeID;
		start.Data["TerminationReason"] = Reason;
		DateTime terminationTime = DateTime.Now;
		switch (TerminationStatus)
		{
		case FlowTerminationStatus.Completed:
			start.SetStatusType(StatusTypeEnum.Completed);
			break;
		case FlowTerminationStatus.Failed:
			start.Failed(Reason, Title, terminationTime, NodeID);
			break;
		case FlowTerminationStatus.Canceled:
			start.SetStatusType(StatusTypeEnum.Canceled);
			break;
		case FlowTerminationStatus.OverTime:
			start.OverTime(Title, terminationTime, NodeID);
			start.Data["Msg"] = Reason;
			break;
		default:
			throw new InvalidOperationException($"不支持的流程结束状态：{TerminationStatus}");
		}
	}
}
