using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/09 合规验证")]
public class AlgComplianceMathNode : CVBaseServerNode
{
	private string _TempName;

	private int _TempId;

	private ComplianceMathType _ComplianceMath;

	private STNodeEditText<string> m_ctrl_temp;

	private STNodeEditText<ComplianceMathType> m_ctrl_type;

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

	[STNodeProperty("类别", "合规类别", true)]
	public ComplianceMathType ComplianceMath
	{
		get
		{
			return _ComplianceMath;
		}
		set
		{
			SetMathType(value);
		}
	}

	private void setTempName()
	{
		m_ctrl_temp.Value = $"{_TempId}:{_TempName}";
	}

	public AlgComplianceMathNode()
		: base("合规算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height += 25;
		operatorCode = "Compliance_Math";
		_TempName = "";
		_ComplianceMath = ComplianceMathType.CIE;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<string>), m_custom_item, "模板:", _TempName);
		m_custom_item.Y += 25;
		m_ctrl_type = CreateControl(typeof(STNodeEditText<ComplianceMathType>), m_custom_item, "类别:", _ComplianceMath);
	}

	private void SetMathType(ComplianceMathType value)
	{
		_ComplianceMath = value;
		m_ctrl_type.Value = value;
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		ComplianceMathParam complianceMathParam = new ComplianceMathParam(-1, _TempName, _ComplianceMath);
		getPreStepParam(start, complianceMathParam);
		return complianceMathParam;
	}
}
