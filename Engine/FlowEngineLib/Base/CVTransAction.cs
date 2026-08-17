using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FlowEngineLib.Base;

public class CVTransAction
{
	public CVStartCFC trans_action;

	public DateTime startTime;

	public Dictionary<string, CVBaseEventCmd> m_sever_actionEvent;

	private readonly object actionEventsLock = new object();

	private int canceled;

	public bool IsCanceled => Volatile.Read(ref canceled) != 0;

	public CVTransAction(CVStartCFC trans_action)
	{
		this.trans_action = trans_action;
		startTime = DateTime.Now;
		m_sever_actionEvent =
			new Dictionary<string, CVBaseEventCmd>(
				StringComparer.Ordinal);
	}

	public void AddTTL()
	{
		trans_action.AddTTL(startTime);
	}

	public void DoPublishStatus(string serverName, string deviceCode, string nodeName, CVServerResponse status, int zIdx)
	{
		trans_action.AddResult(nodeName, status, startTime);
		trans_action.BuildStatusMsg(serverName, deviceCode, zIdx);
		trans_action.GetStartNode().DoPublishStatus(serverName);
	}

	public void NodeOverTime(string nodeName, string nodeId = "")
	{
		trans_action.OverTime(nodeName, startTime, nodeId);
		FinishFlow();
	}

	public void NodeFinished(string masterValue, int masterId, int masterResultType)
	{
		trans_action.MasterValue(masterValue, masterId, masterResultType);
	}

	public void NodeFinished(string nodeType, dynamic respData)
	{
		if (!MasterResultDataHelper.TryRead(respData, nodeType, out string masterValue, out int masterId, out int masterResultType))
		{
			return;
		}

		NodeFinished(masterValue, masterId, masterResultType);
	}

	public void NodeFailed(string msg, string nodeName, string nodeId = "")
	{
		trans_action.Failed(msg, nodeName, startTime, nodeId);
		FinishFlow();
	}

	private void FinishFlow()
	{
		if (trans_action.TryDoFinishing())
		{
			trans_action.FireFinished();
		}
	}

	public void Cancel()
	{
		CVBaseEventCmd[] commands;
		lock (actionEventsLock)
		{
			Interlocked.Exchange(ref canceled, 1);
			commands = m_sever_actionEvent.Values.ToArray();
		}
		foreach (CVBaseEventCmd command in commands)
		{
			command.waiter.SignalMessageReceived();
		}
	}

	public void ResetStartTime()
	{
		startTime = DateTime.Now;
	}

	internal CVBaseEventCmd GetOrAddActionCommand(
		CVMQTTRequest request)
	{
		lock (actionEventsLock)
		{
			if (m_sever_actionEvent.TryGetValue(
					request.MsgID,
					out CVBaseEventCmd existing))
			{
				return existing;
			}

			var command = new CVBaseEventCmd(
				request,
				null);
			m_sever_actionEvent.Add(request.MsgID, command);
			return command;
		}
	}

	internal bool TryStartActionCommand(
		CVMQTTRequest request,
		bool allowStoppedFlow,
		out CVBaseEventCmd command)
	{
		lock (actionEventsLock)
		{
			if (IsCanceled || (!allowStoppedFlow && !trans_action.IsRunning))
			{
				command = null;
				return false;
			}

			if (m_sever_actionEvent.TryGetValue(
					request.MsgID,
					out command))
			{
				return true;
			}

			command = new CVBaseEventCmd(
				request,
				null);
			m_sever_actionEvent.Add(request.MsgID, command);
			return true;
		}
	}

	internal bool TryAddActionCommand(
		string messageId,
		CVBaseEventCmd command)
	{
		lock (actionEventsLock)
		{
			if (IsCanceled)
			{
				return false;
			}
			return m_sever_actionEvent.TryAdd(
				messageId,
				command);
		}
	}

	internal bool TryGetActionCommand(
		string messageId,
		out CVBaseEventCmd command)
	{
		lock (actionEventsLock)
		{
			return m_sever_actionEvent.TryGetValue(
				messageId,
				out command);
		}
	}

	internal bool TryTakeActionCommand(
		string messageId,
		out CVBaseEventCmd command)
	{
		lock (actionEventsLock)
		{
			if (!m_sever_actionEvent.TryGetValue(
					messageId,
					out command))
			{
				return false;
			}

			m_sever_actionEvent.Remove(messageId);
			return true;
		}
	}

	internal CVBaseEventCmd[] GetActionCommandsSnapshot()
	{
		lock (actionEventsLock)
		{
			return m_sever_actionEvent.Values.ToArray();
		}
	}

	internal KeyValuePair<string, CVBaseEventCmd>[]
		GetActionCommandPairsSnapshot()
	{
		lock (actionEventsLock)
		{
			return m_sever_actionEvent.ToArray();
		}
	}

	internal bool TryGetFirstActionCommandKey(
		out string messageId)
	{
		lock (actionEventsLock)
		{
			if (m_sever_actionEvent.Count == 0)
			{
				messageId = string.Empty;
				return false;
			}

			messageId = m_sever_actionEvent.First().Key;
			return true;
		}
	}
}
