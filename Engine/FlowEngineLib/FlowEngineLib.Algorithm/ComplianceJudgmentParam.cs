namespace FlowEngineLib.Algorithm;

public class ComplianceJudgmentParam : AlgorithmParam
{
	public ComplianceJudgmentParam()
		: this(-1)
	{
	}

	public ComplianceJudgmentParam(int masterId)
	{
		base.MasterId = masterId;
	}
}
