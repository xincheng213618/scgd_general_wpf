using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.SMU;
using FlowEngineLib.Start;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

public class SMUBaseNode : CVBaseServerNode, ICVLoopNextNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUBaseNode));

	protected STNodeOption m_in_next;

	protected STNodeEditText<string> m_ctrl_curValue;

	protected STNodeEditText<string> m_ctrl_lpName;

	protected STNodeEditText<SMUChannelType> m_ctrl_channel;

	protected string loopName;

	protected SMUChannelType _channel;

	protected SourceType _source;

	protected bool m_IsCloseOutput;

	protected float m_begin_val;

	protected float m_end_val;

	protected int m_point_num;

	protected double m_cur_src_val;

	protected float _limitVal;

	protected double m_step_val;

	protected int m_step_idx;

	public bool IsStarted;

	protected List<SMUSrcValueData> srcValues;

	[STNodeProperty("LoopName", "LoopName", false, true)]
	public string LoopName
	{
		get
		{
			return loopName;
		}
		set
		{
			loopName = value;
			m_ctrl_lpName.Value = loopName;
		}
	}

	[STNodeProperty("关闭输出", "关闭输出", true)]
	public bool IsCloseOutput
	{
		get
		{
			return m_IsCloseOutput;
		}
		set
		{
			m_IsCloseOutput = value;
		}
	}

	public SMUBaseNode(string title, string nodeType, string nodeName, string deviceCode)
		: base(title, nodeType, nodeName, deviceCode)
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		m_IsCloseOutput = false;
		IsStarted = false;
		operatorCode = "GetData";
		srcValues = null;
		_source = SourceType.Voltage_V;
		m_begin_val = 0f;
		m_end_val = 5f;
		_limitVal = 1f;
		m_point_num = 5;
		m_step_idx = 0;
		loopName = "SMULoop";
		base.Height += 70;
	}

	protected void CreateSMUNextControl()
	{
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		m_in_next.DataTransfer += m_in_next_DataTransfer;
	}

	protected void CreateSMUControl()
	{
		CreateSMUNextControl();
		Rectangle custom_item = m_custom_item;
		custom_item.Y = 50;
		m_ctrl_channel = CreateControl(typeof(STNodeEditText<SMUChannelType>), custom_item, "通道:", _channel);
		custom_item.Y += 25;
		m_ctrl_curValue = CreateControl(typeof(STNodeEditText<string>), custom_item, "当前值:", string.Empty);
		custom_item.Y += 25;
		m_ctrl_lpName = CreateControl(typeof(STNodeEditText<string>), custom_item, "LoopName:", loopName);
	}

	protected virtual void updateUI()
	{
		if (m_step_idx == 0)
		{
			m_ctrl_curValue.Value = $"{m_begin_val}-{m_end_val}/{m_point_num}";
		}
		else
		{
			m_ctrl_curValue.Value = string.Format("{2:F4}:{0}/{1}", m_step_idx, m_point_num, m_cur_src_val);
		}
	}

	protected virtual void end(CVTransAction trans)
	{
		updateUI();
	}

	private void SendToCloseOutput(CVTransAction trans)
	{
		CVStartCFC trans_action = trans.trans_action;
		SendToCloseOutput(trans_action.SerialNumber, trans.trans_action.GetStartNode());
	}

	private void SendToCloseOutput(string serialNumber, BaseStartNode startNode)
	{
		if (logger.IsDebugEnabled)
		{
			logger.Debug("Send To Server CloseOutput");
		}
		string token = GetToken();
		SMUCloseOutputRequestParam data = new SMUCloseOutputRequestParam
		{
			Channel = _channel
		};
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, "CloseOutput", serialNumber, data, token, base.ZIndex);
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent act = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		startNode.DoPublish(act);
	}

	private void m_in_next_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.TargetOption.Data == null)
		{
			return;
		}
		CVLoopCFC cVLoopCFC = (CVLoopCFC)e.TargetOption.Data;
		CVTransAction trans = null;
		if (HasTransAction(cVLoopCFC.SerialNumber, ref trans) && trans.trans_action.IsRunning)
		{
			if (HasNext())
			{
				m_cur_src_val = BuildNextSrcValue();
				DoNextActionEvent(trans);
				m_step_idx++;
				updateUI();
				AddCFCData(trans.trans_action);
			}
			else
			{
				end(trans);
			}
		}
	}

	protected virtual double BuildNextSrcValue()
	{
		return m_cur_src_val + m_step_val;
	}

	protected void AddIVData(CVServerResponse resp, CVStartCFC start)
	{
		double v = resp.Data.V;
		double i = resp.Data.I;
		int masterId = resp.Data.MasterId;
		int masterResultType = resp.Data.MasterResultType;
		SMUResultData value = new SMUResultData(_channel, v, i, masterId, masterResultType);
		string key = "SMUResult";
		start.Data[key] = value;
	}

	protected void AddCFCData(CVStartCFC start)
	{
		string key = loopName;
		LoopDataInfo loopDataInfo = null;
		if (start.Data.ContainsKey(key))
		{
			loopDataInfo = start.Data[key] as LoopDataInfo;
		}
		else
		{
			loopDataInfo = new LoopDataInfo();
			start.Data.Add(key, loopDataInfo);
		}
		loopDataInfo.Step = m_step_idx;
		loopDataInfo.HasNext = HasNext();
	}

	private MQActionEvent DoNextActionEvent(CVTransAction trans)
	{
		CVStartCFC trans_action = trans.trans_action;
		string token = GetToken();
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, trans_action.SerialNumber, new SMUData(_channel, _source == SourceType.Voltage_V, m_cur_src_val, _limitVal), token, base.ZIndex);
		CVBaseEventCmd cmd = AddActionCmd(trans, cVMQTTRequest);
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent mQActionEvent = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		DoTransferToServer(trans, mQActionEvent, cmd);
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] Next Step _source value = {1}", ToShortString(), m_cur_src_val);
		}
		return mQActionEvent;
	}

	protected override void DoTransCompleted(CVTransAction trans, CVStartCFC action)
	{
		base.DoTransCompleted(trans, action);
		end(trans);
	}

	protected bool HasNext()
	{
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] HasNext Step = {1}/{2}", ToShortString(), m_step_idx, m_point_num);
		}
		return m_step_idx < m_point_num;
	}

	protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
		base.OnServerResponse(resp, startCFC);
		if (resp != null && resp.Status == ActionStatusEnum.Finish)
		{
			AddIVData(resp, startCFC);
		}
	}

	protected override CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		if (cVStartCFC.IsRunning)
		{
			if (BuildValueData())
			{
				m_op_end.TransferData(null);
				m_step_idx = 0;
				result = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), operatorCode, cVStartCFC.SerialNumber, new SMUData(_channel, _source == SourceType.Voltage_V, m_cur_src_val, _limitVal), GetToken(), base.ZIndex);
				IsStarted = true;
				m_step_idx++;
				updateUI();
				AddCFCData(cVStartCFC);
			}
			else if (logger.IsErrorEnabled)
			{
				logger.ErrorFormat("[{0}]Build SMU Value data failed", ToShortString());
			}
		}
		else
		{
			m_cur_src_val = 0.0;
			m_step_val = 0.0;
		}
		return result;
	}

	protected virtual bool BuildValueData()
	{
		double num = m_end_val - m_begin_val;
		if (m_point_num > 1)
		{
			m_step_val = num / (double)(m_point_num - 1);
		}
		else
		{
			m_step_val = num;
		}
		m_cur_src_val = m_begin_val;
		return true;
	}

	protected override void Reset(CVStartCFC action)
	{
		IsStarted = false;
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}]Reset,IsCloseOutput={1}", ToShortString(), m_IsCloseOutput);
		}
		if (m_IsCloseOutput)
		{
			string serialNumber = action.SerialNumber;
			BaseStartNode startNode = action.GetStartNode();
			Task.Delay(500).ContinueWith(delegate
			{
				SendToCloseOutput(serialNumber, startNode);
			});
		}
	}
}
