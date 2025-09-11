using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class OLEDImageCroppingParam : AlgorithmPreStepParam
{
	public int ROI_MasterId { get; set; }

	public string ImgFileName { get; set; }

	public FileExtType FileType { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public OLEDImageCroppingParam(string tempName, string imgFileName)
	{
		ImgFileName = imgFileName;
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
		base.MasterId = -1;
		ROI_MasterId = -1;
	}
}
