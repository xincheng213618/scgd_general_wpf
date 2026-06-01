namespace FlowEngineLib.Node.POI;

public class RealPOIData
{
	public string ImgFileName { get; set; }

	public POITypeData POITypeData { get; set; }

	public int CIE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public int MasterResultType { get; set; }

	public CVTemplateParam FilterTemplate { get; set; }

	public CVTemplateParam ReviseTemplate { get; set; }

	public CVTemplateParam OutputTemplate { get; set; }

	public CVTemplateParam SubPixelTemplate { get; set; }

	public string POIReviseFileName { get; set; }

	public bool IsResultAdd { get; set; }

	public bool IsSubPixel { get; set; }

	public bool IsCCTWave { get; set; }

	public SMUResultData SMUData { get; set; }

	public RealPOIData(string imgFileName, string filterTempName, string reviseTempName, string reviseFileName, string outTempName, string subPixelTempName, POITypeData poiData, int cie_mid, int cie_resultType, int poi_mid, bool isResultAdd, bool isSubPixel, bool isCCTWave)
	{
		ImgFileName = imgFileName;
		POIReviseFileName = reviseFileName;
		POITypeData = poiData;
		CIE_MasterId = cie_mid;
		MasterResultType = cie_resultType;
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
		SubPixelTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = subPixelTempName
		};
		IsResultAdd = isResultAdd;
		IsSubPixel = isSubPixel;
		IsCCTWave = isCCTWave;
	}
}
