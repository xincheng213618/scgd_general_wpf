using FlowEngineLib.Algorithm;

namespace FlowEngineLib.Node.OLED;

public class OLEDRebuildPixelsParam : AlgorithmImageParam
{
	public int CIE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public OLEDRebuildPixelsParam(string outTempName)
		: this(outTempName, -1, -1)
	{
	}

	public OLEDRebuildPixelsParam(string outTempName, int cie_mid, int poi_mid)
	{
		CIE_MasterId = cie_mid;
		POI_MasterId = poi_mid;
		OutputTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = outTempName
		};
	}
}
