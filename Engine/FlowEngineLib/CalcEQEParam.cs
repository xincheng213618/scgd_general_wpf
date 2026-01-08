using FlowEngineLib.Algorithm;

namespace FlowEngineLib;

public class CalcEQEParam : AlgorithmPreStepParam
{
	public int SMU_MasterId { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public CalcEQEParam(string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
