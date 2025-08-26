using System;
using System.Collections.Generic;

namespace FlowEngineLib.Base;

public class CVTransAction
{
	public CVStartCFC trans_action;

	public DateTime startTime;

	public Dictionary<string, CVBaseEventCmd> m_sever_actionEvent;

	public CVTransAction(CVStartCFC trans_action)
	{
		this.trans_action = trans_action;
		startTime = DateTime.Now;
		m_sever_actionEvent = new Dictionary<string, CVBaseEventCmd>();
	}

	public void AddTTL()
	{
		trans_action.AddTTL(startTime);
	}

	public void DoPublishStatus(string serverName, string deviceCode, string nodeName, CVServerResponse status)
	{
		trans_action.AddResult(nodeName, status, startTime);
		trans_action.BuildStatusMsg(serverName, deviceCode);
		trans_action.GetStartNode().DoPublishStatus(serverName);
	}

	public void NodeOverTime(string nodeName)
	{
		trans_action.OverTime(nodeName, startTime);
	}

	public void NodeFinished(string masterValue, int masterId, int masterResultType)
	{
		trans_action.MasterValue(masterValue, masterId, masterResultType);
	}

	public void NodeFailed(string msg, string nodeName)
	{
		trans_action.Failed(msg, nodeName, startTime);
	}

	public void Cancel()
	{
		foreach (KeyValuePair<string, CVBaseEventCmd> item in m_sever_actionEvent)
		{
			item.Value.waiter.SignalMessageReceived();
		}
	}

	public void ResetStartTime()
	{
		startTime = DateTime.Now;
	}
}
