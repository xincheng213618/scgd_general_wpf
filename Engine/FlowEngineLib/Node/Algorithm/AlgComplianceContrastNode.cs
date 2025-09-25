using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using log4net;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

public class AlgComplianceContrastNode : CVBaseServerNodeIn2Hub
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(AlgComplianceContrastNode));

	private OperationType _Operation;

	private string _TempName;

	private int _TempId;

	private STNodeEditText<string> m_ctrl_temp;

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

	public AlgComplianceContrastNode()
		: base("对比度", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height += 25;
		operatorCode = "Compliance_Contrast";
		_TempName = "";
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_editText = CreateControl(typeof(STNodeEditText<OperationType>), m_custom_item, "运算:", _Operation);
		m_custom_item.Y += 25;
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
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
		return new ComplianceContrastParam(_TempName, (int)_Operation, array[0].MasterId, array[1].MasterId);
	}
}
