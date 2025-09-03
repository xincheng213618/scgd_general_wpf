namespace FlowEngineLib;

public class CommSensorData
{
	public CVTemplateParam TemplateParam { get; set; }

	public CommSensorData(string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
