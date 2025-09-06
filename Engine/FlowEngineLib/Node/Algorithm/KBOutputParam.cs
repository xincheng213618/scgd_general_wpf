using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class KBOutputParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public KBOutputParam(int tempId, string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
