using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FlowEngineLib.Base;

public class CVBaseCFC
{
	public DateTime StartTime;

	public DateTime EndTime;

	public long Id;

	public string SerialNumber;

	public bool IsDel;

	public int TTL;

	private ActionTypeEnum ActionType;

	public StatusTypeEnum FlowStatus;

	public Dictionary<string, object> Data;

	[JsonIgnore]
	public bool IsPaused => FlowStatus == StatusTypeEnum.Paused;

	[JsonIgnore]
	public bool IsRunning => FlowStatus == StatusTypeEnum.Runing;

	[JsonIgnore]
	public bool IsStop
	{
		get
		{
			if (FlowStatus != StatusTypeEnum.Canceled)
			{
				return FlowStatus == StatusTypeEnum.Failed;
			}
			return true;
		}
	}

	public CVBaseCFC(string sn, ActionTypeEnum actionType)
		: this(DateTime.Now.Ticks, sn, actionType)
	{
	}

	public CVBaseCFC(long id, string sn, ActionTypeEnum actionType)
	{
		StartTime = DateTime.Now;
		Id = id;
		TTL = 0;
		IsDel = false;
		SerialNumber = sn;
		ActionType = actionType;
		Data = new Dictionary<string, object>();
		setStatus();
	}

	public CVBaseCFC(StatusTypeEnum statusType, Dictionary<string, object> data)
	{
		FlowStatus = statusType;
		Data = new Dictionary<string, object>(data);
	}

	public string GetSerialNumber()
	{
		return SerialNumber;
	}

	public void SetStatusType(StatusTypeEnum statusType)
	{
		FlowStatus = statusType;
	}

	public void Failed(string message, string nodeName, DateTime startTime)
	{
		SetStatusType(StatusTypeEnum.Failed);
		AddData("Msg", message);
		AddData("ErrorNodeName", nodeName);
		AddResultStatus(nodeName, FlowStatus.ToString(), startTime);
	}

	internal ActionTypeEnum GetActionType()
	{
		return ActionType;
	}

	public void SetActionType(ActionTypeEnum actionType)
	{
		ActionType = actionType;
		setStatus();
	}

	protected void setStatus()
	{
		switch (ActionType)
		{
		case ActionTypeEnum.Start:
			FlowStatus = StatusTypeEnum.Runing;
			break;
		case ActionTypeEnum.Pause:
			FlowStatus = StatusTypeEnum.Paused;
			break;
		case ActionTypeEnum.Stop:
			FlowStatus = StatusTypeEnum.Canceled;
			break;
		case ActionTypeEnum.Fail:
			FlowStatus = StatusTypeEnum.Failed;
			break;
		default:
			FlowStatus = StatusTypeEnum.Failed;
			break;
		}
	}

	public void AddTTL(DateTime startTime)
	{
		TTL += (int)(DateTime.Now - startTime).TotalSeconds;
	}

	public void AddResult(string nodeName, CVServerResponse status, DateTime startTime)
	{
		AddResultStatus(nodeName, status.Status.ToString(), startTime);
	}

	public void OverTime(string nodeName, DateTime startTime)
	{
		SetStatusType(StatusTypeEnum.OverTime);
		AddResultStatus(nodeName, FlowStatus.ToString(), startTime);
		AddData("ErrorNodeName", nodeName);
	}

	private void AddResultStatus(string nodeName, string status, DateTime startTime)
	{
		AddTTL(startTime);
		AddData(nodeName, status);
	}

	private void AddData(string nodeName, object status)
	{
		if (Data.ContainsKey(nodeName))
		{
			Data[nodeName] = status;
		}
		else
		{
			Data.Add(nodeName, status);
		}
	}

	public bool GetDataValueInt(string key, ref int value)
	{
		if (Data.ContainsKey(key) && Data[key] != null)
		{
			value = Convert.ToInt32(Data[key]);
			return true;
		}
		return false;
	}

	public bool GetDataValueString(string key, ref string value)
	{
		if (Data.ContainsKey(key) && Data[key] != null)
		{
			value = Data[key].ToString();
			return true;
		}
		return false;
	}

	public void MasterValue(string masterValue, int masterId, int masterResultType)
	{
		AddData("MasterValue", masterValue);
		AddData("MasterId", masterId);
		AddData("MasterResultType", masterResultType);
	}
}
