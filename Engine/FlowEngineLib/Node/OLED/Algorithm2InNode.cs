using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

[STNode("/03_2 Algorithm")]
public class Algorithm2InNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(Algorithm2InNode));

	private int _OrderIndex;

	private string _TempName;

	private int _TempId;

	private Algorithm2Type _Algorithm;

	private int _BufferLen;

	private bool _IsAdd;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<Algorithm2Type> m_ctrl_editText;

	[STNodeProperty("参数模板", "参数模板", true)]
	public string TempName
	{
		get
		{
			return _TempName;
		}
		set
		{
			_TempName = value;
			setTempName();
		}
	}

	[STNodeProperty("参数模板ID", "参数模板ID", true)]
	public int TempId
	{
		get
		{
			return _TempId;
		}
		set
		{
			_TempId = value;
			setTempName();
		}
	}

	[STNodeProperty("算子", "算子", true)]
	public Algorithm2Type Algorithm
	{
		get
		{
			return _Algorithm;
		}
		set
		{
			_Algorithm = value;
			setAlgorithmType();
		}
	}

	[STNodeProperty("缓存大小", "缓存大小", true)]
	public int BufferLen
	{
		get
		{
			return _BufferLen;
		}
		set
		{
			_BufferLen = value;
		}
	}

	[STNodeProperty("是否新增", "是否新增", true)]
	public bool IsAdd
	{
		get
		{
			return _IsAdd;
		}
		set
		{
			_IsAdd = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public Algorithm2InNode()
		: base("AI算法2", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "OLED.JND.CalVas";
		_Algorithm = Algorithm2Type.JND;
		_TempName = "";
		_TempId = -1;
		m_in_text = "IN_IMG";
		m_in2_text = "IN_ROI";
		base.Height += 25;
		_IsAdd = false;
		_OrderIndex = -1;
		_BufferLen = 1024;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<Algorithm2Type>), m_custom_item, "算子:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		switch (_Algorithm)
		{
		case Algorithm2Type.MTF:
			operatorCode = "MTF";
			break;
		case Algorithm2Type.灯带检测:
			operatorCode = "LEDStripDetection";
			break;
		case Algorithm2Type.JND:
			operatorCode = "OLED.JND.CalVas";
			break;
		case Algorithm2Type.图像裁剪:
			operatorCode = "OLED.GetRIAand";
			break;
		case Algorithm2Type.SFR:
			operatorCode = "SFR";
			break;
		case Algorithm2Type.SFR_FindROI:
			operatorCode = "ARVR.SFR.FindROI";
			break;
		case Algorithm2Type.十字计算:
			operatorCode = "FindCross";
			break;
		default:
			operatorCode = "OLED.JND.CalVas";
			break;
		}
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		Algorithm2InParam algorithm2InParam = new Algorithm2InParam(_TempName, _IsAdd, -1, _OrderIndex, _BufferLen);
		getPreStepParam(0, algorithm2InParam);
		getPreStepParam(1, algorithmPreStepParam);
		algorithm2InParam.POI_MasterId = algorithmPreStepParam.MasterId;
		algorithm2InParam.SMUData = GetSMUResult(start);
		return algorithm2InParam;
	}
}
