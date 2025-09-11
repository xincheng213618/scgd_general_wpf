namespace FlowEngineLib.Algorithm;

public class TPAlgorithmInputParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public string InputParam { get; set; }

	public FileExtType FileType { get; set; }

	public AlgorithmPreStepParam[] MasterResult { get; set; }

	public TPAlgorithmInputParam(int tempId, string tempName, int resultCount)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
		MasterResult = new AlgorithmPreStepParam[resultCount];
		FileType = FileExtType.None;
	}
}
