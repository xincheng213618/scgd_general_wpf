using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib.Algorithm;

public class ComplianceMathParam : AlgorithmParam
{
	public bool IsBreak { get; set; }

	public ComplianceMathType ComplianceType { get; set; }

	public ComplianceMathParam()
	{
	}

	public ComplianceMathParam(ComplianceMathType _ComplianceMath, bool isBreak)
		: this()
	{
		base.MasterId = -1;
		ComplianceType = _ComplianceMath;
		IsBreak = isBreak;
	}
}
