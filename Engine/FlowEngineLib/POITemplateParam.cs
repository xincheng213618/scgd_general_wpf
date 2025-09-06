namespace FlowEngineLib;

public class POITemplateParam
{
	public CVTemplateParam POI { get; set; }

	public CVTemplateParam Filter { get; set; }

	public CVTemplateParam Revise { get; set; }

	public POITemplateParam(string tempName, string filterTempName, string reviseTempName)
	{
		POI = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
		if (!string.IsNullOrEmpty(filterTempName))
		{
			Filter = new CVTemplateParam
			{
				ID = -1,
				Name = filterTempName
			};
		}
		if (!string.IsNullOrEmpty(reviseTempName))
		{
			Revise = new CVTemplateParam
			{
				ID = -1,
				Name = reviseTempName
			};
		}
	}
}
