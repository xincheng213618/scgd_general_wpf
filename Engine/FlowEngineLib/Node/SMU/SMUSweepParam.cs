namespace FlowEngineLib.Node.SMU;

public class SMUSweepParam
{
	public bool IsCloseOutput { get; set; }

	public SweepDataParam DeviceParam { get; set; }

	public CVTemplateParam TemplateParam { get; set; }

	public SMUSweepParam(string templateName, bool isCloseOutput)
	{
		IsCloseOutput = isCloseOutput;
		if (!string.IsNullOrEmpty(templateName))
		{
			TemplateParam = new CVTemplateParam
			{
				ID = -1,
				Name = templateName
			};
		}
	}
}
