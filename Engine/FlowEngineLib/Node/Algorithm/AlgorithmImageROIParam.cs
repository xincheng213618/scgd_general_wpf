using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmImageROIParam : AlgorithmImageParam
{
	public string ResultDataFileName { get; set; }

	public AlgorithmImageROIParam(string outputImgFile)
	{
		ResultDataFileName = outputImgFile;
	}
}
