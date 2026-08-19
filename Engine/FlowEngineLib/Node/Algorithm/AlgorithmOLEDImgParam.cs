using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDImgParam : AlgorithmParam
{
	public string ResultDataFileName { get; set; }

	public AlgorithmOLEDImgParam(string outputFileName)
	{
		ResultDataFileName = outputFileName;
	}
}
