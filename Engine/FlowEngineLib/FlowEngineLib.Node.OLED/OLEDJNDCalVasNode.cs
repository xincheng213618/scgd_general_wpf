using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.OLED;

public class OLEDJNDCalVasNode : CVBaseServerNodeIn2Hub
{
	private int _OrderIndex;

	private string _TempName;

	private Algorithm2Type _Algorithm;

	private int _BufferLen;

	private bool _IsAdd;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<Algorithm2Type> m_ctrl_editText;

	[STNodeProperty("o-index", "Input Order Index", true, false, false)]
	public int OrderIndex
	{
		get
		{
			return _OrderIndex;
		}
		set
		{
			_OrderIndex = value;
		}
	}

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
			m_ctrl_temp.Value = value;
		}
	}

	[STNodeProperty("算子类别", "算子类别", true)]
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

	public OLEDJNDCalVasNode()
		: base("JND算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default", 2)
	{
		operatorCode = "OLED.JND.CalVas";
		_Algorithm = Algorithm2Type.JND;
		_TempName = "";
		m_in_text = "IN_IMG";
		m_in2_text = "IN_ROI";
		base.Height = 115;
		_IsAdd = false;
		_OrderIndex = -1;
		_BufferLen = 1024;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_custom_item.Y += 15;
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<Algorithm2Type>), m_custom_item, "算法:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		switch (_Algorithm)
		{
		case Algorithm2Type.MTF:
			operatorCode = "MTF";
			break;
		case Algorithm2Type.JND:
			operatorCode = "OLED.JND.CalVas";
			break;
		case Algorithm2Type.图像裁剪:
			operatorCode = "OLED.GetRIAand";
			break;
		default:
			operatorCode = "OLED.JND.CalVas";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
		AlgorithmCalVasParam algorithmCalVasParam = new AlgorithmCalVasParam(_TempName, _IsAdd, -1, _OrderIndex, _BufferLen);
		getPreStepParam(masterInput[0], algorithmCalVasParam);
		getPreStepParam(masterInput[1], algorithmPreStepParam);
		algorithmCalVasParam.POI_MasterId = algorithmPreStepParam.MasterId;
		if (start.Data.ContainsKey("Image"))
		{
			start.Data.Remove("Image");
		}
		return algorithmCalVasParam;
	}
}
