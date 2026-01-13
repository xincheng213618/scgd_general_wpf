using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.POI;

public class PoiAnalysisParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public PoiAnalysisParam(int tempId, string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
