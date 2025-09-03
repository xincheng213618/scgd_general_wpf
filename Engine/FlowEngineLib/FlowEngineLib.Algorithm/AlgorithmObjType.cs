namespace FlowEngineLib.Algorithm;

public class AlgorithmObjType
{
	public static AlgorithmObjType instance = new AlgorithmObjType();

	public AlgorithmType algorithmType { get; set; }

	public AlgorithmARVRType algorithmARVRType { get; set; }

	public TPAlgorithmType TPAlgorithmType { get; set; }
}
