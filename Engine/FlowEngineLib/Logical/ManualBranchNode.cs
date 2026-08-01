using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using System;
using System.Drawing;

namespace FlowEngineLib.Logical;

public enum ManualBranchPath
{
	A,
	B
}

[STNode("/01 运算", "在 A/B 两条执行路径之间手动切换")]
public sealed class ManualBranchNode : CVCommonNode
{
	private ManualBranchPath selectedPath;
	private STNodeOption input = STNodeOption.Empty;
	private STNodeOption outputA = STNodeOption.Empty;
	private STNodeOption outputB = STNodeOption.Empty;

	[STNodeProperty("当前路径", "选择本次流程只执行 A 路径或 B 路径，也可直接点击节点上的 A/B 按钮", true)]
	public ManualBranchPath SelectedPath
	{
		get => selectedPath;
		set
		{
			if (selectedPath == value)
			{
				return;
			}
			selectedPath = value;
			UpdateOutputDisplay();
			OnPropertyChanged();
			Invalidate();
		}
	}

	public ManualBranchNode()
		: base("手动分支", "ManualBranch", "BR1", "DEV01")
	{
		selectedPath = ManualBranchPath.A;
		AutoSize = false;
		Width = StandardNodeWidth;
		Height = StandardNodeMinHeight;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		TitleColor = Color.FromArgb(200, Color.Goldenrod);
		input = InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		outputA = OutputOptions.Add("OUT_A", typeof(CVStartCFC), bSingle: false);
		outputB = OutputOptions.Add("OUT_B", typeof(CVStartCFC), bSingle: false);
		input.DataTransfer += Input_DataTransfer;
		UpdateOutputDisplay();
	}

	protected override void OnDrawBody(DrawingTools dt)
	{
		base.OnDrawBody(dt);
		DrawPathSelector(dt);
	}

	protected override void OnMouseClick(STNodeMouseEventArgs e)
	{
		base.OnMouseClick(e);
		if (e.Button != STMouseButtons.Left)
		{
			return;
		}

		Rectangle selectorBounds = GetPathSelectorBounds(absolute: false);
		if (selectorBounds.Contains(e.Location))
		{
			SelectedPath = e.X < selectorBounds.Left + selectorBounds.Width / 2
				? ManualBranchPath.A
				: ManualBranchPath.B;
		}
	}

	private void Input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		object data = e.Status == ConnectionStatus.Connected ? e.TargetOption.Data : null;
		if (SelectedPath == ManualBranchPath.A)
		{
			outputA.TransferData(data);
		}
		else
		{
			outputB.TransferData(data);
		}
	}

	private void UpdateOutputDisplay()
	{
		if (outputA != STNodeOption.Empty)
		{
			SetOptionText(outputA, SelectedPath == ManualBranchPath.A ? "OUT_A [ON]" : "OUT_A");
			SetOptionTextColor(outputA, SelectedPath == ManualBranchPath.A ? Color.White : Color.Gray);
		}
		if (outputB != STNodeOption.Empty)
		{
			SetOptionText(outputB, SelectedPath == ManualBranchPath.B ? "OUT_B [ON]" : "OUT_B");
			SetOptionTextColor(outputB, SelectedPath == ManualBranchPath.B ? Color.White : Color.Gray);
		}
	}

	private void DrawPathSelector(DrawingTools dt)
	{
		Rectangle selectorBounds = GetPathSelectorBounds(absolute: true);
		int leftWidth = selectorBounds.Width / 2;
		Rectangle pathABounds = new Rectangle(selectorBounds.Left, selectorBounds.Top, leftWidth, selectorBounds.Height);
		Rectangle pathBBounds = new Rectangle(selectorBounds.Left + leftWidth, selectorBounds.Top, selectorBounds.Width - leftWidth, selectorBounds.Height);
		DrawPathButton(dt, pathABounds, "A", SelectedPath == ManualBranchPath.A);
		DrawPathButton(dt, pathBBounds, "B", SelectedPath == ManualBranchPath.B);
	}

	private void DrawPathButton(DrawingTools dt, Rectangle bounds, string text, bool selected)
	{
		dt.SolidBrush.Color = selected ? Color.FromArgb(190, Color.ForestGreen) : Color.FromArgb(110, Color.DimGray);
		dt.Graphics.FillRectangle(dt.SolidBrush, bounds);
		dt.Pen.Color = selected ? Color.White : Color.Gray;
		dt.Graphics.DrawRectangle(dt.Pen, bounds);

		StringAlignment alignment = m_sf.Alignment;
		StringAlignment lineAlignment = m_sf.LineAlignment;
		try
		{
			m_sf.Alignment = StringAlignment.Center;
			m_sf.LineAlignment = StringAlignment.Center;
			dt.SolidBrush.Color = selected ? Color.White : Color.LightGray;
			dt.Graphics.DrawString(text, Font, dt.SolidBrush, bounds, m_sf);
		}
		finally
		{
			m_sf.Alignment = alignment;
			m_sf.LineAlignment = lineAlignment;
		}
	}

	private Rectangle GetPathSelectorBounds(bool absolute)
	{
		return new Rectangle(
			(absolute ? Left : 0) + StandardNodeContentPadding,
			(absolute ? Top : 0) + TitleHeight + CompactSummaryTop,
			Math.Max(0, Width - StandardNodeContentPadding * 2),
			CompactSummaryHeight);
	}
}
