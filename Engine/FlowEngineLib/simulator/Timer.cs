using System.Collections.Generic;
using System.Timers;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

[STNode("/00 全局")]
internal class Timer : CVCommonNodeHub
{
	private int _Time = 2000;

	private Dictionary<string, MyTimer> timer = new Dictionary<string, MyTimer>();

	[STNodeProperty("Timer.Time", "Time")]
	public int Time
	{
		get
		{
			return _Time;
		}
		set
		{
			_Time = value;
		}
	}

	public Timer()
		: base(bSingle: true, "Timer")
	{
	}

	protected override void input_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		STNodeOption option = sender as STNodeOption;
		int index = base.InputOptions.IndexOf(option);
		if (e.Status != ConnectionStatus.Connected)
		{
			base.OutputOptions[index].Data = null;
			base.OutputOptions[index].TransferData();
			return;
		}
		base.OutputOptions[index].Data = e.TargetOption.Data;
		if (e.TargetOption.Data != null && e.TargetOption.Data.GetType() == typeof(CVStartCFC))
		{
			CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
			if (cVStartCFC.FlowStatus == StatusTypeEnum.Runing)
			{
				SetTimer(_Time, base.OutputOptions[index]);
			}
			else if (timer.ContainsKey(cVStartCFC.SerialNumber))
			{
				MyTimer myTimer = timer[cVStartCFC.SerialNumber];
				timer.Remove(cVStartCFC.SerialNumber);
				myTimer.Stop();
				myTimer.TransferData(cVStartCFC.GetActionType());
				myTimer.Dispose();
			}
			else
			{
				base.OutputOptions[index].TransferData();
			}
		}
		else
		{
			base.OutputOptions[index].TransferData();
		}
	}

	private void SetTimer(double interval, STNodeOption OutputOption)
	{
		if (interval > 0.0)
		{
			CVStartCFC cVStartCFC = (CVStartCFC)OutputOption.Data;
			MyTimer myTimer = new MyTimer(interval, OutputOption);
			myTimer.Elapsed += DoOutputTransfer;
			myTimer.AutoReset = true;
			myTimer.Enabled = true;
			timer.Add(cVStartCFC.SerialNumber, myTimer);
		}
		else
		{
			OutputOption.TransferData();
		}
	}

	private void DoOutputTransfer(object source, ElapsedEventArgs e)
	{
		MyTimer myTimer = source as MyTimer;
		myTimer.Stop();
		string sn = myTimer.sn;
		if (timer.ContainsKey(sn))
		{
			timer.Remove(sn);
			myTimer.TransferData();
		}
		myTimer.Dispose();
	}
}
