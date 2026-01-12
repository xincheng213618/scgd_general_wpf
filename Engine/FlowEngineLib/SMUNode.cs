using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib;

[STNode("/04 源表")]
public class SMUNode : SMUBaseNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUNode));

	[STNodeProperty("电(压/流)源", "电(压/流)源", true)]
	public SourceType Source
	{
		get
		{
			return _source;
		}
		set
		{
			_source = value;
		}
	}

	[STNodeProperty("通道", "通道", true)]
	public SMUChannelType Channel
	{
		get
		{
			return _channel;
		}
		set
		{
			_channel = value;
			m_ctrl_channel.Value = _channel;
		}
	}

	[STNodeProperty("起始值", "起始值", true)]
	public float BeginVal
	{
		get
		{
			return m_begin_val;
		}
		set
		{
			m_begin_val = value;
			updateUI();
		}
	}

	[STNodeProperty("结束值", "结束值", true)]
	public float EndVal
	{
		get
		{
			return m_end_val;
		}
		set
		{
			m_end_val = value;
			updateUI();
		}
	}

	[STNodeProperty("限值", "限值", true)]
	public float LimitVal
	{
		get
		{
			return _limitVal;
		}
		set
		{
			_limitVal = value;
		}
	}

	[STNodeProperty("点数", "点数", true)]
	public int PointNum
	{
		get
		{
			return m_point_num;
		}
		set
		{
			m_point_num = value;
			updateUI();
		}
	}

	public SMUNode()
		: base("源表", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateSMUControl();
		updateUI();
	}
}
