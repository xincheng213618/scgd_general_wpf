namespace FlowEngineLib;

public class AutoFocusData
{
	public CVTemplateParam AutoFocusTemplate { get; set; }

	public AutoFocusData(string tempName)
	{
		AutoFocusTemplate = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
