using System;
using System.Collections.Generic;
using FlowEngineLib.Start;
using Newtonsoft.Json;

namespace FlowEngineLib.Base;

public class CVStartCFC : CVBaseCFC
{
	private sealed class FlowFinishState
	{
		public readonly object Lock = new object();

		public bool IsFinished;
	}

	[JsonIgnore]
	private BaseStartNode StartNode;

	[JsonIgnore]
	private FlowFinishState finishState = new FlowFinishState();

	public CVStartCFC()
		: this("")
	{
	}

	public CVStartCFC(StatusTypeEnum statusType, Dictionary<string, object> data)
		: base(statusType, data)
	{
	}

	public CVStartCFC(CVStartCFC startCFC)
		: this(startCFC.FlowStatus, startCFC.Data)
	{
		StartNode = startCFC.StartNode;
		finishState = startCFC.finishState;
		StartTime = startCFC.StartTime;
		EndTime = startCFC.EndTime;
		Id = startCFC.Id;
		SerialNumber = startCFC.SerialNumber;
		IsDel = startCFC.IsDel;
		TTL = startCFC.TTL;
		SetActionType(startCFC.GetActionType());
		FlowStatus = startCFC.FlowStatus;
	}

	public CVStartCFC(string sn)
		: this(ActionTypeEnum.Start, sn)
	{
	}

	public CVStartCFC(ActionTypeEnum actionType, string sn)
		: this(null, actionType, sn)
	{
	}

	public CVStartCFC(BaseStartNode startNode, ActionTypeEnum actionType, string sn)
		: base(sn, actionType)
	{
		StartNode = startNode;
	}

	public void DoFinishing()
	{
		lock (finishState.Lock)
		{
			if (finishState.IsFinished)
			{
				return;
			}
			finishState.IsFinished = true;
			DoFinishingCore();
		}
	}

	public bool TryDoFinishing()
	{
		lock (finishState.Lock)
		{
			if (finishState.IsFinished)
			{
				return false;
			}
			finishState.IsFinished = true;
			DoFinishingCore();
			return true;
		}
	}

	private void DoFinishingCore()
	{
		IsDel = true;
		EndTime = DateTime.Now;
		NormalizeStopStatus();
		if (FlowStatus == StatusTypeEnum.Runing)
		{
			FlowStatus = StatusTypeEnum.Completed;
		}
		StartNode?.DoFinishing(this);
	}

	public BaseStartNode GetStartNode()
	{
		return StartNode;
	}

	public void SetStartNode(BaseStartNode startNode)
	{
		StartNode = startNode;
	}

	public bool IsSameFlow(CVStartCFC other)
	{
		return other != null && string.Equals(SerialNumber, other.SerialNumber, StringComparison.Ordinal);
	}

	public string BuildStatusMsg(string serverCode, string deviceCode, int zIdx)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("TTL", TTL);
		foreach (KeyValuePair<string, object> datum in Data)
		{
			dictionary.Add(datum.Key, datum.Value);
		}
		return JsonConvert.SerializeObject(new CVMQTTRequest(serverCode, deviceCode, FlowStatus.ToString(), SerialNumber, dictionary, string.Empty, zIdx), Formatting.None);
	}

	public TimeSpan GetTotalTime()
	{
		return EndTime - StartTime;
	}

	public string ToShortString()
	{
		return SerialNumber + "/" + FlowStatus.ToString() + "/" + StartTime.ToString("O");
	}

	public void FireFinished()
	{
		if (StartNode != null)
		{
			StartNode?.FireFinished(this);
		}
	}
}
