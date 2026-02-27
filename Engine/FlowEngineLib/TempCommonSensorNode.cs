using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/07 传感器")]
public class TempCommonSensorNode : CVBaseServerNode
{
	[STNodeProperty("参数模板", "参数模板名称", true)]
	public string TempName
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

	public TempCommonSensorNode()
		: base("通用传感器-模板", "Sensor", "SVR.Sensor.Default", "DEV.Sensor.Default")
	{
		operatorCode = "ExecCmd";
		_TempName = "";
		_TempId = -1;
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
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new TempCommSensorData(_TempId, _TempName);
	}
}
