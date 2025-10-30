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

	private bool _IsWithND;

	private bool _SelfDark;

	private bool _AutoInitDark;

	private STNodeEditText<SPCommCmdType> m_ctrl_cmd;

	private STNodeEditText<float> m_ctrl_editText;

	private STNodeEditText<int> m_ctrl_AveNum;

	private STNodeEditText<string> m_ctrl_Dark;

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

	[STNodeProperty("启用ND", "启用ND滤轮(自动曝光)", true)]
	public bool IsWithND
	{
		get
		{
			return _IsWithND;
		}
		set
		{
			_IsWithND = value;
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
			setDarkDis();
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
			setDarkDis();
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
		base.Height += 75;
	}

	private void setDarkDis()
	{
		m_ctrl_Dark.Value = GetDarkDis();
	}

	private void setValue(SPCommCmdType _Cmd)
	{
		m_ctrl_cmd.Value = _Cmd;
		if (_Cmd == SPCommCmdType.校零)
		{
			m_ctrl_editText.Visable = false;
			m_ctrl_AveNum.Visable = false;
			m_ctrl_Dark.Visable = false;
			operatorCode = "InitDark";
		}
		else
		{
			m_ctrl_editText.Visable = true;
			m_ctrl_AveNum.Visable = true;
			m_ctrl_Dark.Visable = true;
			operatorCode = "GetData";
		}
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		Rectangle custom_item = m_custom_item;
		m_ctrl_cmd = CreateControl(typeof(STNodeEditText<SPCommCmdType>), custom_item, "命令:", _Cmd);
		custom_item.Y += 25;
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<float>), custom_item, "积分时间:", _Temp);
		custom_item.Y += 25;
		m_ctrl_AveNum = CreateControl(typeof(STNodeEditText<int>), custom_item, "平均次数:", _AveNum);
		custom_item.Y += 25;
		m_ctrl_Dark = CreateControl(typeof(STNodeEditText<string>), custom_item, "自动/自适应:", GetDarkDis());
	}

	private string GetDarkDis()
	{
		return $"{_AutoInitDark}/{_SelfDark}";
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
			IsWithND = _IsWithND,
			SMUData = sMUResult
		};
	}
}
