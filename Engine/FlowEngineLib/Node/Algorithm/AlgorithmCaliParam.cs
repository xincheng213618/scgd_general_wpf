using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class AlgorithmCaliParam : AlgorithmPreStepParam
{
	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public AlgorithmCaliParam(string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
