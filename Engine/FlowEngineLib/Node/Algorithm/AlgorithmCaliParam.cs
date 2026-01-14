using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmCaliParam : AlgorithmImageParam
{
	public string ResultDataFileName { get; set; }

	public AlgorithmCaliParam(string outputImgFile)
	{
		ResultDataFileName = outputImgFile;
	}
}
