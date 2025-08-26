using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/03_3 第三方算法")]
public class TPAlgorithmNode : CVBaseServerNode
{
	private TPAlgorithmType _Algorithm;

	private string _TempName;

	private int _TempId;

	private string _ImgFileName;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<TPAlgorithmType> m_ctrl_editText;

	private STNodeEditText<string> m_ctrl_op;

	[STNodeProperty("算子类型", "算子类型", true)]
	public TPAlgorithmType Algorithm
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

	[STNodeProperty("算子", "算子", true)]
	public string Operator
	{
		get
		{
			return operatorCode;
		}
		set
		{
			operatorCode = value;
			m_ctrl_op.Value = value;
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

	[STNodeProperty("图像文件", "图像文件", true)]
	public string ImgFileName
	{
		get
		{
			return _ImgFileName;
		}
		set
		{
			_ImgFileName = value;
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public TPAlgorithmNode()
		: base("第三方算法", "ThirdPartyAlgorithms", "SVR.TPAlgorithms.Default", "DEV.ThirdPartyAlgorithms.Default")
	{
		operatorCode = "findDotsArrayImp";
		_TempName = "";
		_TempId = -1;
		base.Height += 50;
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<TPAlgorithmType>), m_custom_item, "类型:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_op = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "算子:", operatorCode);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		AlgorithmObjType.instance.TPAlgorithmType = _Algorithm;
		switch (_Algorithm)
		{
		case TPAlgorithmType.像素定位:
			Operator = "findDotsArrayImp";
			break;
		case TPAlgorithmType.重组像素:
			Operator = "rebuildPixelsImp";
			break;
		case TPAlgorithmType.像素缺陷:
			Operator = "findPixelDefectsForRebuildPicImp";
			break;
		case TPAlgorithmType.像素缺陷2:
			Operator = "findPixelDefectsForRebuildPicGradingImp";
			break;
		case TPAlgorithmType.检测灰尘:
			Operator = "findParticlesForRebuildPicImp";
			break;
		case TPAlgorithmType.修补灰尘:
			Operator = "fillParticlesImp";
			break;
		case TPAlgorithmType.检测mura:
			Operator = "findMuraImp";
			break;
		case TPAlgorithmType.检测线:
			Operator = "findLineImp";
			break;
		case TPAlgorithmType.图像组合:
			Operator = "combineSpacingDataImp";
			break;
		case TPAlgorithmType.其他:
			Operator = "";
			break;
		default:
			Operator = "";
			break;
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		TPAlgorithmInputParam tPAlgorithmInputParam = new TPAlgorithmInputParam(_TempId, _TempName, 1);
		if (start.Data.ContainsKey("Image"))
		{
			start.Data.Remove("Image");
		}
		if (!string.IsNullOrEmpty(_ImgFileName))
		{
			tPAlgorithmInputParam.InputParam = _ImgFileName;
			tPAlgorithmInputParam.FileType = FileExtType.Tif;
		}
		else
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(start, algorithmPreStepParam);
			tPAlgorithmInputParam.MasterResult[0] = algorithmPreStepParam;
		}
		return tPAlgorithmInputParam;
	}
}
