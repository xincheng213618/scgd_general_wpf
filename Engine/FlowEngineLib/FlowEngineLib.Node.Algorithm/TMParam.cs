using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class TMParam : AlgorithmPreStepParam
{
	public string TemplateFile { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public TMParam(int tempId, string tempName, string imgFileName, FileExtType fileType, string templateFile)
	{
		FileType = fileType;
		TemplateFile = templateFile;
		ImgFileName = imgFileName;
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
