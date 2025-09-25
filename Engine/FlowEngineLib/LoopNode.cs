using System.Collections.Generic;
using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/01 运算")]
public class LoopNode : CVCommonNode
{
	private STNodeOption m_in_start;

	private STNodeOption m_op_end;

	private STNodeOption m_in_next;

	private float m_begin_val;

	private float m_end_val;

	private float m_step_val;

	private Dictionary<string, LoopDataModel> m_actionBegin;

	private STNodeEditText<string> m_ctrl_editText;

	[STNodeProperty("起始值", "起始值")]
	public float BeginVal
	{
		get
		{
			return m_begin_val;
		}
		set
		{
			m_begin_val = value;
			updateUI(null);
		}
	}

	[STNodeProperty("结束值", "结束值")]
	public float EndVal
	{
		get
		{
			return m_end_val;
		}
		set
		{
			m_end_val = value;
			updateUI(null);
		}
	}

	[STNodeProperty("单步值", "单步值")]
	public float StepVal
	{
		get
		{
			return m_step_val;
		}
		set
		{
			m_step_val = value;
			updateUI(null);
		}
	}

	public LoopNode()
		: base("LoopNode", "Loop", "LP1", "DEV01")
	{
		m_begin_val = 1f;
		m_step_val = 1f;
		m_end_val = 5f;
		base.AutoSize = false;
		base.Width = 150;
		base.Height = 100;
		base.TitleHeight += 10;
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
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		m_op_end = base.OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		m_in_start.DataTransfer += m_in_start_DataTransfer;
		m_in_next.DataTransfer += m_in_next_DataTransfer;
		m_ctrl_editText = new STNodeEditText<string>();
		m_ctrl_editText.Text = "值";
		m_ctrl_editText.DisplayRectangle = new Rectangle(5, 45, base.Width - 10, OptionItemHeight);
		base.Controls.Add(m_ctrl_editText);
		m_actionBegin = new Dictionary<string, LoopDataModel>();
		updateUI(null);
	}

	private void updateUI(LoopDataModel loopData)
	{
		if (loopData == null)
		{
			m_ctrl_editText.Value = string.Format("{1}-{2}/{0}", m_step_val, m_begin_val, m_end_val);
		}
		else
		{
			m_ctrl_editText.Value = $"{loopData.m_cur_step}:{loopData.m_cur_val}/{m_end_val}";
		}
	}

	private void stop(CVStartCFC action, bool clearLabel)
	{
		if (clearLabel)
		{
			updateUI(null);
		}
		if (action != null)
		{
			if (m_actionBegin.ContainsKey(action.SerialNumber))
			{
				m_actionBegin.Remove(action.SerialNumber);
			}
		}
		else
		{
			m_actionBegin.Clear();
		}
	}

	private void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (HasData(e))
		{
			CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
			if (!m_actionBegin.ContainsKey(cVStartCFC.SerialNumber))
			{
				if (cVStartCFC.FlowStatus == StatusTypeEnum.Runing)
				{
					LoopDataModel loopDataModel = new LoopDataModel(m_begin_val, m_end_val, m_step_val, cVStartCFC);
					m_actionBegin.Add(cVStartCFC.SerialNumber, loopDataModel);
					doOutput(loopDataModel);
				}
				else
				{
					m_op_end.TransferData(e.TargetOption.Data);
				}
			}
			else
			{
				m_op_end.TransferData(e.TargetOption.Data);
				if (!cVStartCFC.IsRunning)
				{
					stop(cVStartCFC, clearLabel: true);
				}
			}
		}
		else
		{
			stop(null, clearLabel: true);
		}
	}

	private void m_in_next_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (HasData(e))
		{
			CVLoopCFC cVLoopCFC = (CVLoopCFC)e.TargetOption.Data;
			LoopDataModel curLoop = getCurLoop(cVLoopCFC.SerialNumber);
			if (curLoop != null)
			{
				curLoop.Next();
				doOutput(curLoop);
			}
		}
	}

	private LoopDataModel getCurLoop(string key)
	{
		if (!string.IsNullOrEmpty(key) && m_actionBegin.ContainsKey(key))
		{
			return m_actionBegin[key];
		}
		return null;
	}

	private void doOutput(LoopDataModel loopData)
	{
		LoopDataInfo loopDataInfo = null;
		updateUI(loopData);
		if (loopData.startCFC.Data.ContainsKey(base.NodeName))
		{
			loopDataInfo = loopData.startCFC.Data[base.NodeName] as LoopDataInfo;
		}
		else
		{
			loopDataInfo = new LoopDataInfo();
			loopData.startCFC.Data.Add(base.NodeName, loopDataInfo);
		}
		loopDataInfo.Step = loopData.m_cur_step;
		loopData.startCFC.AddTTL(loopData.startTime);
		if (loopData.IsEnd())
		{
			loopDataInfo.HasNext = false;
			m_op_end.TransferData(loopData.startCFC);
			stop(loopData.startCFC, clearLabel: false);
		}
		else
		{
			loopDataInfo.HasNext = true;
			m_op_end.TransferData(loopData.startCFC);
		}
	}
}
