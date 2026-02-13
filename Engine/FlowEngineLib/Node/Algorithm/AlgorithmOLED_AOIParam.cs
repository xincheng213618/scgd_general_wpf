using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLED_AOIParam : AlgorithmImageParam
{
	public string OutputFileName { get; set; }

	public AlgorithmOLED_AOIParam(string outputFileName)
	{
		OutputFileName = outputFileName;
	}
}
