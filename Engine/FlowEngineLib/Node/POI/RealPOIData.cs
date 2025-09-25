namespace FlowEngineLib.Node.POI;

public class RealPOIData
{
	public POITypeData POITypeData { get; set; }

	public int CIE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public string POIReviseFileName { get; set; }

	public bool IsResultAdd { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }

	public SMUResultData SMUData { get; set; }

	public RealPOIData(string filterTempName, string reviseTempName, string reviseFileName, string outTempName, POITypeData poiData, int cie_mid, int poi_mid, bool isResultAdd, bool isSubPixel, bool isCCTWave)
	{
		POIReviseFileName = reviseFileName;
		POITypeData = poiData;
		CIE_MasterId = cie_mid;
		POI_MasterId = poi_mid;
		FilterTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = filterTempName
		};
		ReviseTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = reviseTempName
		};
		OutputTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = outTempName
		};
		IsResultAdd = isResultAdd;
		IsSubPixel = isSubPixel;
		IsCCTWave = isCCTWave;
	}
}
