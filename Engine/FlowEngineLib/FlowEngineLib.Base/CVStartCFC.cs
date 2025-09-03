using System;
using System.Collections.Generic;
using FlowEngineLib.Start;
using Newtonsoft.Json;

namespace FlowEngineLib.Base;

public class CVStartCFC : CVBaseCFC
{
	[JsonIgnore]
	private BaseStartNode StartNode;

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
		IsDel = true;
		EndTime = DateTime.Now;
		if (FlowStatus == StatusTypeEnum.Runing)
		{
			FlowStatus = StatusTypeEnum.Completed;
		}
		if (StartNode != null)
		{
			StartNode?.DoFinishing(this);
		}
	}

	public BaseStartNode GetStartNode()
	{
		return StartNode;
	}

	public void SetStartNode(BaseStartNode startNode)
	{
		StartNode = startNode;
	}

	public string BuildStatusMsg(string serverCode, string deviceCode)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		dictionary.Add("TTL", TTL);
		foreach (KeyValuePair<string, object> datum in Data)
		{
			dictionary.Add(datum.Key, datum.Value);
		}
		return JsonConvert.SerializeObject(new CVMQTTRequest(serverCode, deviceCode, FlowStatus.ToString(), SerialNumber, dictionary, string.Empty), Formatting.None);
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
