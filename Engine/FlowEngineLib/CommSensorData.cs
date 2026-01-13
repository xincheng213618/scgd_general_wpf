namespace FlowEngineLib;

public class CommSensorData
{
	public CVTemplateParam TemplateParam { get; set; }

	public CommSensorData(int tempId, string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = tempId,
			Name = tempName
		};
	}
}
