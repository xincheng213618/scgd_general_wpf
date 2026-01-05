using System;
using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.End;

[STNode("/00 全局")]
public class CVEndNode : CVCommonNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVEndNode));

	public STNodeOption m_in_start;

	protected STNodeOption[] m_in_loop_next;

	public CVEndNode()
		: base("EndNode", "EndNode", "EN1", "DEV01")
	{
		base.Height = 160;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
		m_in_loop_next = new STNodeOption[5];
		m_in_start = base.InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		for (int i = 0; i < 5; i++)
		{
			m_in_loop_next[i] = base.InputOptions.Add($"IN_LOOP_NEXT{i + 1}", typeof(CVLoopCFC), bSingle: true);
			m_in_loop_next[i].Connected += m_in_loop_next_Connected;
			m_in_loop_next[i].DisConnected += m_in_loop_next_DisConnected;
			m_in_loop_next[i].DataTransfer += m_in_loop_next_DataTransfer;
		}
		m_in_start.Connected += m_in_start_Connected;
		m_in_start.DisConnected += m_in_start_DisConnected;
		m_in_start.DataTransfer += m_in_start_DataTransfer;
	}

	protected virtual void m_in_loop_next_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption op = (STNodeOption)sender;
		if (e.Status == ConnectionStatus.Connected)
		{
			if (HasData(e))
			{
				CVLoopCFC obj = e.TargetOption.Data as CVLoopCFC;
				SetOptionText(op, DateTime.Now.ToString("HH:mm:ss:fffff"));
				obj.DoLoopNextAction();
			}
			else
			{
				SetOptionText(op, "--");
			}
		}
	}

	protected virtual void DoNodeEnded(CVStartCFC startAction)
	{
		if (!startAction.IsDel)
		{
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("===============Flow Do Finishing => {0}/{1}", startAction.SerialNumber, startAction.FlowStatus.ToString());
			}
			startAction.DoFinishing();
			SetOptionText(m_in_start, startAction.ToShortString());
			if (logger.IsInfoEnabled)
			{
				logger.InfoFormat("===============Flow Finished[{0}/{1}/{2}]============", startAction.SerialNumber, startAction.FlowStatus.ToString(), startAction.GetTotalTime().ToString());
			}
			startAction.FireFinished();
		}
		else if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("Flow has Finished. => {0}/{1}", startAction.SerialNumber, startAction.FlowStatus.ToString());
		}
	}

	protected virtual void m_in_loop_next_Connected(object sender, STNodeOptionEventArgs e)
	{
		SetOptionText(sender as STNodeOption, "--");
	}

	protected virtual void m_in_loop_next_DisConnected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "IN_LOOP_NEXT" + base.InputOptions.IndexOf(sTNodeOption));
	}

	protected virtual void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.Status == ConnectionStatus.Connected)
		{
			if (e.TargetOption.Data != null)
			{
				DoNodeEnded((CVStartCFC)e.TargetOption.Data);
			}
			else
			{
				SetOptionText(m_in_start, "--");
			}
		}
	}

	protected virtual void DoStartDisConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
	}

	protected virtual void DoStartConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
	}

	protected void m_in_start_DisConnected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "IN");
		DoStartDisConnected(sTNodeOption, e);
	}

	protected void m_in_start_Connected(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption sTNodeOption = sender as STNodeOption;
		SetOptionText(sTNodeOption, "--");
		DoStartConnected(sTNodeOption, e);
	}
}
