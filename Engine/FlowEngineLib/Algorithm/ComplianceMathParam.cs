using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib.Algorithm;

public class ComplianceMathParam : AlgorithmParam
{
	public ComplianceMathType ComplianceType { get; set; }

	public ComplianceMathParam()
	{
	}

	public ComplianceMathParam(string tempName)
		: this()
	{
		base.TemplateParam.Name = tempName;
	}

	public ComplianceMathParam(int masterId, string tempName, ComplianceMathType _ComplianceMath)
		: this(tempName)
	{
		base.MasterId = masterId;
		ComplianceType = _ComplianceMath;
	}
}
