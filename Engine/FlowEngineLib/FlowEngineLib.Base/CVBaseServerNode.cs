using FlowEngineLib.Algorithm;
using FlowEngineLib.MQTT;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Transactions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FlowEngineLib.Base;

public class CVBaseServerNode : CVCommonNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVBaseServerNode));

	protected string _Token;

	protected int _MinTime;

	protected int _MaxTime;

	protected bool _IsPublishStatus;

	protected STNodeOption m_op_act;

	protected STNodeOption m_op_end;

	protected STNodeOption m_in_start;

	protected STNodeOption m_in_act_status;

	protected Dictionary<string, CVTransAction> m_trans_action;

	protected bool m_is_out_release;

	protected string operatorCode;

	protected bool m_has_svr_item;

	protected Rectangle m_custom_item;

	protected string m_in_text;

	protected STNodeDevText devNameCtrl;

	[STNodeProperty("Token", "Token", true)]
	public string Token
	{
		get
		{
			return _Token;
		}
		set
		{
			_Token = value;
		}
	}

	[STNodeProperty("最小超时", "最小超时", false, true)]
	public int MinTime
	{
		get
		{
			return _MinTime;
		}
		set
		{
			_MinTime = value;
		}
	}

	[STNodeProperty("最大超时", "最大超时", false, false)]
	public int MaxTime
	{
		get
		{
			return _MaxTime;
		}
		set
		{
			_MaxTime = value;
		}
	}

	[STNodeProperty("状态触发", "是否触发状态事件", false, true)]
	public bool IsPublishStatus
	{
		get
		{
			return _IsPublishStatus;
		}
		set
		{
			_IsPublishStatus = value;
		}
	}

	protected string opEventName => operatorCode;

	public string DefaultPublishTopic => m_nodeType + "/CMD/" + m_nodeName;

	public string DefaultSubscribeTopic => m_nodeType + "/STATUS/" + m_nodeName;

	protected CVBaseServerNode(string title, string nodeType, string nodeName, string deviceCode)
		: base(title, nodeType, nodeName, deviceCode)
	{
		Init();
	}

	protected CVBaseServerNode(string title, string nodeType)
		: this(title, nodeType, "S01", "DEV01")
	{
	}

	private void Init()
	{
		m_in_text = "IN";
		operatorCode = "Finish";
		m_has_svr_item = false;
		m_is_out_release = true;
		_MinTime = -1;
		_MaxTime = 5000;
		_IsPublishStatus = false;
		base.AutoSize = false;
		base.Width = 150;
		base.Height = 85;
		base.TitleHeight += 10;
		m_custom_item = new Rectangle(5, 30, 140, 18);
	}

	protected override string OnGetDrawTitle()
	{
		return $"{base.Title}\r\n{base.DeviceCode}";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_start = base.InputOptions.Add(m_in_text, typeof(CVStartCFC), bSingle: true);
		m_op_end = base.OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		if (m_has_svr_item)
		{
			m_in_act_status = base.InputOptions.Add("IN_SVR_RESP", typeof(CVMQTTRequest), bSingle: true);
			m_op_act = base.OutputOptions.Add("OUT_SVR", typeof(MQActionEvent), bSingle: false);
			m_in_act_status.Connected += m_in_op_Connected;
			m_in_act_status.DataTransfer += m_in_act_status_DataTransfer;
		}
		m_in_start.Connected += m_in_op_Connected;
		m_in_start.DataTransfer += m_in_start_DataTransfer;
		m_trans_action = new Dictionary<string, CVTransAction>();
	}

	private void WaitingOverTime(CVBaseEventCmd cmd)
	{
		CVMQTTRequest cmd2 = cmd.cmd;
		int maxDelay = GetMaxDelay();
		Task<bool> task = cmd.waiter.WaitForMessage(maxDelay);
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("[{0}]Task.WaitOverTime => {1}", ToShortString(), task.Result);
		}
		if (task.Result)
		{
			return;
		}
		CVTransAction cVTransAction = RemoveTrans(cmd2.SerialNumber, cmd2.MsgID);
		if (cVTransAction != null)
		{
			if (logger.IsInfoEnabled)
			{
				logger.InfoFormat("[{0}]OverTime => {1} ms", ToShortString(), maxDelay);
			}
			cVTransAction.NodeOverTime(GetFullNodeName());
			Reset();
			m_op_end.TransferData(cVTransAction.trans_action);
		}
		else
		{
			logger.WarnFormat("[{0}]MQTTRequest not exist => {1}", ToShortString(), cmd2.SerialNumber);
		}
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
		return base.Title + "." + m_nodeName;
	}

	private CVTransAction RemoveTrans(string serialNumber, string svrEventId)
	{
		if (m_trans_action.ContainsKey(serialNumber))
		{
			CVTransAction cVTransAction = m_trans_action[serialNumber];
			if (cVTransAction.m_sever_actionEvent.ContainsKey(svrEventId))
			{
				logger.DebugFormat("[{0}]RemoveTrans => {1}/{2}", ToShortString(), serialNumber, svrEventId);
				m_trans_action.Remove(serialNumber);
				return cVTransAction;
			}
		}
		return null;
	}

	protected string GetServiceName()
	{
		return m_nodeName;
	}

	protected string GetDeviceCode()
	{
		return base.DeviceCode;
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

	protected virtual void m_in_op_Connected(object sender, STNodeOptionEventArgs e)
	{
		STNode owner = e.TargetOption.Owner;
		string eventName = "";
		if (sender == m_in_start)
		{
			eventName = "Start";
		}
		else if (sender == m_in_act_status)
		{
			eventName = operatorCode;
		}
		if (e.TargetOption.Owner.GetType() == typeof(MQTTSubscribeHub))
		{
			((MQTTSubscribeHub)owner).SetEventInfo(e.TargetOption, eventName, GetRecvTopic(), m_nodeName, m_deviceCode);
		}
	}

	protected void DoTransferToServer(CVStartCFC action, STNodeOptionEventArgs e)
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
				DoTransferToServer(cVTransAction, act, cmd);
			}
		}
	}

	protected void DoTransferToServer(CVTransAction trans, MQActionEvent act, CVBaseEventCmd cmd)
	{
		base.nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs());
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

	public bool DoServerStatusRecv(CVBaseDataFlowResp statusEvent)
	{
		if (statusEvent.ZIndex != base.ZIndex)
		{
			return false;
		}
		string eventName = statusEvent.EventName;
		string serialNumber = statusEvent.SerialNumber;
		if (string.IsNullOrEmpty(eventName) || eventName.Equals("Heartbeat"))
		{
			logger.WarnFormat("[{0}]EventName is Heartbeat or empty => {1}", ToShortString(), eventName);
			return false;
		}
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] {1} => {2}", ToShortString(), eventName, serialNumber);
		}
		CVTransAction cVTransByEvent = GetCVTransByEvent(serialNumber, eventName);
		if (cVTransByEvent != null)
		{
			CVServerResponse serverResponse = GetServerResponse(cVTransByEvent, statusEvent);
			if (serverResponse.Status != ActionStatusEnum.Pending && cVTransByEvent.m_sever_actionEvent.ContainsKey(serverResponse.Id))
			{
				CVBaseEventCmd cVBaseEventCmd = cVTransByEvent.m_sever_actionEvent[serverResponse.Id];
				cVBaseEventCmd.waiter.SignalMessageReceived();
				cVBaseEventCmd.resp = serverResponse;
				OnServerResponse(serverResponse, cVTransByEvent.trans_action);
				if (!IsCacheActResponse(cVTransByEvent, serverResponse))
				{
					DoOutAction(cVTransByEvent, cVBaseEventCmd);
				}
				return true;
			}
		}
		else
		{
			logger.WarnFormat("[{0}] not find request => {1}", ToShortString(), JsonConvert.SerializeObject(statusEvent));
		}
		return false;
	}

	protected virtual void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
	}

	private void m_in_act_status_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (HasData(e))
		{
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("[{0}] recv status => {1}", ToShortString(), JsonConvert.SerializeObject(e.TargetOption.Data));
			}
			DoServerStatusRecv(e.TargetOption.Data as CVBaseDataFlowResp);
		}
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
			if (eventName.Equals(operatorCode))
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
		if (statusEvent.Code == 102)
		{
			return new CVServerResponse(statusEvent.MsgID, ActionStatusEnum.Pending, statusEvent.Message, statusEvent.EventName, statusEvent.Data);
		}
		return new CVServerResponse(statusEvent.MsgID, ActionStatusEnum.Failed, statusEvent.Message, statusEvent.EventName, statusEvent.Data);
	}

	protected virtual void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (HasData(e))
		{
			if (e.TargetOption.Data is CVStartCFC cVStartCFC)
			{
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat("[{0}]DoServerTransfer => {1}", ToShortString(), cVStartCFC.ToShortString());
				}

                if (cVStartCFC.FlowStatus == StatusTypeEnum.Runing)
				{
					if (m_trans_action.ContainsKey(cVStartCFC.SerialNumber))
					{
						CVTransAction cVTransAction = m_trans_action[cVStartCFC.SerialNumber];
						cVTransAction.ResetStartTime();
						if (cVTransAction.trans_action.FlowStatus == StatusTypeEnum.Paused)
						{
							foreach (CVBaseEventCmd value in cVTransAction.m_sever_actionEvent.Values)
							{
								if (value.cmd.SerialNumber.Equals(cVStartCFC.SerialNumber))
								{
									DoTransNodeEndOut(cVTransAction, value);
									break;
								}
							}
							return;
						}
						if (cVTransAction.trans_action.FlowStatus != 0)
						{
							DoTransferToServer(cVStartCFC, e);
						}
					}
					else
					{
						DoTransferToServer(cVStartCFC, e);
					}
					return;
				}
				if (cVStartCFC.FlowStatus == StatusTypeEnum.Completed)
				{
					DoTransCompleted(cVStartCFC);
					return;
				}
				CVTransAction cVTransByEvent = GetCVTransByEvent(cVStartCFC.SerialNumber, string.Empty);
				if (cVTransByEvent != null)
				{
					if (logger.IsDebugEnabled)
					{
						logger.DebugFormat("[{0}]DoServerTransfer Cancel.", ToShortString());
					}
					cVTransByEvent.Cancel();
                }

                m_op_end.TransferData(e.TargetOption.Data);
                Reset();
			}
			else
			{
				logger.WarnFormat("TargetData Type is not flow common type => {0}", e.TargetOption.DataType.AssemblyQualifiedName);
			}
		}
		else
		{
			m_op_end.TransferData(e.TargetOption.Data);
			if (m_op_act != null)
			{
				m_op_act.TransferData(null);
			}
		}
	}

	protected CVBaseEventCmd AddActionCmd(CVTransAction trans, CVMQTTRequest sendEvent)
	{
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("Add To Server request => {0}", JsonConvert.SerializeObject(sendEvent));
		}
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
		return cVBaseEventCmd;
	}

	protected bool HasTransAction(string serialNumber, ref CVTransAction trans)
	{
		if (m_trans_action.ContainsKey(serialNumber))
		{
			trans = m_trans_action[serialNumber];
			return true;
		}
		return false;
	}

	protected virtual void DoTransCompleted(CVStartCFC action)
	{
		logger.InfoFormat("[{0}]DoTransCompleted => {1}", ToShortString(), action.SerialNumber);
		release(action.SerialNumber);
		m_op_end.TransferData(action);
	}

	private void DoTransNodeEndOut(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (resp.Status == ActionStatusEnum.Finish)
		{
			dynamic data = resp.Data;
			int masterResultType = -1;
			if (data != null)
			{
				string masterValue = data.MasterValue;
				int masterId = data.MasterId;
				if (data.MasterResultType == null && data.ResultType == null)
				{
					string nodeType = base.NodeType;
					if (!(nodeType == "Spectrum"))
					{
						if (nodeType == "SMU")
						{
							masterResultType = 200;
						}
					}
					else
					{
						masterResultType = 300;
					}
				}
				else
				{
					masterResultType = ((data.MasterResultType != null) ? data.MasterResultType : data.ResultType);
				}
				trans.NodeFinished(masterValue, masterId, masterResultType);
            }
        }
		else if (resp.Status == ActionStatusEnum.Failed)
		{
			trans.NodeFailed(cmd.resp.Message, base.DeviceCode);
			logger.InfoFormat("[{0}]CVTransAction Failed => {1}", ToShortString(), JsonConvert.SerializeObject(trans.trans_action));
		}
		if (_IsPublishStatus)
		{
			trans.DoPublishStatus(GetServiceName(), GetDeviceCode(), GetFullNodeName(), resp);
		}
		else
		{
			trans.AddTTL();
		}
		TimeSpan timeSpan = DateTime.Now - trans.startTime;
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("[{0}]Node completed. Transfer to the next node. TotalTime={1}/{2}", ToShortString(), timeSpan.ToString(), trans.startTime.ToString("O"));
		}

        if (logger.IsDebugEnabled)
            logger.Debug($"nodeEndEvent {this.GetType()}: {JsonConvert.SerializeObject(trans.trans_action)}");
		CVStartCFC cVStartCFC = new CVStartCFC();
		cVStartCFC.StartTime = trans.trans_action.StartTime;
        cVStartCFC.Id = trans.trans_action.Id;
        cVStartCFC.TTL = trans.trans_action.TTL;
        cVStartCFC.IsDel = trans.trans_action.IsDel;
        cVStartCFC.SerialNumber = trans.trans_action.SerialNumber;
        cVStartCFC.Data = trans.trans_action.Data;
		cVStartCFC.FlowStatus = trans.trans_action.FlowStatus;
		cVStartCFC.SetActionType(trans.trans_action.GetActionType());
        cVStartCFC.SetStartNode(trans.trans_action.GetStartNode());

        trans.trans_action = cVStartCFC;
        m_op_end.TransferData(trans.trans_action);
		if (logger.IsDebugEnabled)
			logger.Debug($"nodeEndEvent {this.GetType()}: {JsonConvert.SerializeObject(trans.trans_action)}");
        base.nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs());
	}

	protected virtual void release(string serialNumber)
	{
		if (m_trans_action.ContainsKey(serialNumber))
		{
			CVTransAction cVTransAction = m_trans_action[serialNumber];
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("{0} release => {1}", ToShortString(), cVTransAction.trans_action.SerialNumber);
			}
			m_trans_action.Remove(serialNumber);
		}
		if (m_op_act != null)
		{
			m_op_act.TransferData(null);
		}
		Reset();
	}

	protected virtual CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest cVMQTTRequest = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		CVBaseEventObj baseEvent = getBaseEvent(cVStartCFC);
		if (baseEvent != null)
		{
			cVMQTTRequest = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), baseEvent.EventName, cVStartCFC.SerialNumber, baseEvent.Data, GetToken());
			cVMQTTRequest.ZIndex = base.ZIndex;
		}
		return cVMQTTRequest;
	}

	protected virtual CVBaseEventObj getBaseEvent(CVStartCFC start)
	{
		return new CVBaseEventObj
		{
			Data = getBaseEventData(start),
			EventName = operatorCode
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

	protected void RemoveActionCmd(CVTransAction trans, string key)
	{
		if (trans.m_sever_actionEvent.ContainsKey(key))
		{
			trans.m_sever_actionEvent.Remove(key);
		}
	}

	private void DoOutAction(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (m_is_out_release)
		{
			logger.DebugFormat("[{0}]Remove request => {1}/{2}", ToShortString(), trans.trans_action.SerialNumber, cmd.cmd.MsgID);
			m_trans_action.Remove(trans.trans_action.SerialNumber);
		}
		else
		{
			RemoveActionCmd(trans, resp.Id);
		}
		Task.Run(delegate
		{
			DoNodeCompleted(trans, cmd);
		});
	}

	private void DoNodeCompleted(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (_MinTime > 0 && resp.Status == ActionStatusEnum.Finish)
		{
			new LockFreeMessageWaiter().WaitForMessage(_MinTime);
		}
		DoTransNodeEndOut(trans, cmd);
		if (m_op_act != null)
		{
			m_op_act.TransferData(null);
		}
	}

	protected string GetTokenHide()
	{
		string result = string.Empty;
		MQTTServiceInfo service = FlowServiceManager.Instance.GetService(m_nodeType, m_nodeName);
		if (service != null)
		{
			result = service.Token;
		}
		return result;
	}

	protected void getPreStepParam(CVStartCFC start, AlgorithmPreStepParam param)
	{
		int value = -1;
		int masterResultType = -1;
		string key = "MasterResultType";
		string value2 = string.Empty;
		if (start.GetDataValueString(key, ref value2))
		{
			masterResultType = Convert.ToInt32(value2);
		}
		key = "MasterId";
		start.GetDataValueInt(key, ref value);
		key = "MasterValue";
		if (start.GetDataValueString(key, ref value2))
		{
			param.MasterValue = value2;
		}
		param.MasterId = value;
		param.MasterResultType = masterResultType;
	}

	protected FileExtType GetImageFileType(string fileName)
	{
		FileExtType result = FileExtType.None;
		if (!string.IsNullOrEmpty(fileName))
		{
			string text = Path.GetExtension(fileName).ToLower();
			result = (text.Contains("tif") ? FileExtType.Tif : ((!text.Contains("cvraw")) ? ((!text.Contains("cvcie")) ? FileExtType.Tif : FileExtType.CIE) : FileExtType.Raw));
		}
		return result;
	}
}
