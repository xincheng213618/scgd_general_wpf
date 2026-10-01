using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.End;

[STNode("全局", CategoryOrder = 0)]
public class CVEndNode : CVDeviceNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVEndNode));

	private static int endDelayMilliseconds;

	public static int EndDelayMilliseconds
	{
		get => Volatile.Read(ref endDelayMilliseconds);
		set => Volatile.Write(ref endDelayMilliseconds, value is > 0 and <= 500 ? value : 0);
	}

	public STNodeOption m_in_start;

	protected STNodeOption[] m_in_loop_next;

	public CVEndNode()
		: base("EndNode", "EndNode", "EN1", "DEV01")
	{
		base.AutoSize = false;
		base.Width = CompactTerminalNodeWidth;
		base.Height = base.TitleHeight + 3 * base.ItemHeight;
	}

	protected override int MinimumNodeWidth => CompactTerminalNodeWidth;

	protected override bool ShouldDrawOptionText(STNodeOption op) => false;

	protected override void OnCreate()
	{
		base.OnCreate();
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
		m_in_loop_next = new STNodeOption[2];
		m_in_start = base.InputOptions.Add("IN", typeof(CVStartCFC), bSingle: true);
		for (int i = 0; i < 2; i++)
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

	protected virtual async void DoNodeEnded(CVStartCFC startAction)
	{
		try
		{
			int delay = EndDelayMilliseconds;
			if (delay > 0 && startAction.IsRunning && !startAction.TryGetStopStatus(out _)
				&& DateTime.Now - startAction.StartTime >= TimeSpan.FromSeconds(1))
			{
				await Task.Delay(delay).ConfigureAwait(false);
				var startNode = startAction.GetStartNode();
				if (startAction.RuntimeResources.IsDisposed || (startNode != null && startNode.GetCFC(startAction.SerialNumber)?.Id != startAction.Id))
					return;
			}
			if (!startAction.TryDoFinishing())
				return;
			logger.InfoFormat("Flow Finished[{0}/{1}/{2}]", startAction.SerialNumber, startAction.FlowStatus, startAction.GetTotalTime());
			startAction.FireFinished();
		}
		catch (Exception ex)
		{
			logger.Error("Flow end node failed.", ex);
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
