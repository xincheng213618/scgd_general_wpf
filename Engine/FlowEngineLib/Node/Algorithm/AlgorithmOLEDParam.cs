using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDParam : AlgorithmParam
{
	public PointFloat[] FixedLEDPoint { get; set; }

	public CVOLED_FDAType FDAType { get; set; }

	public AlgorithmOLEDParam(CVOLED_COLOR color, string outputFileName)
	{
		base.Color = color;
		base.OutputFileName = outputFileName;
	}
}
