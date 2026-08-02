using FlowEngineLib.Algorithm;
using FlowEngineLib.MQTT;
using FlowEngineLib.Node.Algorithm;
using FlowEngineLib.Runtime;
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
using System.Threading;
using System.Threading.Tasks;

namespace FlowEngineLib.Base;

public class CVBaseServerNode : CVCommonNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVBaseServerNode));

	protected string _Token;
	protected int _MaxTime;
	protected bool _ContinueOnFail;

	internal IFlowFailureRouter RuntimeFailureRouter { get; set; }

	internal FlowNodeRetryPolicy RuntimeRetryPolicy { get; set; }

	internal IFlowServiceResolver RuntimeServiceResolver { get; set; }

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
            OnPropertyChanged();
        }
    }

	public string Token
	{
		get
		{
			return _Token;
		}
		set
		{
			_Token = value;
            OnPropertyChanged();
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
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}
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
        return $"{base.Title}";
	}

	protected override string GetCompactSummaryValue()
	{
		return TempDisName;
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

    private async Task WaitingOverTimeAsync(CVBaseEventCmd cmd)
    {
		CVMQTTRequest cmd2 = cmd.cmd;
		int maxDelay = GetMaxDelay();

        // 使用异步等待，避免线程池阻塞
        logger.DebugFormat("[{0}] WaitForMessageAsync", ToShortString());
        bool result = await cmd.waiter.WaitForMessageAsync(maxDelay);
		if (logger.IsInfoEnabled)
		{
            if (result)
                logger.DebugFormat("[{0}]Task.Completed successfully", ToShortString());
            else
                logger.InfoFormat("[{0}]Task.Timed out after {1}ms", ToShortString(), maxDelay);
		}
		if (result)
		{
			return;
		}
		if (!m_trans_action.TryGetValue(
				cmd2.SerialNumber,
				out CVTransAction trans)
			|| !trans.TryTakeActionCommand(
				cmd2.MsgID,
				out CVBaseEventCmd claimedCommand))
		{
			return;
		}

		string failureMessage = $"OverTime {maxDelay}ms";
		if (TryScheduleRetry(
				trans,
				claimedCommand,
				FlowFailureKind.Timeout,
				failureMessage))
		{
			return;
		}

		if (m_trans_action.TryRemove(
				new KeyValuePair<string, CVTransAction>(
					cmd2.SerialNumber,
					trans)))
		{
			if (logger.IsInfoEnabled)
			{
				logger.InfoFormat("[{0}]OverTime => {1} ms", ToShortString(), maxDelay);
			}
			CompleteRuntimeFailure(
				trans,
				claimedCommand,
				FlowFailureKind.Timeout,
				"NodeTimeout",
				failureMessage,
				-2);
		}
		else
		{
			logger.DebugFormat(
				"[{0}]Timeout lost completion ownership => {1}/{2}",
				ToShortString(),
				cmd2.SerialNumber,
				cmd2.MsgID);
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
		MQTTServiceInfo service = GetRuntimeService();
		if (service != null)
		{
			result = service.PublishTopic;
		}
		return result;
	}

	public string GetRecvTopic()
	{
		string result = DefaultSubscribeTopic;
		MQTTServiceInfo service = GetRuntimeService();
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
		#pragma warning disable CS0618
		if (e.TargetOption.Owner.GetType() == typeof(MQTTSubscribeHub))
		{
			((MQTTSubscribeHub)owner).SetEventInfo(e.TargetOption, eventName, GetRecvTopic(), m_nodeName, m_deviceCode);
		}
		#pragma warning restore CS0618
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
				if (cmd == null)
				{
					return;
				}
				string message = JsonConvert.SerializeObject(actionEvent, Formatting.None);
				string token = GetToken();
				MQActionEvent act = new MQActionEvent(actionEvent.MsgID, m_nodeName, GetDeviceCode(), GetSendTopic(), actionEvent.EventName, message, token);
				DoTransferToServer(cVTransAction, act, cmd);
			}
			else
			{
				cVTransAction.NodeFailed("Build MQTT Request failed", GetFullNodeName(), NodeID);
			}
		}
	}

	protected void DoTransferToServer(
		CVTransAction trans,
		MQActionEvent act,
		CVBaseEventCmd cmd,
		bool publishNodeRun = true)
	{
		svrRecvResp = null;
		if (publishNodeRun)
		{
			PublishNodeRun(CreateNodeRunEventArgs(
				trans,
				act,
				cmd));
		}
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

		// Waiter is already asynchronous; keeping the task directly avoids
		// an extra thread-pool hop while the command itself remains the
		// single completion token shared by response and timeout paths.
		ObserveBackgroundTask(
			WaitingOverTimeAsync(cmd),
			"timeout monitor");
	}

	private FlowEngineNodeRunEventArgs CreateNodeRunEventArgs(
		CVTransAction trans,
		MQActionEvent action,
		CVBaseEventCmd command)
	{
		return new FlowEngineNodeRunEventArgs
		{
			SerialNumber = trans.trans_action.SerialNumber,
			SendTopic = action.Topic,
			SendMsgId = action.MsgID,
			SendEventName = action.EventName,
			SendPayload = action.Message,
			AttemptNumber = command.AttemptNumber,
			MaxAttempts = RuntimeRetryPolicy?.MaxAttempts ?? 1
		};
	}

	private void PublishNodeRun(FlowEngineNodeRunEventArgs args)
	{
		Delegate[] handlers =
			base.nodeRunEvent?.GetInvocationList()
			?? Array.Empty<Delegate>();
		foreach (FlowEngineNodeRunEvent handler in
			handlers.Cast<FlowEngineNodeRunEvent>())
		{
			try
			{
				handler(this, args);
			}
			catch (Exception ex)
			{
				logger.Error(
					$"[{ToShortString()}] node-run subscriber failed",
					ex);
			}
		}
	}

	private void PublishNodeEnd(FlowEngineNodeEndEventArgs args)
	{
		Delegate[] handlers =
			base.nodeEndEvent?.GetInvocationList()
			?? Array.Empty<Delegate>();
		foreach (FlowEngineNodeEndEvent handler in
			handlers.Cast<FlowEngineNodeEndEvent>())
		{
			try
			{
				handler(this, args);
			}
			catch (Exception ex)
			{
				logger.Error(
					$"[{ToShortString()}] node-end subscriber failed",
					ex);
			}
		}
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
			if (cVServerResponse.Status != ActionStatusEnum.Pending
				&& cVTransByEvent.TryTakeActionCommand(
					cVServerResponse.Id,
					out CVBaseEventCmd cVBaseEventCmd))
			{
				cVBaseEventCmd.resp = cVServerResponse;
				cVBaseEventCmd.waiter.SignalMessageReceived();
				try
				{
					OnServerResponse(
						cVServerResponse,
						cVTransByEvent.trans_action);
				}
				catch (Exception ex)
				{
					logger.Error(
						$"[{ToShortString()}] response handling failed",
						ex);
					if (TryScheduleRetry(
							cVTransByEvent,
							cVBaseEventCmd,
							FlowFailureKind.Technical,
							ex.Message))
					{
						return true;
					}
					if (m_trans_action.TryRemove(
							new KeyValuePair<string, CVTransAction>(
								cVTransByEvent.trans_action.SerialNumber,
								cVTransByEvent)))
					{
						CompleteRuntimeFailure(
							cVTransByEvent,
							cVBaseEventCmd,
							FlowFailureKind.Technical,
							"ResponseHandlingFailed",
							ex.Message,
							-3);
					}
					return true;
				}

				if (IsCacheActResponse(
						cVTransByEvent,
						cVServerResponse))
				{
					if (!cVTransByEvent.IsCanceled
						&& cVTransByEvent.trans_action.IsPaused)
					{
						cVTransByEvent.TryAddActionCommand(
							cVServerResponse.Id,
							cVBaseEventCmd);
					}
					else
					{
						PublishCanceledAttempt(
							cVTransByEvent,
							cVBaseEventCmd,
							"Flow stopped while the response was cached.");
					}
					return true;
				}

				if (cVServerResponse.Status == ActionStatusEnum.Failed
					&& TryScheduleRetry(
						cVTransByEvent,
						cVBaseEventCmd,
						FlowFailureKind.Business,
						cVServerResponse.Message))
				{
					return true;
				}
				DoThisNodeCompleted(cVTransByEvent, cVBaseEventCmd);
				return true;
			}
		}
		else
		{
			logger.WarnFormat("[{0}] => {1}", ToShortString(), JsonConvert.SerializeObject(statusEvent));
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
					if (cVTransByEvent != null)
					{
						m_trans_action.TryRemove(
							new KeyValuePair<string, CVTransAction>(
								cVTransByEvent.trans_action.SerialNumber,
								cVTransByEvent));
						cVTransByEvent.Cancel();
						CompleteCanceledPendingAttempts(
							cVTransByEvent,
							"Flow stopped before the node attempt completed.");
						Reset(cVTransByEvent);
					}
					else
					{
						Reset(cVStartCFC);
					}
					FinishFlow(cVStartCFC);
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
							foreach (CVBaseEventCmd value in
								cVTransByEvent.GetActionCommandsSnapshot())
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

	protected CVBaseEventCmd AddActionCmd(
		CVTransAction trans,
		CVMQTTRequest sendEvent,
		int attemptNumber = 1)
	{
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("Add To Server request => {0}", JsonConvert.SerializeObject(sendEvent));
		}
		return trans.TryStartActionCommand(
				sendEvent,
				attemptNumber,
				out CVBaseEventCmd command)
			? command
			: null;
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
		logger.DebugFormat("[{0}]DoTransCompleted => {1}", ToShortString(), action.SerialNumber);
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
		FlowFailureRouteResult failureRouteResult = null;
		bool failureHandled = false;
		string statusMessage = resp.Message ?? string.Empty;
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
			FlowFailure failure = new FlowFailure(
				FlowFailureKind.Business,
				"ServiceFailed",
				statusMessage,
				NodeID,
				GetFullNodeName(),
				DateTime.UtcNow);
			failureRouteResult = RuntimeFailureRouter?.TryRoute(
				this,
				trans.trans_action,
				failure);
			failureHandled = TryDispatchFailureRoute(
				failureRouteResult,
				out string routeFailureMessage);
			if (failureHandled)
			{
				trans.AddTTL();
				logger.WarnFormat(
					"[{0}]CVTransAction Failed routed => {1}/{2}",
					ToShortString(),
					failureRouteResult.TargetNodeId,
					failureRouteResult.TargetInputIndex);
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(routeFailureMessage))
				{
					statusMessage = string.IsNullOrWhiteSpace(statusMessage)
						? routeFailureMessage
						: $"{statusMessage}；{routeFailureMessage}";
				}
				trans.NodeFailed(statusMessage, GetFullNodeName(), NodeID);
				logger.InfoFormat("[{0}]CVTransAction Failed => {1}", ToShortString(), JsonConvert.SerializeObject(trans.trans_action));
			}
		}

		if (resp.Status != ActionStatusEnum.Failed)
		{
			trans.AddTTL();
		}
        TimeSpan timeSpan = DateTime.Now - trans.startTime;
		if (logger.IsInfoEnabled)
		{
			logger.InfoFormat("[{0}]Node completed. Transfer to the next node. TotalTime={1}/{2}", ToShortString(), timeSpan.ToString(), trans.startTime.ToString("O"));
		}
		if (!failureHandled)
		{
			m_op_end.TransferData(trans.trans_action);
		}
		PublishNodeEnd(new FlowEngineNodeEndEventArgs
		{
			SerialNumber = trans.trans_action.SerialNumber,
			RecvTopic = GetRecvTopic(),
			RecvMsgId = cmd.cmd?.MsgID,
			RecvEventName = cmd.resp?.EventName,
			RecvStatusCode = cmd.resp?.Status == ActionStatusEnum.Finish || isIgnoredFailed ? 0 : (cmd.resp?.Status == ActionStatusEnum.Failed ? -1 : null),
			RecvStatusMessage = isIgnoredFailed
				? $"Ignored Failed: {cmd.resp?.Message}"
				: statusMessage,
			RecvPayload = cmd.resp?.Data != null ? JsonConvert.SerializeObject(cmd.resp.Data) : null,
			FailureKind = resp.Status == ActionStatusEnum.Failed
				? FlowFailureKind.Business
				: null,
			FailureHandled = failureHandled,
			FailureRouteTargetNodeId = failureHandled
				? failureRouteResult?.TargetNodeId
				: null,
			AttemptNumber = cmd.AttemptNumber,
			MaxAttempts = RuntimeRetryPolicy?.MaxAttempts ?? 1
		});
	}

	internal ConnectionStatus CanTransferFailureTo(STNodeOption targetInput)
	{
		return m_op_end.CanTransferDataTo(targetInput);
	}

	internal ConnectionStatus TransferFailureTo(
		STNodeOption targetInput,
		CVStartCFC action)
	{
		return m_op_end.TransferDataTo(targetInput, action);
	}

	private bool TryDispatchFailureRoute(
		FlowFailureRouteResult route,
		out string failureMessage)
	{
		failureMessage = route?.Message ?? string.Empty;
		if (route?.IsRouted != true)
		{
			return false;
		}

		try
		{
			ConnectionStatus dispatchStatus = route.Dispatch();
			if (dispatchStatus == ConnectionStatus.Connected)
			{
				return true;
			}

			failureMessage =
				$"错误分支运行时传输被拒绝：{dispatchStatus}。";
		}
		catch (Exception ex)
		{
			logger.Error(
				$"[{ToShortString()}] runtime error route dispatch failed",
				ex);
			failureMessage = $"错误分支运行失败：{ex.Message}";
		}
		return false;
	}

	private void CompleteRuntimeFailure(
		CVTransAction trans,
		CVBaseEventCmd command,
		FlowFailureKind failureKind,
		string failureCode,
		string failureMessage,
		int statusCode)
	{
		if (trans == null || command?.cmd == null)
		{
			return;
		}

		var failure = new FlowFailure(
			failureKind,
			failureCode,
			failureMessage ?? string.Empty,
			NodeID,
			GetFullNodeName(),
			DateTime.UtcNow);
		FlowFailureRouteResult route = RuntimeFailureRouter?.TryRoute(
			this,
			trans.trans_action,
			failure);
		bool failureHandled = TryDispatchFailureRoute(
			route,
			out string routeFailureMessage);
		string statusMessage = failureMessage ?? string.Empty;
		if (failureHandled)
		{
			trans.AddTTL();
		}
		else
		{
			if (!string.IsNullOrWhiteSpace(routeFailureMessage))
			{
				statusMessage = string.IsNullOrWhiteSpace(statusMessage)
					? routeFailureMessage
					: $"{statusMessage}；{routeFailureMessage}";
			}
			if (failureKind == FlowFailureKind.Timeout)
			{
				trans.NodeOverTime(GetFullNodeName(), NodeID);
			}
			else
			{
				trans.NodeFailed(
					statusMessage,
					GetFullNodeName(),
					NodeID);
			}
		}

		Reset(trans);
		if (!failureHandled)
		{
			m_op_end.TransferData(trans.trans_action);
		}
		PublishNodeEnd(new FlowEngineNodeEndEventArgs
		{
			SerialNumber = trans.trans_action.SerialNumber,
			RecvTopic = GetRecvTopic(),
			RecvMsgId = command.cmd.MsgID,
			RecvEventName = command.resp?.EventName
				?? command.cmd.EventName,
			RecvStatusCode = statusCode,
			RecvStatusMessage = statusMessage,
			RecvPayload = command.resp?.Data != null
				? JsonConvert.SerializeObject(command.resp.Data)
				: null,
			FailureKind = failureKind,
			FailureHandled = failureHandled,
			FailureRouteTargetNodeId = failureHandled
				? route?.TargetNodeId
				: null,
			AttemptNumber = command.AttemptNumber,
			MaxAttempts = RuntimeRetryPolicy?.MaxAttempts ?? 1
		});
	}

	private bool TryScheduleRetry(
		CVTransAction trans,
		CVBaseEventCmd failedCommand,
		FlowFailureKind failureKind,
		string failureMessage)
	{
		if (trans == null
			|| failedCommand?.cmd == null
			|| RuntimeRetryPolicy == null
			|| (failureKind == FlowFailureKind.Business
				&& ContinueOnFail))
		{
			return false;
		}

		FlowRetryDecision decision = RuntimeRetryPolicy.GetDecision(
			failedCommand.AttemptNumber,
			failureKind);
		if (!decision.ShouldRetry
			|| !trans.trans_action.IsRunning
			|| trans.IsCanceled)
		{
			return false;
		}

		trans.AddTTL();
		PublishNodeEnd(new FlowEngineNodeEndEventArgs
		{
			SerialNumber = trans.trans_action.SerialNumber,
			RecvTopic = GetRecvTopic(),
			RecvMsgId = failedCommand.cmd.MsgID,
			RecvEventName = failedCommand.resp?.EventName,
			RecvStatusCode =
				failureKind == FlowFailureKind.Timeout ? -2 : -1,
			RecvStatusMessage = failureMessage ?? string.Empty,
			RecvPayload = failedCommand.resp?.Data != null
				? JsonConvert.SerializeObject(failedCommand.resp.Data)
				: null,
			FailureKind = failureKind,
			WillRetry = true,
			AttemptNumber = failedCommand.AttemptNumber,
			MaxAttempts = RuntimeRetryPolicy.MaxAttempts,
			RetryDelayMs = (int)decision.Delay.TotalMilliseconds
		});
		if (m_op_svr_out_act != null)
		{
			try
			{
				m_op_svr_out_act.TransferData(null);
			}
			catch (Exception ex)
			{
				logger.Error(
					$"[{ToShortString()}] retry output reset failed",
					ex);
			}
		}
		ObserveBackgroundTask(
			RetryAsync(
				trans,
				failedCommand.cmd,
				decision),
			"retry");
		return true;
	}

	private void ObserveBackgroundTask(
		Task task,
		string operation)
	{
		_ = task.ContinueWith(
			faultedTask => logger.Error(
				$"[{ToShortString()}] {operation} task failed",
				faultedTask.Exception),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnFaulted
				| TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	private void CompleteCanceledPendingAttempts(
		CVTransAction trans,
		string message)
	{
		foreach (var item in
			trans.GetActionCommandPairsSnapshot())
		{
			if (!trans.TryTakeActionCommand(
					item.Key,
					out CVBaseEventCmd command))
			{
				continue;
			}

			command.waiter.SignalMessageReceived();
			PublishCanceledAttempt(trans, command, message);
		}
	}

	private void PublishCanceledAttempt(
		CVTransAction trans,
		CVBaseEventCmd command,
		string message)
	{
		PublishNodeEnd(new FlowEngineNodeEndEventArgs
		{
			SerialNumber = trans.trans_action.SerialNumber,
			RecvTopic = GetRecvTopic(),
			RecvMsgId = command.cmd?.MsgID,
			RecvEventName = command.cmd?.EventName,
			RecvStatusCode = -4,
			RecvStatusMessage = message,
			FailureKind = FlowFailureKind.Canceled,
			AttemptNumber = command.AttemptNumber,
			MaxAttempts = RuntimeRetryPolicy?.MaxAttempts ?? 1
		});
	}

	private async Task RetryAsync(
		CVTransAction trans,
		CVMQTTRequest previousRequest,
		FlowRetryDecision decision)
	{
		string resourceKey =
			$"retry:{NodeID}:{previousRequest.MsgID}";
		var retryCancellation =
			new RetryCancellationResource();
		CancellationToken retryToken =
			retryCancellation.Token;
		CVMQTTRequest retryRequest = null;
		CVBaseEventCmd retryCommand = null;
		MQActionEvent actionEvent = null;
		bool attemptStarted = false;
		bool nodeRunPublished = false;
		try
		{
			trans.trans_action.RuntimeResources.Set(
				resourceKey,
				retryCancellation);
			// Even a zero-delay policy must not recurse through synchronous
			// response handlers and grow the caller stack.
			await Task.Yield();
			await Task.Delay(
				decision.Delay,
				retryToken).ConfigureAwait(false);
			retryToken.ThrowIfCancellationRequested();

			if (!trans.trans_action.IsRunning
				|| trans.IsCanceled
				|| !m_trans_action.TryGetValue(
					trans.trans_action.SerialNumber,
					out CVTransAction current)
				|| !ReferenceEquals(current, trans))
			{
				return;
			}

			retryRequest = new CVMQTTRequest(
				previousRequest.Version,
				previousRequest.ServiceCode,
				previousRequest.DeviceCode,
				previousRequest.EventName,
				previousRequest.SerialNumber,
				previousRequest.Data,
				previousRequest.Token,
				previousRequest.ZIndex);
			string message = JsonConvert.SerializeObject(
				retryRequest,
				Formatting.None);
			actionEvent = new MQActionEvent(
				retryRequest.MsgID,
				m_nodeName,
				GetDeviceCode(),
				GetSendTopic(),
				retryRequest.EventName,
				message,
				GetToken());
			retryToken.ThrowIfCancellationRequested();
			retryCommand = AddActionCmd(
				trans,
				retryRequest,
				decision.NextAttempt);
			if (retryCommand == null)
			{
				return;
			}
			attemptStarted = true;
			PublishNodeRun(CreateNodeRunEventArgs(
				trans,
				actionEvent,
				retryCommand));
			nodeRunPublished = true;
			retryToken.ThrowIfCancellationRequested();
			DoTransferToServer(
				trans,
				actionEvent,
				retryCommand,
				publishNodeRun: false);
		}
		catch (OperationCanceledException)
			when (retryCancellation.IsCancellationRequested
				|| trans.IsCanceled
				|| !trans.trans_action.IsRunning)
		{
			if (nodeRunPublished)
			{
				CompleteCanceledPendingAttempts(
					trans,
					"Retry canceled before dispatch.");
			}
		}
		catch (ObjectDisposedException)
			when (retryCancellation.IsCancellationRequested
				|| trans.trans_action.RuntimeResources.IsDisposed)
		{
			if (nodeRunPublished)
			{
				CompleteCanceledPendingAttempts(
					trans,
					"Retry canceled because the flow already finished.");
			}
		}
		catch (Exception ex)
		{
			logger.Error(
				$"[{ToShortString()}] retry dispatch failed",
				ex);
			if (!trans.trans_action.IsRunning
				|| trans.IsCanceled
				|| !m_trans_action.TryGetValue(
					trans.trans_action.SerialNumber,
					out CVTransAction current)
				|| !ReferenceEquals(current, trans))
			{
				return;
			}

			retryRequest ??= CloneRetryRequest(previousRequest);
			if (!attemptStarted)
			{
				retryCommand = AddActionCmd(
					trans,
					retryRequest,
					decision.NextAttempt);
				if (retryCommand == null)
				{
					return;
				}
				attemptStarted = true;
			}

			if (!nodeRunPublished)
			{
				actionEvent ??= new MQActionEvent(
					retryRequest.MsgID,
					m_nodeName,
					retryRequest.DeviceCode,
					string.Empty,
					retryRequest.EventName,
					JsonConvert.SerializeObject(
						retryRequest,
						Formatting.None),
					retryRequest.Token);
				PublishNodeRun(
					CreateNodeRunEventArgs(
						trans,
						actionEvent,
						retryCommand));
				nodeRunPublished = true;
			}

			if (!trans.TryTakeActionCommand(
					retryRequest.MsgID,
					out CVBaseEventCmd claimedCommand))
			{
				return;
			}
			if (TryScheduleRetry(
					trans,
					claimedCommand,
					FlowFailureKind.Technical,
					ex.Message))
			{
				return;
			}

			if (m_trans_action.TryRemove(
					new KeyValuePair<string, CVTransAction>(
						trans.trans_action.SerialNumber,
						trans)))
			{
				CompleteRuntimeFailure(
					trans,
					claimedCommand,
					FlowFailureKind.Technical,
					"RetryDispatchFailed",
					ex.Message,
					-3);
			}
		}
		finally
		{
			trans.trans_action.RuntimeResources.Remove(resourceKey);
			retryCancellation.Dispose();
		}
	}

	private sealed class RetryCancellationResource : IDisposable
	{
		private readonly CancellationTokenSource source = new();

		private int disposed;

		public CancellationToken Token => source.Token;

		public bool IsCancellationRequested =>
			Volatile.Read(ref disposed) != 0;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref disposed, 1) != 0)
			{
				return;
			}

			try
			{
				source.Cancel();
			}
			finally
			{
				source.Dispose();
			}
		}
	}

	private static CVMQTTRequest CloneRetryRequest(
		CVMQTTRequest previousRequest)
	{
		return new CVMQTTRequest(
			previousRequest.Version,
			previousRequest.ServiceCode,
			previousRequest.DeviceCode,
			previousRequest.EventName,
			previousRequest.SerialNumber,
			previousRequest.Data,
			previousRequest.Token,
			previousRequest.ZIndex);
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
		trans.TryTakeActionCommand(key, out _);
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
		MQTTServiceInfo service = GetRuntimeService();
		if (service != null)
		{
			result = service.Token;
		}
		return result;
	}

	private MQTTServiceInfo GetRuntimeService()
	{
		IFlowServiceResolver serviceResolver =
			RuntimeServiceResolver
			?? FlowRuntimeServiceResolver.Ambient;
		return serviceResolver != null
			? serviceResolver.GetService(m_nodeType, m_nodeName)
			: FlowServiceManager.Instance.GetService(m_nodeType, m_nodeName);
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
