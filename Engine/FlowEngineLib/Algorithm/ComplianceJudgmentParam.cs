namespace FlowEngineLib.Algorithm;

public class ComplianceJudgmentParam : AlgorithmParam
{
	public bool IsBreak { get; set; }

	public ComplianceJudgmentParam(bool isBreak)
	{
		IsBreak = isBreak;
		base.MasterId = -1;
	}
}
