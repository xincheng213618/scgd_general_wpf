using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlowEngineLib.MQTT;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Base;

public class CVServiceProxy
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVServiceProxy));

	protected STNodeOption m_op_end;

	protected STNodeOption m_op_act;

	protected STNodeOption m_in_act_status;

	protected Dictionary<string, CVTransAction> m_trans_action;

	protected bool m_is_out_release;

	protected int _MinTime;

	protected bool _IsPublishStatus;

	protected int _MaxTime;

	protected string m_nodeName;

	protected string m_nodeType;

	protected string serverRespEventName;

	public string Title { get; private set; }

	public string NodeID { get; private set; }

	public string DeviceCode { get; set; }

	public string Token { get; set; }

	public int ZIndex { get; set; }

	public string DefaultPublishTopic => m_nodeType + "/CMD/" + m_nodeName;

	public string DefaultSubscribeTopic => m_nodeType + "/STATUS/" + m_nodeName;

	public CVServiceProxy(string nodeId, string nodeName, string title)
	{
		m_nodeName = nodeName;
		Title = title;
		NodeID = nodeId;
	}

	protected string GetDeviceCode()
	{
		return DeviceCode;
	}

	protected string GetServiceName()
	{
		return m_nodeType + "/CMD/" + m_nodeName;
	}

	public string GetSendTopic()
	{
		string result = DefaultPublishTopic;
		MQTTServiceInfo service = FlowServiceManager.Instance.GetService(m_nodeType, m_nodeName);
		if (service != null)
		{
			result = service.PublishTopic;
		}
		return result;
	}

	public string GetRecvTopic()
	{
		string result = DefaultSubscribeTopic;
		MQTTServiceInfo service = FlowServiceManager.Instance.GetService(m_nodeType, m_nodeName);
		if (service != null)
		{
			result = service.SubscribeTopic;
		}
		return result;
	}

	protected string GetToken()
	{
		return Token;
	}

	public void DoServerTransfer(CVStartCFC action, STNodeOptionEventArgs e)
	{
		CVTransAction cVTransAction = null;
		if (m_trans_action.ContainsKey(action.SerialNumber))
		{
			cVTransAction = m_trans_action[action.SerialNumber];
			cVTransAction.trans_action = action;
		}
		else if (action.IsRunning)
		{
			cVTransAction = new CVTransAction(action);
			m_trans_action.Add(action.SerialNumber, cVTransAction);
			logger.DebugFormat("{0} DoServerTransfer => {1}", Title, action.SerialNumber);
		}
		if (cVTransAction != null)
		{
			CVMQTTRequest actionEvent = getActionEvent(e);
			if (actionEvent != null)
			{
				CVBaseEventCmd cmd = AddActionCmd(cVTransAction, actionEvent);
				string message = JsonConvert.SerializeObject(actionEvent, Formatting.None);
				string token = GetToken();
				MQActionEvent act = new MQActionEvent(actionEvent.MsgID, m_nodeName, GetDeviceCode(), GetSendTopic(), actionEvent.EventName, message, token);
				DoServerTransfer(cVTransAction, act, cmd);
			}
		}
	}

	protected void DoServerTransfer(CVTransAction trans, MQActionEvent act, CVBaseEventCmd cmd)
	{
		logger.DebugFormat("DoServerTransfer => {0}", JsonConvert.SerializeObject(act));
		if (m_in_act_status == null || m_in_act_status.ConnectionCount == 0)
		{
			trans.trans_action.GetStartNode().DoSubscribe(GetRecvTopic(), this);
		}
		trans.ResetStartTime();
		if (m_op_act != null && m_op_act.ConnectionCount > 0)
		{
			act.Topic = GetRecvTopic();
			m_op_act.TransferData(act);
		}
		else
		{
			trans.trans_action.GetStartNode().DoPublish(act);
		}
		Task.Run(delegate
		{
			WaitingOverTime(cmd);
		});
	}

	public void DoServerStatusDataTransfer(object data)
	{
		CVTransAction cVTransAction = null;
		CVServerResponse cVServerResponse = null;
		if (data.GetType() == typeof(CVBaseDataFlowResp))
		{
			CVBaseDataFlowResp cVBaseDataFlowResp = (CVBaseDataFlowResp)data;
			if (cVBaseDataFlowResp.EventName.Equals("Heartbeat"))
			{
				return;
			}
			logger.DebugFormat("{0} DoServerStatusDataTransfer => {1}/{2}", Title, cVBaseDataFlowResp.SerialNumber, cVBaseDataFlowResp.EventName);
			cVTransAction = GetCVTransByEvent(cVBaseDataFlowResp.SerialNumber, cVBaseDataFlowResp.EventName);
			cVServerResponse = GetServerResponse(cVTransAction, cVBaseDataFlowResp);
		}
		else if (data.GetType() == typeof(CVMQTTRequest))
		{
			CVMQTTRequest cVMQTTRequest = (CVMQTTRequest)data;
			if (cVMQTTRequest.EventName.Equals("Heartbeat"))
			{
				return;
			}
			cVTransAction = GetCVTransByEvent(cVMQTTRequest.SerialNumber, cVMQTTRequest.EventName);
			cVServerResponse = GetServerResponse(cVTransAction, cVMQTTRequest);
		}
		if (cVServerResponse != null && cVTransAction != null && cVTransAction.trans_action.FlowStatus == StatusTypeEnum.Runing && cVTransAction.m_sever_actionEvent.ContainsKey(cVServerResponse.Id))
		{
			logger.DebugFormat("DoServerStatusDataTransfer => {0}", JsonConvert.SerializeObject(cVTransAction.trans_action));
			CVBaseEventCmd cVBaseEventCmd = cVTransAction.m_sever_actionEvent[cVServerResponse.Id];
			cVBaseEventCmd.resp = cVServerResponse;
			OnServerResponse(cVServerResponse);
			if (!IsCacheActResponse(cVTransAction, cVServerResponse))
			{
				DoOutAction(cVTransAction, cVBaseEventCmd);
			}
		}
	}

	protected virtual void OnServerResponse(CVServerResponse resp)
	{
	}

	protected CVBaseEventCmd AddActionCmd(CVTransAction trans, CVMQTTRequest sendEvent)
	{
		CVBaseEventCmd cVBaseEventCmd;
		if (trans.m_sever_actionEvent.ContainsKey(sendEvent.MsgID))
		{
			cVBaseEventCmd = trans.m_sever_actionEvent[sendEvent.MsgID];
		}
		else
		{
			cVBaseEventCmd = new CVBaseEventCmd(sendEvent, null);
			trans.m_sever_actionEvent.Add(sendEvent.MsgID, cVBaseEventCmd);
		}
		_ = cVBaseEventCmd.resp;
		return cVBaseEventCmd;
	}

	protected void RemoveActionCmd(CVTransAction trans, string key)
	{
		if (trans.m_sever_actionEvent.ContainsKey(key))
		{
			trans.m_sever_actionEvent.Remove(key);
		}
	}

	public void DoMinAction(CVTransAction trans, CVBaseEventCmd cmd)
	{
		Task.Run(async delegate
		{
			await Task.Delay(_MinTime);
			DoOutEndTransfer(trans, cmd);
			if (m_op_act != null)
			{
				m_op_act.TransferData(null);
			}
		});
	}

	private void DoOutAction(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (m_is_out_release)
		{
			logger.DebugFormat("{0} DoOutAction => {1}/{2}", Title, trans.trans_action.SerialNumber, cmd.cmd.MsgID);
			m_trans_action.Remove(trans.trans_action.SerialNumber);
		}
		else
		{
			RemoveActionCmd(trans, resp.Id);
		}
		if (_MinTime > 0 && resp.Status == ActionStatusEnum.Finish)
		{
			DoMinAction(trans, cmd);
			return;
		}
		DoOutEndTransfer(trans, cmd);
		if (m_op_act != null)
		{
			m_op_act.TransferData(null);
		}
	}

	private void DoOutEndTransfer(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (resp.Status == ActionStatusEnum.Finish)
		{
			trans.trans_action.SetStatusType(StatusTypeEnum.Runing);
			if (cmd.resp.Data != null)
			{
				trans.trans_action.Data["MasterId"] = (object)cmd.resp.Data.MasterId;
			}
		}
		else if (resp.Status == ActionStatusEnum.Failed)
		{
			trans.trans_action.SetStatusType(StatusTypeEnum.Failed);
			if (!trans.trans_action.Data.ContainsKey("Msg"))
			{
				trans.trans_action.Data.Add("Msg", cmd.resp.Message);
			}
			else
			{
				trans.trans_action.Data["Msg"] = cmd.resp.Message;
			}
		}
		if (_IsPublishStatus)
		{
			trans.DoPublishStatus(GetServiceName(), GetDeviceCode(), GetFullNodeName(), resp);
		}
		else
		{
			trans.AddTTL();
		}
		m_op_end.TransferData(trans.trans_action);
		cmd.waiter.SignalMessageReceived();
	}

	protected virtual CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest cVMQTTRequest = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		CVBaseEventObj baseEvent = getBaseEvent(cVStartCFC);
		if (baseEvent != null)
		{
			cVMQTTRequest = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), baseEvent.EventName, cVStartCFC.SerialNumber, baseEvent.Data, GetToken());
			cVMQTTRequest.ZIndex = ZIndex;
		}
		return cVMQTTRequest;
	}

	protected virtual CVBaseEventObj getBaseEvent(CVStartCFC start)
	{
		return new CVBaseEventObj
		{
			EventName = serverRespEventName,
			Data = getBaseEventData(start)
		};
	}

	protected virtual object getBaseEventData(CVStartCFC start)
	{
		return new CommonEventData(start.SerialNumber, "");
	}

	private bool IsCacheActResponse(CVTransAction trans, CVServerResponse status)
	{
		return trans.trans_action.FlowStatus == StatusTypeEnum.Paused;
	}

	private void WaitingOverTime(CVBaseEventCmd cmd)
	{
		CVMQTTRequest cmd2 = cmd.cmd;
		int maxDelay = GetMaxDelay();
		Task<bool> task = cmd.waiter.WaitForMessage(maxDelay);
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("{0}/{1}/{2}/{3} => Task.WaitOverTime={4}[{5} ms]", Title, DeviceCode, ZIndex, NodeID, task.Result, maxDelay);
		}
		if (task.Result)
		{
			return;
		}
		CVTransAction cVTransAction = RemoveTrans(cmd2.SerialNumber, cmd2.MsgID);
		if (cVTransAction != null)
		{
			cVTransAction.NodeOverTime(GetFullNodeName());
			if (logger.IsInfoEnabled)
			{
				logger.InfoFormat("{0}/{1}/{2}/{3} => OverTime", Title, DeviceCode, ZIndex, NodeID);
			}
			Reset();
			m_op_end.TransferData(cVTransAction.trans_action);
		}
	}

	private CVTransAction RemoveTrans(string serialNumber, string svrEventId)
	{
		if (m_trans_action.ContainsKey(serialNumber))
		{
			CVTransAction cVTransAction = m_trans_action[serialNumber];
			if (cVTransAction.m_sever_actionEvent.ContainsKey(svrEventId))
			{
				logger.DebugFormat("{0} RemoveTrans => {1}/{2}", Title, serialNumber, svrEventId);
				m_trans_action.Remove(serialNumber);
				return cVTransAction;
			}
		}
		return null;
	}

	protected virtual int GetMaxDelay()
	{
		return _MaxTime;
	}

	protected virtual void Reset()
	{
	}

	private string GetFullNodeName()
	{
		return Title + "." + m_nodeName;
	}

	private CVTransAction GetCVTransByEvent(string serialNumber, string eventName)
	{
		if (!string.IsNullOrEmpty(serialNumber) && m_trans_action.ContainsKey(serialNumber))
		{
			CVTransAction result = m_trans_action[serialNumber];
			if (string.IsNullOrEmpty(eventName))
			{
				return result;
			}
			if (eventName.Equals(serverRespEventName))
			{
				return result;
			}
		}
		return null;
	}

	private CVServerResponse GetServerResponse(CVTransAction trans, CVBaseDataFlowResp statusEvent)
	{
		CVServerResponse cVServerResponse = null;
		if (statusEvent.Code == 0)
		{
			return new CVServerResponse(statusEvent.MsgID, ActionStatusEnum.Finish, statusEvent.Message, statusEvent.EventName, statusEvent.Data);
		}
		return new CVServerResponse(statusEvent.MsgID, ActionStatusEnum.Failed, statusEvent.Message, statusEvent.EventName, statusEvent.Data);
	}

	private CVServerResponse GetServerResponse(CVTransAction trans, CVMQTTRequest statusEvent)
	{
		CVServerResponse cVServerResponse = null;
		if (statusEvent.Data != null)
		{
			cVServerResponse = JsonConvert.DeserializeObject<CVServerResponse>(statusEvent.Data.ToString());
			cVServerResponse.EventName = statusEvent.EventName;
		}
		else
		{
			cVServerResponse = new CVServerResponse(trans.m_sever_actionEvent.First().Key, ActionStatusEnum.Failed, "Data is null", statusEvent.EventName, statusEvent.Data);
		}
		return cVServerResponse;
	}
}
