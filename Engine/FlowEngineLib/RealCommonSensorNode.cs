using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class RealCommonSensorNode : CVBaseServerNode
{
	private CommSensorCmdType _CmdType;

	private string _CmdSend;

	private string _CmdReceive;

	private int _CmdTimeout;

	private int _RetryCount;

	private int _Delay;

	private STNodeEditText<string> m_ctrl_cmd_send;

	private STNodeEditText<string> m_ctrl_cmd_recv;

	private STNodeEditText<int> m_ctrl_cmd_timeout;

	private STNodeEditText<int> m_ctrl_cmd_retry;

	private STNodeEditText<int> m_ctrl_cmd_delay;

	private STNodeEditText<CommSensorCmdType> m_ctrl_cmd_type;

	[STNodeProperty("指令类型", "指令类型", true)]
	public CommSensorCmdType CmdType
	{
		get
		{
			return _CmdType;
		}
		set
		{
			_CmdType = value;
			m_ctrl_cmd_type.Value = value;
		}
	}

	[STNodeProperty("发送指令", "发送指令", true)]
	public string CmdSend
	{
		get
		{
			return _CmdSend;
		}
		set
		{
			_CmdSend = value;
			m_ctrl_cmd_send.Value = value;
		}
	}

	[STNodeProperty("接收指令", "接收指令", true)]
	public string CmdReceive
	{
		get
		{
			return _CmdReceive;
		}
		set
		{
			_CmdReceive = value;
			m_ctrl_cmd_recv.Value = value;
		}
	}

	[STNodeProperty("指令超时", "指令超时", true)]
	public int CmdTimeout
	{
		get
		{
			return _CmdTimeout;
		}
		set
		{
			_CmdTimeout = value;
			m_ctrl_cmd_timeout.Value = value;
		}
	}

	[STNodeProperty("指令重试", "指令重试", true)]
	public int RetryCount
	{
		get
		{
			return _RetryCount;
		}
		set
		{
			_RetryCount = value;
			m_ctrl_cmd_retry.Value = value;
		}
	}

	[STNodeProperty("指令延时", "指令延时", true)]
	public int Delay
	{
		get
		{
			return _Delay;
		}
		set
		{
			_Delay = value;
			m_ctrl_cmd_delay.Value = value;
		}
	}

	public RealCommonSensorNode()
		: base("通用传感器-指令", "Sensor", "SVR.Sensor.Default", "DEV.Sensor.Default")
	{
		operatorCode = "ExecCmd";
		_CmdTimeout = 100;
		_RetryCount = 3;
		_Delay = 0;
		_CmdSend = "";
		_CmdReceive = "";
		base.Width = 250;
		base.Height += 125;
		m_custom_item = new Rectangle(5, 30, 240, 18);
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		initCtrl();
	}

	private void initCtrl()
	{
		m_ctrl_cmd_type = CreateControl(typeof(STNodeEditText<CommSensorCmdType>), m_custom_item, "指令类型:", _CmdType);
		m_custom_item.Y += 25;
		m_ctrl_cmd_send = CreateStringControl(m_custom_item, "发送指令:", _CmdSend);
		m_custom_item.Y += 25;
		m_ctrl_cmd_recv = CreateStringControl(m_custom_item, "接收指令:", _CmdReceive);
		m_custom_item.Y += 25;
		m_ctrl_cmd_timeout = CreateControl(typeof(STNodeEditText<int>), m_custom_item, "指令超时:", _CmdTimeout);
		m_custom_item.Y += 25;
		m_ctrl_cmd_retry = CreateControl(typeof(STNodeEditText<int>), m_custom_item, "指令重试:", _RetryCount);
		m_custom_item.Y += 25;
		m_ctrl_cmd_delay = CreateControl(typeof(STNodeEditText<int>), m_custom_item, "指令延时:", _Delay);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new RealCommSensorData(_CmdType, _CmdSend, _CmdReceive, _CmdTimeout, _RetryCount, _Delay);
	}
}
