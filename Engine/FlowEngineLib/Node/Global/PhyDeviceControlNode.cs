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
		base.Height = 105;
		_DeviceType = CVDeviceType.相机;
		_CmdType = CVDeviceControlCmd.Open;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = new STNodeEditText<CVDeviceType>();
		m_ctrl_editText.Text = "类型";
		m_ctrl_editText.DisplayRectangle = m_custom_item;
		m_ctrl_editText.Value = _DeviceType;
		base.Controls.Add(m_ctrl_editText);
		m_custom_item.Y += 25;
		m_ctrl_cmd = new STNodeEditText<CVDeviceControlCmd>();
		m_ctrl_cmd.Text = "命令";
		m_ctrl_cmd.DisplayRectangle = m_custom_item;
		m_ctrl_cmd.Value = _CmdType;
		base.Controls.Add(m_ctrl_cmd);
	}

	private void setDeviceType()
	{
		m_ctrl_editText.Value = _DeviceType;
		switch (_DeviceType)
		{
		case CVDeviceType.相机:
			base.NodeType = "Camera";
			m_nodeName = "SVR.Camera.Default";
			base.DeviceCode = "DEV.Camera.Default";
			break;
		case CVDeviceType.PG:
			base.NodeType = "PG";
			m_nodeName = "SVR.PG.Default";
			base.DeviceCode = "DEV.PG.Default";
			break;
		case CVDeviceType.光谱仪:
			base.NodeType = "Spectrum";
			m_nodeName = "SVR.Spectrum.Default";
			base.DeviceCode = "DEV.Spectrum.Default";
			break;
		case CVDeviceType.源表:
			base.NodeType = "SMU";
			m_nodeName = "SVR.SMU.Default";
			base.DeviceCode = "DEV.SMU.Default";
			break;
		case CVDeviceType.通用传感器:
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
		case CVDeviceType.光谱仪:
			deviceControlData = new SPDeviceControlData();
			break;
		}
		return deviceControlData;
	}
}
