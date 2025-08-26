using System.Drawing;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.SMU;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUModelNode : CVBaseServerNode
{
	private STNodeOption m_in_next;

	private STNodeEditText<string> m_ctrl_editText;

	private STNodeEditText<string> m_ctrl_model;

	private SourceType m_source;

	private float m_begin_val;

	private float m_end_val;

	private float m_limit_val;

	private int m_point_num;

	private double m_cur_val;

	private double m_step_val;

	private int m_step_count;

	private string loopName;

	private string modelName;

	private bool IsStarted;

	[STNodeProperty("模板", "模板", true)]
	public string ModelName
	{
		get
		{
			return modelName;
		}
		set
		{
			modelName = value;
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
		}
	}

	public SMUModelNode()
		: base("源表[模板]", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		m_has_svr_item = false;
		m_is_out_release = false;
		operatorCode = "ModelGetData";
		m_source = SourceType.电压;
		m_begin_val = 0f;
		m_end_val = 5f;
		m_limit_val = 0f;
		m_point_num = 5;
		m_step_count = 0;
		modelName = "";
		IsStarted = false;
		LoopName = "SMULoop";
		base.Height += 45;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		m_in_next.DataTransfer += m_in_next_DataTransfer;
		m_ctrl_model = new STNodeEditText<string>();
		m_ctrl_model.Text = "模板";
		m_ctrl_model.DisplayRectangle = new Rectangle(m_custom_item.X, 50, m_custom_item.Width, m_custom_item.Height);
		base.Controls.Add(m_ctrl_model);
		m_ctrl_editText = new STNodeEditText<string>();
		m_ctrl_editText.Text = "当前值";
		m_ctrl_editText.DisplayRectangle = new Rectangle(m_custom_item.X, 75, m_custom_item.Width, m_custom_item.Height);
		base.Controls.Add(m_ctrl_editText);
		updateUI();
	}

	private void updateUI()
	{
		if (IsStarted)
		{
			if (m_step_count == 0)
			{
				m_ctrl_editText.Value = $"{m_begin_val}-{m_end_val}/{m_point_num}";
			}
			else
			{
				m_ctrl_editText.Value = string.Format("{2:F4}:{0}/{1}", m_step_count, m_point_num, m_cur_val);
			}
		}
		else
		{
			m_ctrl_editText.Value = "";
		}
		m_ctrl_model.Value = modelName;
	}

	private void end()
	{
		IsStarted = false;
		updateUI();
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
				m_cur_val += m_step_val;
				DoNextActionEvent(trans);
				m_step_count++;
				updateUI();
				AddCFCData(trans.trans_action);
			}
			else
			{
				end();
			}
		}
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
		loopDataInfo.Step = m_step_count;
		loopDataInfo.HasNext = HasNext();
	}

	private MQActionEvent DoNextActionEvent(CVTransAction trans)
	{
		CVStartCFC trans_action = trans.trans_action;
		operatorCode = "GetData";
		string token = GetToken();
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(m_nodeName, m_deviceCode, operatorCode, trans_action.SerialNumber, new SMUData(m_source == SourceType.电压, m_cur_val, m_limit_val), token);
		CVBaseEventCmd cmd = AddActionCmd(trans, cVMQTTRequest);
		string message = JsonConvert.SerializeObject(cVMQTTRequest, Formatting.None);
		MQActionEvent mQActionEvent = new MQActionEvent(cVMQTTRequest.MsgID, m_nodeName, m_deviceCode, GetSendTopic(), cVMQTTRequest.EventName, message, token);
		DoTransferToServer(trans, mQActionEvent, cmd);
		return mQActionEvent;
	}

	protected override void DoTransCompleted(CVStartCFC action)
	{
		base.DoTransCompleted(action);
		end();
	}

	protected bool HasNext()
	{
		return m_step_count < m_point_num;
	}

	protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
		if (resp != null && resp.Status == ActionStatusEnum.Finish && resp.EventName == "ModelGetData")
		{
			IsStarted = true;
			m_source = ((!(bool)resp.Data.ScanRequestParam.IsSourceV) ? SourceType.电流 : SourceType.电压);
			m_begin_val = (float)resp.Data.ScanRequestParam.BeginValue;
			m_end_val = (float)resp.Data.ScanRequestParam.EndValue;
			m_limit_val = (float)resp.Data.ScanRequestParam.LimitValue;
			m_point_num = (int)resp.Data.ScanRequestParam.Points;
			double num = m_end_val - m_begin_val;
			if (m_point_num > 1)
			{
				m_step_val = num / (double)(m_point_num - 1);
			}
			else
			{
				m_step_val = num;
			}
			m_cur_val = m_begin_val;
			updateUI();
		}
	}

	protected override CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		if (cVStartCFC.IsRunning)
		{
			m_step_count = 0;
			m_op_end.TransferData(null);
			operatorCode = "ModelGetData";
			result = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, cVStartCFC.SerialNumber, new SMUModelData(modelName), GetToken());
			m_step_count++;
			AddCFCData(cVStartCFC);
		}
		else
		{
			m_cur_val = 0.0;
			m_step_val = 0.0;
		}
		return result;
	}
}
