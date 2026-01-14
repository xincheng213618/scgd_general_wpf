using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgComplianceContrastNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgComplianceContrastNode));

	private OperationType _Operation;

	private STNodeEditText<OperationType> m_ctrl_editText;

	[STNodeProperty("运算", "运算", true)]
	public OperationType Operation
	{
		get
		{
			return _Operation;
		}
		set
		{
			_Operation = value;
			setOperationType();
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

	public AlgComplianceContrastNode()
		: base("对比度", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height += 25;
		operatorCode = "Compliance_Contrast";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<OperationType>), m_custom_item, "运算:", _Operation);
		m_custom_item.Y += 25;
		CreateTempControl(m_custom_item);
	}

	private void setOperationType()
	{
		m_ctrl_editText.Value = _Operation;
	}

	protected override object getBaseEventData(CVStartCFC startCFC)
	{
		AlgorithmPreStepParam[] array = new AlgorithmPreStepParam[masterInput.Length];
		for (int i = 0; i < masterInput.Length; i++)
		{
			AlgorithmPreStepParam algorithmPreStepParam = new AlgorithmPreStepParam();
			getPreStepParam(i, algorithmPreStepParam);
			array[i] = algorithmPreStepParam;
		}
		ComplianceContrastParam complianceContrastParam = new ComplianceContrastParam((int)_Operation, array[0].MasterId, array[1].MasterId);
		BuildTemp(complianceContrastParam);
		return complianceContrastParam;
	}
}
