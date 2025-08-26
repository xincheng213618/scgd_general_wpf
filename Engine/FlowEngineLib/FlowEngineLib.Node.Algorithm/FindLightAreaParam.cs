using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.Algorithm;

public class FindLightAreaParam : AlgorithmPreStepParam
{
	public CVTemplateParam SavePOITemplate { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public int[] OIndex { get; set; }

	public int BufferLen { get; set; }

	public FindLightAreaParam(int tempId, string tempName, string imgFileName, FileExtType fileType, string poiTempName, int[] oIndex)
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
