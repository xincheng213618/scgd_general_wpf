namespace FlowEngineLib.Node.POI;

public class POIReviseData
{
	public CVTemplateParam TemplateParam { get; set; }

	public CVTemplateParam OutputParam { get; set; }

	public string POIPointName { get; set; }

	public bool IsSelfResultRevise { get; set; }

	public int SPE_MasterId { get; set; }

	public int POI_MasterId { get; set; }

	public POIReviseData(int spe, int poi, string templateName, string outputName, string poiPointName, bool isSelfResultRevise)
	{
		SPE_MasterId = spe;
		POI_MasterId = poi;
		POIPointName = poiPointName;
		IsSelfResultRevise = isSelfResultRevise;
		if (!string.IsNullOrEmpty(templateName))
		{
			TemplateParam = new CVTemplateParam
			{
				ID = -1,
				Name = templateName
			};
		}
		if (!string.IsNullOrEmpty(outputName))
		{
			OutputParam = new CVTemplateParam
			{
				ID = -1,
				Name = outputName
			};
		}
	}
}
