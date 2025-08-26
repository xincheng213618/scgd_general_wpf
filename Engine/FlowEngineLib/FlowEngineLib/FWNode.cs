using System.Drawing;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class FWNode : CVBaseServerNode
{
	private int _Port;

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

	public FWNode()
		: base("滤色轮", "FilterWheel", "SVR.FilterWheel.Default", "DEV.FilterWheel.Default")
	{
		operatorCode = "SetPort";
		_Port = 0;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_port = CreateControl(typeof(STNodeEditText<int>), custom_item, "位置:", _Port);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new FWData(_Port);
	}
}
