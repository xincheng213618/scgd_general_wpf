using System.Drawing;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Spectrum;

[STNode("/05 光谱仪")]
public class SpectrumNode : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(SpectrumNode));

	private SPCommCmdType _Cmd;

	private float _Temp;

	private int _AveNum;

	private bool _AutoIntTime;

	private bool _SelfDark;

	private bool _AutoInitDark;

	private STNodeEditText<SPCommCmdType> m_ctrl_cmd;

	private STNodeEditText<float> m_ctrl_editText;

	private STNodeEditText<int> m_ctrl_AveNum;

	[STNodeProperty("命令", "命令", true)]
	public SPCommCmdType PGCmd
	{
		get
		{
			return _Cmd;
		}
		set
		{
			_Cmd = value;
			setValue(_Cmd);
		}
	}

	[STNodeProperty("积分时间", "积分时间", true)]
	public float Temp
	{
		get
		{
			return _Temp;
		}
		set
		{
			_Temp = value;
			m_ctrl_editText.Value = value;
		}
	}

	[STNodeProperty("平均次数", "平均次数", true)]
	public int AveNum
	{
		get
		{
			return _AveNum;
		}
		set
		{
			_AveNum = value;
			m_ctrl_AveNum.Value = value;
		}
	}

	[STNodeProperty("自动积分", "自动积分", true)]
	public bool AutoIntTime
	{
		get
		{
			return _AutoIntTime;
		}
		set
		{
			_AutoIntTime = value;
		}
	}

	[STNodeProperty("自适应校零", "自适应校零", true)]
	public bool SelfDark
	{
		get
		{
			return _SelfDark;
		}
		set
		{
			_SelfDark = value;
		}
	}

	[STNodeProperty("自动校零", "自动校零", true)]
	public bool AutoInitDark
	{
		get
		{
			return _AutoInitDark;
		}
		set
		{
			_AutoInitDark = value;
		}
	}

	public SpectrumNode()
		: base("光谱仪", "Spectrum", "SVR.Spectrum.Default", "DEV.Spectrum.Default")
	{
		operatorCode = "GetData";
		_Cmd = SPCommCmdType.检测;
		_Temp = 100f;
		_AveNum = 1;
		_AutoIntTime = false;
		_SelfDark = false;
		base.Height += 50;
	}

	private void setValue(SPCommCmdType _Cmd)
	{
		m_ctrl_cmd.Value = _Cmd;
		if (_Cmd == SPCommCmdType.校零)
		{
			m_ctrl_editText.Visable = false;
			m_ctrl_AveNum.Visable = false;
			operatorCode = "InitDark";
		}
		else
		{
			m_ctrl_editText.Visable = true;
			m_ctrl_AveNum.Visable = true;
			operatorCode = "GetData";
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		int x = m_custom_item.X;
		int width = m_custom_item.Width;
		int y = m_custom_item.Y;
		m_ctrl_cmd = new STNodeEditText<SPCommCmdType>();
		m_ctrl_cmd.Text = "命令 ";
		m_ctrl_cmd.DisplayRectangle = new Rectangle(x, y, width, m_custom_item.Height);
		m_ctrl_cmd.Value = _Cmd;
		base.Controls.Add(m_ctrl_cmd);
		m_ctrl_editText = new STNodeEditText<float>();
		m_ctrl_editText.Text = "积分时间:";
		m_ctrl_editText.DisplayRectangle = new Rectangle(x, y + 25, width, m_custom_item.Height);
		m_ctrl_editText.Value = _Temp;
		base.Controls.Add(m_ctrl_editText);
		m_ctrl_AveNum = new STNodeEditText<int>();
		m_ctrl_AveNum.Text = "平均次数:";
		m_ctrl_AveNum.DisplayRectangle = new Rectangle(x, y + 50, width, m_custom_item.Height);
		m_ctrl_AveNum.Value = _AveNum;
		base.Controls.Add(m_ctrl_AveNum);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		SMUResultData sMUResult = GetSMUResult(start);
		return new SpectrumParamData
		{
			IntegralTime = _Temp,
			NumberOfAverage = _AveNum,
			AutoIntegration = _AutoIntTime,
			SelfAdaptionInitDark = _SelfDark,
			AutoInitDark = _AutoInitDark,
			SMUData = sMUResult
		};
	}
}
