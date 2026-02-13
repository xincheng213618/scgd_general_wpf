using System.Drawing;
using FlowEngineLib.Base;
using FlowEngineLib.SMU;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUModelNode : SMUBaseNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUModelNode));

	[STNodeProperty("模板", "模板", true)]
	public string ModelName
	{
		get
		{
			return _TempName;
		}
		set
		{
			setTempName(value);
		}
	}

	public SMUModelNode()
		: base("源表[模板]", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		operatorCode = "ModelGetData";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateSMUNextControl();
		Rectangle custom_item = m_custom_item;
		custom_item.Y = 50;
		CreateTempControl(custom_item);
		custom_item.Y += 25;
		m_ctrl_curValue = CreateControl(typeof(STNodeEditText<string>), custom_item, "当前值:", string.Empty);
		custom_item.Y += 25;
		m_ctrl_lpName = CreateControl(typeof(STNodeEditText<string>), custom_item, "LoopName:", loopName);
		updateUI();
	}

	protected override void updateUI()
	{
		if (IsStarted)
		{
			base.updateUI();
		}
		else
		{
			m_ctrl_curValue.Value = "";
		}
		m_ctrl_temp.Value = _TempName;
	}

	private void AddIVDataMy(CVServerResponse resp, CVStartCFC start)
	{
		if (resp.EventName == "ModelGetData")
		{
			double v = resp.Data.ResultData.V;
			double i = resp.Data.ResultData.I;
			int masterId = resp.Data.MasterId;
			int masterResultType = resp.Data.MasterResultType;
			SMUResultData value = new SMUResultData(_channel, v, i, masterId, masterResultType);
			string key = "SMUResult";
			start.Data[key] = value;
		}
		else if (resp.EventName == "GetData")
		{
			double v2 = resp.Data.V;
			double i2 = resp.Data.I;
			int masterId2 = resp.Data.MasterId;
			int masterResultType2 = resp.Data.MasterResultType;
			SMUResultData value2 = new SMUResultData(_channel, v2, i2, masterId2, masterResultType2);
			string key2 = "SMUResult";
			start.Data[key2] = value2;
		}
	}

	protected override void OnServerResponse(CVServerResponse resp, CVStartCFC startCFC)
	{
		svrRecvResp = resp;
		if (resp != null && resp.Status == ActionStatusEnum.Finish)
		{
			AddIVDataMy(resp, startCFC);
			if (resp.EventName == "ModelGetData" && resp.Data != null && resp.Data.ScanRequestParam != null)
			{
				_channel = (SMUChannelType)resp.Data.ScanRequestParam.Channel;
				_source = ((!(bool)resp.Data.ScanRequestParam.IsSourceV) ? SourceType.Current_I : SourceType.Voltage_V);
				m_begin_val = (float)resp.Data.ScanRequestParam.BeginValue;
				m_end_val = (float)resp.Data.ScanRequestParam.EndValue;
				_limitVal = (float)resp.Data.ScanRequestParam.LimitValue;
				m_point_num = (int)resp.Data.ScanRequestParam.Points;
				operatorCode = "GetData";
				BuildValueData();
				updateUI();
			}
		}
	}

	protected override CVMQTTRequest getActionEvent(STNodeOptionEventArgs e)
	{
		CVMQTTRequest result = null;
		CVStartCFC cVStartCFC = (CVStartCFC)e.TargetOption.Data;
		if (cVStartCFC.IsRunning)
		{
			m_step_idx = 0;
			m_op_end.TransferData(null);
			operatorCode = "ModelGetData";
			result = new CVMQTTRequest(GetServiceName(), m_deviceCode, operatorCode, cVStartCFC.SerialNumber, new SMUModelData(_TempName), GetToken(), base.ZIndex);
			IsStarted = true;
			m_step_idx++;
			AddCFCData(cVStartCFC);
		}
		else
		{
			m_cur_src_val = 0.0;
			m_step_val = 0.0;
		}
		return result;
	}
}
