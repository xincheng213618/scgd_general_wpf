using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class BlackMuraParam : AlgorithmPreStepParam
{
	public int[] OIndex { get; set; }

	public CVTemplateParam SavePOITemplate { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public BlackMuraParam(int tempId, string tempName, string imgFileName, FileExtType fileType, string poiTempName, int[] oIndex)
	{
		FileType = fileType;
		ImgFileName = imgFileName;
		SavePOITemplate = new CVTemplateParam
		{
			ID = -1,
			Name = poiTempName
		};
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
		OIndex = oIndex;
	}
}
