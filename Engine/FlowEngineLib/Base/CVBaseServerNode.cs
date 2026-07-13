using FlowEngineLib.Algorithm;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Algorithm;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FlowEngineLib.Base;

public class CVBaseServerNode : CVCommonNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVBaseServerNode));

	protected string _Token;
	protected int _MaxTime;
	protected bool _ContinueOnFail;

	protected STNodeOption m_op_svr_out_act;

	protected STNodeOption m_op_end;

	protected STNodeOption m_in_start;

	protected STNodeOption m_in_act_status;

	protected ConcurrentDictionary<string, CVTransAction> m_trans_action;

	protected bool m_is_out_release;

	protected string operatorCode;

	protected string _TempName;

	protected int _TempId;

	protected STNodeEditText<string> m_ctrl_temp;

	protected string _ImgFileName;

	protected bool m_has_svr_item;

	protected Rectangle m_custom_item;

	protected string m_in_text;

	protected CVServerResponse svrRecvResp;

	[System.ComponentModel.DataAnnotations.Display(Order = -200)]
    [System.ComponentModel.PropertyEditorTypeAttribute(typeof(FlowEngineLib.PropertyEditor.FlowDeviceNameEditor))]
    [STNodeProperty("设备代码", "设备代码", false, false)]
	public new string DeviceCode
	{
		get
		{
			return base.DeviceCode;
		}
		set
		{
			base.DeviceCode = value;
		}
	}

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


	[STNodeProperty("允许失败继续", "服务返回Fail时按正常流程继续", true)]
	public bool ContinueOnFail
	{
		get
		{
			return _ContinueOnFail;
		}
		set
		{
			_ContinueOnFail = value;
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

    [STNodeProperty("Subtitle", "Subtitle", false, true)]
    public string Subtitle { get => _Subtitle; set { _Subtitle = value; } }
    private string _Subtitle = string.Empty;

    public string TempDisName => _TempName;

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
		_ContinueOnFail = false;
		_TempId = -1;
		_MaxTime = 5000;
		_TempName = "";
		_ImgFileName = string.Empty;
		base.AutoSize = false;
		base.Width = StandardNodeWidth;
		base.Height = 85;
		m_custom_item = new Rectangle(StandardNodeContentPadding, 30, StandardNodeContentWidth, 18);
	}




    public override string OnGetDrawTitle()
	{
		if (!string.IsNullOrWhiteSpace(Subtitle))
		{
            return $"{Subtitle}";

        }
        return $"{base.Title}";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_start = base.InputOptions.Add(m_in_text, typeof(CVStartCFC), bSingle: true);
		m_op_end = base.OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
		if (m_has_svr_item)
		{
			m_in_act_status = base.InputOptions.Add("IN_SVR_RESP", typeof(CVMQTTRequest), bSingle: true);
			m_op_svr_out_act = base.OutputOptions.Add("OUT_SVR", typeof(MQActionEvent), bSingle: false);
			m_in_act_status.Connected += m_in_op_Connected;
			m_in_act_status.DataTransfer += m_in_act_status_DataTransfer;
		}
		m_in_start.Connected += m_in_op_Connected;
		m_in_start.DataTransfer += m_in_start_DataTransfer;
		m_trans_action = new ConcurrentDictionary<string, CVTransAction>();
	}

	public STNodeEditText<string> CreateTempControl(Rectangle rect, string text = "模板:")
	{
		m_ctrl_temp = CreateStringControl(rect, text, TempDisName);
		return m_ctrl_temp;
	}

	public CVTemplateParam BuildTemp()
	{
		return new CVTemplateParam
		{
			ID = _TempId,
			Name = _TempName
		};
	}

	public CVTemplateParam BuildTemp(AlgorithmBaseParam param)
	{
		param.TemplateParam = BuildTemp();
		return param.TemplateParam;
	}

	public void BuildImageParam(string _ImgFileName, CVOLED_COLOR _Color, AlgorithmImageParam _param)
	{
		_param.Color = _Color;
		if (!string.IsNullOrEmpty(_ImgFileName))
		{
			_param.ImgFileName = _ImgFileName;
			_param.FileType = GetImageFileType(_ImgFileName);
		}
		BuildTemp(_param);
	}

    public void BuildImageParam(string _ImgFileName, CVOLED_Channel channel, AlgorithmImageParam _param)
    {
        _param.Channel = channel;
        if (!string.IsNullOrEmpty(_ImgFileName))
        {
            _param.ImgFileName = _ImgFileName;
            _param.FileType = GetImageFileType(_ImgFileName);
        }
        BuildTemp(_param);
    }

    public void BuildImageParam(string _ImgFileName, AlgorithmImageParam _param)
    {
        BuildImageParam(_ImgFileName, CVOLED_COLOR.GREEN, _param);
    }

    public void BuildImageParam(AlgorithmImageParam _param)
    {
        BuildImageParam(_ImgFileName, CVOLED_COLOR.GREEN, _param);
    }

    public void BuildImageParam(CVOLED_COLOR _Color, AlgorithmImageParam _param)
    {
        BuildImageParam(_ImgFileName, _Color, _param);
    }

    public void BuildImageParam(CVOLED_Channel channel, AlgorithmImageParam _param)
    {
        BuildImageParam(_ImgFileName, channel, _param);
    }


	protected void setTempName(string name)
	{
		_TempName = name;
		if (m_ctrl_temp != null)
		{
			m_ctrl_temp.Value = TempDisName;
		}
	}

    private async void WaitingOverTime(CVBaseEventCmd cmd)
    {
		CVMQTTRequest cmd2 = cmd.cmd;
		int maxDelay = GetMaxDelay();

        // 使用异步等待，避免线程池阻塞
        logger.InfoFormat("[{0}] WaitForMessageAsync", ToShortString());
        bool result = await cmd.waiter.WaitForMessageAsync(maxDelay);
		if (logger.IsInfoEnabled)
		{
            if (result)
                logger.InfoFormat("[{0}]Task.Completed successfully", ToShortString());
            else
                logger.InfoFormat("[{0}]Task.Timed out after {1}ms", ToShortString(), maxDelay);
		}
        if (result)
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
			Reset(cVTransAction);
			m_op_end.TransferData(cVTransAction.trans_action);
			base.nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs
			{
				RecvTopic = GetRecvTopic(),
				RecvMsgId = cmd2.MsgID,
				RecvStatusCode = -2,
				RecvStatusMessage = $"OverTime {maxDelay}ms"
			});
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

	protected virtual void Reset(CVTransAction trans)
	{
		if (trans != null)
		{
			Reset(trans.trans_action);
		}
	}

	private string GetFullNodeName()
	{
		return base.Title + "." + m_nodeName;
	}

	private CVTransAction RemoveTrans(string serialNumber, string svrEventId)
	{
		if (m_trans_action.TryGetValue(serialNumber, out var cVTransAction))
		{
			if (cVTransAction.m_sever_actionEvent.ContainsKey(svrEventId))
			{
				logger.DebugFormat("[{0}]RemoveTrans => {1}/{2}", ToShortString(), serialNumber, svrEventId);
				m_trans_action.TryRemove(serialNumber, out _);
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
		if (m_trans_action.TryGetValue(action.SerialNumber, out cVTransAction))
		{
			cVTransAction.trans_action = action;
		}
		else if (action.IsRunning)
		{
			cVTransAction = new CVTransAction(action);
			m_trans_action.TryAdd(action.SerialNumber, cVTransAction);
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
			else
			{
				cVTransAction.NodeFailed("Build MQTT Request failed", GetFullNodeName());
			}
		}
	}

	protected void DoTransferToServer(CVTransAction trans, MQActionEvent act, CVBaseEventCmd cmd)
	{
		svrRecvResp = null;
		base.nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs
		{
			SendTopic = act.Topic,
			SendMsgId = act.MsgID,
			SendEventName = act.EventName,
			SendPayload = act.Message
		});
		if (m_in_act_status == null || m_in_act_status.ConnectionCount == 0)
		{
			trans.trans_action.GetStartNode().DoSubscribe(GetRecvTopic(), this);
		}
		trans.ResetStartTime();
		if (m_op_svr_out_act != null && m_op_svr_out_act.ConnectionCount > 0)
		{
			act.Topic = GetRecvTopic();
			m_op_svr_out_act.TransferData(act);
		}
		else
		{
			trans.trans_action.GetStartNode().DoPublish(act);
		}

		// 使用 Task.Run 启动超时监控，避免阻塞主线程
		Task.Run(() => WaitingOverTime(cmd));
	}

	public bool DoServerStatusRecv(CVBaseDataFlowResp statusEvent)
	{
		if (!IsThisNode(statusEvent))
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
			CVServerResponse cVServerResponse = BuildServerResponse(cVTransByEvent, statusEvent);
			if (cVServerResponse.Status != ActionStatusEnum.Pending && cVTransByEvent.m_sever_actionEvent.ContainsKey(cVServerResponse.Id))
			{
				CVBaseEventCmd cVBaseEventCmd = cVTransByEvent.m_sever_actionEvent[cVServerResponse.Id];
				cVBaseEventCmd.waiter.SignalMessageReceived();
				cVBaseEventCmd.resp = cVServerResponse;
				OnServerResponse(cVServerResponse, cVTransByEvent.trans_action);
				if (!IsCacheActResponse(cVTransByEvent, cVServerResponse))
				{
					DoThisNodeCompleted(cVTransByEvent, cVBaseEventCmd);
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

	private bool IsThisNode(CVBaseDataFlowResp statusEvent)
	{
		if (statusEvent.ZIndex == base.ZIndex && statusEvent.EventName == operatorCode)
		{
			return true;
		}
		return false;
	}

	protected virtual void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
		svrRecvResp = resp;
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
		if (!string.IsNullOrEmpty(serialNumber) && m_trans_action.TryGetValue(serialNumber, out var result))
		{
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

	private CVServerResponse BuildServerResponse(CVTransAction trans, CVBaseDataFlowResp statusEvent)
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
		if (e.Status != ConnectionStatus.Connected)
		{
			return;
		}
		if (HasData(e))
		{
			if (e.TargetOption.Data is CVStartCFC { SerialNumber: var serialNumber } cVStartCFC)
			{
				CVTransAction cVTransByEvent = GetCVTransByEvent(serialNumber, string.Empty);
				if (logger.IsDebugEnabled)
				{
					logger.DebugFormat("[{0}]DoServerTransfer => {1}", ToShortString(), cVStartCFC.ToShortString());
				}
				cVStartCFC.NormalizeStopStatus();
				if (ShouldEndFlowImmediately(cVStartCFC))
				{
					FinishFlow(cVStartCFC);
					if (cVTransByEvent != null)
					{
						cVTransByEvent.Cancel();
						Reset(cVTransByEvent);
					}
					else
					{
						Reset(cVStartCFC);
					}
					m_op_end.TransferData(e.TargetOption.Data);
					return;
				}
				if (cVStartCFC.FlowStatus == StatusTypeEnum.Runing)
				{
					if (cVTransByEvent != null)
					{
						cVTransByEvent.ResetStartTime();
						if (cVTransByEvent.trans_action.FlowStatus == StatusTypeEnum.Paused)
						{
							foreach (CVBaseEventCmd value in cVTransByEvent.m_sever_actionEvent.Values)
							{
								if (value.cmd.SerialNumber.Equals(serialNumber))
								{
									DoTransNodeEndOut(cVTransByEvent, value);
									break;
								}
							}
							return;
						}
						if (cVTransByEvent.trans_action.FlowStatus != StatusTypeEnum.Runing)
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
					DoTransCompleted(cVTransByEvent, cVStartCFC);
					return;
				}
				if (cVTransByEvent != null)
				{
					if (logger.IsDebugEnabled)
					{
						logger.DebugFormat("[{0}]DoServerTransfer Cancel.", ToShortString());
					}
					cVTransByEvent.Cancel();
					Reset(cVTransByEvent);
				}
				else
				{
					Reset(cVStartCFC);
				}
				m_op_end.TransferData(e.TargetOption.Data);
			}
			else
			{
				logger.WarnFormat("TargetData Type is not flow common type => {0}", e.TargetOption.DataType.AssemblyQualifiedName);
			}
		}
		else
		{
			m_op_end.TransferData(e.TargetOption.Data);
			if (m_op_svr_out_act != null)
			{
				m_op_svr_out_act.TransferData(null);
			}
		}
	}

	protected virtual void Reset(CVStartCFC action)
	{
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
		if (m_trans_action.TryGetValue(serialNumber, out var found))
		{
			trans = found;
			return true;
		}
		return false;
	}

	protected virtual void DoTransCompleted(CVTransAction trans, CVStartCFC action)
	{
		logger.InfoFormat("[{0}]DoTransCompleted => {1}", ToShortString(), action.SerialNumber);
		release(action.SerialNumber);
		m_op_end.TransferData(action);
	}

	private bool ShouldContinueOnFailedResponse(CVServerResponse resp)
	{
		return ContinueOnFail && resp.Status == ActionStatusEnum.Failed;
	}

	private void DoIgnoredFailedResponse(CVTransAction trans, CVServerResponse resp)
	{
		string nodeName = GetFullNodeName();
		trans.trans_action.SetStatusType(StatusTypeEnum.Runing);
		AddIgnoredFailedNode(trans.trans_action, resp, nodeName);
		if (resp.Data != null)
		{
			trans.NodeFinished(base.NodeType, resp.Data);
		}
		logger.WarnFormat("[{0}]CVTransAction Failed ignored by ContinueOnFail => {1}", ToShortString(), JsonConvert.SerializeObject(trans.trans_action));
	}

	private static void AddIgnoredFailedNode(CVStartCFC action, CVServerResponse resp, string nodeName)
	{
		const string key = "IgnoredFailedNodes";
		Dictionary<string, object> item = new Dictionary<string, object>
		{
			["NodeName"] = nodeName,
			["Message"] = resp.Message ?? string.Empty,
			["EventName"] = resp.EventName ?? string.Empty,
			["MsgID"] = resp.Id ?? string.Empty,
			["Time"] = DateTime.Now.ToString("O")
		};
		if (action.Data.TryGetValue(key, out object value) && value is List<Dictionary<string, object>> list)
		{
			list.Add(item);
		}
		else
		{
			action.Data[key] = new List<Dictionary<string, object>> { item };
		}
	}

	private void DoTransNodeEndOut(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		bool isIgnoredFailed = ShouldContinueOnFailedResponse(resp);
		if (resp.Status == ActionStatusEnum.Finish)
		{
			dynamic data = resp.Data;
			if (data != null)
			{
				trans.NodeFinished(base.NodeType, data);
			}
		}
		else if (isIgnoredFailed)
		{
			DoIgnoredFailedResponse(trans, resp);
		}
		else if (resp.Status == ActionStatusEnum.Failed)
		{
			trans.NodeFailed(cmd.resp.Message, GetFullNodeName());
			logger.InfoFormat("[{0}]CVTransAction Failed => {1}", ToShortString(), JsonConvert.SerializeObject(trans.trans_action));
		}

		trans.AddTTL();
        TimeSpan timeSpan = DateTime.Now - trans.startTime;
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("[{0}]Node completed. Transfer to the next node. TotalTime={1}/{2}", ToShortString(), timeSpan.ToString(), trans.startTime.ToString("O"));
		}
		m_op_end.TransferData(trans.trans_action);
		base.nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs
		{
			RecvTopic = GetRecvTopic(),
			RecvMsgId = cmd.cmd?.MsgID,
			RecvEventName = cmd.resp?.EventName,
			RecvStatusCode = cmd.resp?.Status == ActionStatusEnum.Finish || isIgnoredFailed ? 0 : (cmd.resp?.Status == ActionStatusEnum.Failed ? -1 : null),
			RecvStatusMessage = isIgnoredFailed ? $"Ignored Failed: {cmd.resp?.Message}" : cmd.resp?.Message,
			RecvPayload = cmd.resp?.Data != null ? JsonConvert.SerializeObject(cmd.resp.Data) : null
		});
	}

	protected virtual void release(string serialNumber)
	{
		m_trans_action.TryRemove(serialNumber, out var cVTransAction);
		if (cVTransAction != null)
		{
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("{0} release => {1}", ToShortString(), cVTransAction.trans_action.SerialNumber);
			}
		}
		if (m_op_svr_out_act != null)
		{
			m_op_svr_out_act.TransferData(null);
		}
		Reset(cVTransAction);
	}

	protected virtual CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		CVBaseEventObj baseEvent = getBaseEvent(cVStartCFC);
		if (baseEvent != null)
		{
			result = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), baseEvent.EventName, cVStartCFC.SerialNumber, baseEvent.Data, GetToken(), base.ZIndex);
		}
		return result;
	}

	protected CVBaseServerNode GetInputOpOwnerSvrNode(int idx)
	{
		if (idx < 0 || idx >= base.InputOptions.Count)
		{
			logger.ErrorFormat("[{0}]Input count less input index => {1} < {2}", ToShortString(), base.InputOptions.Count, idx);
			return null;
		}
		STNodeOption sTNodeOption = base.InputOptions[idx];
		CVBaseServerNode result = null;
		if (sTNodeOption.ConnectionCount == 1)
		{
			STNodeOption sTNodeOption2 = sTNodeOption.ConnectedOption.First();
			if (sTNodeOption2.Owner.GetType().IsSubclassOf(typeof(CVBaseServerNode)))
			{
				result = sTNodeOption2.Owner as CVBaseServerNode;
			}
		}
		else
		{
			logger.ErrorFormat("[{0}]Input[{1}] is disconnected", ToShortString(), idx);
		}
		return result;
	}

	protected virtual CVBaseEventObj getBaseEvent(CVStartCFC start)
	{
		CVBaseEventObj cVBaseEventObj = new CVBaseEventObj();
		if (start.Data.ContainsKey("Image"))
		{
			start.Data.Remove("Image");
		}
		cVBaseEventObj.Data = getBaseEventData(start);
		cVBaseEventObj.EventName = operatorCode;
		return cVBaseEventObj;
	}

	protected virtual object getBaseEventData(CVStartCFC start)
	{
		return new CommonEventData(start.SerialNumber, "");
	}

	private bool IsCacheActResponse(CVTransAction trans, CVServerResponse status)
	{
		return trans.trans_action.FlowStatus == StatusTypeEnum.Paused;
	}

	private static bool ShouldEndFlowImmediately(CVStartCFC start)
	{
		return start.TryGetStopStatus(out _);
	}

	private static void FinishFlow(CVStartCFC start)
	{
		if (start.TryDoFinishing())
		{
			start.FireFinished();
		}
	}

	protected void RemoveActionCmd(CVTransAction trans, string key)
	{
		if (trans.m_sever_actionEvent.ContainsKey(key))
		{
			trans.m_sever_actionEvent.Remove(key);
		}
	}

	private void DoThisNodeCompleted(CVTransAction trans, CVBaseEventCmd cmd)
	{
		CVServerResponse resp = cmd.resp;
		if (m_is_out_release)
		{
			logger.DebugFormat("[{0}]Remove request => {1}/{2}", ToShortString(), trans.trans_action.SerialNumber, cmd.cmd.MsgID);
			m_trans_action.TryRemove(trans.trans_action.SerialNumber, out _);
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
		try
		{
			DoTransNodeEndOut(trans, cmd);
		}
		catch (Exception ex)
		{
			logger.ErrorFormat("[{0}]DoNodeCompleted transfer failed => {1}", ToShortString(), ex);
		}
		finally
		{
			if (m_op_svr_out_act != null)
			{
				try
				{
					m_op_svr_out_act.TransferData(null);
				}
				catch (Exception ex)
				{
					logger.ErrorFormat("[{0}]DoNodeCompleted clear server output failed => {1}", ToShortString(), ex);
				}
			}
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

	protected bool GetRecvMasterResult(AlgorithmPreStepParam param)
	{
		if (param == null || svrRecvResp?.Data == null)
		{
			return false;
		}

		if (!MasterResultDataHelper.TryRead(svrRecvResp.Data, base.NodeType, out string masterValue, out int masterId, out int masterResultType))
		{
			return false;
		}

		param.MasterValue = masterValue;
		param.MasterId = masterId;
		param.MasterResultType = masterResultType;
		return true;
	}

	protected bool getPreStepParam(int idx, AlgorithmPreStepParam param)
	{
		CVBaseServerNode inputOpOwnerSvrNode = GetInputOpOwnerSvrNode(idx);
		if (inputOpOwnerSvrNode != null)
		{
			return inputOpOwnerSvrNode.GetRecvMasterResult(param);
		}
		return false;
	}

	protected void getPreStepParam(CVStartCFC start, AlgorithmPreStepParam param)
	{
		if (!getPreStepParam(0, param))
		{
			_getPreStepParam(start, param);
		}
	}

	protected void _getPreStepParam(CVStartCFC start, AlgorithmPreStepParam param)
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

	protected SMUResultData GetSMUResult(CVStartCFC start)
	{
		if (start.Data.ContainsKey("SMUResult"))
		{
			string text = JsonConvert.SerializeObject(start.Data["SMUResult"]);
			if (logger.IsDebugEnabled)
			{
				logger.DebugFormat("{0}", text);
			}
			return JsonConvert.DeserializeObject<SMUResultData>(text);
		}
		return null;
	}
}
