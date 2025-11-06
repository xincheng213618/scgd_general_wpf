using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class FWNode : CVBaseServerNode
{
	private int _Port;

	private FWModelType _ModelType;

	private STNodeEditText<int> m_ctrl_port;

	[STNodeProperty("位置", "位置", true)]
	public int Port
	{
		get
		{
			return _Port;
		}
		set
		{
			_Port = value;
			m_ctrl_port.Value = value;
		}
	}

	[STNodeProperty("类别", "类别", true)]
	public FWModelType ModelType
	{
		get
		{
			return _ModelType;
		}
		set
		{
			_ModelType = value;
			setModelType(value);
		}
	}

	public FWNode()
		: base("滤色轮", "FilterWheel", "SVR.FilterWheel.Default", "DEV.FilterWheel.Default")
	{
		operatorCode = "SetPort";
		_Port = 0;
		_ModelType = FWModelType.FilterWheel;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_port = CreateControl(typeof(STNodeEditText<int>), custom_item, "位置:", _Port);
	}

	private void setModelType(FWModelType model)
	{
		switch (model)
		{
		case FWModelType.FilterWheel:
			m_nodeType = model.ToString();
			m_nodeName = "SVR.FilterWheel.Default";
			base.DeviceCode = "DEV.FilterWheel.Default";
			break;
		case FWModelType.Spectrum_FilterWheel:
			m_nodeType = "Spectrum";
			m_nodeName = "SVR.Spectrum.Default";
			base.DeviceCode = "DEV.Spectrum.Default";
			break;
		case FWModelType.Camera_FilterWheel:
			m_nodeType = "Camera";
			m_nodeName = "SVR.Camera.Default";
			base.DeviceCode = "DEV.Camera.Default";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new FWData(_Port);
	}
}
