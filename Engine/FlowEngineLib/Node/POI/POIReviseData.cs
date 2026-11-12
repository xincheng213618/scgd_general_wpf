namespace FlowEngineLib.Node.POI;

public class POIReviseData
{
	public CVTemplateParam TemplateParam { get; set; }

	public CVTemplateParam OutputParam { get; set; }

	public string POIPointName { get; set; }

	public bool IsSelfResultRevise { get; set; }

	public int SPE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public POIReviseData(int spe, int poi, string outputName, string poiPointName, bool isSelfResultRevise)
	{
		SPE_MasterId = spe;
		POI_MasterId = poi;
		POIPointName = poiPointName;
		IsSelfResultRevise = isSelfResultRevise;
		// Legacy algorithm services read OutputParam.Name even when template output is disabled.
		OutputParam = new CVTemplateParam
		{
			ID = -1,
			Name = outputName ?? string.Empty
		};
	}
}
