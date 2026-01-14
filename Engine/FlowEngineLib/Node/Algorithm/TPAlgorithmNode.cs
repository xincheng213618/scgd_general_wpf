using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/12 第三方算法")]
public class TPAlgorithmNode : CVBaseServerNode
{
	private TPAlgorithmType _Algorithm;

	private STNodeEditText<TPAlgorithmType> m_ctrl_editText;

	private STNodeEditText<string> m_ctrl_op;

	[STNodeProperty("类别", "算子类别", true)]
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
			setTempName(value);
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

	public TPAlgorithmNode()
		: base("第三方算法", "TPAlgorithms", "SVR.TPAlgorithms.Default", "DEV.TPAlgorithms.Default")
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
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<TPAlgorithmType>), m_custom_item, "类别:", _Algorithm);
		m_custom_item.Y += 25;
		m_ctrl_op = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "算子:", operatorCode);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
	}

	private void setAlgorithmType()
	{
		m_ctrl_editText.Value = _Algorithm;
		AlgorithmObjType.instance.TPAlgorithmType = _Algorithm;
		if (_Algorithm == TPAlgorithmType.其他)
		{
			Operator = "";
		}
		else
		{
			Operator = "";
		}
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		TPAlgorithmInputParam tPAlgorithmInputParam = new TPAlgorithmInputParam(1);
		BuildTemp(tPAlgorithmInputParam);
		if (!string.IsNullOrEmpty(_ImgFileName))
		{
			tPAlgorithmInputParam.InputParam = _ImgFileName;
			tPAlgorithmInputParam.FileType = GetImageFileType(_ImgFileName);
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
