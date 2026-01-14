using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/12 第三方算法")]
public class TPAlgorithm2Node : CVBaseServerNodeIn2Hub
{
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
			setTempName(value);
		}
	}

	public TPAlgorithm2Node()
		: base("第三方算法2", "TPAlgorithms", "SVR.TPAlgorithms.Default", "DEV.TPAlgorithms.Default")
	{
		operatorCode = "";
		base.Height += 25;
		base.MaxTime = 15000;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_op = CreateStringControl(m_custom_item, "算子:", operatorCode);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		TPAlgorithmInputParam tPAlgorithmInputParam = new TPAlgorithmInputParam(2);
		BuildTemp(tPAlgorithmInputParam);
		for (int i = 0; i < masterInput.Length; i++)
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(i, algorithmPreStepParam);
			tPAlgorithmInputParam.MasterResult[i] = algorithmPreStepParam;
		}
		return tPAlgorithmInputParam;
	}
}
