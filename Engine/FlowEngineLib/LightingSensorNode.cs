using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("传感器", CategoryOrder = 700)]
public class LightingSensorNode : CVBaseServerNode
{
	private LightingCommand _command;
	private LightingChannel _channel;
	private bool _turnOn;
	private STNodeEditText<LightingCommand> m_ctrl_cmd;
	private STNodeEditText<LightingChannel> m_ctrl_ch;

	[STNodeProperty("指令", "指令", true)]
	public LightingCommand Command
	{
		get => _command;
		set
		{
			_command = value;
			if (m_ctrl_cmd != null) m_ctrl_cmd.Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("通道", "通道", true)]
	public LightingChannel Channel
	{
		get => _channel;
		set
		{
			_channel = value;
			if (m_ctrl_ch != null) m_ctrl_ch.Value = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("打开", "是否打开", true)]
	public bool TurnOn
	{
		get => _turnOn;
		set
		{
			_turnOn = value;
			OnPropertyChanged();
		}
	}

	public LightingSensorNode()
		: base("光源控制器", "LightingControl", "SVR.Lighting.Default", "DEV.LightingControl.Default")
	{
		operatorCode = "PM_LightTurnOn";
		_channel = LightingChannel.A;
		_command = LightingCommand.控制;
		_turnOn = true;
		Height += 30;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_cmd = CreateControl(typeof(STNodeEditText<LightingCommand>), m_custom_item, "指令:", _command);
		m_custom_item.Y += 25;
		m_ctrl_ch = CreateControl(typeof(STNodeEditText<LightingChannel>), m_custom_item, "通道:", _channel);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		operatorCode = _command == LightingCommand.控制
			? (_turnOn ? "PM_LightTurnOn" : "PM_LightTurnOff")
			: "PM_GetValue";
		return new LightingSensorData(_channel);
	}
}
