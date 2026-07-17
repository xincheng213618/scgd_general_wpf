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

	protected bool _IsCloseOutput;

	protected bool _IsAutoRng;

	protected double _SrcRng;

	protected double _LmtRng;

	protected float m_begin_val;

	protected float m_end_val;

	protected float _limitVal;

	protected int m_point_num;

	protected STNodeEditText<string> m_ctrl_type;

	protected STNodeEditText<string> m_ctrl_value;

	protected STNodeEditText<float> m_ctrl_limit;

	protected STNodeEditText<bool> m_ctrl_closeOut;

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
			OnPropertyChanged();
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
			OnPropertyChanged();
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
			OnPropertyChanged();
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
			OnPropertyChanged();
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
			OnPropertyChanged();
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
			OnPropertyChanged();
		}
	}

	[STNodeProperty("关闭输出", "关闭输出", true)]
	public bool IsCloseOutput
	{
		get
		{
			return _IsCloseOutput;
		}
		set
		{
			_IsCloseOutput = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("自动量程", "自动量程", true)]
	public bool IsAutoRng
	{
		get
		{
			return _IsAutoRng;
		}
		set
		{
			_IsAutoRng = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("源量程", "源量程", true)]
	public double SrcRng
	{
		get
		{
			return _SrcRng;
		}
		set
		{
			_SrcRng = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("限量程", "限量程", true)]
	public double LmtRng
	{
		get
		{
			return _LmtRng;
		}
		set
		{
			_LmtRng = value;
			OnPropertyChanged();
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
		_IsCloseOutput = true;
		_IsAutoRng = true;
		m_begin_val = 0f;
		m_end_val = 5f;
		_limitVal = 5f;
		m_point_num = 5;
		base.Height += 75;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_type = CreateStringControl(m_custom_item, "类型:", DisTypeString);
		m_custom_item.Y += 25;
		m_ctrl_value = CreateStringControl(m_custom_item, "源值:", DisValueString);
		m_custom_item.Y += 25;
		m_ctrl_limit = CreateControl(typeof(STNodeEditText<float>), m_custom_item, "限值:", _limitVal);
		m_custom_item.Y += 25;
		m_ctrl_closeOut = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "关闭输出:", _IsCloseOutput);
	}

	private void updateUI()
	{
		m_ctrl_type.Value = DisTypeString;
		m_ctrl_value.Value = DisValueString;
		m_ctrl_limit.Value = _limitVal;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		SMUSweepParam sMUSweepParam = new SMUSweepParam(_TempName, _IsCloseOutput);
		SweepDataParam deviceParam = new SweepDataParam
		{
			Channel = _channel,
			IsSourceV = (_source == SourceType.Voltage_V),
			BeginValue = m_begin_val,
			EndValue = m_end_val,
			LimitValue = _limitVal,
			Points = m_point_num,
			IsAutoRng = _IsAutoRng,
			SrcRng = _SrcRng,
			LmtRng = _LmtRng
		};
		sMUSweepParam.DeviceParam = deviceParam;
		return sMUSweepParam;
	}
}
