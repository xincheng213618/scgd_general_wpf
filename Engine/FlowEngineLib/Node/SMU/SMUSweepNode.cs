using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.SMU;

[STNode("/04 源表")]
public class SMUSweepNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SMUSweepNode));

	protected SMUChannelType _channel;

	protected SourceType _source;

	protected bool m_IsCloseOutput;

	protected float m_begin_val;

	protected float m_end_val;

	protected float _limitVal;

	protected int m_point_num;

	protected STNodeEditText<string> m_ctrl_type;

	protected STNodeEditText<string> m_ctrl_value;

	protected STNodeEditText<float> m_ctrl_limit;

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
		}
	}

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
			updateUI();
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
			updateUI();
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
			updateUI();
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
		}
	}

	private string DisTypeString => $"{_channel.ToString()}/{_source.ToString()}";

	private string DisValueString => $"{m_begin_val:F2}-{m_end_val:F2}/{m_point_num}";

	public SMUSweepNode()
		: base("源表扫描", "SMU", "SVR.SMU.Default", "DEV.SMU.Default")
	{
		operatorCode = "Scan";
		_channel = SMUChannelType.A;
		_source = SourceType.Voltage_V;
		m_IsCloseOutput = true;
		m_begin_val = 0f;
		m_end_val = 5f;
		_limitVal = 5f;
		m_point_num = 5;
		base.Height += 75;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_type = CreateStringControl(m_custom_item, "类型:", DisTypeString);
		m_custom_item.Y += 25;
		m_ctrl_value = CreateStringControl(m_custom_item, "源值:", DisValueString);
		m_custom_item.Y += 25;
		m_ctrl_limit = CreateControl(typeof(STNodeEditText<float>), m_custom_item, "限值:", _limitVal);
	}

	private void updateUI()
	{
		m_ctrl_type.Value = DisTypeString;
		m_ctrl_value.Value = DisValueString;
		m_ctrl_limit.Value = _limitVal;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		SMUSweepParam sMUSweepParam = new SMUSweepParam(_TempName, m_IsCloseOutput);
		if (string.IsNullOrEmpty(_TempName))
		{
			SweepDataParam deviceParam = new SweepDataParam
			{
				Channel = _channel,
				IsSourceV = (_source == SourceType.Voltage_V),
				BeginValue = m_begin_val,
				EndValue = m_end_val,
				LimitValue = _limitVal,
				Points = m_point_num
			};
			sMUSweepParam.DeviceParam = deviceParam;
		}
		return sMUSweepParam;
	}
}
