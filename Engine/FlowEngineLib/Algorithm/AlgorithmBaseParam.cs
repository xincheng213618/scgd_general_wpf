namespace FlowEngineLib.Algorithm;

public abstract class AlgorithmBaseParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public AlgorithmBaseParam()
	{
	}

	public AlgorithmBaseParam(AlgorithmPreStepParam param)
		: base(param)
	{
	}
}
