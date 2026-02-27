namespace FlowEngineLib;

public class TempCommSensorData
{
	public CVTemplateParam TemplateParam { get; set; }

	public TempCommSensorData(int tempId, string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
