using FlowEngineLib.Node.Algorithm;

namespace FlowEngineLib.Algorithm;

public class AlgorithmPreStepParam
{
	public int MasterId { get; set; }

	public string MasterValue { get; set; }

	public int MasterResultType { get; set; }

	public AlgorithmPreStepParam(AlgorithmPreStepParam param)
		: this(param.MasterId, (CVResultType)param.MasterResultType)
	{
		MasterValue = param.MasterValue;
	}

	public AlgorithmPreStepParam()
		: this(-1, CVResultType.None)
	{
	}

	public AlgorithmPreStepParam(int masterId, CVResultType preResultType)
	{
		MasterResultType = (int)preResultType;
		MasterId = masterId;
	}
}
