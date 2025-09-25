using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/01 运算")]
public class LoopNextNode : CVCommonNode
{
	private STNodeOption m_in_start;

	private STNodeOption m_op_loop;

	private STNodeOption m_op_end;

	private STNodeEditText<string> m_ctrl_editText;

	public LoopNextNode()
		: base("LoopNextNode", "LoopNext", "LP1", "DEV01")
	{
		base.AutoSize = false;
		base.Width = 140;
		base.Height = 100;
		base.TitleHeight += 10;
	}

	protected override void OnNodeNameChanged(string oldValue, string newValue)
	{
		Invalidate();
	}

	protected override string OnGetDrawTitle()
	{
		return $"{base.Title}\r\n{base.NodeName}";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
		m_in_start = base.InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		m_op_end = base.OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		m_op_loop = base.OutputOptions.Add("OUT_LP_NEXT", typeof(CVLoopCFC), bSingle: false);
		m_in_start.DataTransfer += m_in_start_DataTransfer;
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<string>), new Rectangle(5, 45, base.Width - 10, OptionItemHeight), "Value:", string.Empty);
	}

	private void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (HasData(e))
		{
			CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
			if (cVStartCFC.IsRunning)
			{
				if (cVStartCFC.Data.ContainsKey(base.NodeName) && cVStartCFC.Data[base.NodeName].GetType() == typeof(LoopDataInfo))
				{
					LoopDataInfo loopDataInfo = cVStartCFC.Data[base.NodeName] as LoopDataInfo;
					m_ctrl_editText.Value = $"{loopDataInfo.Step}:{loopDataInfo.HasNext}";
					if (loopDataInfo.HasNext)
					{
						m_op_loop.TransferData(new CVLoopCFC(cVStartCFC, base.NodeName));
						return;
					}
					m_op_end.TransferData(e.TargetOption.Data);
					m_op_loop.TransferData(null);
				}
			}
			else
			{
				m_op_end.TransferData(e.TargetOption.Data);
				m_op_loop.TransferData(null);
			}
		}
		else
		{
			m_op_end.TransferData(null);
			m_op_loop.TransferData(null);
		}
	}
}
