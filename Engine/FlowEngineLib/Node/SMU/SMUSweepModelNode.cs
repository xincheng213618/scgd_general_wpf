using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.SMU;

[STNode("/04 源表")]
public class SMUSweepModelNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUSweepModelNode));

	protected bool m_IsCloseOutput;

	protected STNodeEditText<bool> m_ctrl_closeOut;

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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("关闭输出", "关闭输出", true)]
	public bool IsCloseOutput
	{
		get
		{
			return m_IsCloseOutput;
		}
		set
		{
			m_IsCloseOutput = value;
			OnPropertyChanged();
		}
	}

	public SMUSweepModelNode()
		: base("源表扫描-模板", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		operatorCode = "Scan";
		base.Height += 25;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_closeOut = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "关闭输出:", m_IsCloseOutput);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		return new SMUSweepParam(_TempName, m_IsCloseOutput);
	}
}
