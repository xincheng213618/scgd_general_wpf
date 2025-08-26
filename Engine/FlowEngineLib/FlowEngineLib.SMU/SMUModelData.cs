namespace FlowEngineLib.SMU;

public class SMUModelData
{
	public CVTemplateParam TemplateParam { get; set; }

	public SMUModelData(string modelName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = modelName
		};
	}
}
