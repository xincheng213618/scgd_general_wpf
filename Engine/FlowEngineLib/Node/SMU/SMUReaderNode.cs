using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.SMU;

[STNode("/04 源表")]
public class SMUReaderNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUReaderNode));

	private int _WaitTime;

	private STNodeEditText<int> m_ctrl_time;

	[STNodeProperty("等待时间", "等待时间", true)]
	public int WaitTime
	{
		get
		{
			return _WaitTime;
		}
		set
		{
			_WaitTime = value;
			updateUI();
		}
	}

	public SMUReaderNode()
		: base("源表结果", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		operatorCode = "SMU.MeasureResult";
		_WaitTime = 500;
	}

	private void updateUI()
	{
		m_ctrl_time.Value = _WaitTime;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_time = CreateControl(typeof(STNodeEditText<int>), m_custom_item, "等待时间:", _WaitTime);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new SMUGetMeasureResultParam(_WaitTime);
	}
}
