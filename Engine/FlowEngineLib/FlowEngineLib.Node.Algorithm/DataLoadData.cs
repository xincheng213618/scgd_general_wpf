namespace FlowEngineLib.Node.Algorithm;

public class DataLoadData
{
	public CVTemplateParam TemplateParam { get; set; }

	public DataLoadData(string tempName)
	{
		TemplateParam = new CVTemplateParam
		{
			ID = -1,
			Name = tempName
		};
	}
}
