using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.SMU;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUFromCSVNode : CVBaseServerNode, ICVLoopNextNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUFromCSVNode));

	private SMUChannelType _channel;

	private string _csvFileName;

	private string loopName;

	private bool m_IsCloseOutput;

	private STNodeOption m_in_next;

	private STNodeEditText<string> m_ctrl_editText;

	private STNodeEditText<string> m_ctrl_lpName;

	private STNodeEditText<SMUChannelType> m_ctrl_channel;

	private float m_limit_val;

	private int m_point_num;

	private List<SMUCsvSrcData> srcValues;

	private double m_cur_val;

	private int m_step_idx;

	[STNodeProperty("电(压/流)源", "电(压/流)源", true)]
	public SourceType Source { get; set; }

	[STNodeProperty("通道", "通道", true)]
	public SMUChannelType Channel
	{
		get
		{
			return _channel;
		}
		set
		{
			_channel = value;
			m_ctrl_channel.Value = _channel;
		}
	}

	[STNodeProperty("CsvFileName", "CsvFileName", false, true)]
	public string CsvFileName
	{
		get
		{
			return _csvFileName;
		}
		set
		{
			_csvFileName = value;
			LoadFromCsv(_csvFileName);
			updateUI();
		}
	}

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

	public SMUFromCSVNode()
		: base("源表[CSV]", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		m_IsCloseOutput = false;
		operatorCode = "GetData";
		m_point_num = 0;
		m_step_idx = 0;
		loopName = "SMULoop";
		base.Height += 70;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		m_in_next.DataTransfer += m_in_next_DataTransfer;
		Rectangle custom_item = m_custom_item;
		custom_item.Y = 50;
		m_ctrl_channel = CreateControl(typeof(STNodeEditText<SMUChannelType>), custom_item, "通道:", _channel);
		custom_item.Y += 25;
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<string>), custom_item, "当前值:", string.Empty);
		custom_item.Y += 25;
		m_ctrl_lpName = CreateControl(typeof(STNodeEditText<string>), custom_item, "LoopName:", loopName);
		updateUI();
	}

	private void updateUI()
	{
		m_ctrl_editText.Value = string.Format("{2:F4}:{0}/{1}", m_step_idx, m_point_num, m_cur_val);
	}

	private void LoadFromCsv(string csvFileName)
	{
		if (!string.IsNullOrEmpty(csvFileName) && File.Exists(csvFileName))
		{
			using (FileStream stream = new FileStream(csvFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				using StreamReader streamReader = new StreamReader(stream);
				using CsvReader csvReader = new CsvReader(streamReader, CultureInfo.InvariantCulture);
				srcValues = csvReader.GetRecords<SMUCsvSrcData>().ToList();
				if (srcValues != null && srcValues.Count > 0)
				{
					m_point_num = srcValues.Count;
					m_step_idx = 0;
					m_cur_val = srcValues[m_step_idx].SrcValue;
					m_limit_val = srcValues[m_step_idx].LimitValue;
				}
				else
				{
					if (logger.IsErrorEnabled)
					{
						logger.ErrorFormat("CsvFileName content is empty or has an invalid format.");
					}
					m_point_num = 0;
					m_cur_val = 0.0;
					m_limit_val = 0f;
				}
				streamReader.Close();
				return;
			}
		}
		if (logger.IsErrorEnabled)
		{
			logger.ErrorFormat("CsvFileName is null or not exist.");
		}
		m_point_num = 0;
		m_cur_val = 0.0;
		m_limit_val = 0f;
	}

	private void end(CVTransAction trans)
	{
		updateUI();
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}]Device is end,IsCloseOutput={1}", ToShortString(), m_IsCloseOutput);
		}
		if (m_IsCloseOutput && trans != null)
		{
			SendToCloseOutput(trans);
		}
	}

	private void SendToCloseOutput(CVTransAction trans)
	{
		if (logger.IsDebugEnabled)
		{
			logger.Debug("Send To Server CloseOutput");
		}
		CVStartCFC trans_action = trans.trans_action;
		string token = GetToken();
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, "CloseOutput", trans_action.SerialNumber, null, token, base.ZIndex);
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent act = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		trans.trans_action.GetStartNode().DoPublish(act);
	}

	private void m_in_next_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		if (e.TargetOption.Data == null || srcValues == null || srcValues.Count <= m_step_idx)
		{
			return;
		}
		CVLoopCFC cVLoopCFC = (CVLoopCFC)e.TargetOption.Data;
		CVTransAction trans = null;
		if (HasTransAction(cVLoopCFC.SerialNumber, ref trans) && trans.trans_action.IsRunning)
		{
			if (HasNext())
			{
				m_cur_val = srcValues[m_step_idx].SrcValue;
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

	private void AddIVData(CVServerResponse resp, CVStartCFC start)
	{
		SMUChannelType channel = SMUChannelType.A;
		double v = resp.Data.V;
		double i = resp.Data.I;
		int masterId = resp.Data.MasterId;
		int masterResultType = resp.Data.MasterResultType;
		SMUResultData value = new SMUResultData(channel, v, i, masterId, masterResultType);
		string key = "SMUResult";
		start.Data[key] = value;
	}

	private void AddCFCData(CVStartCFC start)
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
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, trans_action.SerialNumber, new SMUData(_channel, Source == SourceType.Voltage_V, m_cur_val, m_limit_val), token, base.ZIndex);
		CVBaseEventCmd cmd = AddActionCmd(trans, cVMQTTRequest);
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent mQActionEvent = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		DoTransferToServer(trans, mQActionEvent, cmd);
		if (logger.IsDebugEnabled)
		{
			logger.DebugFormat("[{0}] Next Step Source value = {1}", ToShortString(), m_cur_val);
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
			if (m_point_num > 0)
			{
				m_point_num = srcValues.Count;
				m_step_idx = 0;
				m_cur_val = srcValues[m_step_idx].SrcValue;
				m_limit_val = srcValues[m_step_idx].LimitValue;
				m_op_end.TransferData(null);
				result = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), operatorCode, cVStartCFC.SerialNumber, new SMUData(_channel, Source == SourceType.Voltage_V, m_cur_val, m_limit_val), GetToken(), base.ZIndex);
				m_step_idx++;
				updateUI();
				AddCFCData(cVStartCFC);
			}
			else if (logger.IsErrorEnabled)
			{
				logger.ErrorFormat("CsvFileName content is empty or has an invalid format.");
			}
		}
		else
		{
			m_cur_val = 0.0;
		}
		return result;
	}

	protected override void Reset(CVTransAction trans)
	{
		logger.DebugFormat("[{0}]Reset", ToShortString());
		if (m_IsCloseOutput && trans != null)
		{
			SendToCloseOutput(trans);
		}
	}
}
