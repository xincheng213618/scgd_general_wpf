using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib.Algorithm;

public class ComplianceMathParam : AlgorithmParam
{
	public ComplianceMathType ComplianceType { get; set; }

	public ComplianceMathParam()
	{
	}

	public ComplianceMathParam(ComplianceMathType _ComplianceMath)
		: this()
	{
		base.MasterId = -1;
		ComplianceType = _ComplianceMath;
	}
}
