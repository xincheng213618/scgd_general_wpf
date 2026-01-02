using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/12 第三方算法")]
public class TPAlgorithm2Node : CVBaseServerNodeIn2Hub
{
	private string _TempName;

	private int _TempId;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<string> m_ctrl_op;

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

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public TPAlgorithm2Node()
		: base("第三方算法2", "TPAlgorithms", "SVR.TPAlgorithms.Default", "DEV.TPAlgorithms.Default")
	{
		operatorCode = "";
		_TempName = "";
		_TempId = -1;
		base.Height += 25;
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_op = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "算子:", operatorCode);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", $"{_TempId}:{_TempName}");
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		TPAlgorithmInputParam tPAlgorithmInputParam = new TPAlgorithmInputParam(_TempId, _TempName, 2);
		for (int i = 0; i < masterInput.Length; i++)
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(i, algorithmPreStepParam);
			tPAlgorithmInputParam.MasterResult[i] = algorithmPreStepParam;
		}
		return tPAlgorithmInputParam;
	}
}
