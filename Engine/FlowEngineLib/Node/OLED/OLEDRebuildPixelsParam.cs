using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class OLEDRebuildPixelsParam : AlgorithmPreStepParam
{
	public CVTemplateParam TemplateParam { get; set; }

	public int CIE_MasterId { get; set; }

	public string ImgFileName { get; set; }

	public int POI_MasterId { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public OLEDRebuildPixelsParam(string outTempName, string tempName, string imgFile)
		: this(outTempName, tempName, imgFile, -1, -1)
	{
	}

	public OLEDRebuildPixelsParam(string outTempName, string tempName, string imgFile, int cie_mid, int poi_mid)
	{
		ImgFileName = imgFile;
		CIE_MasterId = cie_mid;
		POI_MasterId = poi_mid;
		OutputTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = outTempName
		};
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
