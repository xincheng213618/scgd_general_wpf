using System.Drawing;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.SMU;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUNode : CVBaseServerNode
{
	private STNodeOption m_in_next;

	private STNodeEditText<string> m_ctrl_editText;

	private float m_begin_val;

	private float m_end_val;

	private int m_point_num;

	private double m_cur_val;

	private double m_step_val;

	private int m_step_count;

	private string loopName;

	[STNodeProperty("电(压/流)源", "电(压/流)源", true)]
	public SourceType Source { get; set; }

	[STNodeProperty("起始值", "起始值", true)]
	public float BeginVal
	{
		get
		{
			return m_begin_val;
		}
		set
		{
			m_begin_val = value;
			updateUI();
		}
	}

	[STNodeProperty("结束值", "结束值", true)]
	public float EndVal
	{
		get
		{
			return m_end_val;
		}
		set
		{
			m_end_val = value;
			updateUI();
		}
	}

	[STNodeProperty("限值", "限值", true)]
	public float LimitVal { get; set; }

	[STNodeProperty("点数", "点数", true)]
	public int PointNum
	{
		get
		{
			return m_point_num;
		}
		set
		{
			m_point_num = value;
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

	public SMUNode()
		: base("源表", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		m_is_out_release = false;
		m_has_svr_item = false;
		operatorCode = "GetData";
		m_begin_val = 0f;
		m_end_val = 5f;
		m_point_num = 5;
		m_step_count = 0;
		LoopName = "SMULoop";
		base.Height += 20;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in_next = base.InputOptions.Add("IN_LP_NEXT", typeof(CVLoopCFC), bSingle: true);
		base.InputOptions.Add(STNodeOption.Empty);
		base.InputOptions.Add(STNodeOption.Empty);
		m_in_next.DataTransfer += m_in_next_DataTransfer;
		m_ctrl_editText = new STNodeEditText<string>();
		m_ctrl_editText.Text = "当前值";
		Rectangle custom_item = m_custom_item;
		custom_item.Y = 50;
		m_ctrl_editText.DisplayRectangle = custom_item;
		base.Controls.Add(m_ctrl_editText);
		updateUI();
	}

	private void updateUI()
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

	private void end()
	{
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
		string token = GetToken();
		CVMQTTRequest cVMQTTRequest = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, trans_action.SerialNumber, new SMUData(Source == SourceType.电压, m_cur_val, LimitVal), token, base.ZIndex);
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
		return m_step_count < PointNum;
	}

	protected override CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		if (cVStartCFC.IsRunning)
		{
			double num = EndVal - BeginVal;
			if (PointNum > 1)
			{
				m_step_val = num / (double)(PointNum - 1);
			}
			else
			{
				m_step_val = num;
			}
			m_cur_val = BeginVal;
			m_step_count = 0;
			m_op_end.TransferData(null);
			result = new CVMQTTRequest(GetServiceName(), GetDeviceCode(), operatorCode, cVStartCFC.SerialNumber, new SMUData(Source == SourceType.电压, m_cur_val, LimitVal), GetToken(), base.ZIndex);
			m_step_count++;
			updateUI();
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
