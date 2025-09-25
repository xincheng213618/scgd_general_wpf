using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Global;

[STNode("/00 全局")]
public class PhyDeviceControlNode : CVBaseServerNode
{
	private CVDeviceType _DeviceType;

	private CVDeviceControlCmd _CmdType;

	private STNodeEditText<CVDeviceType> m_ctrl_editText;

	private STNodeEditText<CVDeviceControlCmd> m_ctrl_cmd;

	[STNodeProperty("设备类型", "设备类型", true)]
	public CVDeviceType DeviceType
	{
		get
		{
			return _DeviceType;
		}
		set
		{
			_DeviceType = value;
			setDeviceType();
		}
	}

	[STNodeProperty("控制命令", "控制命令", true)]
	public CVDeviceControlCmd CmdType
	{
		get
		{
			return _CmdType;
		}
		set
		{
			_CmdType = value;
			m_ctrl_cmd.Value = value;
			setCmdCtl();
		}
	}

	public PhyDeviceControlNode()
		: base("物理设备控制", "Camera", "SVR.Camera.Default", "DEV.Camera.Default")
	{
		operatorCode = "Open";
		base.Height = 108;
		_DeviceType = CVDeviceType.Camera;
		_CmdType = CVDeviceControlCmd.Open;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<CVDeviceType>), m_custom_item, "DeviceType:", _DeviceType);
		m_custom_item.Y += 25;
		m_ctrl_cmd = CreateControl(typeof(STNodeEditText<CVDeviceControlCmd>), m_custom_item, "Command:", _CmdType);
	}

	private void setDeviceType()
	{
		m_ctrl_editText.Value = _DeviceType;
		switch (_DeviceType)
		{
		case CVDeviceType.Camera:
			base.NodeType = "Camera";
			m_nodeName = "SVR.Camera.Default";
			base.DeviceCode = "DEV.Camera.Default";
			break;
		case CVDeviceType.PG:
			base.NodeType = "PG";
			m_nodeName = "SVR.PG.Default";
			base.DeviceCode = "DEV.PG.Default";
			break;
		case CVDeviceType.Spectrometer:
			base.NodeType = "Spectrum";
			m_nodeName = "SVR.Spectrum.Default";
			base.DeviceCode = "DEV.Spectrum.Default";
			break;
		case CVDeviceType.SMU:
			base.NodeType = "SMU";
			m_nodeName = "SVR.SMU.Default";
			base.DeviceCode = "DEV.SMU.Default";
			break;
		case CVDeviceType.GeneralSensor:
			base.NodeType = "Sensor";
			m_nodeName = "SVR.Sensor.Default";
			base.DeviceCode = "DEV.Sensor.Default";
			break;
		}
	}

	private void setCmdCtl()
	{
		switch (_CmdType)
		{
		case CVDeviceControlCmd.Open:
			operatorCode = "Open";
			break;
		case CVDeviceControlCmd.Close:
			operatorCode = "Close";
			break;
		case CVDeviceControlCmd.Reopen:
			operatorCode = "Reopen";
			break;
		case CVDeviceControlCmd.Scan:
			operatorCode = "Scan";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		DeviceControlData deviceControlData = null;
		deviceControlData = new DeviceControlData();
		switch (_DeviceType)
		{
		case CVDeviceType.Spectrometer:
			deviceControlData = new SPDeviceControlData();
			break;
		}
		return deviceControlData;
	}
}
