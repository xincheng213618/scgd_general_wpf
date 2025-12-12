using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class EQEParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public EQEParam(string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
