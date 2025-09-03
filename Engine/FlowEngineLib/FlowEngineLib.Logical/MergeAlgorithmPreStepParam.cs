using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Logical;

public class MergeAlgorithmPreStepParam : AlgorithmPreStepParam
{
	public int order { get; set; }

	public MergeAlgorithmPreStepParam(int order)
	{
		this.order = order;
	}
}
