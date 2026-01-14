using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.POI;

public class PoiAnalysisAndSMUParam : AlgorithmBaseParam
{
	public int SMU_MasterId { get; set; }

	public PoiAnalysisAndSMUParam(int smuMasterId)
	{
		SMU_MasterId = smuMasterId;
	}
}
