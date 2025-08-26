using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class KBParam : AlgorithmPreStepParam
{
	public CVTemplateParam CaliTemplate { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public KBParam(int tempId, string tempName, string imgFileName, FileExtType fileType, string caliTemplate)
	{
		FileType = fileType;
		CaliTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = caliTemplate
		};
		ImgFileName = imgFileName;
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
