using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Node.Algorithm;

[STNode("/09 合规验证")]
public class AlgComplianceJudgmentNode : CVBaseServerNode
{
	private bool _IsBreak;

	private STNodeEditText<bool> m_ctrl_temp;

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

	public AlgComplianceJudgmentNode()
		: base("合规判定", "Algorithm", "SVR.Algorithm.Default", "DEV.Algorithm.Default")
	{
		operatorCode = "Compliance.Judgment";
		_IsBreak = true;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_ctrl_temp = CreateControl(typeof(STNodeEditText<bool>), m_custom_item, "中断:", _IsBreak);
	}

	private void setBreak()
	{
		m_ctrl_temp.Value = _IsBreak;
	}

	protected override object getBaseEventData(CVStartCFC start)
	{
		ComplianceJudgmentParam complianceJudgmentParam = new ComplianceJudgmentParam();
		getPreStepParam(start, complianceJudgmentParam);
		return complianceJudgmentParam;
	}
}
