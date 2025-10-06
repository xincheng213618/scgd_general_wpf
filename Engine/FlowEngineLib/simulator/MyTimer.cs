using System;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.simulator;

internal class MyTimer : System.Timers.Timer
{
	public STNodeOption OutputOption;

	public string sn;

	public DateTime startTime;

	public MyTimer(double interval, STNodeOption outputOption)
		: base(interval)
	{
		startTime = DateTime.Now;
		OutputOption = outputOption;
		CVStartCFC cVStartCFC = (CVStartCFC)OutputOption.Data;
		sn = cVStartCFC.SerialNumber;
	}

	internal void TransferData(ActionTypeEnum actionType)
	{
		CVStartCFC obj = (CVStartCFC)OutputOption.Data;
		obj.SetActionType(actionType);
		obj.AddTTL(startTime);
		OutputOption.TransferData();
	}

	internal void TransferData()
	{
		((CVStartCFC)OutputOption.Data)?.AddTTL(startTime);
		OutputOption.TransferData();
	}
}
