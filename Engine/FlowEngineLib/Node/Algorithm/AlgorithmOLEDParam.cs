using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmOLEDParam : AlgorithmParam
{
	public PointFloat[] FixedLEDPoint { get; set; }

	public CVOLED_FDAType FDAType { get; set; }

	public string ImgPosResultFile { get; set; }

	public AlgorithmOLEDParam(string outputFileName, CVOLED_FDAType _FDAType, string _ImgPosResultFile, PointFloat[] _FixedLEDPoint)
	{
		base.OutputFileName = outputFileName;
		FDAType = _FDAType;
		ImgPosResultFile = _ImgPosResultFile;
		FixedLEDPoint = _FixedLEDPoint;
	}
}
