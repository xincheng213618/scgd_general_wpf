namespace FlowEngineLib.Algorithm;

public class TPAlgorithmInputParam : AlgorithmBaseParam
{
	public string InputParam { get; set; }

	public FileExtType FileType { get; set; }

	public AlgorithmPreStepParam[] MasterResult { get; set; }

	public TPAlgorithmInputParam(int resultCount)
	{
		MasterResult = new AlgorithmPreStepParam[resultCount];
		FileType = FileExtType.None;
	}
}
