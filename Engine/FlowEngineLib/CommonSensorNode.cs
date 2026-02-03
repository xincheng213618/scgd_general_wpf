using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class CommonSensorNode : CVBaseServerNode
{
	private CommCmdType _CmdType;

	private string _CmdSend;

	private string _CmdReceive;

	private STNodeEditText<string> m_ctrl_cmd_send;

	private STNodeEditText<string> m_ctrl_cmd_recv;

	private STNodeEditText<CommCmdType> m_ctrl_cmd_type;

	[STNodeProperty("参数模板", "参数模板名称", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
            _TempName = value;
            setTempName(_TempName);
		}
	}

	[STNodeProperty("指令类型", "指令类型", true)]
	public CommCmdType CmdType
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

	public CommonSensorNode()
		: base("通用传感器", "Sensor", "SVR.Sensor.Default", "DEV.Sensor.Default")
	{
		operatorCode = "ExecCmd";
		_TempName = "";
		_TempId = -1;
		_CmdSend = "";
		_CmdReceive = "";
		base.Width = 220;
		base.Height += 75;
		m_custom_item = new Rectangle(5, 30, 210, 18);
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		initCtrl();
	}

	private void initCtrl()
	{
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", base.TempDisName);
		m_custom_item.Y += 25;
		m_ctrl_cmd_type = CreateControl(typeof(STNodeEditText<CommCmdType>), m_custom_item, "指令类型:", _CmdType);
		m_custom_item.Y += 25;
		m_ctrl_cmd_send = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "发送指令:", _CmdSend);
		m_custom_item.Y += 25;
		m_ctrl_cmd_recv = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "接收指令:", _CmdReceive);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new CommSensorData(_TempId, _TempName);
	}
}
