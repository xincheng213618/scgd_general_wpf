using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/09 合规验证")]
public class AlgComplianceMathNode : CVBaseServerNode
{
	private ComplianceMathType _ComplianceMath;

	private bool _IsBreak;

	private STNodeEditText<ComplianceMathType> m_ctrl_type;

	private STNodeEditText<bool> m_ctrl_break;

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

	[STNodeProperty("是否中断", "是否中断", true)]
	public bool IsBreak
	{
		get
		{
			return _IsBreak;
		}
		set
		{
			_IsBreak = value;
			setBreak();
		}
	}

	public AlgComplianceMathNode()
		: base("合规算法", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		base.Height += 50;
		operatorCode = "Compliance_Math";
		_TempName = "";
		_ComplianceMath = ComplianceMathType.CIE;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateTempControl(m_custom_item);
		m_custom_item.Y += 25;
		m_ctrl_type = CreateControl(typeof(STNodeEditText<ComplianceMathType>), m_custom_item, "类别:", _ComplianceMath);
		m_custom_item.Y += 25;
		m_ctrl_break = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "中断:", _IsBreak);
	}

	private void setBreak()
	{
		m_ctrl_break.Value = _IsBreak;
	}

	private void SetMathType(ComplianceMathType value)
	{
		_ComplianceMath = value;
		m_ctrl_type.Value = value;
		base.nodeEvent?.Invoke(this, new FlowEngineNodeEventArgs());
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		ComplianceMathParam complianceMathParam = new ComplianceMathParam(_ComplianceMath, _IsBreak);
		getPreStepParam(start, complianceMathParam);
		complianceMathParam.TemplateParam = BuildTemp();
		return complianceMathParam;
	}
}
